using System.Globalization;
using System.Net;
using ChromaDB.Client.Common;
using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;

namespace ChromaDB.Client;

/// <summary>
/// The client of a Chroma server, for its tenants, databases and collections. The records of a collection go through the
/// <c>ChromaCollectionClient</c> that <c>GetCollectionClient</c> returns.
/// </summary>
public class ChromaClient : IDisposable
{
	private readonly ChromaConfigurationOptions _options;
	private readonly ChromaHttpClient _httpClient;
	private readonly HttpClient? _ownHttpClient;
	private readonly ChromaTenant _currentTenant;
	private readonly ChromaDatabase _currentDatabase;

	/// <summary>
	/// The options of this client, as it was created with them.
	/// </summary>
	public virtual ChromaConfigurationOptions Options => _options;

	/// <summary>
	/// A client that sends its requests with the given <c>HttpClient</c>, to the server of the options. It adds the credentials
	/// of the options to each of its requests, without changing the <c>HttpClient</c>, and <c>Dispose</c> leaves it open.
	/// </summary>
	/// <param name="options">The options of the client: the server, the credentials, the tenant and the database.</param>
	/// <param name="httpClient">The <c>HttpClient</c> that sends the requests; the client does not dispose it.</param>
	public ChromaClient(ChromaConfigurationOptions options, HttpClient httpClient)
		: this(options, new ChromaHttpClient(httpClient, options))
	{ }

	/// <summary>
	/// A client of the server at the URI, like <c>http://localhost:8000</c>, with an <c>HttpClient</c> of its own, which
	/// <c>Dispose</c> closes.
	/// </summary>
	/// <param name="uri">The URI of the server, like <c>http://localhost:8000</c>.</param>
	public ChromaClient(string uri)
		: this(new ChromaConfigurationOptions(uri))
	{ }

	/// <summary>
	/// A client of the server of the options, with an <c>HttpClient</c> of its own, which <c>Dispose</c> closes. On .NET 8 and later
	/// its connections last two minutes, as with <c>AddChromaClient</c>, so a change of the address of the server in the DNS is seen.
	/// The clients that <c>WithMetadataValues</c>, <c>WithTenantAndDatabaseFromIdentityAsync</c> and <c>GetCollectionClient</c> return
	/// share that <c>HttpClient</c>: they work until this client is disposed.
	/// </summary>
	/// <param name="options">The options of the client: the server, the credentials, the tenant and the database.</param>
	public ChromaClient(ChromaConfigurationOptions options)
		: this(options, CreateHttpClient(options, out var ownHttpClient))
	{
		_ownHttpClient = ownHttpClient;
	}

	/// <summary>
	/// For a mock in tests, made by a subclass or by a mocking library, as in the Azure SDKs: a client without options and
	/// without an <c>HttpClient</c>, whose members are all virtual. It sends no request: only the members it overrides work.
	/// </summary>
	protected ChromaClient()
	{
		_options = null!;
		_httpClient = null!;
		_currentTenant = null!;
		_currentDatabase = null!;
	}

	// An HttpClient of its own, closed when the options are rejected, like basic authentication with a token in the Authorization header.
	private static ChromaHttpClient CreateHttpClient(ChromaConfigurationOptions options, out HttpClient httpClient)
	{
		httpClient = NewHttpClient();
		try
		{
			return new ChromaHttpClient(httpClient, options);
		}
		catch
		{
			httpClient.Dispose();
			throw;
		}
	}

	// As the handlers of IHttpClientFactory, renewed every two minutes by default; SocketsHttpHandler is not in .NET Standard 2.0.
	private static HttpClient NewHttpClient()
#if NET
		=> new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) });
#else
		=> new();
#endif

	/// <summary>
	/// Closes the <c>HttpClient</c> the client created; one given to the constructor stays open.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Closes the <c>HttpClient</c> the client created, when <c>disposing</c>.
	/// </summary>
	/// <param name="disposing">Whether it is called by <c>Dispose()</c>, not by a finalizer.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			_ownHttpClient?.Dispose();
		}
	}

	// The span and the duration of each operation, on the server, on a tenant, or in a database: the tenant and the database
	// are the ones the request goes to.
	private Task<T> ServerOperation<T>(string name, Func<Task<T>> body)
		=> ChromaInstrumentation.Run(name, null, null, _options.Uri, body);

	private Task<T> TenantOperation<T>(string name, string? tenant, Func<Task<T>> body)
		=> ChromaInstrumentation.Run(name, null, TenantName(tenant), _options.Uri, body);

	private Task TenantOperation(string name, string? tenant, Func<Task> body)
		=> ChromaInstrumentation.Run(name, null, TenantName(tenant), _options.Uri, body);

	private Task<T> DatabaseOperation<T>(string name, string? collection, string? tenant, string? database, Func<Task<T>> body)
		=> ChromaInstrumentation.Run(name, collection, $"{TenantName(tenant)}|{DatabaseName(database)}", _options.Uri, body);

	private Task DatabaseOperation(string name, string? collection, string? tenant, string? database, Func<Task> body)
		=> ChromaInstrumentation.Run(name, collection, $"{TenantName(tenant)}|{DatabaseName(database)}", _options.Uri, body);

	// Chroma Cloud creates a collection with a key of the schema beyond its quota of bytes for a metadata key, and then rejects every
	// write with the key.
	private void CheckKeysOnChromaCloud(ChromaCollectionDefinition definition)
	{
		if (_httpClient.IsChromaCloud && definition.Schema?.Keys.FirstOrDefault(key => System.Text.Encoding.UTF8.GetByteCount(key) > ChromaCloudQuotas.MaxMetadataKeyBytes) is { } key)
		{
			throw new ArgumentException($"The key \"{key}\" of the schema has more than {ChromaCloudQuotas.MaxMetadataKeyBytes} bytes: Chroma Cloud creates the collection, but then rejects every write with the key.", nameof(definition));
		}
	}

	// The message when the collection the server answers with does not have the settings of the definition: another space, as a
	// collection that exists keeps its own and Chroma 1.3.0 ignores the one of a schema that turns no index on or off, or an index other
	// than the one the settings are for. Chroma 1.0.5 and earlier send no configuration to tell it by.
	// As the Python client of Chroma, a new collection with the generator of the embedding function of Chroma declares it, "default". Chroma
	// 0.x takes no embedding function: there the collection is created without, as before.
	private async Task<ChromaCollectionDefinition> WithDeclaredEmbeddingFunction(ChromaCollectionDefinition definition, CancellationToken cancellationToken)
	{
#if !NETSTANDARD2_0
		var declared = definition.WithDeclaredEmbeddingFunction();
		if (declared != definition && !await _httpClient.IsChroma0(cancellationToken))
		{
			return declared;
		}
#else
		await Task.CompletedTask;
#endif
		return definition;
	}

	private static string? SettingsIgnored(ChromaCollectionDefinition definition, ChromaCollection collection)
	{
		if (definition.Configuration is not { } settings)
		{
			return null;
		}
		if (settings.Space is { } space && collection.Space is { } actual && actual != space)
		{
			return $"The collection has the space {ChromaSpaceNames.ToName(actual)}, not {ChromaSpaceNames.ToName(space)}: a collection that exists keeps its space"
				+ (definition.SettingsInSchema ? ", and Chroma 1.3.0 ignores the space of a schema that turns no index on or off, which 1.3.2 and later apply." : ".");
		}
		if (collection.ConfigurationJson is not { ValueKind: System.Text.Json.JsonValueKind.Object } configuration)
		{
			return null;
		}
		var hasHnsw = configuration.TryGetProperty("hnsw", out var hnsw) && hnsw.ValueKind == System.Text.Json.JsonValueKind.Object;
		var hasSpann = configuration.TryGetProperty("spann", out var spann) && spann.ValueKind == System.Text.Json.JsonValueKind.Object;
		if (settings.Hnsw is not null && !hasHnsw && hasSpann)
		{
			return "The collection has a SPANN index, as on Chroma Cloud, which ignores HNSW settings: set Spann instead.";
		}
		if (settings.Spann is not null && !hasSpann && hasHnsw)
		{
			return "The collection has an HNSW index, as on a single Chroma server, which ignores SPANN settings: set Hnsw instead.";
		}
		return null;
	}

	private string TenantName(string? tenant)
		=> tenant is not null and not [] ? tenant : _currentTenant.Name;

	private string DatabaseName(string? database)
		=> database is not null and not [] ? database : _currentDatabase.Name;

	private ChromaClient(ChromaConfigurationOptions options, ChromaHttpClient httpClient)
	{
		_options = options;
		_httpClient = httpClient;
		_currentTenant = options.Tenant is not null and not []
			? new ChromaTenant(options.Tenant)
			: ClientConstants.DefaultTenant;
		_currentDatabase = options.Database is not null and not []
			? new ChromaDatabase(options.Database)
			: ClientConstants.DefaultDatabase;
	}

	/// <summary>
	/// The collections in the tenant and database of the options, or in the ones it is given.
	/// </summary>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <param name="database">The database, or null for the one of the options.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The collections.</returns>
	public virtual Task<IReadOnlyList<ChromaCollection>> ListCollectionsAsync(string? tenant = null, string? database = null, CancellationToken cancellationToken = default)
		=> DatabaseOperation<IReadOnlyList<ChromaCollection>>("list_collections", null, tenant, database, async () =>
		{
			tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
			database = database is not null and not [] ? database : _currentDatabase.Name;
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", tenant)
				.Insert("{database}", database);
			return await _httpClient.WithSpaces(await _httpClient.Get<List<ChromaCollection>>(_httpClient.Routes.Collections, requestParams, cancellationToken), cancellationToken);
		});

	/// <summary>
	/// One page of the collections, in the order of the server.
	/// </summary>
	/// <param name="limit">The most collections to return.</param>
	/// <param name="offset">How many collections to skip first.</param>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <param name="database">The database, or null for the one of the options.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The collections.</returns>
	public virtual Task<IReadOnlyList<ChromaCollection>> ListCollectionsAsync(int limit, int offset = 0, string? tenant = null, string? database = null, CancellationToken cancellationToken = default)
		=> DatabaseOperation<IReadOnlyList<ChromaCollection>>("list_collections", null, tenant, database, async () =>
		{
			tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
			database = database is not null and not [] ? database : _currentDatabase.Name;
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", tenant)
				.Insert("{database}", database)
				.Insert("{limit}", limit.ToString(CultureInfo.InvariantCulture))
				.Insert("{offset}", offset.ToString(CultureInfo.InvariantCulture));
			var route = _httpClient.Routes.Collections;
			route += (route.Contains("?") ? "&" : "?") + "limit={limit}&offset={offset}";
			return await _httpClient.WithSpaces(await _httpClient.Get<List<ChromaCollection>>(route, requestParams, cancellationToken), cancellationToken);
		});

	/// <summary>
	/// The collection with the given name, in the tenant and database of the options, or in the ones it is given.
	/// </summary>
	/// <param name="name">The name of the collection.</param>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <param name="database">The database, or null for the one of the options.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The collection.</returns>
	public virtual Task<ChromaCollection> GetCollectionAsync(string name, string? tenant = null, string? database = null, CancellationToken cancellationToken = default)
		=> DatabaseOperation("get_collection", name, tenant, database, () => GetCollectionCore(name, tenant, database, cancellationToken));

	// The collection, or null when it does not exist.
	private async Task<ChromaCollection?> TryGetCollection(string name, string? tenant, string? database, CancellationToken cancellationToken)
	{
		try
		{
			return await GetCollectionCore(name, tenant, database, cancellationToken);
		}
		catch (ChromaException ex) when (ex.IsMissingCollection)
		{
			return null;
		}
	}

	// Without a span of its own: CollectionExists has one, where a missing collection is not an error.
	private async Task<ChromaCollection> GetCollectionCore(string name, string? tenant, string? database, CancellationToken cancellationToken)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		var requestParams = new RequestQueryParams()
			.Insert("{collectionName}", name)
			.Insert("{tenant}", tenant)
			.Insert("{database}", database);
		return await _httpClient.WithSpace(await _httpClient.Get<ChromaCollection>(_httpClient.Routes.CollectionByName, requestParams, cancellationToken), cancellationToken);
	}

	/// <summary>
	/// Whether the collection exists. A missing collection: <c>404</c> from Chroma 1.x, <c>400</c> or <c>500</c> from the 0.x
	/// servers, always with "does not exist" in the message, also when the tenant or the database is missing. Any other error,
	/// like a bare <c>404</c> from a wrong address, still throws.
	/// </summary>
	/// <param name="name">The name of the collection.</param>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <param name="database">The database, or null for the one of the options.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>Whether the collection exists.</returns>
	public virtual Task<bool> CollectionExistsAsync(string name, string? tenant = null, string? database = null, CancellationToken cancellationToken = default)
		=> DatabaseOperation("collection_exists", name, tenant, database, async () => await TryGetCollection(name, tenant, database, cancellationToken) is not null);

	/// <summary>
	/// The same client, reading metadata values another way: same <c>HttpClient</c> and options, and what it learned about the
	/// server. No request is sent, and this client does not change.
	/// </summary>
	/// <param name="metadataValues">How the client reads the values of the metadata.</param>
	/// <returns>The new client; this one does not change.</returns>
	public virtual ChromaClient WithMetadataValues(ChromaMetadataValues metadataValues)
		=> new(_options.WithMetadataValues(metadataValues), _httpClient.WithMetadataValues(metadataValues));

	/// <summary>
	/// The same client, with the tenant and the database of its credentials, which the server tells in <c>auth/identity</c>, as the
	/// <c>CloudClient</c> of the Python client of Chroma does. The tenant is taken unless it is <c>*</c>; the database only when the
	/// credentials have exactly one, other than <c>*</c>: on Chroma Cloud an API key for one database gives both, an API key for a
	/// whole tenant only the tenant. A tenant or a database of the options other than the default one must match the one the
	/// credentials give, when they give one, otherwise it throws a <c>ChromaException</c>; with several databases in the credentials,
	/// the one of the options stays as it is, as in the Python client. The same <c>HttpClient</c> and what this client learned about
	/// the server are kept, and this client does not change.
	/// </summary>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The new client; this one does not change.</returns>
	public virtual async Task<ChromaClient> WithTenantAndDatabaseFromIdentityAsync(CancellationToken cancellationToken = default)
	{
		var identity = await GetUserIdentityAsync(cancellationToken);
		var tenant = identity.Tenant is { Length: > 0 } and not "*" ? identity.Tenant : null;
		var databases = identity.Databases?.Distinct().ToList();
		var database = databases is [{ Length: > 0 } single] && single != "*" ? single : null;
		if (tenant is not null && _options.Tenant is { Length: > 0 } givenTenant && givenTenant != ClientConstants.DefaultTenantName && givenTenant != tenant)
		{
			throw new ChromaException($"The tenant {givenTenant} of the options is not {tenant}, the one of the credentials.");
		}
		if (database is not null && _options.Database is { Length: > 0 } givenDatabase && givenDatabase != ClientConstants.DefaultDatabaseName && givenDatabase != database)
		{
			throw new ChromaException($"The database {givenDatabase} of the options is not {database}, the one of the credentials.");
		}
		var options = tenant is not null ? _options.WithTenant(tenant) : _options;
		options = database is not null ? options.WithDatabase(database) : options;
		return new(options, _httpClient);
	}

	/// <summary>
	/// A client for the records of the collection, with the options and the <c>HttpClient</c> of this client: no request is sent.
	/// </summary>
	/// <param name="collection">The collection, as the server returned it.</param>
	/// <returns>The client of the records of the collection.</returns>
	public virtual ChromaCollectionClient GetCollectionClient(ChromaCollection collection)
		=> new(collection, _options, _httpClient);

	/// <summary>
	/// A client for the records of the collection with the name, in the tenant and database of the options, whichever collection has
	/// that name: it reads the collection before its first request, and again when a request fails on the id it read before. When the
	/// name has another id then, as when the collection was deleted and created again elsewhere, the operation runs again, once, on that
	/// collection; otherwise the failure stands. No request is sent now.
	/// </summary>
	/// <param name="name">The name of the collection.</param>
	/// <returns>The client of the records of the collection with the name.</returns>
	public virtual ChromaCollectionClient GetCollectionClient(string name)
		=> new(name, _options, _httpClient);

	/// <summary>
	/// The same as the overload that takes a <c>ChromaCollection</c>, without getting the collection first: the requests on
	/// a collection need only its id.
	/// </summary>
	/// <param name="collectionId">The id of the collection.</param>
	/// <param name="collectionName">The name of the collection.</param>
	/// <returns>The client of the records of the collection.</returns>
	public virtual ChromaCollectionClient GetCollectionClient(Guid collectionId, string collectionName)
		=> new(new ChromaCollection(collectionName) { Id = collectionId }, _options, _httpClient);

	/// <summary>
	/// The collection with the given id, in the tenant and database of the options, or in the ones it is given. It needs the v2 API
	/// of Chroma 1.5.7 or later: the older servers answer <c>404 Not Found</c>.
	/// </summary>
	/// <param name="id">The id of the collection.</param>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <param name="database">The database, or null for the one of the options.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The collection.</returns>
	public virtual Task<ChromaCollection> GetCollectionByIdAsync(Guid id, string? tenant = null, string? database = null, CancellationToken cancellationToken = default)
		=> DatabaseOperation("get_collection", null, tenant, database, async () =>
		{
			tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
			database = database is not null and not [] ? database : _currentDatabase.Name;
			var requestParams = new RequestQueryParams()
				.Insert("{collection_id}", id.ToString())
				.Insert("{tenant}", tenant)
				.Insert("{database}", database);
			return await _httpClient.WithSpace(await _httpClient.Get<ChromaCollection>(_httpClient.Routes.CollectionById, requestParams, cancellationToken), cancellationToken);
		});

	/// <summary>
	/// The heartbeat of the server, a time in nanoseconds.
	/// </summary>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The heartbeat.</returns>
	public virtual Task<ChromaHeartbeat> HeartbeatAsync(CancellationToken cancellationToken = default)
		=> ServerOperation("heartbeat", async () =>
		{
			return await _httpClient.Get<ChromaHeartbeat>(_httpClient.Routes.Heartbeat, new RequestQueryParams(), cancellationToken);
		});

	/// <summary>
	/// Whether the server is ready to serve requests, from Chroma 1.0.0; a server that is not ready answers <c>503</c>,
	/// a <c>ChromaException</c>.
	/// </summary>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>Whether the parts of the server are ready.</returns>
	public virtual Task<ChromaHealthcheck> HealthcheckAsync(CancellationToken cancellationToken = default)
		=> ServerOperation("healthcheck", async () =>
		{
			return await _httpClient.Get<ChromaHealthcheck>(_httpClient.Routes.Healthcheck, new RequestQueryParams(), cancellationToken);
		});

	/// <summary>
	/// A collection by its Chroma Resource Name, <c>&lt;tenant resource name&gt;:&lt;database&gt;:&lt;collection&gt;</c>:
	/// Chroma Cloud only, as in the JavaScript client of Chroma. The operation is hidden in the OpenAPI description. On Chroma Cloud
	/// this request, sent with an API key limited to one database and with an API key for the whole tenant, got <c>403</c>, also for
	/// a collection of that tenant.
	/// </summary>
	/// <param name="crn">The Chroma Resource Name of the collection, <c>&lt;tenant resource name&gt;:&lt;database&gt;:&lt;collection&gt;</c>.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The collection.</returns>
	public virtual Task<ChromaCollection> GetCollectionByCrnAsync(string crn, CancellationToken cancellationToken = default)
		=> ServerOperation("get_collection", async () =>
		{
			var requestParams = new RequestQueryParams()
				.Insert("{crn}", crn);
			return await _httpClient.Get<ChromaCollection>(_httpClient.Routes.CollectionByCrn, requestParams, cancellationToken);
		});

	/// <summary>
	/// Creates a collection with the given name and metadata, in the tenant and database of the options, or in the ones it is given.
	/// </summary>
	/// <param name="name">The name of the collection.</param>
	/// <param name="metadata">The metadata of the collection, or null for none.</param>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <param name="database">The database, or null for the one of the options.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The collection created.</returns>
	public virtual Task<ChromaCollection> CreateCollectionAsync(string name, IReadOnlyDictionary<string, object>? metadata = null, string? tenant = null, string? database = null, CancellationToken cancellationToken = default)
		=> CreateCollectionAsync(new ChromaCollectionDefinition(name) { Metadata = metadata }, tenant, database, cancellationToken);

	/// <summary>
	/// Creates a collection from its definition, with its name, metadata, configuration and schema, in the tenant and database of the
	/// options, or in the ones it is given. The space and the HNSW settings go as the <c>hnsw:</c> metadata, the SPANN settings and the
	/// embedding function in the configuration of the request. When the server ignores settings it reports, it deletes the collection
	/// and throws a <c>ChromaException</c>.
	/// </summary>
	/// <param name="definition">The collection: name, metadata, configuration and schema.</param>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <param name="database">The database, or null for the one of the options.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The collection created.</returns>
	public virtual Task<ChromaCollection> CreateCollectionAsync(ChromaCollectionDefinition definition, string? tenant = null, string? database = null, CancellationToken cancellationToken = default)
		=> DatabaseOperation("create_collection", definition.Name, tenant, database, async () =>
		{
			tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
			database = database is not null and not [] ? database : _currentDatabase.Name;
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", tenant)
				.Insert("{database}", database);
			definition.Validate();
			definition = await WithDeclaredEmbeddingFunction(definition.ForServer(_httpClient.IsChromaCloud), cancellationToken);
			CheckKeysOnChromaCloud(definition);
			// Chroma 0.5.4 to 0.6.3 fail on the configuration of the request, and 0.4.10 to 0.5.3 ignore it.
			if (definition.SettingsInConfiguration && await _httpClient.IsChroma0(cancellationToken))
			{
				throw new ChromaException("The SPANN settings and the embedding function of a new collection need Chroma 1.0.0 or later: Chroma 0.x does not apply them.");
			}
			var request = new CreateCollectionRequest()
			{
				Name = definition.Name,
				Metadata = definition.ToRequestMetadata(),
				Configuration = definition.ToRequestConfiguration(),
				Schema = definition.ToRequestSchema(),
			};
			var collection = await _httpClient.Post<CreateCollectionRequest, ChromaCollection>(_httpClient.Routes.Collections, request, requestParams, cancellationToken);
			// Chroma 1.0.0 to 1.2.2 create the collection without the schema and without an error: the collection just created goes.
			if (definition.SettingsInSchema && collection.SchemaJson is not { ValueKind: System.Text.Json.JsonValueKind.Object })
			{
				// Not canceled with the call: the answer arrived, so the collection is created, and it must go. A call canceled before the
				// answer leaves nothing to tell whether the collection was created, so nothing is deleted then.
				await DatabaseOperation("delete_collection", collection.Name, tenant, database, () => DeleteCollectionCore(collection.Name, tenant, database, deleteRecordsFirst: false, CancellationToken.None));
				throw new ChromaException("The server creates the collection without its schema: Chroma 1.3.0 and later apply it. The collection was deleted.");
			}
			// As the deletion, not canceled with the call: the answer arrived, and the space decides whether the collection must go.
			await _httpClient.WithSpace(collection, CancellationToken.None);
			// Chroma 1.3.0 creates the collection with the space of a schema that turns no index on or off ignored: l2.
			if (SettingsIgnored(definition, collection) is { } ignored)
			{
				await DatabaseOperation("delete_collection", collection.Name, tenant, database, () => DeleteCollectionCore(collection.Name, tenant, database, deleteRecordsFirst: false, CancellationToken.None));
				throw new ChromaException($"{ignored} The collection was deleted.");
			}
			return collection;
		});

	/// <summary>
	/// The collection with the given name, created when it does not exist, in the tenant and database of the options, or in
	/// the ones it is given.
	/// </summary>
	/// <param name="name">The name of the collection.</param>
	/// <param name="metadata">The metadata of the collection, or null for none.</param>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <param name="database">The database, or null for the one of the options.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The collection, created or existing.</returns>
	public virtual Task<ChromaCollection> GetOrCreateCollectionAsync(string name, IReadOnlyDictionary<string, object>? metadata = null, string? tenant = null, string? database = null, CancellationToken cancellationToken = default)
		=> GetOrCreateCollectionAsync(new ChromaCollectionDefinition(name) { Metadata = metadata }, tenant, database, cancellationToken);

	/// <summary>
	/// The collection with the name of the definition, created from the definition when it does not exist, in the tenant and
	/// database of the options, or in the ones it is given, with the settings of the configuration as <c>CreateCollectionAsync</c> sends them.
	/// </summary>
	/// <param name="definition">The collection: name, metadata, configuration and schema.</param>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <param name="database">The database, or null for the one of the options.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The collection, created or existing.</returns>
	public virtual Task<ChromaCollection> GetOrCreateCollectionAsync(ChromaCollectionDefinition definition, string? tenant = null, string? database = null, CancellationToken cancellationToken = default)
		=> DatabaseOperation("get_or_create_collection", definition.Name, tenant, database, async () =>
		{
			tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
			database = database is not null and not [] ? database : _currentDatabase.Name;
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", tenant)
				.Insert("{database}", database);
			definition.Validate();
			definition = await WithDeclaredEmbeddingFunction(definition.ForServer(_httpClient.IsChromaCloud), cancellationToken);
			CheckKeysOnChromaCloud(definition);
			// Chroma 0.5.4 to 0.6.3 fail on the configuration of the request, and 0.4.10 to 0.5.3 ignore it.
			if (definition.SettingsInConfiguration && await _httpClient.IsChroma0(cancellationToken))
			{
				throw new ChromaException("The SPANN settings and the embedding function of a new collection need Chroma 1.0.0 or later: Chroma 0.x does not apply them.");
			}
			// Chroma 0.4 writes the space of the definition into the metadata of a collection that exists, whose index keeps its own
			// space, so that the collection then reports a space it does not use: on the 0.x servers the client reads the collection
			// first, and throws before the request when it has another space.
			if (definition.Configuration?.Space is not null && await _httpClient.IsChroma0(cancellationToken)
				&& await TryGetCollection(definition.Name, tenant, database, cancellationToken) is { } existing
				&& SettingsIgnored(definition, existing) is { } overwritten)
			{
				throw new ChromaException(overwritten);
			}
			var request = new GetOrCreateCollectionRequest()
			{
				Name = definition.Name,
				Metadata = definition.ToRequestMetadata(),
				Configuration = definition.ToRequestConfiguration(),
				Schema = definition.ToRequestSchema(),
			};
			var collection = await _httpClient.WithSpace(await _httpClient.Post<GetOrCreateCollectionRequest, ChromaCollection>(_httpClient.Routes.Collections, request, requestParams, cancellationToken), cancellationToken);
			// As in CreateCollection, but the collection stays: it may have existed before.
			if (definition.SettingsInSchema && collection.SchemaJson is not { ValueKind: System.Text.Json.JsonValueKind.Object })
			{
				throw new ChromaException("The server answers without the schema of the collection: Chroma 1.3.0 and later apply it.");
			}
			if (SettingsIgnored(definition, collection) is { } ignored)
			{
				throw new ChromaException(ignored);
			}
			return collection;
		});

	/// <summary>
	/// Deletes the collection with the given name, in the tenant and database of the options, or in the ones it is given, with one
	/// request. The overload with <c>deleteRecordsFirst</c> can delete the records first.
	/// </summary>
	/// <param name="name">The name of the collection.</param>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <param name="database">The database, or null for the one of the options.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	public virtual Task DeleteCollectionAsync(string name, string? tenant = null, string? database = null, CancellationToken cancellationToken = default)
		=> DeleteCollectionAsync(name, deleteRecordsFirst: false, tenant, database, cancellationToken);

	/// <summary>
	/// Deletes the collection with the given name, in the tenant and database of the options, or in the ones it is given.
	/// </summary>
	/// <param name="name">The name of the collection.</param>
	/// <param name="deleteRecordsFirst">Whether to delete the records first, in batches, on Chroma 1.x except Chroma Cloud. Chroma 1.5 gives
	/// the lists in the metadata of the records of a deleted collection to the next records it stores, in other collections too: pass
	/// true when the records have lists in their metadata. It takes about two requests for every 5,461 records, about 370 for a million.
	/// If it stops halfway, the collection keeps part of its records or none of them: delete it again.</param>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <param name="database">The database, or null for the one of the options.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	public virtual Task DeleteCollectionAsync(string name, bool deleteRecordsFirst, string? tenant = null, string? database = null, CancellationToken cancellationToken = default)
		=> DatabaseOperation("delete_collection", name, tenant, database, () => DeleteCollectionCore(name, tenant, database, deleteRecordsFirst, cancellationToken));

	/// <summary>
	/// Deletes the collection with the given name when it exists, as <c>DeleteCollectionAsync</c> does, and tells whether it did. A missing
	/// collection is not an error: every server tells it in its own way, which the client recognizes as <c>CollectionExistsAsync</c> does.
	/// </summary>
	/// <param name="name">The name of the collection.</param>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <param name="database">The database, or null for the one of the options.</param>
	/// <param name="deleteRecordsFirst">Whether to delete the records first, as in <c>DeleteCollectionAsync</c>.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>Whether the collection existed and was deleted.</returns>
	public virtual Task<bool> DeleteCollectionIfExistsAsync(string name, string? tenant = null, string? database = null, bool deleteRecordsFirst = false, CancellationToken cancellationToken = default)
		=> DatabaseOperation("delete_collection", name, tenant, database, async () =>
		{
			try
			{
				await DeleteCollectionCore(name, tenant, database, deleteRecordsFirst, cancellationToken);
				return true;
			}
			catch (ChromaException ex) when (ex.IsMissingCollection)
			{
				return false;
			}
		});

	// Without a span of its own. A collection just created has no records to delete first.
	private async Task DeleteCollectionCore(string name, string? tenant, string? database, bool deleteRecordsFirst, CancellationToken cancellationToken)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		var requestParams = new RequestQueryParams()
			.Insert("{collectionName}", name)
			.Insert("{tenant}", tenant)
			.Insert("{database}", database);
		if (deleteRecordsFirst && await KeepsTheListsOfDeletedRecords(cancellationToken))
		{
			await new ChromaCollectionClient(await GetCollectionCore(name, tenant, database, cancellationToken), _options, _httpClient).DeleteAllRecords(cancellationToken);
		}
		await _httpClient.Delete(_httpClient.Routes.CollectionByName, requestParams, cancellationToken);
	}

	// Works around KD-12 (docs/COMPATIBILITY.md)
	// Chroma 1.5 keeps the lists in the metadata of the records of a collection or a database it deletes, and gives them to the next
	// records it stores, in any collection and database; deleting the records first deletes their lists. Every Chroma 1.x sends the
	// same version, so the records go first on all of them: not on Chroma 0.x, which stores no lists, nor on Chroma Cloud.
	private async Task<bool> KeepsTheListsOfDeletedRecords(CancellationToken cancellationToken)
		=> !_httpClient.IsChromaCloud && !await _httpClient.IsChroma0(cancellationToken);

	/// <summary>
	/// The version of the server. The 0.x servers send their own version; every Chroma 1.x answers <c>1.0.0</c>.
	/// </summary>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The version of the server, like <c>1.5.9</c>.</returns>
	public virtual Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
		=> ServerOperation("get_version", async () =>
		{
			return await _httpClient.Get<string>(_httpClient.Routes.Version, new RequestQueryParams(), cancellationToken);
		});

	/// <summary>
	/// The user the server sees for the credentials of the client, with its tenant and databases.
	/// </summary>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The identity: the user, the tenant and the databases of the credentials.</returns>
	public virtual Task<ChromaUserIdentity> GetUserIdentityAsync(CancellationToken cancellationToken = default)
		=> ServerOperation("get_user_identity", async () =>
		{
			return await _httpClient.Get<ChromaUserIdentity>(_httpClient.Routes.UserIdentity, new RequestQueryParams(), cancellationToken);
		});

	/// <summary>
	/// The limits of the server from <c>pre-flight-checks</c>, like <c>max_batch_size</c>, the most records a single add, update,
	/// upsert or delete can carry.
	/// </summary>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The limits and the features the server declares.</returns>
	public virtual Task<ChromaPreFlightChecks> GetPreFlightChecksAsync(CancellationToken cancellationToken = default)
		=> ServerOperation("get_pre_flight_checks", async () =>
		{
			return await _httpClient.Get<ChromaPreFlightChecks>(_httpClient.Routes.PreFlightChecks, new RequestQueryParams(), cancellationToken);
		});

	/// <summary>
	/// Resets the server and returns its answer. Chroma Cloud does not allow it to an API key.
	/// </summary>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>Whether the server was reset.</returns>
	public virtual Task<bool> ResetAsync(CancellationToken cancellationToken = default)
		=> ServerOperation("reset", async () =>
		{
			return await _httpClient.Post<ResetRequest, bool>(_httpClient.Routes.Reset, null, new RequestQueryParams(), cancellationToken);
		});

	/// <summary>
	/// The number of collections in the tenant and database of the options, or in the ones it is given.
	/// </summary>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <param name="database">The database, or null for the one of the options.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The number of collections.</returns>
	public virtual Task<int> CountCollectionsAsync(string? tenant = null, string? database = null, CancellationToken cancellationToken = default)
		=> DatabaseOperation("count_collections", null, tenant, database, async () =>
		{
			tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
			database = database is not null and not [] ? database : _currentDatabase.Name;
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", tenant)
				.Insert("{database}", database);
			return await _httpClient.Get<int>(_httpClient.Routes.CollectionsCount, requestParams, cancellationToken);
		});

	/// <summary>
	/// Creates a tenant with the given name. Chroma Cloud does not allow it to an API key.
	/// </summary>
	/// <param name="name">The name of the tenant.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	public virtual Task CreateTenantAsync(string name, CancellationToken cancellationToken = default)
		=> TenantOperation("create_tenant", name, async () =>
		{
			var request = new CreateTenantRequest()
			{
				Name = name,
			};
			await _httpClient.Post(_httpClient.Routes.Tenants, request, new RequestQueryParams(), cancellationToken);
		});

	/// <summary>
	/// The tenant with the given name, with the resource name that <c>UpdateTenantAsync</c> sets.
	/// </summary>
	/// <param name="name">The name of the tenant.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The tenant.</returns>
	public virtual Task<ChromaTenant> GetTenantAsync(string name, CancellationToken cancellationToken = default)
		=> TenantOperation("get_tenant", name, async () =>
		{
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", name);
			return await _httpClient.Get<ChromaTenant>(_httpClient.Routes.Tenant, requestParams, cancellationToken);
		});

	/// <summary>
	/// Sets the name of the tenant in the resource names of Chroma Cloud, like the CRN of a collection.
	/// </summary>
	/// <param name="name">The name of the tenant.</param>
	/// <param name="resourceName">The resource name of the tenant, the first part of the CRNs of its collections.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	public virtual Task UpdateTenantAsync(string name, string resourceName, CancellationToken cancellationToken = default)
		=> TenantOperation("update_tenant", name, async () =>
		{
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", name);
			var request = new UpdateTenantRequest()
			{
				ResourceName = resourceName,
			};
			await _httpClient.Patch(_httpClient.Routes.Tenant, request, requestParams, cancellationToken);
		});

	/// <summary>
	/// Creates a database with the given name, in the tenant of the options, or in the one it is given.
	/// </summary>
	/// <param name="name">The name of the database.</param>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	public virtual Task CreateDatabaseAsync(string name, string? tenant = null, CancellationToken cancellationToken = default)
		=> DatabaseOperation("create_database", null, tenant, name, async () =>
		{
			tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", tenant);
			var request = new CreateDatabaseRequest()
			{
				Name = name,
			};
			await _httpClient.Post(_httpClient.Routes.Databases, request, requestParams, cancellationToken);
		});

	/// <summary>
	/// The database with the given name, in the tenant of the options, or in the one it is given.
	/// </summary>
	/// <param name="name">The name of the database.</param>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The database.</returns>
	public virtual Task<ChromaDatabase> GetDatabaseAsync(string name, string? tenant = null, CancellationToken cancellationToken = default)
		=> DatabaseOperation("get_database", null, tenant, name, async () =>
		{
			tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
			var requestParams = new RequestQueryParams()
				.Insert("{database}", name)
				.Insert("{tenant}", tenant);
			return await _httpClient.Get<ChromaDatabase>(_httpClient.Routes.Database, requestParams, cancellationToken);
		});

	/// <summary>
	/// The databases in the tenant of the options, or in the one it is given. It needs the v2 API of Chroma 0.6.3 or later:
	/// the older servers answer <c>405 Method Not Allowed</c>.
	/// </summary>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The databases.</returns>
	public virtual Task<IReadOnlyList<ChromaDatabase>> ListDatabasesAsync(string? tenant = null, CancellationToken cancellationToken = default)
		=> TenantOperation<IReadOnlyList<ChromaDatabase>>("list_databases", tenant, async () =>
		{
			tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", tenant);
			return await _httpClient.Get<List<ChromaDatabase>>(_httpClient.Routes.Databases, requestParams, cancellationToken);
		});

	/// <summary>
	/// One page of the databases, in the order of the server.
	/// </summary>
	/// <param name="limit">The most databases to return.</param>
	/// <param name="offset">How many databases to skip first.</param>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The databases.</returns>
	public virtual Task<IReadOnlyList<ChromaDatabase>> ListDatabasesAsync(int limit, int offset = 0, string? tenant = null, CancellationToken cancellationToken = default)
		=> TenantOperation<IReadOnlyList<ChromaDatabase>>("list_databases", tenant, async () =>
		{
			tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", tenant)
				.Insert("{limit}", limit.ToString(CultureInfo.InvariantCulture))
				.Insert("{offset}", offset.ToString(CultureInfo.InvariantCulture));
			var route = _httpClient.Routes.Databases;
			route += (route.Contains("?") ? "&" : "?") + "limit={limit}&offset={offset}";
			return await _httpClient.Get<List<ChromaDatabase>>(route, requestParams, cancellationToken);
		});

	/// <summary>
	/// Deletes the database with the given name, in the tenant of the options, or in the one it is given, with one request. It needs
	/// the v2 API of Chroma 0.6.3 or later: the older servers answer <c>405 Method Not Allowed</c>. The overload with
	/// <c>deleteRecordsFirst</c> can delete the records of its collections first.
	/// </summary>
	/// <param name="name">The name of the database.</param>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	public virtual Task DeleteDatabaseAsync(string name, string? tenant = null, CancellationToken cancellationToken = default)
		=> DeleteDatabaseAsync(name, deleteRecordsFirst: false, tenant, cancellationToken);

	/// <summary>
	/// Deletes the database with the given name, in the tenant of the options, or in the one it is given. It needs the v2 API of
	/// Chroma 0.6.3 or later: the older servers answer <c>405 Method Not Allowed</c>.
	/// </summary>
	/// <param name="name">The name of the database.</param>
	/// <param name="deleteRecordsFirst">Whether to delete the records of its collections first, as in <c>DeleteCollectionAsync</c>.</param>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	public virtual Task DeleteDatabaseAsync(string name, bool deleteRecordsFirst, string? tenant = null, CancellationToken cancellationToken = default)
		=> DatabaseOperation("delete_database", null, tenant, name, async () =>
		{
			tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
			var requestParams = new RequestQueryParams()
				.Insert("{database}", name)
				.Insert("{tenant}", tenant);
			if (deleteRecordsFirst && await KeepsTheListsOfDeletedRecords(cancellationToken))
			{
				foreach (var collection in await _httpClient.Get<List<ChromaCollection>>(_httpClient.Routes.Collections, requestParams, cancellationToken))
				{
					await new ChromaCollectionClient(collection, _options, _httpClient).DeleteAllRecords(cancellationToken);
				}
			}
			await _httpClient.Delete(_httpClient.Routes.Database, requestParams, cancellationToken);
		});
}
