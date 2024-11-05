using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class ClientTests : ChromaTestsBase
{
	[Test]
	public async Task NoAuth()
	{
		var client = new ChromaClient(ConfigurationOptions, HttpClient);
		await Assert.ThatAsync(client.Heartbeat, Throws.Nothing);
	}

	[Test]
	public async Task HeartbeatSimple()
	{
		var client = new ChromaClient(ConfigurationOptions, HttpClient);
		var result = await client.Heartbeat();
		Assert.That(result.NanosecondHeartbeat, Is.GreaterThan(0));
	}

	[Test]
	public async Task GetVersionSimple()
	{
		var client = new ChromaClient(ConfigurationOptions, HttpClient);
		var result = await client.GetVersion();
		Assert.That(result, Does.Match(@"\d+\.\d+"));
	}

	[Test]
	public async Task GetCollectionSimple()
	{
		var name = $"collection{Random.Shared.Next()}";

		var client = new ChromaClient(ConfigurationOptions, HttpClient);
		await client.CreateCollection(name);
		var result = await client.GetCollection(name);
		Assert.That(result, Is.Not.Null);
		Assert.That(result.Name, Is.EqualTo(name));
	}

	[Test]
	public async Task ListCollectionsSimple()
	{
		var names = new[] { $"collection{Random.Shared.Next()}", $"collection{Random.Shared.Next()}" };

		var client = new ChromaClient(ConfigurationOptions, HttpClient);
		await client.CreateCollection(names[0]);
		await client.CreateCollection(names[1]);
		var result = await client.ListCollections();
		Assert.That(result, Is.Not.Null);
		Assert.That(result, Has.Count.GreaterThanOrEqualTo(2));
		Assert.That(result.Select(x => x.Name), Contains.Item(names[0]));
		Assert.That(result.Select(x => x.Name), Contains.Item(names[1]));
	}

	[Test]
	public async Task CreateCollectionWithoutMetadata()
	{
		var name = $"collection{Random.Shared.Next()}";

		var client = new ChromaClient(ConfigurationOptions, HttpClient);
		var result = await client.CreateCollection(name);
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

		var client = new ChromaClient(ConfigurationOptions, HttpClient);
		var result = await client.CreateCollection(name,
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

		var client = new ChromaClient(ConfigurationOptions, HttpClient);
		await client.CreateCollection(name);
		await Assert.ThatAsync(async () => await client.CreateCollection(name), Throws.InstanceOf<ChromaException>().With.Message.Not.Null.And.With.Message.Not.Empty);
	}

	[Test]
	public async Task DeleteCollection()
	{
		var name = $"collection{Random.Shared.Next()}";

		var client = new ChromaClient(ConfigurationOptions, HttpClient);
		await client.CreateCollection(name);
		await client.DeleteCollection(name);
	}

	[Test]
	public async Task DeleteCollectionNotExists()
	{
		var name = $"collection{Random.Shared.Next()}";

		var client = new ChromaClient(ConfigurationOptions, HttpClient);
		await Assert.ThatAsync(async () => await client.DeleteCollection(name), Throws.InstanceOf<ChromaException>().With.Message.Not.Null.And.With.Message.Not.Empty);
	}

	[Test]
	public async Task GetOrCreateCollectionDoesNotExist()
	{
		var name = $"collection{Random.Shared.Next()}";

		var client = new ChromaClient(ConfigurationOptions, HttpClient);
		var result = await client.GetOrCreateCollection(name);
		Assert.That(result, Is.Not.Null);
		Assert.That(result.Name, Is.EqualTo(name));
	}

	[Test]
	public async Task GetOrCreateCollectionDoesExist()
	{
		var name = $"collection{Random.Shared.Next()}";

		var client = new ChromaClient(ConfigurationOptions, HttpClient);
		var result1 = await client.GetOrCreateCollection(name);
		var result2 = await client.GetOrCreateCollection(name);
		Assert.That(result1, Is.Not.Null);
		Assert.That(result1.Name, Is.EqualTo(name));
		Assert.That(result2, Is.Not.Null);
		Assert.That(result2.Name, Is.EqualTo(name));
		Assert.That(result1.Id, Is.EqualTo(result2.Id));
	}

	[Test]
	public async Task CountCollections()
	{
		var client = new ChromaClient(ConfigurationOptions, HttpClient);
		var list = await client.ListCollections();
		var result = await client.CountCollections();
		Assert.That(result, Is.EqualTo(list.Count));
	}
}
