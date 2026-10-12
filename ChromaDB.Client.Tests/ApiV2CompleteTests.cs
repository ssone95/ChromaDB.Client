using System.Net;
using ChromaDB.Client.Models;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

// The operations added to cover the v2 API, against each version of Chroma, and against Chroma Cloud with CHROMA_TEST_URI.
[TestFixture]
public class ApiV2CompleteTests : ChromaTestsBase
{
	[Test]
	public async Task Healthcheck()
	{
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		if (!HealthcheckSupported)
		{
			await Assert.ThatAsync(() => client.HealthcheckAsync(), Throws.InstanceOf<ChromaException>().With.Property(nameof(ChromaException.StatusCode)).EqualTo(HttpStatusCode.NotFound));
			return;
		}
		var result = await client.HealthcheckAsync();
		Assert.That(result.IsExecutorReady, Is.True);
	}

	[Test]
	public async Task RegexAndNotRegex()
	{
		var collection = await Init();
		if (!RegexSupported)
		{
			await Assert.ThatAsync(() => collection.GetAsync(whereDocument: ChromaWhereDocumentOperator.Regex("^apple")), Throws.InstanceOf<ChromaException>());
			return;
		}
		Assert.That((await collection.GetAsync(whereDocument: ChromaWhereDocumentOperator.Regex("^apple"))).Select(x => x.Id), Is.EquivalentTo(new[] { "a", "d" }));
		Assert.That((await collection.GetAsync(whereDocument: ChromaWhereDocumentOperator.NotRegex("^apple"))).Select(x => x.Id), Is.EquivalentTo(new[] { "b", "c" }));
	}

	// On the servers that ignore the limit, nothing is deleted: without the check they would delete both apples.
	[Test]
	public async Task DeleteWithLimit()
	{
		var collection = await Init();
		var delete = new ChromaDelete { WhereDocument = ChromaWhereDocumentOperator.Contains("apple"), Limit = 1 };
		if (!DeleteLimitSupported)
		{
			await Assert.ThatAsync(() => collection.DeleteAsync(delete), Throws.InstanceOf<ChromaException>().With.Message.Contains("Chroma 1.5.3"));
			Assert.That(await collection.CountAsync(), Is.EqualTo(4));
			return;
		}
		Assert.That(await collection.DeleteAsync(delete), Is.EqualTo(1));
		Assert.That(await collection.CountAsync(), Is.EqualTo(3));
	}

	[Test]
	public async Task DeleteByFilterOnly()
	{
		var collection = await Init();
		var deleted = await collection.DeleteAsync(new ChromaDelete { WhereDocument = ChromaWhereDocumentOperator.Contains("apple") });
		Assert.That(deleted, DeleteLimitSupported ? Is.EqualTo(2) : Is.Null);
		Assert.That((await collection.GetAsync()).Select(x => x.Id), Is.EquivalentTo(new[] { "b", "c" }));
	}

	// A single server indexes at once; Chroma Cloud may leave out of IndexOnly the records not indexed yet.
	[Test]
	public async Task CountAtAReadLevel()
	{
		var collection = await Init();
		Assert.That(await collection.CountAsync(ChromaReadLevel.IndexAndWal), Is.EqualTo(4));
		Assert.That(await collection.CountAsync(ChromaReadLevel.IndexOnly), ChromaCloud ? Is.InRange(0, 4) : Is.EqualTo(4));
	}

	// A single server has an HNSW index, Chroma Cloud a SPANN one.
	[Test]
	public async Task ModifyConfiguration()
	{
		var collection = await Init();
		var update = ChromaCloud
			? new ChromaCollectionConfigurationUpdate { Spann = new() { SearchNprobe = 32 } }
			: new ChromaCollectionConfigurationUpdate { Hnsw = new() { EfSearch = 200 } };
		if (!NewConfigurationApplied)
		{
			await Assert.ThatAsync(() => collection.ModifyConfigurationAsync(update), Throws.InstanceOf<ChromaException>().With.Message.Contains("Chroma 1.0.6"));
			return;
		}
		await collection.ModifyConfigurationAsync(update);
		var configuration = (await new ChromaClient(BaseConfigurationOptions, HttpClient).GetCollectionAsync(collection.Collection.Name)).ConfigurationJson!.Value;
		Assert.That(ChromaCloud
			? configuration.GetProperty("spann").GetProperty("search_nprobe").GetInt32()
			: configuration.GetProperty("hnsw").GetProperty("ef_search").GetInt32(), Is.EqualTo(ChromaCloud ? 32 : 200));
	}

	// Fork, its count and the indexing status exist only on Chroma Cloud: a single server answers with an error.
	[Test]
	public async Task ForkAndIndexingStatus()
	{
		var collection = await Init();
		if (!ChromaCloud)
		{
			await Assert.ThatAsync(() => collection.ForkAsync($"collection{Random.Shared.Next()}"), Throws.InstanceOf<ChromaException>());
			await Assert.ThatAsync(() => collection.ForkCountAsync(), Throws.InstanceOf<ChromaException>());
			await Assert.ThatAsync(() => collection.GetIndexingStatusAsync(), Throws.InstanceOf<ChromaException>());
			return;
		}
		var fork = await collection.ForkAsync($"collection{Random.Shared.Next()}");
		Assert.That(await new ChromaClient(BaseConfigurationOptions, HttpClient).GetCollectionClient(fork).CountAsync(), Is.EqualTo(4));
		Assert.That(await collection.ForkCountAsync(), Is.EqualTo(1));
		Assert.That((await collection.GetIndexingStatusAsync()).TotalOps, Is.GreaterThanOrEqualTo(4));
	}

	// On Chroma Cloud the tenant and the database of the API key; a single server, also one already running, always answers
	// with the default ones.
	[Test]
	public async Task TenantAndDatabaseFromIdentity()
	{
		Assume.That(ApiVersion, Is.EqualTo(ChromaApiVersion.V2), "The v1 API has no auth/identity.");
		var options = new ChromaConfigurationOptions(uri: BaseConfigurationOptions.Uri.ToString()).WithApiVersion(ApiVersion);
		options = BaseConfigurationOptions.ChromaToken is { } token ? options.WithChromaToken(token) : options;
		var client = await new ChromaClient(options, HttpClient).WithTenantAndDatabaseFromIdentityAsync();
		Assert.That(client.Options.Tenant, Is.EqualTo(ChromaCloud ? BaseConfigurationOptions.Tenant ?? "default_tenant" : "default_tenant"));
		Assert.That(client.Options.Database, Is.EqualTo(ChromaCloud ? BaseConfigurationOptions.Database ?? "default_database" : "default_database"));
		Assert.That(await client.ListCollectionsAsync(), Is.Not.Null);
	}

	// The Search API exists only on Chroma Cloud: a single server answers with an error (501 from Chroma 1.x).
	[Test]
	public async Task Search()
	{
		var collection = await Init();
		var search = new ChromaSearch
		{
			WhereDocument = ChromaWhereDocumentOperator.Contains("apple"),
			Rank = ChromaRank.Knn(new([1f, 0f])),
			Limit = 3,
			Select = [ChromaSearchKeys.Document, ChromaSearchKeys.Score],
		};
		if (!ChromaCloud)
		{
			await Assert.ThatAsync(() => collection.SearchAsync(search), Throws.InstanceOf<ChromaException>());
			return;
		}
		var apples = await collection.SearchAsync(search);
		Assert.That(apples.Select(x => (x.Id, x.Document, x.Score)), Is.EqualTo(new[] { ("a", "apple pie", (float?)0f), ("d", "apple juice", (float?)0.5f) }));
		var rrf = await collection.SearchAsync(new ChromaSearch
		{
			Rank = ChromaRank.Rrf([ChromaRank.Knn(new([1f, 0f]), returnRank: true), ChromaRank.Knn(new([0f, 1f]), returnRank: true)]),
			Limit = 4,
			Select = [ChromaSearchKeys.Score],
		});
		Assert.That(rrf.Select(x => x.Score), Is.Ordered.Ascending);
		Assert.That(rrf, Has.Count.EqualTo(4));
		var two = await collection.SearchAsync([new ChromaSearch { Ids = ["b", "c"], Select = [ChromaSearchKeys.Document] }, new ChromaSearch { Rank = ChromaRank.Knn(new([0f, 1f])), Limit = 1 }]);
		Assert.That(two[0].Select(x => x.Document), Is.EquivalentTo(new[] { "banana split", "cherry tart" }));
		Assert.That(two[1].Single().Id, Is.EqualTo("b"));
	}

	// Sparse vector indexes and sparse vectors in metadata exist only on Chroma Cloud. A single server rejects the schema from Chroma 1.3.0,
	// and the earlier ones create the collection without it: then the client deletes it. Sparse vectors in metadata fail on a single
	// server; Chroma 0.x would drop them, so the client rejects them first.
	[Test]
	public async Task SchemaAndSparseVectors()
	{
		var client = new ChromaClient(BaseConfigurationOptions.WithMetadataValues(ChromaMetadataValues.Exact), HttpClient);
		var name = $"collection{Random.Shared.Next()}";
		var definition = new ChromaCollectionDefinition(name)
		{
			Schema = new ChromaCollectionSchema().WithSparseVectorIndex("doc_bm25", ChromaSearchKeys.Document, bm25: true, ChromaEmbeddingFunctionReference.ChromaBm25()),
		};
		var records = new ChromaRecords(["a", "b"])
		{
			Embeddings = [new([1f, 0f]), new([0f, 1f])],
			Documents = ["apple pie", "banana split"],
			Metadatas = [new Dictionary<string, object> { ["doc_bm25"] = new ChromaSparseVector([1, 5], [0.5f, 0.7f]) }, new Dictionary<string, object> { ["doc_bm25"] = new ChromaSparseVector([2], [0.9f]) }],
		};
		if (!ChromaCloud)
		{
			await Assert.ThatAsync(() => client.CreateCollectionAsync(definition), Throws.InstanceOf<ChromaException>());
			Assert.That(await client.CollectionExistsAsync(name), Is.False);
			if (!RejectedSparseVectorsBreakTheServer)
			{
				var plain = client.GetCollectionClient(await client.CreateCollectionAsync($"collection{Random.Shared.Next()}"));
				await Assert.ThatAsync(() => plain.AddAsync(records), Throws.InstanceOf<ChromaException>());
			}
			return;
		}
		var collection = await client.CreateCollectionAsync(definition);
		var index = collection.SparseVectorIndexes.Single();
		Assert.That((index.Key, index.SourceKey, index.Bm25, index.EmbeddingFunction), Is.EqualTo(("doc_bm25", "#document", true, "chroma_bm25")));
		var collectionClient = client.GetCollectionClient(collection);
		await collectionClient.AddAsync(records);
		var stored = (ChromaSparseVector)(await collectionClient.GetAsync("a", include: ChromaGetInclude.Metadatas))!.Metadata!["doc_bm25"];
		Assert.That((stored.Indices, stored.Values), Is.EqualTo(((IReadOnlyList<uint>)[1, 5], (IReadOnlyList<float>)[0.5f, 0.7f])));
		var found = await collectionClient.SearchAsync(new ChromaSearch { Rank = ChromaRank.SparseKnn(new ChromaSparseVector([1, 5], [1f, 1f]), "doc_bm25"), Limit = 1 });
		Assert.That(found.Single().Id, Is.EqualTo("a"));
	}

	// The options of Chroma Cloud from a connection string, as the settings of an application keep them.
	[Test]
	public async Task ConnectionStringOfChromaCloud()
	{
		Assume.That(ChromaCloud, Is.True, "A connection string with a token, a tenant and a database is for Chroma Cloud.");
		var options = ChromaConfigurationOptions.FromConnectionString(
			$"Endpoint={BaseConfigurationOptions.Uri};Token={BaseConfigurationOptions.ChromaToken};Tenant={BaseConfigurationOptions.Tenant};Database={BaseConfigurationOptions.Database}");
		using var client = new ChromaClient(options);
		var name = $"collection{Random.Shared.Next()}";
		// Its own HttpClient, which the fixture does not see: the collection is deleted here.
		var collection = await client.CreateCollectionAsync(name);
		try
		{
			Assert.That((collection.Tenant, collection.Database), Is.EqualTo((BaseConfigurationOptions.Tenant, BaseConfigurationOptions.Database)));
			Assert.That(await client.CollectionExistsAsync(name), Is.True);
		}
		finally
		{
			await client.DeleteCollectionAsync(name);
		}
	}

	// Hybrid search on Chroma Cloud: the client computes the BM25 vectors of the documents and of the text queries from the schema,
	// searched alone and fused with the dense vectors.
	[Test]
	public async Task HybridSearchWithBm25()
	{
		Assume.That(ChromaCloud, Is.True, "Only Chroma Cloud has sparse vector indexes.");
		var bm25 = new ChromaBm25(k: 1.5);
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		// The space goes in the schema: Chroma Cloud rejects a configuration together with a schema.
		var collection = client.GetCollectionClient(await client.CreateCollectionAsync(new ChromaCollectionDefinition($"collection{Random.Shared.Next()}")
		{
			Configuration = new() { Space = ChromaSpace.Cosine },
			Schema = new ChromaCollectionSchema().WithSparseVectorIndex("doc_bm25", ChromaSearchKeys.Document, bm25: true, bm25.Reference),
		}));
		Assert.That(collection.Collection.Space, Is.EqualTo(ChromaSpace.Cosine));
		string[] documents = ["apple pie with cinnamon", "banana split with chocolate", "cherry tart", "apple juice and apple cider"];
		await collection.AddAsync(new ChromaRecords(["a", "b", "c", "d"])
		{
			Embeddings = [new([1f, 0f]), new([0f, 1f]), new([1f, 1f]), new([0.5f, 0.5f])],
			Documents = [.. documents],
		});
		var stored = (ChromaSparseVector)(await client.WithMetadataValues(ChromaMetadataValues.Exact).GetCollectionClient(collection.Collection).GetAsync("b"))!.Metadata!["doc_bm25"];
		Assert.That(stored.ToString(), Is.EqualTo(bm25.Embed(documents[1]).ToString()));
		var keyword = await collection.SearchAsync(new ChromaSearch { Rank = ChromaRank.SparseKnn("apples", "doc_bm25"), Limit = 2 });
		Assert.That(keyword.Select(x => x.Id), Is.EquivalentTo(new[] { "a", "d" }));
		var hybrid = await collection.SearchAsync(new ChromaSearch
		{
			Rank = ChromaRank.Rrf([ChromaRank.Knn(new([0f, 1f]), returnRank: true), ChromaRank.SparseKnn("banana", "doc_bm25", returnRank: true)]),
			Limit = 1,
		});
		Assert.That(hybrid.Single().Id, Is.EqualTo("b"));
	}

	// Chroma Cloud takes a metadata value of at most 8,182 bytes, and a document of 16,384: a longer document goes without its copy, and
	// its record, without metadata keys, comes back with an empty Metadata. A key of the schema beyond 36 bytes, which Chroma Cloud takes
	// and then rejects on every write, stops before the request.
	[Test]
	public async Task QuotasOfChromaCloud()
	{
		Assume.That(ChromaCloud, Is.True, "Only Chroma Cloud has these quotas.");
		var client = new ChromaClient(BaseConfigurationOptions.WithMetadataValues(ChromaMetadataValues.Exact), HttpClient);
		var name = $"collection{Random.Shared.Next()}";
		var tooLong = new ChromaCollectionDefinition(name) { Schema = new ChromaCollectionSchema().WithIndex(ChromaSchemaIndex.StringInverted, new string('k', 37)) };
		Assert.That(() => client.CreateCollectionAsync(tooLong), Throws.ArgumentException);
		Assert.That(await client.CollectionExistsAsync(name), Is.False);
		var collection = client.GetCollectionClient(await client.CreateCollectionAsync(name)).WithDocumentCopyKey("text");
		var longText = new string('a', ChromaCloudQuotas.MaxMetadataValueBytes + 1);
		await collection.AddAsync(new ChromaRecords(["short", "long"]) { Embeddings = [new([1f, 0f]), new([0f, 1f])], Documents = ["apple pie", longText] });
		var records = (await collection.GetAsync(include: ChromaGetInclude.Metadatas | ChromaGetInclude.Documents)).ToDictionary(x => x.Id);
		Assert.That(records["short"].Metadata!["text"], Is.EqualTo("apple pie"));
		Assert.That(records["long"].Metadata, Is.Not.Null.And.Empty);
		Assert.That(records["long"].Document, Is.EqualTo(longText));
	}

	// With HybridRrf the records without the terms of the text get no points from it: they keep the order of the dense search.
	[Test]
	public async Task HybridRrfOnChromaCloud()
	{
		Assume.That(ChromaCloud, Is.True, "Only Chroma Cloud has sparse vector indexes.");
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var collection = client.GetCollectionClient(await client.CreateCollectionAsync(new ChromaCollectionDefinition($"collection{Random.Shared.Next()}")
		{
			Configuration = new() { Space = ChromaSpace.Cosine },
			Schema = new ChromaCollectionSchema().WithSparseVectorIndex("doc_bm25", ChromaSearchKeys.Document, bm25: true, new ChromaBm25().Reference),
		}));
		await collection.AddAsync(new ChromaRecords(["a", "b", "c", "d"])
		{
			Embeddings = [new([1f, 0f]), new([0f, 1f]), new([0.6f, 0.8f]), new([0.8f, 0.6f])],
			Documents = ["apple pie", "banana split", "cherry tart", "date cake"],
		});
		var bm25Key = collection.Collection.FindBm25Index(ChromaSearchKeys.Document)!.Key;
		var results = await collection.SearchAsync(new ChromaSearch { Rank = ChromaRank.HybridRrf(new([0f, 1f]), "banana", bm25Key, limit: 4), Limit = 4, Select = [ChromaSearchKeys.Score] });
		Assert.That(results.Select(x => x.Id), Is.EqualTo(new[] { "b", "c", "d", "a" }));
		Assert.That(results[1].Score!.Value, Is.EqualTo(1 / 61.0).Within(1e-6));
	}

	async Task<ChromaCollectionClient> Init()
	{
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var collection = client.GetCollectionClient(await client.CreateCollectionAsync($"collection{Random.Shared.Next()}"));
		await collection.AddAsync(["a", "b", "c", "d"], embeddings: [new([1f, 0f]), new([0f, 1f]), new([1f, 1f]), new([0.5f, 0.5f])],
			documents: ["apple pie", "banana split", "cherry tart", "apple juice"]);
		return collection;
	}
}
