using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class CollectionClientTests : ChromaTestsBase
{
	[Test]
	public async Task CountEmptyCollection()
	{
		var client = await Init();
		var result = await client.Count();
		Assert.That(result, Is.EqualTo(0));
	}

	[Test]
	public async Task CountNonEmptyCollection()
	{
		var client = await Init();
		await client.Add([$"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}"]);
		var result = await client.Count();
		Assert.That(result, Is.EqualTo(6));
	}

	[Test]
	public async Task PeekDefault()
	{
		var client = await Init();
		await client.Add([$"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}"]);
		var result = await client.Peek();
		Assert.That(result, Is.Not.Empty);
	}

	[Test]
	public async Task PeekExplicitLimit()
	{
		var client = await Init();
		await client.Add([$"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}"]);
		var result = await client.Peek(
			limit: 2);
		Assert.That(result, Has.Count.EqualTo(2));
	}

	[Test]
	public async Task ModifyCollectionName()
	{
		var client = await Init();
		await client.Modify(
			name: $"{client.Collection.Name}_modified");
	}

	[Test]
	public async Task ModifyCollectionMetadata()
	{
		var metadata = new Dictionary<string, object>()
		{
			{ "test", "foo" },
			{ "test2", 10 },
		};

		var client = await Init();
		await client.Modify(
			metadata: metadata);
	}

	[Test]
	public async Task ModifyCollectionAll()
	{
		var metadata = new Dictionary<string, object>()
		{
			{ "test", 10 },
			{ "test3", "bar" },
		};

		var client = await Init();
		await client.Modify(
			name: $"{client.Collection.Name}_modified",
			metadata: metadata);
	}

	async Task<ChromaCollectionClient> Init()
	{
		var name = $"collection{Random.Shared.Next()}";
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var collection = await client.CreateCollection(name);
		return new ChromaCollectionClient(collection, BaseConfigurationOptions, HttpClient);
	}
}
