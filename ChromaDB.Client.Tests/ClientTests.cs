using ChromaDB.Client.Models;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class ClientTests : ChromaTestsBase
{
	[Test]
	public async Task HeartbeatSimple()
	{
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var result = await client.HeartbeatAsync();
		Assert.That(result.NanosecondHeartbeat, Is.GreaterThan(0));
	}

	[Test]
	public async Task GetVersionSimple()
	{
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var result = await client.GetVersionAsync();
		Assert.That(result, Does.Match(@"\d+\.\d+"));
	}

	// The client with an HttpClient of its own.
	[Test]
	public async Task GetVersionWithoutAnHttpClient()
	{
		using var client = new ChromaClient(BaseConfigurationOptions);
		var result = await client.GetVersionAsync();
		Assert.That(result, Does.Match(@"\d+\.\d+"));
	}

	[Test]
	public async Task GetUserIdentitySimple()
	{
		Assume.That(ApiVersion, Is.EqualTo(ChromaApiVersion.V2), "The v1 API has no auth/identity.");
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var result = await client.GetUserIdentityAsync();
		// A container answers with the defaults whatever tenant the tests use; a server already running, like Chroma Cloud,
		// with the tenant of the key and its databases.
		Assert.That(result.Tenant, Is.EqualTo(RunningServer ? BaseConfigurationOptions.Tenant ?? "default_tenant" : "default_tenant"));
		Assert.That(result.Databases, RunningServer ? Is.Not.Empty : Contains.Item("default_database"));
	}

	[Test]
	public async Task GetPreFlightChecksSimple()
	{
		Assume.That(PreFlightChecksSupported, Is.True, "Chroma 0.4.10 has no pre-flight-checks.");
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var result = await client.GetPreFlightChecksAsync();
		Assert.That(result.MaxBatchSize, Is.GreaterThan(0));
		Assert.That(result.SupportsBase64Encoding, Base64EncodingReported ? Is.Not.Null : Is.Null);
	}

	[Test]
	public async Task SharedHttpClientIsNotModified()
	{
		using var httpClient = new HttpClient();
		var client = new ChromaClient(BaseConfigurationOptions.WithChromaToken("token"), httpClient);
		await client.HeartbeatAsync();
		Assert.That(httpClient.BaseAddress, Is.Null);
		Assert.That(httpClient.DefaultRequestHeaders.Contains("X-Chroma-Token"), Is.False);
	}

	[Test]
	public async Task SharedHttpClientWithAnotherUri()
	{
		using var httpClient = new HttpClient();
		var client = new ChromaClient(BaseConfigurationOptions, httpClient);
		await client.HeartbeatAsync();
		_ = new ChromaClient(BaseConfigurationOptions.WithUri("http://localhost:1/api/v2/"), httpClient);
		await Assert.ThatAsync(() => client.HeartbeatAsync(), Throws.Nothing);
	}

	[Test]
	public async Task SharedHttpClientWithAnotherUriForCollection()
	{
		using var httpClient = NewHttpClient();
		var client = new ChromaClient(BaseConfigurationOptions, httpClient);
		var collection = await client.CreateCollectionAsync($"collection{Random.Shared.Next()}");
		Assert.That(() => new ChromaCollectionClient(collection, BaseConfigurationOptions.WithUri("http://localhost:1/api/v2/"), httpClient), Throws.Nothing);
		await Assert.ThatAsync(() => new ChromaCollectionClient(collection, BaseConfigurationOptions, httpClient).CountAsync(), Throws.Nothing);
	}

	[Test]
	public async Task UriWithHostAndPortOnly()
	{
		var options = BaseConfigurationOptions.WithUri(BaseConfigurationOptions.Uri.GetLeftPart(UriPartial.Authority));
		var client = new ChromaClient(options, HttpClient);
		var collection = await client.GetOrCreateCollectionAsync($"collection{Random.Shared.Next()}");
		var result = await new ChromaCollectionClient(collection, options, HttpClient).CountAsync();
		Assert.That(result, Is.EqualTo(0));
	}

	[Test]
	public async Task GetCollectionSimple()
	{
		var name = $"collection{Random.Shared.Next()}";

		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		await client.CreateCollectionAsync(name);
		var result = await client.GetCollectionAsync(name);
		Assert.That(result, Is.Not.Null);
		Assert.That(result.Name, Is.EqualTo(name));
	}

	// Two records in the same direction: 0 apart with cosine, 1 apart with l2, and 1 - 2 = -1 for the farther one with ip.
	[TestCase(ChromaSpace.L2, new[] { 0f, 1f })]
	[TestCase(ChromaSpace.Cosine, new[] { 0f, 0f })]
	[TestCase(ChromaSpace.InnerProduct, new[] { -1f, 0f })]
	public async Task CreateCollectionWithSpace(ChromaSpace space, float[] distances)
	{
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var collection = await client.CreateCollectionAsync(new ChromaCollectionDefinition($"collection{Random.Shared.Next()}") { Configuration = new() { Space = space } });
		Assert.That(collection.Space, Is.EqualTo(space));
		Assert.That((await client.GetCollectionAsync(collection.Name)).Space, Is.EqualTo(space));

		var collectionClient = new ChromaCollectionClient(collection, BaseConfigurationOptions, HttpClient);
		await collectionClient.AddAsync(["a", "b"], embeddings: [new([1f, 0f]), new([2f, 0f])]);
		var result = await collectionClient.QueryAsync(new ReadOnlyMemory<float>([1f, 0f]), nResults: 2, include: ChromaQueryInclude.Distances);
		Assert.That(result.Select(x => x.Distance).OrderBy(x => x), Is.EqualTo(distances).Within(0.0001f));
	}

	// With a schema the space goes in the schema, which Chroma 1.3.2 and later apply.
	[TestCase(ChromaSpace.Cosine, new[] { 0f, 0f })]
	[TestCase(ChromaSpace.InnerProduct, new[] { -1f, 0f })]
	public async Task CreateCollectionWithSpaceAndSchema(ChromaSpace space, float[] distances)
	{
		Assume.That(SpaceInSchemaApplied, Is.True, "Chroma 1.3.2 and later apply the space in the schema.");
		await SpaceWithSchema(space, new ChromaCollectionSchema(), distances);
	}

	// Chroma 1.3.0 applies the space of a schema that also turns an index on or off, here one turned off.
	[TestCase(ChromaSpace.Cosine, new[] { 0f, 0f })]
	[TestCase(ChromaSpace.InnerProduct, new[] { -1f, 0f })]
	public async Task CreateCollectionWithSpaceAndSchemaWithAnIndexTurnedOff(ChromaSpace space, float[] distances)
	{
		Assume.That(SchemaApplied, Is.True, "Chroma 1.3.0 and later apply the schema.");
		await SpaceWithSchema(space, new ChromaCollectionSchema().WithoutIndex(ChromaSchemaIndex.StringInverted, "k"), distances);
	}

	private async Task SpaceWithSchema(ChromaSpace space, ChromaCollectionSchema schema, float[] distances)
	{
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var collection = await client.CreateCollectionAsync(new ChromaCollectionDefinition($"collection{Random.Shared.Next()}") { Configuration = new() { Space = space }, Schema = schema });
		Assert.That(collection.Space, Is.EqualTo(space));
		Assert.That((await client.GetCollectionAsync(collection.Name)).Space, Is.EqualTo(space));

		var collectionClient = client.GetCollectionClient(collection);
		await collectionClient.AddAsync(["a", "b"], embeddings: [new([1f, 0f]), new([2f, 0f])]);
		var result = await collectionClient.QueryAsync(new ReadOnlyMemory<float>([1f, 0f]), nResults: 2, include: ChromaQueryInclude.Distances);
		Assert.That(result.Select(x => x.Distance).OrderBy(x => x), Is.EqualTo(distances).Within(0.0001f));
	}

	[Test]
	public async Task GetOrCreateCollectionWithSpace()
	{
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var definition = new ChromaCollectionDefinition($"collection{Random.Shared.Next()}") { Configuration = new() { Space = ChromaSpace.Cosine } };
		var created = await client.GetOrCreateCollectionAsync(definition);
		var existing = await client.GetOrCreateCollectionAsync(definition);
		Assert.That(existing.Id, Is.EqualTo(created.Id));
		Assert.That(existing.Space, Is.EqualTo(ChromaSpace.Cosine));
	}

	// A collection that exists keeps its space: GetOrCreateCollectionAsync throws when it has another one than the definition asks for,
	// where the server reports it.
	[Test]
	public async Task GetOrCreateCollectionWithAnotherSpace()
	{
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var name = $"collection{Random.Shared.Next()}";
		var created = await client.CreateCollectionAsync(new ChromaCollectionDefinition(name) { Configuration = new() { Space = ChromaSpace.L2 } });
		Assume.That(created.Space, Is.Not.Null, "The server does not report the space of the collection.");
		await Assert.ThatAsync(() => client.GetOrCreateCollectionAsync(new ChromaCollectionDefinition(name) { Configuration = new() { Space = ChromaSpace.Cosine } }),
			Throws.InstanceOf<ChromaException>().With.Message.Contains("not cosine"));
	}

	// Without a space Chroma uses l2; the servers before 1.0.6 do not report it reliably.
	[Test]
	public async Task SpaceOfACollectionWithoutOne()
	{
		var collection = await new ChromaClient(BaseConfigurationOptions, HttpClient).CreateCollectionAsync($"collection{Random.Shared.Next()}");
		// Chroma 0.x keeps the space only in the metadata, so the client reads l2, its default; 1.0.0 to 1.0.5 do not report it.
		Assert.That(collection.Space, ConfigurationSpaceReported || !IsChroma1 ? Is.EqualTo(ChromaSpace.L2) : Is.Null);
	}

	[Test]
	public async Task GetCollectionClient()
	{
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var collection = await client.CreateCollectionAsync($"collection{Random.Shared.Next()}");
		await client.GetCollectionClient(collection).AddAsync(["a"], embeddings: [new([1f, 0f])]);
		Assert.That(await client.GetCollectionClient(collection.Id, collection.Name).CountAsync(), Is.EqualTo(1));
	}

	[Test]
	public async Task CollectionClientFromTheId()
	{
		var collection = await new ChromaClient(BaseConfigurationOptions, HttpClient).CreateCollectionAsync($"collection{Random.Shared.Next()}");
		var collectionClient = new ChromaCollectionClient(collection.Id, collection.Name, BaseConfigurationOptions, HttpClient);
		await collectionClient.AddAsync(["a"], embeddings: [new([1f, 0f])]);
		Assert.That(await collectionClient.CountAsync(), Is.EqualTo(1));
		Assert.That((await new ChromaCollectionClient(collection, BaseConfigurationOptions, HttpClient).GetAsync("a"))?.Id, Is.EqualTo("a"));
	}

	[Test]
	public async Task CollectionExists()
	{
		var name = $"collection{Random.Shared.Next()}";

		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		Assert.That(await client.CollectionExistsAsync(name), Is.False);
		await client.CreateCollectionAsync(name);
		Assert.That(await client.CollectionExistsAsync(name), Is.True);
		await client.DeleteCollectionAsync(name);
		Assert.That(await client.CollectionExistsAsync(name), Is.False);
	}

	[Test]
	public async Task GetCollectionById()
	{
		Assume.That(CollectionByIdSupported, Is.True, "Only the v2 API of Chroma 1.5.7 and later gets a collection by its id.");
		var name = $"collection{Random.Shared.Next()}";

		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var collection = await client.CreateCollectionAsync(name, metadata: new Dictionary<string, object> { ["key"] = "value" });
		var result = await client.GetCollectionByIdAsync(collection.Id);
		Assert.That(result.Id, Is.EqualTo(collection.Id));
		Assert.That(result.Name, Is.EqualTo(name));
		Assert.That(result.Metadata?["key"], Is.EqualTo("value"));
		Assert.That(result.Tenant, Is.EqualTo(collection.Tenant));
		Assert.That(result.Database, Is.EqualTo(collection.Database));
	}

	[Test]
	public async Task GetCollectionByIdNotExists()
	{
		Assume.That(CollectionByIdSupported, Is.True, "Only the v2 API of Chroma 1.5.7 and later gets a collection by its id.");

		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		await Assert.ThatAsync(() => client.GetCollectionByIdAsync(Guid.NewGuid()), Throws.InstanceOf<ChromaException>().With.Message.Contains("does not exist"));
	}

	// The id is looked up in the tenant and database of the request only.
	[Test]
	public async Task GetCollectionByIdInAnotherDatabase()
	{
		Assume.That(OtherTenantsAndDatabasesTested, Is.True, "A server already running may not let the tests create or look up other tenants and databases.");
		Assume.That(CollectionByIdSupported, Is.True, "Only the v2 API of Chroma 1.5.7 and later gets a collection by its id.");
		var database = $"database{Random.Shared.Next()}";

		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var collection = await client.CreateCollectionAsync($"collection{Random.Shared.Next()}");
		await client.CreateDatabaseAsync(database);
		Assert.That((await client.GetCollectionByIdAsync(collection.Id)).Id, Is.EqualTo(collection.Id));
		await Assert.ThatAsync(() => client.GetCollectionByIdAsync(collection.Id, database: database), Throws.InstanceOf<ChromaException>().With.Message.Contains("does not exist"));
	}

	// The older servers have no such path: the client reports it with a ChromaException that names the request,
	// "Not Found" from the Python servers, "NotFound" from Chroma 1.x, which answers with an empty body.
	[Test]
	public async Task GetCollectionByIdNotSupported()
	{
		Assume.That(CollectionByIdSupported, Is.False, "This server gets a collection by its id.");

		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var collection = await client.CreateCollectionAsync($"collection{Random.Shared.Next()}");
		await Assert.ThatAsync(() => client.GetCollectionByIdAsync(collection.Id), Throws.InstanceOf<ChromaException>().With.Message.Match($"^Not ?Found: GET /api/v[12]/.*collections/by-id/{collection.Id}$"));
	}

	[Test]
	public async Task ListCollectionsSimple()
	{
		var names = new[] { $"collection{Random.Shared.Next()}", $"collection{Random.Shared.Next()}" };

		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		await client.CreateCollectionAsync(names[0]);
		await client.CreateCollectionAsync(names[1]);
		var result = await client.ListCollectionsAsync();
		Assert.That(result, Is.Not.Null);
		Assert.That(result, Has.Count.GreaterThanOrEqualTo(2));
		Assert.That(result.Select(x => x.Name), Contains.Item(names[0]));
		Assert.That(result.Select(x => x.Name), Contains.Item(names[1]));
	}

	[Test]
	public async Task ListCollectionsPages()
	{
		Assume.That(OtherTenantsAndDatabasesTested, Is.True, "A server already running may not let the tests create or look up other tenants and databases.");
		Assume.That(ListCollectionsPagingSupported, Is.True, "Chroma 0.4.15 and earlier ignore the limit and the offset of the list of the collections.");
		var database = $"database{Random.Shared.Next()}";

		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		await client.CreateDatabaseAsync(database);
		var names = new[] { $"collection{Random.Shared.Next()}", $"collection{Random.Shared.Next()}", $"collection{Random.Shared.Next()}" };
		foreach (var name in names)
		{
			await client.CreateCollectionAsync(name, database: database);
		}
		var first = await client.ListCollectionsAsync(limit: 2, database: database);
		var second = await client.ListCollectionsAsync(limit: 2, offset: 2, database: database);
		Assert.That(first, Has.Count.EqualTo(2));
		Assert.That(second, Has.Count.EqualTo(1));
		Assert.That(first.Concat(second).Select(x => x.Name), Is.EquivalentTo(names));
	}

	[Test]
	public async Task CreateCollectionWithoutMetadata()
	{
		var name = $"collection{Random.Shared.Next()}";

		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var result = await client.CreateCollectionAsync(name);
		Assert.That(result, Is.Not.Null);
		Assert.That(result.Name, Is.EqualTo(name));
	}

	[Test]
	public async Task CreateCollectionWithMetadata()
	{
		var name = $"collection{Random.Shared.Next()}";
		var metadata = new Dictionary<string, object>()
		{
			{ "test", "foo" },
			{ "test2", 10 },
		};

		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var result = await client.CreateCollectionAsync(name,
			metadata: metadata);
		Assert.That(result, Is.Not.Null);
		Assert.That(result.Name, Is.EqualTo(name));
		Assert.That(result.Metadata, Is.Not.Null);
		Assert.That(result.Metadata["test"], Is.EqualTo(metadata["test"]));
		Assert.That(result.Metadata["test2"], Is.EqualTo(metadata["test2"]));
	}

	[Test]
	public async Task CreateCollectionAlreadyExists()
	{
		var name = $"collection{Random.Shared.Next()}";

		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		await client.CreateCollectionAsync(name);
		await Assert.ThatAsync(async () => await client.CreateCollectionAsync(name), Throws.InstanceOf<ChromaException>().With.Message.Matches($@"^Collection \[?{name}\]? already exists"));
	}

	[Test]
	public async Task DeleteCollection()
	{
		var name = $"collection{Random.Shared.Next()}";

		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		await client.CreateCollectionAsync(name);
		await client.DeleteCollectionAsync(name);
	}

	[Test]
	public async Task DeleteCollectionNotExists()
	{
		var name = $"collection{Random.Shared.Next()}";

		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		await Assert.ThatAsync(async () => await client.DeleteCollectionAsync(name), Throws.InstanceOf<ChromaException>().With.Message.Matches($@"^Collection \[?{name}\]? does not exist"));
	}

	// A client made by name works on the collection of that name also after it was deleted and created again elsewhere.
	[Test]
	public async Task CollectionClientByName()
	{
		var name = $"collection{Random.Shared.Next()}";
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		await client.CreateCollectionAsync(name);
		var byName = client.GetCollectionClient(name);
		await byName.AddAsync(["a", "b"], embeddings: [new([1f, 0f]), new([0f, 1f])]);
		var first = (await byName.GetCollectionAsync()).Id;
		await client.DeleteCollectionAsync(name);
		await client.CreateCollectionAsync(name);
		Assert.That(await byName.CountAsync(), Is.EqualTo(0));
		await byName.AddAsync(["c"], embeddings: [new([1f, 1f])]);
		Assert.That((await byName.GetAsync()).Select(x => x.Id), Is.EqualTo(new[] { "c" }));
		Assert.That(byName.Collection.Id, Is.Not.EqualTo(first));
	}

	// Every tested server tells a missing collection in its own way, which DeleteCollectionIfExistsAsync recognizes.
	[Test]
	public async Task DeleteCollectionIfExists()
	{
		var name = $"collection{Random.Shared.Next()}";

		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		await client.CreateCollectionAsync(name);
		Assert.That(await client.DeleteCollectionIfExistsAsync(name), Is.True);
		Assert.That(await client.CollectionExistsAsync(name), Is.False);
		Assert.That(await client.DeleteCollectionIfExistsAsync(name), Is.False);
	}

	[Test]
	public async Task GetOrCreateCollectionDoesNotExist()
	{
		var name = $"collection{Random.Shared.Next()}";

		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var result = await client.GetOrCreateCollectionAsync(name);
		Assert.That(result, Is.Not.Null);
		Assert.That(result.Name, Is.EqualTo(name));
	}

	[Test]
	public async Task GetOrCreateCollectionDoesExist()
	{
		var name = $"collection{Random.Shared.Next()}";

		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var result1 = await client.GetOrCreateCollectionAsync(name);
		var result2 = await client.GetOrCreateCollectionAsync(name);
		Assert.That(result1, Is.Not.Null);
		Assert.That(result1.Name, Is.EqualTo(name));
		Assert.That(result2, Is.Not.Null);
		Assert.That(result2.Name, Is.EqualTo(name));
		Assert.That(result1.Id, Is.EqualTo(result2.Id));
	}

	[Test]
	public async Task CountCollections()
	{
		Assume.That(CountCollectionsAndNotContainsSupported, Is.True, "Chroma 0.4.15 has no count_collections, no $not_contains filter and no tenant and database in the collections.");
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var list = await client.ListCollectionsAsync();
		var result = await client.CountCollectionsAsync();
		Assert.That(result, Is.EqualTo(list.Count));
	}
}
