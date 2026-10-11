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
		var result = await client.GetAsync(Id1,
			include: ChromaGetInclude.None);
		Assert.That(result, Is.Not.Null);
		Assert.That(result.Id, Is.EqualTo(Id1));
		Assert.That(result.Embedding, Is.Null);
		Assert.That(result.Metadata, Is.Null);
		Assert.That(result.Document, Is.Null);
	}

	[Test]
	public async Task GetSingleIdIncludeEmbeddings()
	{
		var client = await Init();
		var result = await client.GetAsync(Id1,
			include: ChromaGetInclude.Embeddings);
		Assert.That(result, Is.Not.Null);
		Assert.That(result.Id, Is.EqualTo(Id1));
		Assert.That(result.Embedding, Is.EqualTo(Embeddings1).Using(EmbeddingsComparer.Instance));
		Assert.That(result.Metadata, Is.Null);
		Assert.That(result.Document, Is.Null);
	}

	[Test]
	public async Task GetSingleIdIncludeMetadatas()
	{
		var client = await Init();
		var result = await client.GetAsync(Id1,
			include: ChromaGetInclude.Metadatas);
		Assert.That(result, Is.Not.Null);
		Assert.That(result.Id, Is.EqualTo(Id1));
		Assert.That(result.Embedding, Is.Null);
		Assert.That(result.Metadata, Is.EqualTo(Metadata1));
		Assert.That(result.Document, Is.Null);
	}

	[Test]
	public async Task GetSingleIdIncludeDocuments()
	{
		var client = await Init();
		var result = await client.GetAsync(Id1,
			include: ChromaGetInclude.Documents);
		Assert.That(result, Is.Not.Null);
		Assert.That(result.Id, Is.EqualTo(Id1));
		Assert.That(result.Embedding, Is.Null);
		Assert.That(result.Metadata, Is.Null);
		Assert.That(result.Document, Is.EqualTo(Doc1));
	}

	[Test]
	public async Task GetSingleIdIncludeAll()
	{
		var client = await Init();
		var result = await client.GetAsync(Id1,
			include: ChromaGetInclude.Embeddings | ChromaGetInclude.Metadatas | ChromaGetInclude.Documents);
		Assert.That(result, Is.Not.Null);
		Assert.That(result.Id, Is.EqualTo(Id1));
		Assert.That(result.Embedding, Is.EqualTo(Embeddings1).Using(EmbeddingsComparer.Instance));
		Assert.That(result.Metadata, Is.EqualTo(Metadata1));
		Assert.That(result.Document, Is.EqualTo(Doc1));
	}

	[Test]
	public async Task GetMultipleIdsIncludeNothing()
	{
		var client = await Init();
		var result = await client.GetAsync(
			ids: [Id1, Id2],
			include: ChromaGetInclude.None);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0].Id, Is.EqualTo(Id1));
		Assert.That(result[0].Embedding, Is.Null);
		Assert.That(result[0].Metadata, Is.Null);
		Assert.That(result[0].Document, Is.Null);
		Assert.That(result[1].Id, Is.EqualTo(Id2));
		Assert.That(result[1].Embedding, Is.Null);
		Assert.That(result[1].Metadata, Is.Null);
		Assert.That(result[1].Document, Is.Null);
	}

	[Test]
	public async Task GetMultipleIdsIncludeEmbeddings()
	{
		var client = await Init();
		var result = await client.GetAsync(
			ids: [Id1, Id2],
			include: ChromaGetInclude.Embeddings);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0].Id, Is.EqualTo(Id1));
		Assert.That(result[0].Embedding, Is.EqualTo(Embeddings1).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.Null);
		Assert.That(result[0].Document, Is.Null);
		Assert.That(result[1].Id, Is.EqualTo(Id2));
		Assert.That(result[1].Embedding, Is.EqualTo(Embeddings2).Using(EmbeddingsComparer.Instance));
		Assert.That(result[1].Metadata, Is.Null);
		Assert.That(result[1].Document, Is.Null);
	}

	[Test]
	public async Task GetMultipleIdsIncludeMetadatas()
	{
		var client = await Init();
		var result = await client.GetAsync(
			ids: [Id1, Id2],
			include: ChromaGetInclude.Metadatas);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0].Id, Is.EqualTo(Id1));
		Assert.That(result[0].Embedding, Is.Null);
		Assert.That(result[0].Metadata, Is.EqualTo(Metadata1));
		Assert.That(result[0].Document, Is.Null);
		Assert.That(result[1].Id, Is.EqualTo(Id2));
		Assert.That(result[1].Embedding, Is.Null);
		Assert.That(result[1].Metadata, Is.EqualTo(Metadata2));
		Assert.That(result[1].Document, Is.Null);
	}

	[Test]
	public async Task GetMultipleIdsIncludeDocuments()
	{
		var client = await Init();
		var result = await client.GetAsync(
			ids: [Id1, Id2],
			include: ChromaGetInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0].Id, Is.EqualTo(Id1));
		Assert.That(result[0].Embedding, Is.Null);
		Assert.That(result[0].Metadata, Is.Null);
		Assert.That(result[0].Document, Is.EqualTo(Doc1));
		Assert.That(result[1].Id, Is.EqualTo(Id2));
		Assert.That(result[1].Embedding, Is.Null);
		Assert.That(result[1].Metadata, Is.Null);
		Assert.That(result[1].Document, Is.EqualTo(Doc2));
	}

	[Test]
	public async Task GetMultipleIdsIncludeAll()
	{
		var client = await Init();
		var result = await client.GetAsync(
			ids: [Id1, Id2],
			include: ChromaGetInclude.Embeddings | ChromaGetInclude.Metadatas | ChromaGetInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result[0].Id, Is.EqualTo(Id1));
		Assert.That(result[0].Embedding, Is.EqualTo(Embeddings1).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.EqualTo(Metadata1));
		Assert.That(result[0].Document, Is.EqualTo(Doc1));
		Assert.That(result[1].Id, Is.EqualTo(Id2));
		Assert.That(result[1].Embedding, Is.EqualTo(Embeddings2).Using(EmbeddingsComparer.Instance));
		Assert.That(result[1].Metadata, Is.EqualTo(Metadata2));
		Assert.That(result[1].Document, Is.EqualTo(Doc2));
	}

	[Test]
	public async Task GetLimitIncludeAll()
	{
		var client = await Init();
		var result = await client.GetAsync(
			ids: [Id1, Id2],
			include: ChromaGetInclude.Embeddings | ChromaGetInclude.Metadatas | ChromaGetInclude.Documents,
			limit: 1);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Id, Is.EqualTo(Id1));
		Assert.That(result[0].Embedding, Is.EqualTo(Embeddings1).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.EqualTo(Metadata1));
		Assert.That(result[0].Document, Is.EqualTo(Doc1));
	}

	[Test]
	public async Task GetLimitOffsetIncludeAll()
	{
		var client = await Init();
		var result = await client.GetAsync(
			ids: [Id1, Id2],
			include: ChromaGetInclude.Embeddings | ChromaGetInclude.Metadatas | ChromaGetInclude.Documents,
			limit: 1,
			offset: 1);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Id, Is.EqualTo(Id2));
		Assert.That(result[0].Embedding, Is.EqualTo(Embeddings2).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.EqualTo(Metadata2));
		Assert.That(result[0].Document, Is.EqualTo(Doc2));
	}

	[Test]
	public async Task GetWhereEqualIncludeAll()
	{
		var client = await Init();
		var result = await client.GetAsync(
			where: ChromaWhereOperator.Equal(MetadataKey2, Metadata2[MetadataKey2]),
			include: ChromaGetInclude.Embeddings | ChromaGetInclude.Metadatas | ChromaGetInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Id, Is.EqualTo(Id2));
		Assert.That(result[0].Embedding, Is.EqualTo(Embeddings2).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.EqualTo(Metadata2));
		Assert.That(result[0].Document, Is.EqualTo(Doc2));
	}

	[Test]
	public async Task GetWhereNotEqualIncludeAll()
	{
		var client = await Init();
		var result = await client.GetAsync(
			where: ChromaWhereOperator.NotEqual(MetadataKey2, Metadata1[MetadataKey2]),
			include: ChromaGetInclude.Embeddings | ChromaGetInclude.Metadatas | ChromaGetInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Id, Is.EqualTo(Id2));
		Assert.That(result[0].Embedding, Is.EqualTo(Embeddings2).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.EqualTo(Metadata2));
		Assert.That(result[0].Document, Is.EqualTo(Doc2));
	}

	[Test]
	public async Task GetWhereInIncludeAll()
	{
		var client = await Init();
		var result = await client.GetAsync(
			where: ChromaWhereOperator.In(MetadataKey2, Metadata2[MetadataKey2]),
			include: ChromaGetInclude.Embeddings | ChromaGetInclude.Metadatas | ChromaGetInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Id, Is.EqualTo(Id2));
		Assert.That(result[0].Embedding, Is.EqualTo(Embeddings2).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.EqualTo(Metadata2));
		Assert.That(result[0].Document, Is.EqualTo(Doc2));
	}

	[Test]
	public async Task GetWhereNotInIncludeAll()
	{
		var client = await Init();
		var result = await client.GetAsync(
			where: ChromaWhereOperator.NotIn(MetadataKey2, Metadata1[MetadataKey2]),
			include: ChromaGetInclude.Embeddings | ChromaGetInclude.Metadatas | ChromaGetInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Id, Is.EqualTo(Id2));
		Assert.That(result[0].Embedding, Is.EqualTo(Embeddings2).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.EqualTo(Metadata2));
		Assert.That(result[0].Document, Is.EqualTo(Doc2));
	}

	[Test]
	public async Task GetWhereGreaterThanIncludeAll()
	{
		var client = await Init();
		var result = await client.GetAsync(
			where: ChromaWhereOperator.GreaterThan(MetadataKey2, Metadata1[MetadataKey2]),
			include: ChromaGetInclude.Embeddings | ChromaGetInclude.Metadatas | ChromaGetInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Id, Is.EqualTo(Id2));
		Assert.That(result[0].Embedding, Is.EqualTo(Embeddings2).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.EqualTo(Metadata2));
		Assert.That(result[0].Document, Is.EqualTo(Doc2));
	}

	[Test]
	public async Task GetWhereLessThanIncludeAll()
	{
		var client = await Init();
		var result = await client.GetAsync(
			where: ChromaWhereOperator.LessThan(MetadataKey2, Metadata2[MetadataKey2]),
			include: ChromaGetInclude.Embeddings | ChromaGetInclude.Metadatas | ChromaGetInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Id, Is.EqualTo(Id1));
		Assert.That(result[0].Embedding, Is.EqualTo(Embeddings1).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.EqualTo(Metadata1));
		Assert.That(result[0].Document, Is.EqualTo(Doc1));
	}

	[Test]
	public async Task GetWhereGreaterThanOrEqualIncludeAll()
	{
		var client = await Init();
		var result = await client.GetAsync(
			where: ChromaWhereOperator.GreaterThanOrEqual(MetadataKey2, Metadata2[MetadataKey2]),
			include: ChromaGetInclude.Embeddings | ChromaGetInclude.Metadatas | ChromaGetInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Id, Is.EqualTo(Id2));
		Assert.That(result[0].Embedding, Is.EqualTo(Embeddings2).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.EqualTo(Metadata2));
		Assert.That(result[0].Document, Is.EqualTo(Doc2));
	}

	[Test]
	public async Task GetWhereLessThanOrEqualIncludeAll()
	{
		var client = await Init();
		var result = await client.GetAsync(
			where: ChromaWhereOperator.LessThanOrEqual(MetadataKey2, Metadata1[MetadataKey2]),
			include: ChromaGetInclude.Embeddings | ChromaGetInclude.Metadatas | ChromaGetInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Id, Is.EqualTo(Id1));
		Assert.That(result[0].Embedding, Is.EqualTo(Embeddings1).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.EqualTo(Metadata1));
		Assert.That(result[0].Document, Is.EqualTo(Doc1));
	}

	[Test]
	public async Task GetWhereAndIncludeAll()
	{
		var client = await Init();
		var result = await client.GetAsync(
			where: ChromaWhereOperator.Equal(MetadataKey2, Metadata2[MetadataKey2]) && ChromaWhereOperator.NotEqual(MetadataKey2, Metadata1[MetadataKey2]),
			include: ChromaGetInclude.Embeddings | ChromaGetInclude.Metadatas | ChromaGetInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Id, Is.EqualTo(Id2));
		Assert.That(result[0].Embedding, Is.EqualTo(Embeddings2).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.EqualTo(Metadata2));
		Assert.That(result[0].Document, Is.EqualTo(Doc2));
	}

	[Test]
	public async Task GetWhereOrIncludeAll()
	{
		var client = await Init();
		var result = await client.GetAsync(
			where: ChromaWhereOperator.Equal(MetadataKey2, Metadata2[MetadataKey2]) || ChromaWhereOperator.NotEqual(MetadataKey2, Metadata1[MetadataKey2]),
			include: ChromaGetInclude.Embeddings | ChromaGetInclude.Metadatas | ChromaGetInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Id, Is.EqualTo(Id2));
		Assert.That(result[0].Embedding, Is.EqualTo(Embeddings2).Using(EmbeddingsComparer.Instance));
		Assert.That(result[0].Metadata, Is.EqualTo(Metadata2));
		Assert.That(result[0].Document, Is.EqualTo(Doc2));
	}

	[Test]
	public async Task GetWhereDocumentContainsIncludeDocuments()
	{
		var client = await Init();
		var result = await client.GetAsync(
			whereDocument: ChromaWhereDocumentOperator.Contains(Doc1[^1]),
			include: ChromaGetInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Id, Is.EqualTo(Id1));
		Assert.That(result[0].Embedding, Is.Null);
		Assert.That(result[0].Metadata, Is.Null);
		Assert.That(result[0].Document, Is.EqualTo(Doc1));
	}

	[Test]
	public async Task GetWhereDocumentNotContainsIncludeDocuments()
	{
		Assume.That(CountCollectionsAndNotContainsSupported, Is.True, "Chroma 0.4.15 has no count_collections, no $not_contains filter and no tenant and database in the collections.");
		var client = await Init();
		var result = await client.GetAsync(
			whereDocument: ChromaWhereDocumentOperator.NotContains(Doc2[^1]),
			include: ChromaGetInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Id, Is.EqualTo(Id1));
		Assert.That(result[0].Embedding, Is.Null);
		Assert.That(result[0].Metadata, Is.Null);
		Assert.That(result[0].Document, Is.EqualTo(Doc1));
	}

	[Test]
	public async Task GetWhereDocumentAndOrIncludeDocuments()
	{
		Assume.That(CountCollectionsAndNotContainsSupported, Is.True, "Chroma 0.4.15 has no count_collections, no $not_contains filter and no tenant and database in the collections.");
		var client = await Init();
		var result = await client.GetAsync(
			whereDocument: ChromaWhereDocumentOperator.Contains(Doc1) && ChromaWhereDocumentOperator.NotContains(Doc1) || ChromaWhereDocumentOperator.NotContains(Doc2),
			include: ChromaGetInclude.Documents);
		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Id, Is.EqualTo(Id1));
		Assert.That(result[0].Embedding, Is.Null);
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

	// Not pushes the negation into the operators: $ne and $nin match the records without the key, from Chroma 0.5.15, and $lte does
	// not. All matches every record, and None none.
	[Test]
	public async Task NotAllAndNone()
	{
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var collection = new ChromaCollectionClient(await client.CreateCollectionAsync($"collection{Random.Shared.Next()}"), BaseConfigurationOptions, HttpClient);
		await collection.AddAsync(["a", "b", "c", "d"],
			embeddings: [new([1f, 0f]), new([0f, 1f]), new([1f, 1f]), new([0.5f, 0.5f])],
			metadatas: [new Dictionary<string, object> { ["k"] = 1 }, new Dictionary<string, object> { ["k"] = 5 }, new Dictionary<string, object> { ["other"] = 1 }, new Dictionary<string, object> { ["k"] = 3 }]);
		async Task<IEnumerable<string>> Ids(ChromaWhereOperator where) => (await collection.GetAsync(where: where, include: ChromaGetInclude.None)).Select(x => x.Id).Order();
		var withoutTheKey = NotEqualMatchesRecordsWithoutTheKey ? new[] { "c" } : [];
		Assert.That(await Ids(ChromaWhereOperator.Not(ChromaWhereOperator.Equal("k", 1))), Is.EqualTo(withoutTheKey.Concat(["b", "d"]).Order()));
		Assert.That(await Ids(ChromaWhereOperator.Not(ChromaWhereOperator.In("k", 1, 5))), Is.EqualTo(withoutTheKey.Concat(["d"]).Order()));
		Assert.That(await Ids(ChromaWhereOperator.Not(ChromaWhereOperator.GreaterThan("k", 2))), Is.EqualTo(new[] { "a" }));
		Assert.That(await Ids(ChromaWhereOperator.Not(ChromaWhereOperator.Equal("k", 1) | ChromaWhereOperator.Equal("k", 5))), Is.EqualTo(withoutTheKey.Concat(["d"]).Order()));
		Assert.That(await Ids(ChromaWhereOperator.All), Is.EqualTo(new[] { "a", "b", "c", "d" }));
		Assert.That(await Ids(ChromaWhereOperator.None), Is.Empty);
		Assert.That(await Ids(ChromaWhereOperator.In("k")), Is.Empty);
		Assert.That(await Ids(ChromaWhereOperator.NotIn("k") & ChromaWhereOperator.Equal("k", 3)), Is.EqualTo(new[] { "d" }));
	}

	// A chain of 50 filters goes as one $or or $and list, which Chroma takes: nested, 32 were too deep for System.Text.Json.
	[Test]
	public async Task ChainsOfManyFilters()
	{
		Assume.That(ChromaCloud, Is.False, "Chroma Cloud takes at most 8 predicates in a filter, its default quota.");
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var collection = new ChromaCollectionClient(await client.CreateCollectionAsync($"collection{Random.Shared.Next()}"), BaseConfigurationOptions, HttpClient);
		var ids = Enumerable.Range(0, 100).Select(i => $"r{i}").ToList();
		await collection.AddAsync(ids,
			embeddings: ids.Select(_ => new ReadOnlyMemory<float>([1f, 0f])).ToList(),
			metadatas: Enumerable.Range(0, 100).Select(i => (IReadOnlyDictionary<string, object>)new Dictionary<string, object> { ["i"] = i }).ToList());
		var or = ChromaWhereOperator.Equal("i", 0);
		var and = ChromaWhereOperator.NotEqual("i", 0);
		for (var i = 1; i < 50; i++)
		{
			or |= ChromaWhereOperator.Equal("i", i);
			and &= ChromaWhereOperator.NotEqual("i", i);
		}
		Assert.That((await collection.GetAsync(where: or)).Select(x => x.Id).Order(), Is.EqualTo(ids.Take(50).Order()));
		Assert.That((await collection.GetAsync(where: and)).Select(x => x.Id).Order(), Is.EqualTo(ids.Skip(50).Order()));

		// The client splits a long list: in one list, Chroma 1.x answers 500 from 988 filters, and Chroma 1.5.9 crashes on a stack
		// overflow from about 4,400. Split, 4,000 filters work, and 4,500 leave the server up: Chroma 1.0.0 answers "too many SQL
		// variables" from about 4,090. Chroma 0.6.3 rejects more than 488 filters however they go.
		if (IsChroma1)
		{
			ChromaWhereOperator Chain(int count)
			{
				var chain = ChromaWhereOperator.NotEqual("i", 0);
				for (var i = 1; i < count; i++)
				{
					chain &= ChromaWhereOperator.NotEqual("i", i < 50 ? i : 1000 + i);
				}
				return chain;
			}
			Assert.That((await collection.GetAsync(where: Chain(4_000))).Select(x => x.Id).Order(), Is.EqualTo(ids.Skip(50).Order()));
			try
			{
				await collection.GetAsync(where: Chain(4_500));
			}
			catch (ChromaException ex) when (ex.Message.Contains("too many SQL variables"))
			{
			}
			await client.HeartbeatAsync();
		}
	}

	// A limit beyond the ids goes as the number of the ids: a limit of 1000 is over the quota of 300 of Chroma Cloud, which rejects it.
	[Test]
	public async Task GetIdsWithLimitBeyondTheQuota()
	{
		var client = await Init();
		var result = await client.GetAsync([Id1, Id2], limit: 1000);
		Assert.That(result.Select(x => x.Id), Is.EquivalentTo(new[] { Id1, Id2 }));
	}

	// A date in a filter finds the record that stores the same date: the client sends both as System.Text.Json writes them.
	[Test]
	public async Task GetWhereDate()
	{
		var client = await Init();
		var date = new DateTime(2026, 10, 7, 10, 0, 0);
		var offset = new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.FromHours(2));
		await client.UpdateAsync([Id1], metadatas: [new Dictionary<string, object> { ["date"] = date, ["offset"] = offset }]);
		Assert.That((await client.GetAsync(where: ChromaWhereOperator.Equal("date", date))).Select(x => x.Id), Is.EqualTo(new[] { Id1 }));
		Assert.That((await client.GetAsync(where: ChromaWhereOperator.In("offset", offset))).Select(x => x.Id), Is.EqualTo(new[] { Id1 }));
		Assert.That((await client.GetAsync(where: ChromaWhereOperator.NotEqual("date", date))).Select(x => x.Id), Is.EqualTo(new[] { Id2 }).Or.Empty);
	}

	// A client made by name reads the collection before its first request: a get with a limit of 0, which sends no get, says that the
	// collection does not exist as the other reads do.
	[Test]
	public void GetWithALimitOf0ByNameOnACollectionThatDoesNotExist()
	{
		var collection = new ChromaClient(BaseConfigurationOptions, HttpClient).GetCollectionClient($"missing{Random.Shared.Next()}");
		var error = Assert.ThrowsAsync<ChromaException>(() => collection.GetAsync(limit: 0));
		Assert.That(error?.Message, Is.EqualTo(Assert.ThrowsAsync<ChromaException>(() => collection.GetAsync())!.Message));
	}

	async Task<ChromaCollectionClient> Init()
	{
		var name = $"collection{Random.Shared.Next()}";
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var collection = await client.CreateCollectionAsync(name);
		var collectionClient = new ChromaCollectionClient(collection, BaseConfigurationOptions, HttpClient);
		await collectionClient.AddAsync([Id1, Id2],
			embeddings: [Embeddings1, Embeddings2],
			metadatas: [Metadata1, Metadata2],
			documents: [Doc1, Doc2]);
		return collectionClient;
	}
}
