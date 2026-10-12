using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;

namespace ChromaDB.Client.Common;

// Sends requests with the URI and token of one client, without changing the HttpClient it was given:
// the same HttpClient can be shared by clients for different servers or tokens.
internal sealed class ChromaHttpClient
{
	private readonly HttpClient _httpClient;
	private readonly Uri _baseUri;
	private readonly string? _chromaToken;
	private readonly AuthenticationHeaderValue? _authorization;

	// What the client learns about the server, shared by the ChromaHttpClient made from this one.
	private readonly ServerFacts _server;

	public ChromaRoutes Routes { get; }
	public JsonSerializerOptions DeserializerOptions { get; }
	public bool BatchSplitting { get; }
	public ChromaUpsertStrategy UpsertStrategy { get; }
	public bool ReadsBeyondKeyQuota { get; }
	public int? MaxBatchSize { get; }
	public bool IsChromaCloud { get; }

	public ChromaHttpClient(HttpClient httpClient, ChromaConfigurationOptions options)
	{
		_httpClient = httpClient;
		_server = new ServerFacts();
		_baseUri = CreateBaseUri(options.Uri, options.ApiVersion);
		Routes = options.ApiVersion == ChromaApiVersion.V1 ? ChromaRoutes.V1 : ChromaRoutes.V2;
		DeserializerOptions = HttpClientHelpers.DeserializerOptions(options.MetadataValues);
		BatchSplitting = options.BatchSplitting;
		UpsertStrategy = options.UpsertStrategy;
		ReadsBeyondKeyQuota = options.ReadsBeyondKeyQuota;
		IsChromaCloud = options.IsChromaCloud;
		// Works around KD-21 (docs/COMPATIBILITY.md)
		// Chroma Cloud declares a max_batch_size of 1000, but takes 300 records per write and answers at most 300 per read, without an
		// error: its quotas by default.
		MaxBatchSize = options.MaxBatchSize ?? (IsChromaCloud ? ChromaCloudQuotas.MaxRecordsPerRequest : null);
		if (options.ChromaToken is not null and not [])
		{
			if (options.ChromaTokenTransportHeader == ChromaTokenTransportHeader.Authorization)
			{
				_authorization = new AuthenticationHeaderValue("Bearer", options.ChromaToken);
			}
			else
			{
				_chromaToken = options.ChromaToken;
			}
		}
		if (options.BasicAuthUsername is not null)
		{
			if (_authorization is not null)
			{
				throw new ArgumentException("Basic authentication and a token in the Authorization header cannot be used together.", nameof(options));
			}
			_authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.BasicAuthUsername}:{options.BasicAuthPassword}")));
		}
	}

	// The same server, HttpClient and credentials, reading metadata values another way.
	private ChromaHttpClient(ChromaHttpClient other, ChromaMetadataValues metadataValues)
	{
		_httpClient = other._httpClient;
		_baseUri = other._baseUri;
		_chromaToken = other._chromaToken;
		_authorization = other._authorization;
		_server = other._server;
		Routes = other.Routes;
		BatchSplitting = other.BatchSplitting;
		UpsertStrategy = other.UpsertStrategy;
		ReadsBeyondKeyQuota = other.ReadsBeyondKeyQuota;
		MaxBatchSize = other.MaxBatchSize;
		IsChromaCloud = other.IsChromaCloud;
		DeserializerOptions = HttpClientHelpers.DeserializerOptions(metadataValues);
	}

	public ChromaHttpClient WithMetadataValues(ChromaMetadataValues metadataValues) => new(this, metadataValues);

	public Uri CreateUri(string endpoint) => new(_baseUri, endpoint);

	// Chroma 0.x keeps the space of a collection only in its metadata, and a collection without it uses l2: the client asks the
	// version only for a collection that reports no space, and not on Chroma Cloud, which always reports it.
	public async Task<ChromaCollection> WithSpace(ChromaCollection collection, CancellationToken cancellationToken)
	{
		if (collection.Space is null && !IsChromaCloud && await IsChroma0(cancellationToken))
		{
			collection.DefaultSpace = ChromaSpace.L2;
		}
		return collection;
	}

	public async Task<IReadOnlyList<ChromaCollection>> WithSpaces(List<ChromaCollection> collections, CancellationToken cancellationToken)
	{
		foreach (var collection in collections)
		{
			await WithSpace(collection, cancellationToken);
		}
		return collections;
	}

	// Works around KD-14 (docs/COMPATIBILITY.md)
	// Asked when a request needs it, once for concurrent calls, and again after ServerFacts.Lifetime, in case the server changed;
	// a failed or canceled request is not kept, so the next call asks again.
	// The 0.x servers send their own version; every Chroma 1.x answers "1.0.0", so the version tells only 0.x from 1.x apart.
	public async Task<bool> IsChroma0(CancellationToken cancellationToken)
	{
		var version = _server.Version;
		if (version is not { IsCurrent: true })
		{
			await _server.VersionLock.WaitAsync(cancellationToken);
			try
			{
				version = _server.Version;
				if (version is not { IsCurrent: true })
				{
					version = new Fact<string>(await this.Get<string>(Routes.Version, new RequestQueryParams(), cancellationToken));
					_server.Version = version;
				}
			}
			finally
			{
				_server.VersionLock.Release();
			}
		}
		return version.Value.StartsWith("0.", StringComparison.Ordinal);
	}

	// The endpoints are relative to the base URI, so it needs the trailing slash: without it, new Uri(base, endpoint)
	// replaces the last segment ("api/v2" becomes "api/"). A URI with just the server address gets the path of the API version.
	private static Uri CreateBaseUri(Uri uri, ChromaApiVersion apiVersion)
	{
		if (uri.AbsolutePath is "/")
		{
			return new Uri(uri, apiVersion == ChromaApiVersion.V1 ? "api/v1/" : "api/v2/");
		}
		var path = uri.GetLeftPart(UriPartial.Path);
		return path.EndsWith("/") ? uri : new Uri(path + "/");
	}

	// The quota of records per request that the server enforced, like the 300 of Chroma Cloud, trusted as long as the other facts.
	public int? RecordsLimit => _server.RecordsLimit is { IsCurrent: true } limit ? limit.Value : null;

	public void LearnRecordsLimit(int limit) => _server.RecordsLimit = new Fact<int>(limit);

	// From pre-flight-checks, asked like the version. Null for Chroma 0.4.10, which has no pre-flight-checks, and when the server
	// cannot tell: batch splitting is on by default, and the records then go in one request, as without it.
	public async Task<int?> GetMaxBatchSize(CancellationToken cancellationToken)
	{
		try
		{
			return (await GetPreFlightChecks(cancellationToken))?.MaxBatchSize is > 0 and var limit ? limit : null;
		}
		catch (ChromaException)
		{
			return null;
		}
	}

	// Chroma 1.0.13 and later declare that add, update and upsert take embeddings as base64 strings; the earlier ones reject them.
	// Base64 only makes requests smaller: when the server cannot tell, the embeddings go as numbers, which every server takes.
	public async Task<bool> SupportsBase64Embeddings(CancellationToken cancellationToken)
	{
		try
		{
			return (await GetPreFlightChecks(cancellationToken))?.SupportsBase64Encoding == true;
		}
		catch (ChromaException)
		{
			return false;
		}
	}

	// Chroma 1.5.3 and later apply the limit of a delete, and from that version the OpenAPI description of the server declares it;
	// the earlier versions ignore it and delete every matching record. Asked like the version, only when a delete has a limit;
	// a server without the description, or without the limit in it, is taken as one that ignores it.
	public async Task<bool> SupportsDeleteLimit(CancellationToken cancellationToken)
	{
		var supported = _server.DeleteLimit;
		if (supported is not { IsCurrent: true })
		{
			await _server.DeleteLimitLock.WaitAsync(cancellationToken);
			try
			{
				supported = _server.DeleteLimit;
				if (supported is not { IsCurrent: true })
				{
					bool value;
					try
					{
						// At the root of the server, next to the api/v2/ of the base URI.
						var description = await this.Get<JsonElement>("../../openapi.json", new RequestQueryParams(), cancellationToken);
						value = description.ValueKind == JsonValueKind.Object
							&& description.TryGetProperty("components", out var components) && components.ValueKind == JsonValueKind.Object
							&& components.TryGetProperty("schemas", out var schemas) && schemas.ValueKind == JsonValueKind.Object
							&& schemas.TryGetProperty("DeleteCollectionRecordsPayload", out var payload)
							&& payload.GetRawText().Contains("\"limit\"");
					}
					catch (ChromaException)
					{
						value = false;
					}
					supported = new Fact<bool>(value);
					_server.DeleteLimit = supported;
				}
			}
			finally
			{
				_server.DeleteLimitLock.Release();
			}
		}
		return supported.Value;
	}

	private async Task<ChromaPreFlightChecks?> GetPreFlightChecks(CancellationToken cancellationToken)
	{
		var checks = _server.PreFlightChecks;
		if (checks is not { IsCurrent: true })
		{
			await _server.PreFlightChecksLock.WaitAsync(cancellationToken);
			try
			{
				checks = _server.PreFlightChecks;
				if (checks is not { IsCurrent: true })
				{
					ChromaPreFlightChecks? value;
					try
					{
						value = await this.Get<ChromaPreFlightChecks>(Routes.PreFlightChecks, new RequestQueryParams(), cancellationToken);
					}
					catch (ChromaException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
					{
						value = null;
					}
					checks = new Fact<ChromaPreFlightChecks?>(value);
					_server.PreFlightChecks = checks;
				}
			}
			finally
			{
				_server.PreFlightChecksLock.Release();
			}
		}
		return checks.Value;
	}

	public Task<HttpResponseMessage> SendAsync(HttpRequestMessage httpRequestMessage, CancellationToken cancellationToken)
	{
		if (_chromaToken is not null)
		{
			httpRequestMessage.Headers.Add(ClientConstants.ChromaTokenHeader, _chromaToken);
		}
		if (_authorization is not null)
		{
			httpRequestMessage.Headers.Authorization = _authorization;
		}
		return _httpClient.SendAsync(httpRequestMessage, cancellationToken);
	}

	// Each fact is one object, published with a volatile write, so a reader outside the lock sees it whole.
	internal sealed class ServerFacts
	{
		// How long a fact is trusted before it is asked again: the server can be upgraded or replaced while the client lives.
		internal static TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(2);

		public volatile Fact<string>? Version;
		public readonly SemaphoreSlim VersionLock = new(1, 1);
		public volatile Fact<ChromaPreFlightChecks?>? PreFlightChecks;
		public readonly SemaphoreSlim PreFlightChecksLock = new(1, 1);
		public volatile Fact<bool>? DeleteLimit;
		public volatile Fact<int>? RecordsLimit;
		public readonly SemaphoreSlim DeleteLimitLock = new(1, 1);
	}

	internal sealed class Fact<T>(T value)
	{
		private readonly DateTime _expires = DateTime.UtcNow + ServerFacts.Lifetime;

		public T Value { get; } = value;
		public bool IsCurrent => DateTime.UtcNow < _expires;
	}
}
