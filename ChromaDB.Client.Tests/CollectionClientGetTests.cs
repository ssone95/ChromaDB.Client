﻿using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class CollectionClientGetTests : ChromaDBTestsBase
{
	[Test]
	public async Task GetSingleIdIncludeNothing()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Get(
			ids: [Id1],
			include: []);
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Has.Count.EqualTo(1));
		Assert.That(result.Data![0].Id, Is.EqualTo(Id1));
		Assert.That(result.Data![0].Embeddings, Is.Null);
		Assert.That(result.Data![0].Metadata, Is.Null);
		Assert.That(result.Data![0].Document, Is.Null);
	}

	[Test]
	public async Task GetSingleIdIncludeEmbeddings()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Get(
			ids: [Id1],
			include: ["embeddings"]);
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Has.Count.EqualTo(1));
		Assert.That(result.Data![0].Id, Is.EqualTo(Id1));
		Assert.That(result.Data![0].Embeddings, Is.EqualTo(Embeddings1));
		Assert.That(result.Data![0].Metadata, Is.Null);
		Assert.That(result.Data![0].Document, Is.Null);
	}

	[Test]
	public async Task GetSingleIdIncludeMetadatas()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Get(
			ids: [Id1],
			include: ["metadatas"]);
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Has.Count.EqualTo(1));
		Assert.That(result.Data![0].Id, Is.EqualTo(Id1));
		Assert.That(result.Data![0].Embeddings, Is.Null);
		Assert.That(result.Data![0].Metadata, Is.EqualTo(Metadata1));
		Assert.That(result.Data![0].Document, Is.Null);
	}

	[Test]
	public async Task GetSingleIdIncludeDocuments()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Get(
			ids: [Id1],
			include: ["documents"]);
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Has.Count.EqualTo(1));
		Assert.That(result.Data![0].Id, Is.EqualTo(Id1));
		Assert.That(result.Data![0].Embeddings, Is.Null);
		Assert.That(result.Data![0].Metadata, Is.Null);
		Assert.That(result.Data![0].Document, Is.EqualTo(Doc1));
	}

	[Test]
	public async Task GetSingleIdIncludeAll()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Get(
			ids: [Id1],
			include: ["embeddings", "metadatas", "documents"]);
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Has.Count.EqualTo(1));
		Assert.That(result.Data![0].Id, Is.EqualTo(Id1));
		Assert.That(result.Data![0].Embeddings, Is.EqualTo(Embeddings1));
		Assert.That(result.Data![0].Metadata, Is.EqualTo(Metadata1));
		Assert.That(result.Data![0].Document, Is.EqualTo(Doc1));
	}

	[Test]
	public async Task GetMultipleIdsIncludeNothing()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Get(
			ids: [Id1, Id2],
			include: []);
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Has.Count.EqualTo(2));
		Assert.That(result.Data![0].Id, Is.EqualTo(Id1));
		Assert.That(result.Data![0].Embeddings, Is.Null);
		Assert.That(result.Data![0].Metadata, Is.Null);
		Assert.That(result.Data![0].Document, Is.Null);
		Assert.That(result.Data![1].Id, Is.EqualTo(Id2));
		Assert.That(result.Data![1].Embeddings, Is.Null);
		Assert.That(result.Data![1].Metadata, Is.Null);
		Assert.That(result.Data![1].Document, Is.Null);
	}

	[Test]
	public async Task GetMultipleIdsIncludeEmbeddings()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Get(
			ids: [Id1, Id2],
			include: ["embeddings"]);
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Has.Count.EqualTo(2));
		Assert.That(result.Data![0].Id, Is.EqualTo(Id1));
		Assert.That(result.Data![0].Embeddings, Is.EqualTo(Embeddings1));
		Assert.That(result.Data![0].Metadata, Is.Null);
		Assert.That(result.Data![0].Document, Is.Null);
		Assert.That(result.Data![1].Id, Is.EqualTo(Id2));
		Assert.That(result.Data![1].Embeddings, Is.EqualTo(Embeddings2));
		Assert.That(result.Data![1].Metadata, Is.Null);
		Assert.That(result.Data![1].Document, Is.Null);
	}

	[Test]
	public async Task GetMultipleIdsIncludeMetadatas()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Get(
			ids: [Id1, Id2],
			include: ["metadatas"]);
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Has.Count.EqualTo(2));
		Assert.That(result.Data![0].Id, Is.EqualTo(Id1));
		Assert.That(result.Data![0].Embeddings, Is.Null);
		Assert.That(result.Data![0].Metadata, Is.EqualTo(Metadata1));
		Assert.That(result.Data![0].Document, Is.Null);
		Assert.That(result.Data![1].Id, Is.EqualTo(Id2));
		Assert.That(result.Data![1].Embeddings, Is.Null);
		Assert.That(result.Data![1].Metadata, Is.EqualTo(Metadata2));
		Assert.That(result.Data![1].Document, Is.Null);
	}

	[Test]
	public async Task GetMultipleIdsIncludeDocuments()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Get(
			ids: [Id1, Id2],
			include: ["documents"]);
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Has.Count.EqualTo(2));
		Assert.That(result.Data![0].Id, Is.EqualTo(Id1));
		Assert.That(result.Data![0].Embeddings, Is.Null);
		Assert.That(result.Data![0].Metadata, Is.Null);
		Assert.That(result.Data![0].Document, Is.EqualTo(Doc1));
		Assert.That(result.Data![1].Id, Is.EqualTo(Id2));
		Assert.That(result.Data![1].Embeddings, Is.Null);
		Assert.That(result.Data![1].Metadata, Is.Null);
		Assert.That(result.Data![1].Document, Is.EqualTo(Doc2));
	}

	[Test]
	public async Task GetMultipleIdsIncludeAll()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Get(
			ids: [Id1, Id2],
			include: ["embeddings", "metadatas", "documents"]);
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Has.Count.EqualTo(2));
		Assert.That(result.Data![0].Id, Is.EqualTo(Id1));
		Assert.That(result.Data![0].Embeddings, Is.EqualTo(Embeddings1));
		Assert.That(result.Data![0].Metadata, Is.EqualTo(Metadata1));
		Assert.That(result.Data![0].Document, Is.EqualTo(Doc1));
		Assert.That(result.Data![1].Id, Is.EqualTo(Id2));
		Assert.That(result.Data![1].Embeddings, Is.EqualTo(Embeddings2));
		Assert.That(result.Data![1].Metadata, Is.EqualTo(Metadata2));
		Assert.That(result.Data![1].Document, Is.EqualTo(Doc2));
	}

	[Test]
	public async Task GetLimitIncludeAll()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Get(
			ids: [Id1, Id2],
			include: ["embeddings", "metadatas", "documents"],
			limit: 1);
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Has.Count.EqualTo(1));
		Assert.That(result.Data![0].Id, Is.EqualTo(Id1));
		Assert.That(result.Data![0].Embeddings, Is.EqualTo(Embeddings1));
		Assert.That(result.Data![0].Metadata, Is.EqualTo(Metadata1));
		Assert.That(result.Data![0].Document, Is.EqualTo(Doc1));
	}

	[Test]
	public async Task GetLimitOffsetIncludeAll()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Get(
			ids: [Id1, Id2],
			include: ["embeddings", "metadatas", "documents"],
			limit: 1,
			offset: 1);
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Has.Count.EqualTo(1));
		Assert.That(result.Data![0].Id, Is.EqualTo(Id2));
		Assert.That(result.Data![0].Embeddings, Is.EqualTo(Embeddings2));
		Assert.That(result.Data![0].Metadata, Is.EqualTo(Metadata2));
		Assert.That(result.Data![0].Document, Is.EqualTo(Doc2));
	}

	[Test]
	public async Task GetWhereIncludeAll()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Get(
			where: new Dictionary<string, object> { { MetadataKey2, Metadata2[MetadataKey2] } },
			include: ["embeddings", "metadatas", "documents"]);
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Has.Count.EqualTo(1));
		Assert.That(result.Data![0].Id, Is.EqualTo(Id2));
		Assert.That(result.Data![0].Embeddings, Is.EqualTo(Embeddings2));
		Assert.That(result.Data![0].Metadata, Is.EqualTo(Metadata2));
		Assert.That(result.Data![0].Document, Is.EqualTo(Doc2));
	}

	[Test]
	public async Task GetWhereOperatorIncludeAll()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Get(
			where: new Dictionary<string, object> { { MetadataKey2, new Dictionary<string, object> { { "$lt", Metadata2[MetadataKey2] } } } },
			include: ["embeddings", "metadatas", "documents"]);
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Has.Count.EqualTo(1));
		Assert.That(result.Data![0].Id, Is.EqualTo(Id1));
		Assert.That(result.Data![0].Embeddings, Is.EqualTo(Embeddings1));
		Assert.That(result.Data![0].Metadata, Is.EqualTo(Metadata1));
		Assert.That(result.Data![0].Document, Is.EqualTo(Doc1));
	}

	[Test]
	public async Task GetWhereDocumentIncludeDocuments()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Get(
			whereDocument: new Dictionary<string, object> { { "$not_contains", Doc2[^1] } },
			include: ["documents"]);
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Has.Count.EqualTo(1));
		Assert.That(result.Data![0].Id, Is.EqualTo(Id1));
		Assert.That(result.Data![0].Embeddings, Is.Null);
		Assert.That(result.Data![0].Metadata, Is.Null);
		Assert.That(result.Data![0].Document, Is.EqualTo(Doc1));
	}

	static readonly string Id1 = "id1";
	static readonly string Id2 = "id2";
	static readonly List<float> Embeddings1 = [1, 2, 3];
	static readonly List<float> Embeddings2 = [1.4f, 1.5f, 99.33f];
	static readonly string MetadataKey1 = "key1";
	static readonly string MetadataKey2 = "key2";
	static readonly Dictionary<string, object> Metadata1 = new()
	{
		{ MetadataKey1, "1" },
		{ MetadataKey2, 1 },
	};
	static readonly Dictionary<string, object> Metadata2 = new()
	{
		{ MetadataKey1, "2" },
		{ MetadataKey2, 2 },
	};
	static readonly string Doc1 = "Doc1";
	static readonly string Doc2 = "Doc2";

	async Task<ChromaDBCollectionClient> Init(ChromaDBHttpClient httpClient)
	{
		var name = $"collection{Random.Shared.Next()}";
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var collectionResponse = await client.CreateCollection(name);
		Assert.That(collectionResponse.Success, Is.True);
		var collection = collectionResponse.Data!;
		var collectionClient = new ChromaDBCollectionClient(collection, httpClient);
		var addResponse = await collectionClient.Add([Id1, Id2],
			embeddings: [Embeddings1, Embeddings2],
			metadatas: [Metadata1, Metadata2],
			documents: [Doc1, Doc2]);
		Assert.That(addResponse.Success, Is.True);
		return collectionClient;
	}
}
