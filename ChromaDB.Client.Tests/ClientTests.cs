using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class ClientTests : ChromaDBTestsBase
{
	[Test]
	public async Task NoAuth()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		await Assert.ThatAsync(client.Heartbeat, Throws.Nothing);
	}

	[Test]
	public async Task HeartbeatSimple()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var result = await client.Heartbeat();
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Is.Not.Null);
		Assert.That(result.Data.NanosecondHeartbeat, Is.GreaterThan(0));
	}

	[Test]
	public async Task GetVersionSimple()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var result = await client.GetVersion();
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data, Does.Match(@"\d+\.\d+"));
	}

	[Test]
	public async Task GetCollectionSimple()
	{
		var name = $"collection{Random.Shared.Next()}";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		await client.CreateCollection(name);
		var result = await client.GetCollection(name);
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Is.Not.Null);
		Assert.That(result.Data.Name, Is.EqualTo(name));
	}

	[Test]
	public async Task ListCollectionsSimple()
	{
		var names = new[] { $"collection{Random.Shared.Next()}", $"collection{Random.Shared.Next()}" };

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		await client.CreateCollection(names[0]);
		await client.CreateCollection(names[1]);
		var result = await client.ListCollections();
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Is.Not.Null);
		Assert.That(result.Data, Has.Count.GreaterThanOrEqualTo(2));
		Assert.That(result.Data.Select(x => x.Name), Contains.Item(names[0]));
		Assert.That(result.Data.Select(x => x.Name), Contains.Item(names[1]));
	}

	[Test]
	public async Task CreateCollectionWithoutMetadata()
	{
		var name = $"collection{Random.Shared.Next()}";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var result = await client.CreateCollection(name);
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Is.Not.Null);
		Assert.That(result.Data.Name, Is.EqualTo(name));
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

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var result = await client.CreateCollection(name,
			metadata: metadata);
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Is.Not.Null);
		Assert.That(result.Data.Name, Is.EqualTo(name));
		Assert.That(result.Data.Metadata, Is.Not.Null);
		Assert.That(result.Data.Metadata["test"], Is.EqualTo(metadata["test"]));
		Assert.That(result.Data.Metadata["test2"], Is.EqualTo(metadata["test2"]));
	}

	[Test]
	public async Task CreateCollectionAlreadyExists()
	{
		var name = $"collection{Random.Shared.Next()}";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		await client.CreateCollection(name);
		var result = await client.CreateCollection(name);
		Assert.That(result.Success, Is.False);
		Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
	}

	[Test]
	public async Task DeleteCollection()
	{
		var name = $"collection{Random.Shared.Next()}";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		await client.CreateCollection(name);
		var result = await client.DeleteCollection(name);
		Assert.That(result.Success, Is.True);
	}

	[Test]
	public async Task DeleteCollectionNotExists()
	{
		var name = $"collection{Random.Shared.Next()}";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var result = await client.DeleteCollection(name);
		Assert.That(result.Success, Is.False);
		Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
	}

	[Test]
	public async Task GetOrCreateCollectionDoesNotExist()
	{
		var name = $"collection{Random.Shared.Next()}";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var result = await client.GetOrCreateCollection(name);
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Is.Not.Null);
		Assert.That(result.Data.Name, Is.EqualTo(name));
	}

	[Test]
	public async Task GetOrCreateCollectionDoesExist()
	{
		var name = $"collection{Random.Shared.Next()}";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var result1 = await client.GetOrCreateCollection(name);
		var result2 = await client.GetOrCreateCollection(name);
		Assert.That(result1.Success, Is.True);
		Assert.That(result1.Data, Is.Not.Null);
		Assert.That(result1.Data.Name, Is.EqualTo(name));
		Assert.That(result2.Success, Is.True);
		Assert.That(result2.Data, Is.Not.Null);
		Assert.That(result2.Data.Name, Is.EqualTo(name));
		Assert.That(result1.Data.Id, Is.EqualTo(result2.Data.Id));
	}

	[Test]
	public async Task CountCollections()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var listResponse = await client.ListCollections();
		Assert.That(listResponse.Success, Is.True);
		var result = await client.CountCollections();
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Is.EqualTo(listResponse.Data!.Count));
	}
}
