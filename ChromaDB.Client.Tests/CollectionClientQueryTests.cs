using ChromaDB.Client.Tests.Common;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class CollectionClientQueryTests : ChromaTestsBase
{
	static readonly float DistanceTolerance = 0.0001f;

	[Test]
	public async Task SimpleQuerySingle()
	{
		var client = await Init();
		var result = await client.Query(Embeddings1,
			include: ChromaQueryInclude.Distances | ChromaQueryInclude.Embeddings);
		Assert.That(result, Is.Not.Null);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result.Select(x => x.Distance), Has.Some.Not.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0].Embeddings, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[0].Metadata, Is.Null);
		Assert.That(result[0].Document, Is.Null);
		Assert.That(result[1].Embeddings, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[1].Metadata, Is.Null);
		Assert.That(result[1].Document, Is.Null);
	}

	[Test]
	public async Task SimpleQuerySingleIncludeAll()
	{
		var client = await Init();
		var result = await client.Query(Embeddings1,
			include: ChromaQueryInclude.Distances | ChromaQueryInclude.Embeddings | ChromaQueryInclude.Metadatas | ChromaQueryInclude.Documents);
		Assert.That(result, Is.Not.Null);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result.Select(x => x.Distance), Has.Some.Not.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0].Embeddings, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[0].Metadata, Is.Not.Null.And.Not.Empty);
		Assert.That(result[0].Document, Is.Not.Null.And.Not.Empty);
		Assert.That(result[1].Embeddings, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[1].Metadata, Is.Not.Null.And.Not.Empty);
		Assert.That(result[1].Document, Is.Not.Null.And.Not.Empty);
	}

	[Test]
	public async Task SimpleQueryMultiple()
	{
		var client = await Init();
		var result = await client.Query([Embeddings1, Embeddings2],
			include: ChromaQueryInclude.Distances | ChromaQueryInclude.Embeddings);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(2));
		Assert.That(result[0].Select(x => x.Distance), Has.Some.Not.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Embeddings, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[0][1].Embeddings, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[0][1].Metadata, Is.Null);
		Assert.That(result[0][1].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(2));
		Assert.That(result[1].Select(x => x.Distance), Has.Some.Not.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[1][0].Embeddings, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
		Assert.That(result[1][1].Embeddings, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[1][1].Metadata, Is.Null);
		Assert.That(result[1][1].Document, Is.Null);
	}

	[Test]
	public async Task SimpleQueryMultipleIncludeAll()
	{
		var client = await Init();
		var result = await client.Query([Embeddings1, Embeddings2],
			include: ChromaQueryInclude.Distances | ChromaQueryInclude.Embeddings | ChromaQueryInclude.Metadatas | ChromaQueryInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(2));
		Assert.That(result[0].Select(x => x.Distance), Has.Some.Not.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Embeddings, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[0][0].Metadata, Is.Not.Null.And.Not.Empty);
		Assert.That(result[0][0].Document, Is.Not.Null.And.Not.Empty);
		Assert.That(result[0][1].Embeddings, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[0][1].Metadata, Is.Not.Null.And.Not.Empty);
		Assert.That(result[0][1].Document, Is.Not.Null.And.Not.Empty);
		Assert.That(result[1], Has.Count.EqualTo(2));
		Assert.That(result[1].Select(x => x.Distance), Has.Some.Not.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[1][0].Embeddings, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[1][0].Metadata, Is.Not.Null.And.Not.Empty);
		Assert.That(result[1][0].Document, Is.Not.Null.And.Not.Empty);
		Assert.That(result[1][1].Embeddings, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[1][1].Metadata, Is.Not.Null.And.Not.Empty);
		Assert.That(result[1][1].Document, Is.Not.Null.And.Not.Empty);
	}

	[Test]
	public async Task QuerySingleNResults1()
	{
		var client = await Init();
		var result = await client.Query(Embeddings1,
			include: ChromaQueryInclude.Distances | ChromaQueryInclude.Embeddings,
			nResults: 1);
		Assert.That(result, Is.Not.Null);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0].Embeddings, Is.EqualTo(Embeddings1).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.Null);
		Assert.That(result[0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereEqual()
	{
		var client = await Init();
		var result = await client.Query([Embeddings1, Embeddings2],
			where: ChromaWhere.Equal(MetadataKey2, Metadata1[MetadataKey2]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Id, Is.EqualTo(Id1));
		Assert.That(result[0][0].Embeddings, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.GreaterThan(0));
		Assert.That(result[1][0].Id, Is.EqualTo(Id1));
		Assert.That(result[1][0].Embeddings, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereNotEqual()
	{
		var client = await Init();
		var result = await client.Query([Embeddings1, Embeddings2],
			where: ChromaWhere.NotEqual(MetadataKey2, Metadata2[MetadataKey2]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Id, Is.EqualTo(Id1));
		Assert.That(result[0][0].Embeddings, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.GreaterThan(0));
		Assert.That(result[1][0].Id, Is.EqualTo(Id1));
		Assert.That(result[1][0].Embeddings, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereIn()
	{
		var client = await Init();
		var result = await client.Query([Embeddings1, Embeddings2],
			where: ChromaWhere.In(MetadataKey2, Metadata1[MetadataKey2]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Id, Is.EqualTo(Id1));
		Assert.That(result[0][0].Embeddings, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.GreaterThan(0));
		Assert.That(result[1][0].Id, Is.EqualTo(Id1));
		Assert.That(result[1][0].Embeddings, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereNotIn()
	{
		var client = await Init();
		var result = await client.Query([Embeddings1, Embeddings2],
			where: ChromaWhere.NotIn(MetadataKey2, Metadata2[MetadataKey2]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Id, Is.EqualTo(Id1));
		Assert.That(result[0][0].Embeddings, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.GreaterThan(0));
		Assert.That(result[1][0].Id, Is.EqualTo(Id1));
		Assert.That(result[1][0].Embeddings, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereGreaterThan()
	{
		var client = await Init();
		var result = await client.Query([Embeddings1, Embeddings2],
			where: ChromaWhere.GreaterThan(MetadataKey2, Metadata1[MetadataKey2]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.GreaterThan(0));
		Assert.That(result[0][0].Id, Is.EqualTo(Id2));
		Assert.That(result[0][0].Embeddings, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[1][0].Id, Is.EqualTo(Id2));
		Assert.That(result[1][0].Embeddings, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereLessThan()
	{
		var client = await Init();
		var result = await client.Query([Embeddings1, Embeddings2],
			where: ChromaWhere.LessThan(MetadataKey2, Metadata2[MetadataKey2]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Id, Is.EqualTo(Id1));
		Assert.That(result[0][0].Embeddings, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.GreaterThan(0));
		Assert.That(result[1][0].Id, Is.EqualTo(Id1));
		Assert.That(result[1][0].Embeddings, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereGreaterThanOrEqual()
	{
		var client = await Init();
		var result = await client.Query([Embeddings1, Embeddings2],
			where: ChromaWhere.GreaterThanOrEqual(MetadataKey2, Metadata2[MetadataKey2]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.GreaterThan(0));
		Assert.That(result[0][0].Id, Is.EqualTo(Id2));
		Assert.That(result[0][0].Embeddings, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[1][0].Id, Is.EqualTo(Id2));
		Assert.That(result[1][0].Embeddings, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereLessThanOrEqual()
	{
		var client = await Init();
		var result = await client.Query([Embeddings1, Embeddings2],
			where: ChromaWhere.LessThanOrEqual(MetadataKey2, Metadata1[MetadataKey2]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Id, Is.EqualTo(Id1));
		Assert.That(result[0][0].Embeddings, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.GreaterThan(0));
		Assert.That(result[1][0].Id, Is.EqualTo(Id1));
		Assert.That(result[1][0].Embeddings, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereAndOr()
	{
		var client = await Init();
		var result = await client.Query([Embeddings1, Embeddings2],
			where: ChromaWhere.Equal(MetadataKey2, Metadata1[MetadataKey2]) && ChromaWhere.NotEqual(MetadataKey2, Metadata1[MetadataKey2]) || ChromaWhere.NotEqual(MetadataKey2, Metadata2[MetadataKey2]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Id, Is.EqualTo(Id1));
		Assert.That(result[0][0].Embeddings, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.GreaterThan(0));
		Assert.That(result[1][0].Id, Is.EqualTo(Id1));
		Assert.That(result[1][0].Embeddings, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereDocumentContains()
	{
		var client = await Init();
		var result = await client.Query([Embeddings1, Embeddings2],
			whereDocument: ChromaWhereDocument.Contains(Doc1[^1]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Id, Is.EqualTo(Id1));
		Assert.That(result[0][0].Embeddings, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.GreaterThan(0));
		Assert.That(result[1][0].Id, Is.EqualTo(Id1));
		Assert.That(result[1][0].Embeddings, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereDocumentNotContains()
	{
		var client = await Init();
		var result = await client.Query([Embeddings1, Embeddings2],
			whereDocument: ChromaWhereDocument.NotContains(Doc2[^1]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Id, Is.EqualTo(Id1));
		Assert.That(result[0][0].Embeddings, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.GreaterThan(0));
		Assert.That(result[1][0].Id, Is.EqualTo(Id1));
		Assert.That(result[1][0].Embeddings, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereDocumentAndOr()
	{
		var client = await Init();
		var result = await client.Query([Embeddings1, Embeddings2],
			whereDocument: ChromaWhereDocument.Contains(Doc1) && ChromaWhereDocument.NotContains(Doc1) || ChromaWhereDocument.NotContains(Doc2),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Id, Is.EqualTo(Id1));
		Assert.That(result[0][0].Embeddings, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.GreaterThan(0));
		Assert.That(result[1][0].Id, Is.EqualTo(Id1));
		Assert.That(result[1][0].Embeddings, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
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
