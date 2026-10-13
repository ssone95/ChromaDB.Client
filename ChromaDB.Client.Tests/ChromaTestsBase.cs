using ChromaDB.Client.Models;
using ChromaDB.Client.Tests.TestContainer;
using NUnit.Framework;
using Testcontainers.Chroma;

namespace ChromaDB.Client.Tests;

public abstract class ChromaTestsBase
{
	// On a server already running, the requests of the fixture are recorded: at its end it deletes what they created.
	private readonly CreatedOnTheServer _created = new();
	protected HttpClient HttpClient { get; }

	// CHROMA_TEST_TENANT and CHROMA_TEST_DATABASE run the tests in that tenant and database, created for each fixture, instead of the default ones.
	private static readonly string? TestTenant = Environment.GetEnvironmentVariable("CHROMA_TEST_TENANT") is { Length: > 0 } tenant ? tenant : null;
	private static readonly string? TestDatabase = Environment.GetEnvironmentVariable("CHROMA_TEST_DATABASE") is { Length: > 0 } database ? database : null;

	// CHROMA_TEST_URI runs the tests against a server already running, like Chroma Cloud, instead of a container: CHROMA_TEST_TOKEN
	// goes in X-Chroma-Token, CHROMA_TEST_TENANT and CHROMA_TEST_DATABASE are used as they are, not created, and the collections and
	// databases a fixture creates are deleted at its end. CHROMA_TEST_MAX_BATCH_SIZE is a limit lower than the declared one, like 300 on Chroma Cloud.
	private static readonly string? TestUri = Environment.GetEnvironmentVariable("CHROMA_TEST_URI") is { Length: > 0 } uri ? uri : null;
	private static readonly string? TestToken = Environment.GetEnvironmentVariable("CHROMA_TEST_TOKEN") is { Length: > 0 } token ? token : null;
	protected static readonly int? TestMaxBatchSize = int.TryParse(Environment.GetEnvironmentVariable("CHROMA_TEST_MAX_BATCH_SIZE"), out var size) ? size : null;
	protected static bool RunningServer => TestUri is not null;
	protected static bool ChromaCloud => TestUri?.Contains(".trychroma.com") == true;

	// CHROMA_TEST_API_VERSION=v1 runs the tests with the v1 API, the only one of Chroma 0.5.15 and earlier.
	protected static readonly ChromaApiVersion ApiVersion = Environment.GetEnvironmentVariable("CHROMA_TEST_API_VERSION") is "v1" ? ChromaApiVersion.V1 : ChromaApiVersion.V2;

	// Null when the fixture is skipped before its container starts.
	private ChromaContainer? _container;
	private ChromaConfigurationOptions? _baseConfigurationOptions;

	protected ChromaTestsBase()
	{
		HttpClient = NewHttpClient();
	}

	// An HttpClient whose requests to a server already running are recorded, for the tests that need an HttpClient of their own.
	protected HttpClient NewHttpClient() => RunningServer ? _created.NewHttpClient() : new HttpClient();

	[OneTimeSetUp]
	public async Task OneTimeSetUp()
	{
		if (SkipReason is { } reason)
		{
			Assert.Ignore(reason);
		}
		if ((TestTenant is not null || TestDatabase is not null) && !TenantsSupported)
		{
			Assert.Ignore("Chroma 0.4.14 and earlier have no tenants and databases.");
		}
		if ((TestTenant is not null || TestDatabase is not null) && !RecordsInOtherTenantsSupported)
		{
			Assert.Ignore("Chroma 0.4.15 does not add records to the collections of other tenants and databases.");
		}
		if (RunningServer)
		{
			_baseConfigurationOptions = new ChromaConfigurationOptions(uri: TestUri!).WithApiVersion(ApiVersion);
			_baseConfigurationOptions = TestToken is not null ? _baseConfigurationOptions.WithChromaToken(TestToken) : _baseConfigurationOptions;
			_baseConfigurationOptions = TestTenant is not null ? _baseConfigurationOptions.WithTenant(TestTenant) : _baseConfigurationOptions;
			_baseConfigurationOptions = TestDatabase is not null ? _baseConfigurationOptions.WithDatabase(TestDatabase) : _baseConfigurationOptions;
			return;
		}
		_container = ConfigureContainer(new ChromaBuilder(ChromaImage.Name)).Build();
		await _container.StartAsync();
		_baseConfigurationOptions = new ChromaConfigurationOptions(uri: $"{_container.GetConnectionString()}api/{(ApiVersion == ChromaApiVersion.V1 ? "v1" : "v2")}/")
			.WithApiVersion(ApiVersion);
		if (TestTenant is not null || TestDatabase is not null)
		{
			var client = new ChromaClient(WithServerCredentials(_baseConfigurationOptions), HttpClient);
			if (TestTenant is not null)
			{
				await client.CreateTenantAsync(TestTenant);
				_baseConfigurationOptions = _baseConfigurationOptions.WithTenant(TestTenant);
			}
			// A new tenant has no databases: without CHROMA_TEST_DATABASE, the tests use a default_database created in it.
			var database = TestDatabase ?? (TestTenant is not null ? "default_database" : null);
			if (database is not null)
			{
				await client.CreateDatabaseAsync(database, tenant: TestTenant);
				_baseConfigurationOptions = _baseConfigurationOptions.WithDatabase(database);
			}
		}
	}

	[OneTimeTearDown]
	public async Task OneTimeTearDown()
	{
		if (RunningServer && _baseConfigurationOptions is not null)
		{
			await DeleteWhatTheFixtureCreated();
		}
		_baseConfigurationOptions = null;
		HttpClient.Dispose();
		if (_container is not null)
		{
			await _container.DisposeAsync();
		}
	}

	protected ChromaConfigurationOptions BaseConfigurationOptions => _baseConfigurationOptions ?? throw new InvalidOperationException();

	// On a running server what the tests create stays, and on Chroma Cloud it costs: the fixture deletes it.
	private async Task DeleteWhatTheFixtureCreated()
	{
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		foreach (var place in _created.Collections.GroupBy(x => x.Value, x => x.Key))
		{
			IReadOnlyList<ChromaCollection> collections;
			try
			{
				collections = await client.ListCollectionsAsync(place.Key.Tenant, place.Key.Database);
			}
			catch (ChromaException)
			{
				// A test deleted the database.
				continue;
			}
			foreach (var collection in collections.Where(x => place.Contains(x.Id)))
			{
				// On a Chroma 1.5 already running, the lists of the records would reach the records of the next tests.
				await client.DeleteCollectionAsync(collection.Name, deleteRecordsFirst: true, place.Key.Tenant, place.Key.Database);
			}
		}
		foreach (var (tenant, name) in _created.Databases.Keys)
		{
			try
			{
				await client.DeleteDatabaseAsync(name, deleteRecordsFirst: true, tenant);
			}
			catch (ChromaException)
			{
				// A test deleted it, or the server does not delete databases.
			}
		}
	}

	// Chroma 1.0 removed the built-in authentication and reads its settings from a configuration file.
	protected static bool IsChroma1 => ChromaImage.Version.Major >= 1;

	// A server already running, like Chroma Cloud, may not let the tests create or look up other tenants and databases:
	// Chroma Cloud answers 403 "Permission denied." also for a tenant or a database that does not exist.
	protected static bool OtherTenantsAndDatabasesTested => !RunningServer;

	// Chroma 0.4.14 has no tenants and databases, 0.4.15 has them.
	protected static bool TenantsSupported => ChromaImage.Version >= new Version(0, 4, 15);

	// Missing in Chroma 0.4.15 and there in 0.4.23, the next version whose image starts: count_collections, the $not_contains
	// document filter, and the tenant and database in the collections the server returns.
	protected static bool CountCollectionsAndNotContainsSupported => ChromaImage.Version >= new Version(0, 4, 23);

	// Chroma 0.4.15 creates collections in other tenants and databases, but answers that they do not exist when records
	// are added to them; 0.4.23 adds them.
	protected static bool RecordsInOtherTenantsSupported => ChromaImage.Version >= new Version(0, 4, 23);

	// Chroma 0.4.10 to 0.4.15 ignore the limit and the offset of the list of the collections; 0.4.23 applies them.
	protected static bool ListCollectionsPagingSupported => ChromaImage.Version >= new Version(0, 4, 23);

	// Chroma 0.4.10 has no pre-flight-checks; 0.4.12 has them (the 0.4.11 image does not start).
	protected static bool PreFlightChecksSupported => ChromaImage.Version >= new Version(0, 4, 12);

	// Chroma 1.0.12 and earlier do not send supports_base64_encoding in the pre-flight checks; 1.0.13 sends it.
	protected static bool Base64EncodingReported => ChromaImage.Version >= new Version(1, 0, 13);

	// Chroma 0.4.10 to 0.4.15 reject "uris" in include; 0.4.23 stores and returns the URIs of the records.
	protected static bool UrisSupported => ChromaImage.Version >= new Version(0, 4, 23);

	// Only the v2 API of Chroma 0.6.3 and later lists and deletes databases: 0.6.2 and earlier answer 405 Method Not Allowed.
	protected static bool DatabaseListingSupported => ApiVersion == ChromaApiVersion.V2 && ChromaImage.Version >= new Version(0, 6, 3);

	// Only the v2 API of Chroma 1.5.7 and later gets a collection by its id: 1.5.6 and earlier answer 404 Not Found.
	protected static bool CollectionByIdSupported => ApiVersion == ChromaApiVersion.V2 && ChromaImage.Version >= new Version(1, 5, 7);

	// Chroma 0.6.3 and earlier ignore the ids of a query and search all the records; 1.0.0 searches only those.
	protected static bool IdsInQuerySupported => ApiVersion == ChromaApiVersion.V2 && IsChroma1;

	// Chroma 0.x accepts lists in metadata but drops them; 1.0.0 to 1.4.1 reject them with 422; 1.5.0 stores them and filters them with $contains.
	protected static bool MetadataListsSupported => IsChroma1 && ChromaImage.Version >= new Version(1, 5, 0);
	protected static bool IsChroma0 => !IsChroma1;

	// Chroma 1.0.6 and later send "hnsw.space" in the configuration; 0.5.4 to 1.0.5 send "hnsw_configuration.space", always "l2".
	protected static bool ConfigurationSpaceReported => ChromaImage.Version >= new Version(1, 0, 6);

	// The servers before Chroma 0.5.1 configure their built-in authentication with other settings, and Chroma 1.0 removed it.
	protected static bool BuiltInAuthenticationTested => ChromaImage.Version >= new Version(0, 5, 1) && !IsChroma1;

	// Only the v2 API of Chroma 1.0.0 and later has the healthcheck: the 0.x servers answer 404.
	protected static bool HealthcheckSupported => ApiVersion == ChromaApiVersion.V2 && IsChroma1;

	// Chroma 1.0.12 and later filter documents with $regex and $not_regex; 1.0.0 to 1.0.6 reject them, 1.0.10 closes the connection.
	protected static bool RegexSupported => ChromaImage.Version >= new Version(1, 0, 12);

	// Chroma 1.0.21 to 1.1.1 reject a write with sparse vectors, but then fail the next write on the server, also to another collection,
	// with "Error sending message to compactor". A server already running counts by CHROMA_IMAGE too.
	protected static bool RejectedSparseVectorsBreakTheServer => ChromaImage.Version >= new Version(1, 0, 21) && ChromaImage.Version <= new Version(1, 1, 1);

	// Chroma 1.3.2 and later apply the space in a schema; 1.3.0 only when the schema also turns an index on or off, and keeps l2 otherwise.
	protected static bool SpaceInSchemaApplied => ChromaCloud || ChromaImage.Version >= new Version(1, 3, 2);

	// Chroma 1.5.3 and later apply the limit of a delete, declare it in their OpenAPI description and answer a count, which for a delete by
	// ids is the number of ids given.
	protected static bool DeleteLimitSupported => ChromaImage.Version >= new Version(1, 5, 3);

	// $ne and $nin match the records without the key from Chroma 0.5.15; 0.4.10 to 0.4.23 leave them out.
	protected static bool NotEqualMatchesRecordsWithoutTheKey => ChromaImage.Version >= new Version(0, 5, 15);

	// Chroma 1.0.6 and later apply a new configuration of a collection; the earlier versions answer without applying it.
	protected static bool NewConfigurationApplied => ChromaImage.Version >= new Version(1, 0, 6);

	// Chroma 1.0.0 and later and Chroma Cloud keep the stored document when an update or an upsert sends a null one; every 0.x server
	// deletes it.
	protected static bool NullDocumentKept => ChromaCloud || ChromaImage.Version >= new Version(1, 0, 0);

	// Chroma 1.3.0 and later apply the indexes of a schema.
	protected static bool SchemaApplied => ChromaCloud || ChromaImage.Version >= new Version(1, 3, 0);

	// Chroma 1.5.1 and later turn off the full-text search index of a schema; 1.3.0 to 1.5.0 keep it.
	protected static bool FullTextSearchOffApplied => ChromaCloud || ChromaImage.Version >= new Version(1, 5, 1);

	// Chroma 0.5.1 and later send the dimension and the version of a collection.
	protected static bool CollectionDimensionReported => ChromaImage.Version >= new Version(0, 5, 1);

	// Since Chroma 0.5.20, the server rejects embeddings of different dimensions in the same request.
	protected static bool EmbeddingDimensionsChecked => ChromaImage.Version >= new Version(0, 5, 20);

	// Since Chroma 1.0.16, add and upsert require embeddings.
	protected static bool EmbeddingsRequired => ChromaImage.Version >= new Version(1, 0, 16);

	protected static List<ReadOnlyMemory<float>> Embeddings(int count)
		=> Enumerable.Repeat(new ReadOnlyMemory<float>([1f, 0.5f, 0f, -0.5f, -1f]), count).ToList();

	// Before Chroma 1.0.16 these calls succeed; since then the server rejects them.
	protected static async Task WithoutEmbeddings(Func<Task> action)
	{
		if (EmbeddingsRequired)
			await Assert.ThatAsync(() => action(), Throws.InstanceOf<ChromaException>());
		else
			await action();
	}

	protected virtual ChromaBuilder ConfigureContainer(ChromaBuilder builder) => builder;

	// Skips the whole fixture before its container starts, when the server of this version cannot run it.
	protected virtual string? SkipReason => null;

	// The credentials the server of the fixture requires, if any: the setup needs them to create the test tenant and database.
	protected virtual ChromaConfigurationOptions WithServerCredentials(ChromaConfigurationOptions options) => options;
}
