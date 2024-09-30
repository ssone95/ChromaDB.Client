﻿using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Services.Implementations;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class ChromaDBCollectionTests : ChromaDBTestsBase
{
	[Test]
	public async Task GetCollectionSimple()
	{
		var name = $"collection{Random.Shared.Next()}";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		await client.CreateCollection(new DBCreateCollectionRequest()
		{
			Name = name,
		});
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
		await client.CreateCollection(new DBCreateCollectionRequest()
		{
			Name = names[0],
		});
		await client.CreateCollection(new DBCreateCollectionRequest()
		{
			Name = names[1],
		});
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
		var result = await client.CreateCollection(new DBCreateCollectionRequest()
		{
			Name = name,
		});
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
		var result = await client.CreateCollection(new DBCreateCollectionRequest()
		{
			Name = name,
			Metadata = metadata,
		});
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
		await client.CreateCollection(new DBCreateCollectionRequest()
		{
			Name = name,
		});
		var result = await client.CreateCollection(new DBCreateCollectionRequest()
		{
			Name = name,
		});
		Assert.That(result.Success, Is.False);
		Assert.That(result.ReasonPhrase, Is.Not.Null.And.Not.Empty);
	}

	[Test]
	public async Task DeleteCollection()
	{
		var name = $"collection{Random.Shared.Next()}";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		await client.CreateCollection(new DBCreateCollectionRequest()
		{
			Name = name,
		});
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
		Assert.That(result.ReasonPhrase, Is.Not.Null.And.Not.Empty);
	}

	[Test]
	public async Task GetOrCreateCollectionDoesNotExist()
	{
		var name = $"collection{Random.Shared.Next()}";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var result = await client.GetOrCreateCollection(new DBGetOrCreateCollectionRequest()
		{
			Name = name,
		});
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
		var result1 = await client.GetOrCreateCollection(new DBGetOrCreateCollectionRequest()
		{
			Name = name,
		});
		var result2 = await client.GetOrCreateCollection(new DBGetOrCreateCollectionRequest()
		{
			Name = name,
		});
		Assert.That(result1.Success, Is.True);
		Assert.That(result1.Data, Is.Not.Null);
		Assert.That(result1.Data.Name, Is.EqualTo(name));
		Assert.That(result2.Success, Is.True);
		Assert.That(result2.Data, Is.Not.Null);
		Assert.That(result2.Data.Name, Is.EqualTo(name));
		Assert.That(result1.Data.Id, Is.EqualTo(result2.Data.Id));
	}

	[Test]
	public async Task CountEmptyCollection()
	{
		var name = $"collection{Random.Shared.Next()}";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var collectionResponse = await client.CreateCollection(new DBCreateCollectionRequest()
		{
			Name = name,
		});
		var collectionClient = new ChromaCollectionClient(collectionResponse.Data!, httpClient);
		var result = await collectionClient.Count();
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Is.EqualTo(0));
	}

	[Test]
	public async Task CountNonEmptyCollection()
	{
		var name = $"collection{Random.Shared.Next()}";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var collectionResponse = await client.CreateCollection(new DBCreateCollectionRequest()
		{
			Name = name,
		});
		var collectionClient = new ChromaCollectionClient(collectionResponse.Data!, httpClient);
		await collectionClient.Add(new CollectionAddRequest()
		{
			Ids = [$"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}"],
		});
		var result = await collectionClient.Count();
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Is.EqualTo(6));
	}

	[Test]
	public async Task PeekDefault()
	{
		var name = $"collection{Random.Shared.Next()}";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var collectionResponse = await client.CreateCollection(new DBCreateCollectionRequest()
		{
			Name = name,
		});
		var collectionClient = new ChromaCollectionClient(collectionResponse.Data!, httpClient);
		await collectionClient.Add(new CollectionAddRequest()
		{
			Ids = [$"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}"],
		});
		var result = await collectionClient.Peek(new CollectionPeekRequest());
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data!.Count, Is.GreaterThan(0));
	}

	[Test]
	public async Task PeekExplicitLimit()
	{
		var name = $"collection{Random.Shared.Next()}";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var collectionResponse = await client.CreateCollection(new DBCreateCollectionRequest()
		{
			Name = name,
		});
		var collectionClient = new ChromaCollectionClient(collectionResponse.Data!, httpClient);
		await collectionClient.Add(new CollectionAddRequest()
		{
			Ids = [$"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}", $"{Guid.NewGuid()}"],
		});
		var result = await collectionClient.Peek(new CollectionPeekRequest() {  Limit = 2 });
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data!.Count, Is.EqualTo(2));
	}

	[Test]
	public async Task CountCollections()
	{
		var name = $"collection{Random.Shared.Next()}";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var listResponse = await client.ListCollections();
		Assert.That(listResponse.Success, Is.True);
		var result = await client.CountCollections();
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Is.EqualTo(listResponse.Data!.Count));
	}

	[Test]
	public async Task ModifyCollectionName()
	{
		var name = $"collection{Random.Shared.Next()}";
		var name2 = $"{name}_modified";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var collectionResponse = await client.CreateCollection(new DBCreateCollectionRequest()
		{
			Name = name,
		});
		var collectionClient = new ChromaCollectionClient(collectionResponse.Data!, httpClient);
		var modifyResponse = await collectionClient.Modify(new CollectionModifyRequest()
		{
			Name = name2,
		});
		Assert.That(modifyResponse.Success, Is.True);
	}

	[Test]
	public async Task ModifyCollectionMetadata()
	{
		var name = $"collection{Random.Shared.Next()}";
		var metadata = new Dictionary<string, object>()
		{
			{ "test", "foo" },
			{ "test2", 10 },
		};

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var collectionResponse = await client.CreateCollection(new DBCreateCollectionRequest()
		{
			Name = name,
		});
		var collectionClient = new ChromaCollectionClient(collectionResponse.Data!, httpClient);
		var modifyResponse = await collectionClient.Modify(new CollectionModifyRequest()
		{
			Metadata = metadata,
		});
		Assert.That(modifyResponse.Success, Is.True);
	}

	[Test]
	public async Task ModifyCollectionAll()
	{
		var name = $"collection{Random.Shared.Next()}";
		var name2 = $"{name}_modified";
		var metadata = new Dictionary<string, object>()
		{
			{ "test", "foo" },
			{ "test2", 10 },
		};
		var metadata2 = new Dictionary<string, object>()
		{
			{ "test", 10 },
			{ "test3", "bar" },
		};

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var collectionResponse = await client.CreateCollection(new DBCreateCollectionRequest()
		{
			Name = name,
		});
		var collectionClient = new ChromaCollectionClient(collectionResponse.Data!, httpClient);
		var modifyResponse = await collectionClient.Modify(new CollectionModifyRequest()
		{
			Name = name2,
			Metadata = metadata2,
		});
		Assert.That(modifyResponse.Success, Is.True);
	}
}
