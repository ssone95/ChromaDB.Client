using ChromaDB.Client.Models.Requests;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class CollectionClientTests : ChromaDBTestsBase
{
	[Test]
	public async Task CountEmptyCollection()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Count();
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Is.EqualTo(0));
	}

	[Test]
	public async Task CountNonEmptyCollection()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		await client.Add([$"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}"]);
		var result = await client.Count();
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Is.EqualTo(6));
	}

	[Test]
	public async Task PeekDefault()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		await client.Add([$"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}"]);
		var result = await client.Peek();
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data!.Count, Is.GreaterThan(0));
	}

	[Test]
	public async Task PeekExplicitLimit()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		await client.Add([$"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}"]);
		var result = await client.Peek(
			limit: 2);
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data!.Count, Is.EqualTo(2));
	}

	[Test]
	public async Task ModifyCollectionName()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Modify(
			name: $"{client.Collection.Name}_modified");
		Assert.That(result.Success, Is.True);
	}

	[Test]
	public async Task ModifyCollectionMetadata()
	{
		var metadata = new Dictionary<string, object>()
		{
			{ "test", "foo" },
			{ "test2", 10 },
		};

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Modify(
			metadata: metadata);
		Assert.That(result.Success, Is.True);
	}

	[Test]
	public async Task ModifyCollectionAll()
	{
		var metadata = new Dictionary<string, object>()
		{
			{ "test", 10 },
			{ "test3", "bar" },
		};

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Modify(
			name: $"{client.Collection.Name}_modified",
			metadata: metadata);
		Assert.That(result.Success, Is.True);
	}

	async Task<ChromaDBCollectionClient> Init(ChromaDBHttpClient httpClient)
	{
		var name = $"collection{Random.Shared.Next()}";
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var collectionResponse = await client.CreateCollection(new CreateCollectionRequest { Name = name });
		Assert.That(collectionResponse.Success, Is.True);
		var collection = collectionResponse.Data!;
		return new ChromaDBCollectionClient(collection, httpClient);
	}
}
