using ChromaDB.Client.Common;

namespace ChromaDB.Client;

/// <summary>
/// The options of a client: the server, the default tenant and database, the credentials, the API version, how metadata values
/// are read and how records are split in batches. Each <c>With</c> method returns a copy with one setting changed.
/// </summary>
public class ChromaConfigurationOptions
{
	/// <summary>
	/// The URI of the server. Just the address, like <c>http://localhost:8000</c>, gets the path of the API version, like
	/// <c>/api/v2/</c>; a URI with a path is used as it is, with or without the trailing slash.
	/// </summary>
	public Uri Uri { get; init; }
	/// <summary>
	/// Whether the server is Chroma Cloud: <c>Uri</c> under <c>trychroma.com</c>, or the options say so with <c>WithChromaCloud</c>,
	/// as for Chroma Cloud behind a proxy or another address.
	/// </summary>
	public bool IsChromaCloud => _chromaCloud ?? Uri.Host.EndsWith(".trychroma.com", StringComparison.OrdinalIgnoreCase);

	private bool? _chromaCloud;
	/// <summary>
	/// The tenant of the requests that are not given one; when null or empty, <c>default_tenant</c>.
	/// </summary>
	public string? Tenant { get; init; }
	/// <summary>
	/// The database of the requests that are not given one; when null or empty, <c>default_database</c>.
	/// </summary>
	public string? Database { get; init; }
	/// <summary>
	/// The token sent with each request, in the header that <c>ChromaTokenTransportHeader</c> names; none when null or empty.
	/// </summary>
	public string? ChromaToken { get; init; }
	/// <summary>
	/// The header that carries the token: <c>X-Chroma-Token</c> by default, or <c>Authorization: Bearer</c>.
	/// </summary>
	public ChromaTokenTransportHeader ChromaTokenTransportHeader { get; init; }
	/// <summary>
	/// The user name for basic authentication, sent in the <c>Authorization</c> header of each request; none when null.
	/// It cannot be used together with a token in the <c>Authorization</c> header.
	/// </summary>
	public string? BasicAuthUsername { get; init; }
	/// <summary>
	/// The password for basic authentication, sent with <c>BasicAuthUsername</c>.
	/// </summary>
	public string? BasicAuthPassword { get; init; }
	/// <summary>
	/// The version of the Chroma API the client uses: v2 by default, v1 for Chroma 0.5.15 and earlier.
	/// </summary>
	public ChromaApiVersion ApiVersion { get; init; }
	/// <summary>
	/// How the client reads the values of the metadata: <c>Exact</c> by default, or <c>Inferred</c>.
	/// </summary>
	public ChromaMetadataValues MetadataValues { get; init; } = ChromaMetadataValues.Exact;
	/// <summary>
	/// Whether <c>AddAsync</c>, <c>UpdateAsync</c>, <c>UpsertAsync</c> and <c>DeleteAsync</c> send their records in batches of the
	/// <c>max_batch_size</c> of the server, and <c>GetAsync</c> reads in pages of it; on by default.
	/// </summary>
	public bool BatchSplitting { get; init; } = true;
	/// <summary>
	/// How <c>UpdateAsync</c> and <c>UpsertAsync</c> write the embeddings of records that exist: <c>ChromaUpsertStrategy.Server</c>, the
	/// update and the upsert of the server, by default. <c>ChromaUpsertStrategy.SkipUnchangedEmbeddings</c> works around a defect of
	/// Chroma 1.0.21 to 1.5.9 for the records whose embedding does not change.
	/// </summary>
	public ChromaUpsertStrategy UpsertStrategy { get; init; }
	/// <summary>
	/// Whether <c>UpdateAsync</c> and <c>UpsertAsync</c> on Chroma Cloud read a record first when a write with nulls would take it beyond
	/// the quota of 32 metadata keys, and send only the nulls of the keys it has; false by default, when such a write throws an
	/// <c>ArgumentException</c> before it goes, without a read. A record with more than 32 values and no null goes either way, and Chroma
	/// Cloud rejects it. The read and the write are two requests, with two risks: a write
	/// of another client of the same record between them can mix with this one, and when an id comes more than once in the same write, a
	/// key that a later occurrence deletes can stay if an earlier one wrote it.
	/// </summary>
	public bool ReadsBeyondKeyQuota { get; init; }
	/// <summary>
	/// How a read goes again after an error that can pass, as an answer <c>500</c> of Chroma 1.5.9 to a get while other clients write and
	/// delete the same records: <c>ChromaReadRetries.Default</c> by default, and <c>ChromaReadRetries.None</c> never goes again. A write
	/// never goes again.
	/// </summary>
	public ChromaReadRetries ReadRetries
	{
		get => _readRetries;
		init => _readRetries = value ?? throw new ArgumentNullException(nameof(ReadRetries));
	}
	private readonly ChromaReadRetries _readRetries = ChromaReadRetries.Default;
	private readonly int? _maxBatchSize;

	/// <summary>
	/// The largest batch, in records, when the records are sent in batches: the smaller of this and the <c>max_batch_size</c>
	/// of the server, or this alone where the server declares none. At least one record, also when set directly.
	/// </summary>
	public int? MaxBatchSize
	{
		get => _maxBatchSize;
		init => _maxBatchSize = value is null or > 0 ? value : throw new ArgumentOutOfRangeException(nameof(MaxBatchSize), value, "The batches need at least one record.");
	}

	/// <summary>
	/// Options for the server at the given URI, with the tenant, the database and the token, when given.
	/// </summary>
	/// <param name="uri">The URI of the server, like <c>http://localhost:8000</c>.</param>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <param name="database">The database, or null for the one of the options.</param>
	/// <param name="chromaToken">The token, sent in <c>X-Chroma-Token</c> by default, or null for none.</param>
	public ChromaConfigurationOptions(Uri uri, string? tenant = null, string? database = null, string? chromaToken = null)
	{
		Uri = uri;
		Tenant = tenant;
		Database = database;
		ChromaToken = chromaToken;
	}

	/// <summary>
	/// Options for the server at the given URI, as a string, with the tenant, the database and the token, when given.
	/// </summary>
	/// <param name="uri">The URI of the server, like <c>http://localhost:8000</c>.</param>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <param name="database">The database, or null for the one of the options.</param>
	/// <param name="chromaToken">The token, sent in <c>X-Chroma-Token</c> by default, or null for none.</param>
	public ChromaConfigurationOptions(string uri, string? tenant = null, string? database = null, string? chromaToken = null)
		: this(new Uri(uri), tenant, database, chromaToken)
	{ }

	/// <summary>
	/// Options for the server at <c>http://localhost:8000</c>.
	/// </summary>
	public ChromaConfigurationOptions()
		: this(ClientConstants.DefaultUri)
	{ }

	/// <summary>
	/// Options from a connection string, as an application keeps it in its settings:
	/// <c>Endpoint=https://api.trychroma.com;Token=...;Tenant=...;Database=...</c>. <c>Endpoint</c> is the URI of the server, and a
	/// connection string that is just an http or https URI is the endpoint alone. <c>Token</c> goes in the <c>X-Chroma-Token</c>
	/// header, as Chroma Cloud takes its API keys; <c>Tenant</c> and <c>Database</c> are the ones of the requests. The keys are
	/// case-insensitive, a value with <c>;</c> goes in quotes, and an empty value is no value. Another key, no endpoint, or an endpoint
	/// that is not an http or https URI throws an <c>ArgumentException</c>, each with its message.
	/// </summary>
	/// <param name="connectionString">The connection string.</param>
	/// <returns>The options.</returns>
	public static ChromaConfigurationOptions FromConnectionString(string connectionString)
	{
		if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
		{
			return new ChromaConfigurationOptions(uri);
		}
		var builder = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = connectionString };
		string? endpoint = null, token = null, tenant = null, database = null;
		foreach (string key in builder.Keys)
		{
			var value = builder[key]?.ToString() is { Length: > 0 } text ? text : null;
			switch (key.ToLowerInvariant())
			{
				case "endpoint":
					endpoint = value;
					break;
				case "token":
					token = value;
					break;
				case "tenant":
					tenant = value;
					break;
				case "database":
					database = value;
					break;
				default:
					throw new ArgumentException($"The connection string has the key \"{key}\": it takes Endpoint, Token, Tenant and Database.", nameof(connectionString));
			}
		}
		if (endpoint is null)
		{
			throw new ArgumentException("The connection string needs Endpoint, the URI of the server.", nameof(connectionString));
		}
		// Without the value, which may hold a password.
		if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) || endpointUri.Scheme is not ("http" or "https"))
		{
			throw new ArgumentException("The Endpoint of the connection string is not an http or https URI: it takes the URI of the server, like http://localhost:8000.", nameof(connectionString));
		}
		var options = new ChromaConfigurationOptions(endpointUri, tenant, database);
		return token is null ? options : options.WithChromaToken(token);
	}

	private ChromaConfigurationOptions(ChromaConfigurationOptions options)
		: this(options.Uri, options.Tenant, options.Database, options.ChromaToken)
	{
		ChromaTokenTransportHeader = options.ChromaTokenTransportHeader;
		BasicAuthUsername = options.BasicAuthUsername;
		BasicAuthPassword = options.BasicAuthPassword;
		ApiVersion = options.ApiVersion;
		MetadataValues = options.MetadataValues;
		BatchSplitting = options.BatchSplitting;
		MaxBatchSize = options.MaxBatchSize;
		UpsertStrategy = options.UpsertStrategy;
		ReadsBeyondKeyQuota = options.ReadsBeyondKeyQuota;
		ReadRetries = options.ReadRetries;
		_chromaCloud = options._chromaCloud;
	}

	/// <summary>
	/// A copy of these options with the given URI.
	/// </summary>
	/// <param name="uri">The URI of the server, like <c>http://localhost:8000</c>.</param>
	/// <returns>The new options; these do not change.</returns>
	public ChromaConfigurationOptions WithUri(Uri uri)
		=> new(this) { Uri = uri };

	/// <summary>
	/// A copy of these options with the given URI, as a string.
	/// </summary>
	/// <param name="uri">The URI of the server, like <c>http://localhost:8000</c>.</param>
	/// <returns>The new options; these do not change.</returns>
	public ChromaConfigurationOptions WithUri(string uri)
		=> new(this) { Uri = new Uri(uri) };

	/// <summary>
	/// A copy of these options with the given default tenant.
	/// </summary>
	/// <param name="tenant">The tenant, or null for the one of the options.</param>
	/// <returns>The new options; these do not change.</returns>
	public ChromaConfigurationOptions WithTenant(string tenant)
		=> new(this) { Tenant = tenant };

	/// <summary>
	/// A copy of these options with the given default database.
	/// </summary>
	/// <param name="database">The database, or null for the one of the options.</param>
	/// <returns>The new options; these do not change.</returns>
	public ChromaConfigurationOptions WithDatabase(string database)
		=> new(this) { Database = database };

	/// <summary>
	/// A copy of these options with the given token, in the header these options already name: <c>X-Chroma-Token</c> by default.
	/// </summary>
	/// <param name="chromaToken">The token, sent in <c>X-Chroma-Token</c> by default, or null for none.</param>
	/// <returns>The new options; these do not change.</returns>
	public ChromaConfigurationOptions WithChromaToken(string chromaToken)
		=> new(this) { ChromaToken = chromaToken };

	/// <summary>
	/// A copy of these options with the given token, in the given header.
	/// </summary>
	/// <param name="chromaToken">The token, sent in <c>X-Chroma-Token</c> by default, or null for none.</param>
	/// <param name="transportHeader">The header the token goes in.</param>
	/// <returns>The new options; these do not change.</returns>
	public ChromaConfigurationOptions WithChromaToken(string chromaToken, ChromaTokenTransportHeader transportHeader)
		=> new(this) { ChromaToken = chromaToken, ChromaTokenTransportHeader = transportHeader };

	/// <summary>
	/// A copy of these options with the user name and the password for basic authentication, for the Chroma 0.x servers with
	/// basic authentication.
	/// </summary>
	/// <param name="username">The user name.</param>
	/// <param name="password">The password.</param>
	/// <returns>The new options; these do not change.</returns>
	public ChromaConfigurationOptions WithBasicAuth(string username, string password)
		=> new(this) { BasicAuthUsername = username, BasicAuthPassword = password };

	/// <summary>
	/// A copy of these options that says whether the server is Chroma Cloud, whatever its address: for Chroma Cloud behind a proxy
	/// or another address. On Chroma Cloud the client writes in batches of 300 from the start, checks the keys of a schema before the
	/// creation of a collection, and deletes a collection or a database in one request.
	/// </summary>
	/// <param name="chromaCloud">Whether the server is Chroma Cloud.</param>
	/// <returns>The new options; these do not change.</returns>
	public ChromaConfigurationOptions WithChromaCloud(bool chromaCloud = true)
		=> new(this) { _chromaCloud = chromaCloud };

	/// <summary>
	/// A copy of these options with the given strategy for the embeddings of records that exist in <c>UpdateAsync</c> and
	/// <c>UpsertAsync</c>: see <c>ChromaUpsertStrategy</c>.
	/// </summary>
	/// <param name="strategy">The strategy.</param>
	/// <returns>The new options; these do not change.</returns>
	public ChromaConfigurationOptions WithUpsertStrategy(ChromaUpsertStrategy strategy)
		=> new(this) { UpsertStrategy = strategy };

	/// <summary>
	/// A copy of these options that, on Chroma Cloud, reads a record first when the values and the nulls of an update or an upsert would
	/// take it beyond the quota of 32 metadata keys, instead of throwing: see <c>ReadsBeyondKeyQuota</c>, and its two risks.
	/// </summary>
	/// <param name="readsBeyondKeyQuota">Whether the client reads such a record first; false throws.</param>
	/// <returns>The new options; these do not change.</returns>
	public ChromaConfigurationOptions WithReadsBeyondKeyQuota(bool readsBeyondKeyQuota = true)
		=> new(this) { ReadsBeyondKeyQuota = readsBeyondKeyQuota };

	/// <summary>
	/// A copy of these options with the given attempts and delays of the reads after an error that can pass: see <c>ChromaReadRetries</c>.
	/// </summary>
	/// <param name="readRetries">The attempts and the delays; <c>ChromaReadRetries.None</c> never goes again.</param>
	/// <returns>The new options; these do not change.</returns>
	public ChromaConfigurationOptions WithReadRetries(ChromaReadRetries readRetries)
		=> new(this) { ReadRetries = readRetries ?? throw new ArgumentNullException(nameof(readRetries)) };

	/// <summary>
	/// A copy of these options with the given version of the Chroma API.
	/// </summary>
	/// <param name="apiVersion">The version of the Chroma API.</param>
	/// <returns>The new options; these do not change.</returns>
	public ChromaConfigurationOptions WithApiVersion(ChromaApiVersion apiVersion)
		=> new(this) { ApiVersion = apiVersion };

	/// <summary>
	/// A copy of these options that reads the values of the metadata the given way.
	/// </summary>
	/// <param name="metadataValues">How the client reads the values of the metadata.</param>
	/// <returns>The new options; these do not change.</returns>
	public ChromaConfigurationOptions WithMetadataValues(ChromaMetadataValues metadataValues)
		=> new(this) { MetadataValues = metadataValues };

	/// <summary>
	/// <c>AddAsync</c>, <c>UpdateAsync</c>, <c>UpsertAsync</c> and <c>DeleteAsync</c> send their records in batches of the <c>max_batch_size</c>
	/// of the server, one request after the other, as by default. If a batch fails, the earlier ones stay written. With
	/// <c>false</c> the records go in one request.
	/// </summary>
	/// <param name="batchSplitting">Whether the records go in batches; false sends them in one request.</param>
	/// <returns>The new options; these do not change.</returns>
	public ChromaConfigurationOptions WithBatchSplitting(bool batchSplitting = true)
		=> new(this) { BatchSplitting = batchSplitting };

	/// <summary>
	/// Batches of at most <c>maxBatchSize</c> records, or of the <c>max_batch_size</c> of the server if smaller. For a server whose
	/// limit differs from the one it declares, like Chroma Cloud, which declares 1000 but takes 300 records per write by default.
	/// </summary>
	/// <param name="maxBatchSize">The most records in a request.</param>
	/// <returns>The new options; these do not change.</returns>
	public ChromaConfigurationOptions WithBatchSplitting(int maxBatchSize)
		=> maxBatchSize > 0
			? new(this) { BatchSplitting = true, MaxBatchSize = maxBatchSize }
			: throw new ArgumentOutOfRangeException(nameof(maxBatchSize), maxBatchSize, "The batches need at least one record.");
}
