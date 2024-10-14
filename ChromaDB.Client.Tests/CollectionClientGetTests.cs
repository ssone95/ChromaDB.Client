using ChromaDB.Client.Tests.Common;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class CollectionClientGetTests : ChromaTestsBase
{
	[Test]
	public async Task GetSingleIdIncludeNothing()
	{
		var client = await Init();
		var result = await client.Get(Id1,
			include: ChromaGetInclude.None);
		Assert.That(result, Is.Not.Null);
		Assert.That(result.Id, Is.EqualTo(Id1));
		Assert.That(result.Embeddings, Is.Null);
		Assert.That(result.Metadata, Is.Null);
		Assert.That(result.Document, Is.Null);
	}

	[Test]
	public async Task GetSingleIdIncludeEmbeddings()
	{
		var client = await Init();
		var result = await client.Get(Id1,
			include: ChromaGetInclude.Embeddings);
		Assert.That(result, Is.Not.Null);
		Assert.That(result.Id, Is.EqualTo(Id1));
		Assert.That(result.Embeddings, Is.EqualTo(Embeddings1).Using(EmbeddingsComparer.Instance));
		Assert.That(result.Metadata, Is.Null);
		Assert.That(result.Document, Is.Null);
	}

	[Test]
	public async Task GetSingleIdIncludeMetadatas()
	{
		var client = await Init();
		var result = await client.Get(Id1,
			include: ChromaGetInclude.Metadatas);
		Assert.That(result, Is.Not.Null);
		Assert.That(result.Id, Is.EqualTo(Id1));
		Assert.That(result.Embeddings, Is.Null);
		Assert.That(result.Metadata, Is.EqualTo(Metadata1));
		Assert.That(result.Document, Is.Null);
	}

	[Test]
	public async Task GetSingleIdIncludeDocuments()
	{
		var client = await Init();
		var result = await client.Get(Id1,
			include: ChromaGetInclude.Documents);
		Assert.That(result, Is.Not.Null);
		Assert.That(result.Id, Is.EqualTo(Id1));
		Assert.That(result.Embeddings, Is.Null);
		Assert.That(result.Metadata, Is.Null);
		Assert.That(result.Document, Is.EqualTo(Doc1));
	}

	[Test]
	public async Task GetSingleIdIncludeAll()
	{
		var client = await Init();
		var result = await client.Get(Id1,
			include: ChromaGetInclude.Embeddings | ChromaGetInclude.Metadatas | ChromaGetInclude.Documents);
		Assert.That(result, Is.Not.Null);
		Assert.That(result.Id, Is.EqualTo(Id1));
		Assert.That(result.Embeddings, Is.EqualTo(Embeddings1).Using(EmbeddingsComparer.Instance));
		Assert.That(result.Metadata, Is.EqualTo(Metadata1));
		Assert.That(result.Document, Is.EqualTo(Doc1));
	}

	[Test]
	public async Task GetMultipleIdsIncludeNothing()
	{
		var client = await Init();
		var result = await client.Get(
			ids: [Id1, Id2],
			include: ChromaGetInclude.None);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0].Id, Is.EqualTo(Id1));
		Assert.That(result[0].Embeddings, Is.Null);
		Assert.That(result[0].Metadata, Is.Null);
		Assert.That(result[0].Document, Is.Null);
		Assert.That(result[1].Id, Is.EqualTo(Id2));
		Assert.That(result[1].Embeddings, Is.Null);
		Assert.That(result[1].Metadata, Is.Null);
		Assert.That(result[1].Document, Is.Null);
	}

	[Test]
	public async Task GetMultipleIdsIncludeEmbeddings()
	{
		var client = await Init();
		var result = await client.Get(
			ids: [Id1, Id2],
			include: ChromaGetInclude.Embeddings);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0].Id, Is.EqualTo(Id1));
		Assert.That(result[0].Embeddings, Is.EqualTo(Embeddings1).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.Null);
		Assert.That(result[0].Document, Is.Null);
		Assert.That(result[1].Id, Is.EqualTo(Id2));
		Assert.That(result[1].Embeddings, Is.EqualTo(Embeddings2).Using(EmbeddingsComparer.Instance));
		Assert.That(result[1].Metadata, Is.Null);
		Assert.That(result[1].Document, Is.Null);
	}

	[Test]
	public async Task GetMultipleIdsIncludeMetadatas()
	{
		var client = await Init();
		var result = await client.Get(
			ids: [Id1, Id2],
			include: ChromaGetInclude.Metadatas);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0].Id, Is.EqualTo(Id1));
		Assert.That(result[0].Embeddings, Is.Null);
		Assert.That(result[0].Metadata, Is.EqualTo(Metadata1));
		Assert.That(result[0].Document, Is.Null);
		Assert.That(result[1].Id, Is.EqualTo(Id2));
		Assert.That(result[1].Embeddings, Is.Null);
		Assert.That(result[1].Metadata, Is.EqualTo(Metadata2));
		Assert.That(result[1].Document, Is.Null);
	}

	[Test]
	public async Task GetMultipleIdsIncludeDocuments()
	{
		var client = await Init();
		var result = await client.Get(
			ids: [Id1, Id2],
			include: ChromaGetInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0].Id, Is.EqualTo(Id1));
		Assert.That(result[0].Embeddings, Is.Null);
		Assert.That(result[0].Metadata, Is.Null);
		Assert.That(result[0].Document, Is.EqualTo(Doc1));
		Assert.That(result[1].Id, Is.EqualTo(Id2));
		Assert.That(result[1].Embeddings, Is.Null);
		Assert.That(result[1].Metadata, Is.Null);
		Assert.That(result[1].Document, Is.EqualTo(Doc2));
	}

	[Test]
	public async Task GetMultipleIdsIncludeAll()
	{
		var client = await Init();
		var result = await client.Get(
			ids: [Id1, Id2],
			include: ChromaGetInclude.Embeddings | ChromaGetInclude.Metadatas | ChromaGetInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0].Id, Is.EqualTo(Id1));
		Assert.That(result[0].Embeddings, Is.EqualTo(Embeddings1).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.EqualTo(Metadata1));
		Assert.That(result[0].Document, Is.EqualTo(Doc1));
		Assert.That(result[1].Id, Is.EqualTo(Id2));
		Assert.That(result[1].Embeddings, Is.EqualTo(Embeddings2).Using(EmbeddingsComparer.Instance));
		Assert.That(result[1].Metadata, Is.EqualTo(Metadata2));
		Assert.That(result[1].Document, Is.EqualTo(Doc2));
	}

	[Test]
	public async Task GetLimitIncludeAll()
	{
		var client = await Init();
		var result = await client.Get(
			ids: [Id1, Id2],
			include: ChromaGetInclude.Embeddings | ChromaGetInclude.Metadatas | ChromaGetInclude.Documents,
			limit: 1);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Id, Is.EqualTo(Id1));
		Assert.That(result[0].Embeddings, Is.EqualTo(Embeddings1).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.EqualTo(Metadata1));
		Assert.That(result[0].Document, Is.EqualTo(Doc1));
	}

	[Test]
	public async Task GetLimitOffsetIncludeAll()
	{
		var client = await Init();
		var result = await client.Get(
			ids: [Id1, Id2],
			include: ChromaGetInclude.Embeddings | ChromaGetInclude.Metadatas | ChromaGetInclude.Documents,
			limit: 1,
			offset: 1);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Id, Is.EqualTo(Id2));
		Assert.That(result[0].Embeddings, Is.EqualTo(Embeddings2).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.EqualTo(Metadata2));
		Assert.That(result[0].Document, Is.EqualTo(Doc2));
	}

	[Test]
	public async Task GetWhereIncludeAll()
	{
		var client = await Init();
		var result = await client.Get(
			where: new Dictionary<string, object> { { MetadataKey2, Metadata2[MetadataKey2] } },
			include: ChromaGetInclude.Embeddings | ChromaGetInclude.Metadatas | ChromaGetInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Id, Is.EqualTo(Id2));
		Assert.That(result[0].Embeddings, Is.EqualTo(Embeddings2).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.EqualTo(Metadata2));
		Assert.That(result[0].Document, Is.EqualTo(Doc2));
	}

	[Test]
	public async Task GetWhereOperatorIncludeAll()
	{
		var client = await Init();
		var result = await client.Get(
			where: new Dictionary<string, object> { { MetadataKey2, new Dictionary<string, object> { { "$lt", Metadata2[MetadataKey2] } } } },
			include: ChromaGetInclude.Embeddings | ChromaGetInclude.Metadatas | ChromaGetInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Id, Is.EqualTo(Id1));
		Assert.That(result[0].Embeddings, Is.EqualTo(Embeddings1).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.EqualTo(Metadata1));
		Assert.That(result[0].Document, Is.EqualTo(Doc1));
	}

	[Test]
	public async Task GetWhereDocumentIncludeDocuments()
	{
		var client = await Init();
		var result = await client.Get(
			whereDocument: new Dictionary<string, object> { { "$not_contains", Doc2[^1] } },
			include: ChromaGetInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Id, Is.EqualTo(Id1));
		Assert.That(result[0].Embeddings, Is.Null);
		Assert.That(result[0].Metadata, Is.Null);
		Assert.That(result[0].Document, Is.EqualTo(Doc1));
	}

	static readonly string Id1 = "id1";
	static readonly string Id2 = "id2";
	static readonly ReadOnlyMemory<float> Embeddings1 = new([1, 2, 3]);
	static readonly ReadOnlyMemory<float> Embeddings2 = new([1.4f, 1.5f, 99.33f]);
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

	async Task<ChromaCollectionClient> Init()
	{
		var name = $"collection{Random.Shared.Next()}";
		var client = new ChromaClient(ConfigurationOptions, HttpClient);
		var collection = await client.CreateCollection(name);
		var collectionClient = new ChromaCollectionClient(collection, ConfigurationOptions, HttpClient);
		await collectionClient.Add([Id1, Id2],
			embeddings: [Embeddings1, Embeddings2],
			metadatas: [Metadata1, Metadata2],
			documents: [Doc1, Doc2]);
		return collectionClient;
	}
}
