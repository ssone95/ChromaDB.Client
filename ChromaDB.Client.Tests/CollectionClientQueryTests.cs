using ChromaDB.Client.Models;
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
		var result = await client.QueryAsync(Embeddings1,
			include: ChromaQueryInclude.Distances | ChromaQueryInclude.Embeddings);
		Assert.That(result, Is.Not.Null);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result.Select(x => x.Distance), Has.Some.Not.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0].Embedding, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[0].Metadata, Is.Null);
		Assert.That(result[0].Document, Is.Null);
		Assert.That(result[1].Embedding, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[1].Metadata, Is.Null);
		Assert.That(result[1].Document, Is.Null);
	}

	[Test]
	public async Task SimpleQuerySingleIncludeAll()
	{
		var client = await Init();
		var result = await client.QueryAsync(Embeddings1,
			include: ChromaQueryInclude.Distances | ChromaQueryInclude.Embeddings | ChromaQueryInclude.Metadatas | ChromaQueryInclude.Documents);
		Assert.That(result, Is.Not.Null);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result.Select(x => x.Distance), Has.Some.Not.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0].Embedding, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[0].Metadata, Is.Not.Null.And.Not.Empty);
		Assert.That(result[0].Document, Is.Not.Null.And.Not.Empty);
		Assert.That(result[1].Embedding, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[1].Metadata, Is.Not.Null.And.Not.Empty);
		Assert.That(result[1].Document, Is.Not.Null.And.Not.Empty);
	}

	[Test]
	public async Task SimpleQuerySingleWithoutDistances()
	{
		var client = await Init();
		var result = await client.QueryAsync(Embeddings1,
			include: ChromaQueryInclude.Embeddings);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result.Select(x => x.Distance), Has.All.Null);
	}

	[Test]
	public async Task SimpleQueryMultiple()
	{
		var client = await Init();
		var result = await client.QueryAsync([Embeddings1, Embeddings2],
			include: ChromaQueryInclude.Distances | ChromaQueryInclude.Embeddings);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(2));
		Assert.That(result[0].Select(x => x.Distance), Has.Some.Not.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Embedding, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[0][1].Embedding, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[0][1].Metadata, Is.Null);
		Assert.That(result[0][1].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(2));
		Assert.That(result[1].Select(x => x.Distance), Has.Some.Not.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[1][0].Embedding, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
		Assert.That(result[1][1].Embedding, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[1][1].Metadata, Is.Null);
		Assert.That(result[1][1].Document, Is.Null);
	}

	[Test]
	public async Task SimpleQueryMultipleIncludeAll()
	{
		var client = await Init();
		var result = await client.QueryAsync([Embeddings1, Embeddings2],
			include: ChromaQueryInclude.Distances | ChromaQueryInclude.Embeddings | ChromaQueryInclude.Metadatas | ChromaQueryInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(2));
		Assert.That(result[0].Select(x => x.Distance), Has.Some.Not.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Embedding, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[0][0].Metadata, Is.Not.Null.And.Not.Empty);
		Assert.That(result[0][0].Document, Is.Not.Null.And.Not.Empty);
		Assert.That(result[0][1].Embedding, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[0][1].Metadata, Is.Not.Null.And.Not.Empty);
		Assert.That(result[0][1].Document, Is.Not.Null.And.Not.Empty);
		Assert.That(result[1], Has.Count.EqualTo(2));
		Assert.That(result[1].Select(x => x.Distance), Has.Some.Not.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[1][0].Embedding, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[1][0].Metadata, Is.Not.Null.And.Not.Empty);
		Assert.That(result[1][0].Document, Is.Not.Null.And.Not.Empty);
		Assert.That(result[1][1].Embedding, Is.Not.Null.And.Length.GreaterThan(0));
		Assert.That(result[1][1].Metadata, Is.Not.Null.And.Not.Empty);
		Assert.That(result[1][1].Document, Is.Not.Null.And.Not.Empty);
	}

	[Test]
	public async Task QuerySingleNResults1()
	{
		var client = await Init();
		var result = await client.QueryAsync(Embeddings1,
			include: ChromaQueryInclude.Distances | ChromaQueryInclude.Embeddings,
			nResults: 1);
		Assert.That(result, Is.Not.Null);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0].Embedding, Is.EqualTo(Embeddings1).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.Null);
		Assert.That(result[0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereEqual()
	{
		var client = await Init();
		var result = await client.QueryAsync([Embeddings1, Embeddings2],
			where: ChromaWhereOperator.Equal(MetadataKey2, Metadata1[MetadataKey2]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Id, Is.EqualTo(Id1));
		Assert.That(result[0][0].Embedding, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.GreaterThan(0));
		Assert.That(result[1][0].Id, Is.EqualTo(Id1));
		Assert.That(result[1][0].Embedding, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereNotEqual()
	{
		var client = await Init();
		var result = await client.QueryAsync([Embeddings1, Embeddings2],
			where: ChromaWhereOperator.NotEqual(MetadataKey2, Metadata2[MetadataKey2]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Id, Is.EqualTo(Id1));
		Assert.That(result[0][0].Embedding, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.GreaterThan(0));
		Assert.That(result[1][0].Id, Is.EqualTo(Id1));
		Assert.That(result[1][0].Embedding, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereIn()
	{
		var client = await Init();
		var result = await client.QueryAsync([Embeddings1, Embeddings2],
			where: ChromaWhereOperator.In(MetadataKey2, Metadata1[MetadataKey2]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Id, Is.EqualTo(Id1));
		Assert.That(result[0][0].Embedding, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.GreaterThan(0));
		Assert.That(result[1][0].Id, Is.EqualTo(Id1));
		Assert.That(result[1][0].Embedding, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereNotIn()
	{
		var client = await Init();
		var result = await client.QueryAsync([Embeddings1, Embeddings2],
			where: ChromaWhereOperator.NotIn(MetadataKey2, Metadata2[MetadataKey2]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Id, Is.EqualTo(Id1));
		Assert.That(result[0][0].Embedding, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.GreaterThan(0));
		Assert.That(result[1][0].Id, Is.EqualTo(Id1));
		Assert.That(result[1][0].Embedding, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereGreaterThan()
	{
		var client = await Init();
		var result = await client.QueryAsync([Embeddings1, Embeddings2],
			where: ChromaWhereOperator.GreaterThan(MetadataKey2, Metadata1[MetadataKey2]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.GreaterThan(0));
		Assert.That(result[0][0].Id, Is.EqualTo(Id2));
		Assert.That(result[0][0].Embedding, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[1][0].Id, Is.EqualTo(Id2));
		Assert.That(result[1][0].Embedding, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereLessThan()
	{
		var client = await Init();
		var result = await client.QueryAsync([Embeddings1, Embeddings2],
			where: ChromaWhereOperator.LessThan(MetadataKey2, Metadata2[MetadataKey2]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Id, Is.EqualTo(Id1));
		Assert.That(result[0][0].Embedding, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.GreaterThan(0));
		Assert.That(result[1][0].Id, Is.EqualTo(Id1));
		Assert.That(result[1][0].Embedding, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereGreaterThanOrEqual()
	{
		var client = await Init();
		var result = await client.QueryAsync([Embeddings1, Embeddings2],
			where: ChromaWhereOperator.GreaterThanOrEqual(MetadataKey2, Metadata2[MetadataKey2]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.GreaterThan(0));
		Assert.That(result[0][0].Id, Is.EqualTo(Id2));
		Assert.That(result[0][0].Embedding, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[1][0].Id, Is.EqualTo(Id2));
		Assert.That(result[1][0].Embedding, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereLessThanOrEqual()
	{
		var client = await Init();
		var result = await client.QueryAsync([Embeddings1, Embeddings2],
			where: ChromaWhereOperator.LessThanOrEqual(MetadataKey2, Metadata1[MetadataKey2]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Id, Is.EqualTo(Id1));
		Assert.That(result[0][0].Embedding, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.GreaterThan(0));
		Assert.That(result[1][0].Id, Is.EqualTo(Id1));
		Assert.That(result[1][0].Embedding, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereAndOr()
	{
		var client = await Init();
		var result = await client.QueryAsync([Embeddings1, Embeddings2],
			where: ChromaWhereOperator.Equal(MetadataKey2, Metadata1[MetadataKey2]) && ChromaWhereOperator.NotEqual(MetadataKey2, Metadata1[MetadataKey2]) || ChromaWhereOperator.NotEqual(MetadataKey2, Metadata2[MetadataKey2]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Id, Is.EqualTo(Id1));
		Assert.That(result[0][0].Embedding, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.GreaterThan(0));
		Assert.That(result[1][0].Id, Is.EqualTo(Id1));
		Assert.That(result[1][0].Embedding, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereDocumentContains()
	{
		var client = await Init();
		var result = await client.QueryAsync([Embeddings1, Embeddings2],
			whereDocument: ChromaWhereDocumentOperator.Contains(Doc1[^3..]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Id, Is.EqualTo(Id1));
		Assert.That(result[0][0].Embedding, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.GreaterThan(0));
		Assert.That(result[1][0].Id, Is.EqualTo(Id1));
		Assert.That(result[1][0].Embedding, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereDocumentNotContains()
	{
		Assume.That(CountCollectionsAndNotContainsSupported, Is.True, "Chroma 0.4.15 has no count_collections, no $not_contains filter and no tenant and database in the collections.");
		var client = await Init();
		var result = await client.QueryAsync([Embeddings1, Embeddings2],
			whereDocument: ChromaWhereDocumentOperator.NotContains(Doc2[^3..]),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Id, Is.EqualTo(Id1));
		Assert.That(result[0][0].Embedding, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.GreaterThan(0));
		Assert.That(result[1][0].Id, Is.EqualTo(Id1));
		Assert.That(result[1][0].Embedding, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereDocumentAndOr()
	{
		Assume.That(CountCollectionsAndNotContainsSupported, Is.True, "Chroma 0.4.15 has no count_collections, no $not_contains filter and no tenant and database in the collections.");
		var client = await Init();
		var result = await client.QueryAsync([Embeddings1, Embeddings2],
			whereDocument: ChromaWhereDocumentOperator.Contains(Doc1) && ChromaWhereDocumentOperator.NotContains(Doc1) || ChromaWhereDocumentOperator.NotContains(Doc2),
			include: ChromaQueryInclude.Distances);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0], Has.Count.EqualTo(1));
		Assert.That(result[0][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
		Assert.That(result[0][0].Id, Is.EqualTo(Id1));
		Assert.That(result[0][0].Embedding, Is.Null);
		Assert.That(result[0][0].Metadata, Is.Null);
		Assert.That(result[0][0].Document, Is.Null);
		Assert.That(result[1], Has.Count.EqualTo(1));
		Assert.That(result[1][0].Distance, Is.GreaterThan(0));
		Assert.That(result[1][0].Id, Is.EqualTo(Id1));
		Assert.That(result[1][0].Embedding, Is.Null);
		Assert.That(result[1][0].Metadata, Is.Null);
		Assert.That(result[1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithIds()
	{
		var client = await Init(withThird: true);
		var query = new ChromaQuery([Embeddings1, Embeddings2]) { Ids = [Id2, Id3], Include = ChromaQueryInclude.Distances };
		if (!IdsInQuerySupported)
		{
			// The server searches all the records, and Id1 is the nearest to Embeddings1.
			await Assert.ThatAsync(() => client.QueryAsync(query), Throws.InstanceOf<ChromaException>().With.Message.Contains("outside the ids"));
			return;
		}
		var result = await client.QueryAsync(query);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0].Select(x => x.Id), Is.EquivalentTo(new[] { Id2, Id3 }));
		Assert.That(result[1].Select(x => x.Id), Is.EquivalentTo(new[] { Id2, Id3 }));
		Assert.That(result[1][0].Id, Is.EqualTo(Id2));
		Assert.That(result[1][0].Distance, Is.EqualTo(0).Within(DistanceTolerance));
	}

	[Test]
	public async Task QueryWithOffset()
	{
		var client = await Init(withThird: true);
		var all = await client.QueryAsync(new ChromaQuery([Embeddings1, Embeddings2]) { NResults = 3, Include = ChromaQueryInclude.Distances });
		var skipped = await client.QueryAsync(new ChromaQuery([Embeddings1, Embeddings2]) { NResults = 2, Offset = 1, Include = ChromaQueryInclude.Distances });
		Assert.That(skipped.Select(entries => entries.Select(x => x.Id)), Is.EqualTo(all.Select(entries => entries.Skip(1).Select(x => x.Id))));
	}

	[Test]
	public async Task QueryWithIdsNResults1()
	{
		Assume.That(IdsInQuerySupported, Is.True, "Chroma 0.6.3 and earlier ignore the ids of a query.");
		var client = await Init(withThird: true);
		var result = await client.QueryAsync(new ChromaQuery([Embeddings1]) { Ids = [Id2, Id3], NResults = 1 });
		Assert.That(result.Single().Select(x => x.Id), Is.EqualTo(new[] { Id3 }));
	}

	[Test]
	public async Task QueryWithIdsAndWhere()
	{
		Assume.That(IdsInQuerySupported, Is.True, "Chroma 0.6.3 and earlier ignore the ids of a query.");
		var client = await Init(withThird: true);
		var result = await client.QueryAsync(new ChromaQuery([Embeddings1]) { Ids = [Id1, Id3], Where = ChromaWhereOperator.Equal(MetadataKey2, Metadata2[MetadataKey2]) });
		Assert.That(result.Single().Select(x => x.Id), Is.EqualTo(new[] { Id3 }));
	}

	[Test]
	public async Task QueryWithEmptyIds()
	{
		var client = await Init(withThird: true);
		var query = new ChromaQuery([Embeddings1]) { Ids = [] };
		if (!IdsInQuerySupported)
		{
			await Assert.ThatAsync(() => client.QueryAsync(query), Throws.InstanceOf<ChromaException>().With.Message.Contains("outside the ids"));
			return;
		}
		Assert.That((await client.QueryAsync(query)).Single(), Is.Empty);
	}

	// Chroma 1.x answers 500 "Error finding id" when an id of the query does not exist, and Chroma Cloud leaves it out: the client
	// leaves it out on both. Chroma 0.x ignores the ids.
	[Test]
	public async Task QueryWithMissingIdLeavesItOut()
	{
		var client = await Init(withThird: true);
		var query = new ChromaQuery([Embeddings1, Embeddings2]) { Ids = [Id1, "missing", Id3], Include = ChromaQueryInclude.Distances };
		if (!IdsInQuerySupported)
		{
			await Assert.ThatAsync(() => client.QueryAsync(query), Throws.InstanceOf<ChromaException>().With.Message.Contains("outside the ids"));
			return;
		}
		var result = await client.QueryAsync(query);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0].Select(x => x.Id), Is.EqualTo(new[] { Id1, Id3 }));
		Assert.That(result[1].Select(x => x.Id), Is.EquivalentTo(new[] { Id1, Id3 }));
	}

	[Test]
	public async Task QueryWithDeletedIdLeavesItOut()
	{
		Assume.That(IdsInQuerySupported, Is.True, "Chroma 0.6.3 and earlier ignore the ids of a query.");
		var client = await Init(withThird: true);
		await client.DeleteAsync([Id1]);
		var result = await client.QueryAsync(new ChromaQuery([Embeddings1]) { Ids = [Id1, Id2] });
		Assert.That(result.Single().Select(x => x.Id), Is.EqualTo(new[] { Id2 }));
	}

	[Test]
	public async Task QueryWithOnlyMissingIdsHasNoResults()
	{
		Assume.That(IdsInQuerySupported, Is.True, "Chroma 0.6.3 and earlier ignore the ids of a query.");
		var client = await Init(withThird: true);
		var result = await client.QueryAsync(new ChromaQuery([Embeddings1, Embeddings2]) { Ids = ["missing", "other"] });
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result, Has.All.Empty);
	}

	// Chroma 1.x sends null for an embedding beyond the range of a float in a cosine collection, and for the distance to it in an l2
	// collection: the client reads them, and the other records come back as they are.
	[Test]
	public async Task FloatsBeyondTheRange()
	{
		var chroma = new ChromaClient(BaseConfigurationOptions, HttpClient);
		foreach (var space in new[] { ChromaSpace.Cosine, ChromaSpace.L2 })
		{
			var collection = chroma.GetCollectionClient(await chroma.CreateCollectionAsync(new ChromaCollectionDefinition($"collection{Random.Shared.Next()}") { Configuration = new() { Space = space } }));
			await collection.AddAsync(["ok", "big"], embeddings: [new([3f, 4f]), new([float.MaxValue, float.MaxValue])]);
			var records = (await collection.GetAsync(include: ChromaGetInclude.Embeddings)).ToDictionary(x => x.Id, x => x.Embedding!.Value.ToArray());
			Assert.That(records["ok"], Is.EqualTo(new[] { 3f, 4f }), space.ToString());
			var found = (await collection.QueryAsync([new([3f, 4f])], include: ChromaQueryInclude.Distances | ChromaQueryInclude.Embeddings)).Single();
			Assert.That((found[0].Id, found[0].Distance), Is.EqualTo(("ok", 0f)), space.ToString());
			Assert.That(found.Select(x => x.Id), Is.EquivalentTo(new[] { "ok", "big" }), space.ToString());
		}
	}

	static readonly string Id1 = "id1";
	static readonly string Id2 = "id2";
	static readonly string Id3 = "id3";
	static readonly ReadOnlyMemory<float> Embeddings1 = new([1, 2, 3]);
	static readonly ReadOnlyMemory<float> Embeddings2 = new([1.4f, 1.5f, 99.33f]);
	// Nearer to Embeddings1 than Embeddings2 is, farther than Embeddings1 itself.
	static readonly ReadOnlyMemory<float> Embeddings3 = new([1, 2, 10]);
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
	// Their last three characters tell them apart: on Chroma Cloud a $contains of fewer characters throws (KD-57).
	static readonly string Doc1 = "Doc1";
	static readonly string Doc2 = "Doc2";

	async Task<ChromaCollectionClient> Init(bool withThird = false)
	{
		var name = $"collection{Random.Shared.Next()}";
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var collection = await client.CreateCollectionAsync(name);
		var collectionClient = new ChromaCollectionClient(collection, BaseConfigurationOptions, HttpClient);
		await collectionClient.AddAsync([Id1, Id2],
			embeddings: [Embeddings1, Embeddings2],
			metadatas: [Metadata1, Metadata2],
			documents: [Doc1, Doc2]);
		if (withThird)
		{
			await collectionClient.AddAsync([Id3], embeddings: [Embeddings3], metadatas: [Metadata2], documents: [Doc2]);
		}
		return collectionClient;
	}
}
