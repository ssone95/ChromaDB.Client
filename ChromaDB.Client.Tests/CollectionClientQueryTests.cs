using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Services.Implementations;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class CollectionClientQueryTests : ChromaDBTestsBase
{
	[Test]
	public async Task SimpleQuerySingle()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Query(new CollectionQueryRequest()
		{
			QueryEmbeddings = [Embeddings1],
			Include = ["distances", "embeddings"],
		});
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data!.Count, Is.EqualTo(1));
		Assert.That(result.Data![0].Count, Is.EqualTo(2));
		Assert.That(result.Data![0].Select(x => x.Distance), Has.Some.Not.EqualTo(0));
		Assert.That(result.Data![0][0].Embeddings, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![0][0].Metadata, Is.Null);
		Assert.That(result.Data![0][0].Document, Is.Null);
		Assert.That(result.Data![0][1].Embeddings, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![0][1].Metadata, Is.Null);
		Assert.That(result.Data![0][1].Document, Is.Null);
	}

	[Test]
	public async Task SimpleQuerySingleIncludeAll()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Query(new CollectionQueryRequest()
		{
			QueryEmbeddings = [Embeddings1],
			Include = ["distances", "embeddings", "metadatas", "documents"],
		});
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data!.Count, Is.EqualTo(1));
		Assert.That(result.Data![0].Count, Is.EqualTo(2));
		Assert.That(result.Data![0].Select(x => x.Distance), Has.Some.Not.EqualTo(0));
		Assert.That(result.Data![0][0].Embeddings, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![0][0].Metadata, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![0][0].Document, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![0][1].Embeddings, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![0][1].Metadata, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![0][1].Document, Is.Not.Null.And.Not.Empty);
	}

	[Test]
	public async Task SimpleQueryMultiple()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Query(new CollectionQueryRequest()
		{
			QueryEmbeddings = [Embeddings1, Embeddings2],
			Include = ["distances", "embeddings"],
		});
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data!.Count, Is.EqualTo(2));
		Assert.That(result.Data![0].Count, Is.EqualTo(2));
		Assert.That(result.Data![0].Select(x => x.Distance), Has.Some.Not.EqualTo(0));
		Assert.That(result.Data![0][0].Embeddings, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![0][0].Metadata, Is.Null);
		Assert.That(result.Data![0][0].Document, Is.Null);
		Assert.That(result.Data![0][1].Embeddings, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![0][1].Metadata, Is.Null);
		Assert.That(result.Data![0][1].Document, Is.Null);
		Assert.That(result.Data![1].Count, Is.EqualTo(2));
		Assert.That(result.Data![1].Select(x => x.Distance), Has.Some.Not.EqualTo(0));
		Assert.That(result.Data![1][0].Embeddings, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![1][0].Metadata, Is.Null);
		Assert.That(result.Data![1][0].Document, Is.Null);
		Assert.That(result.Data![1][1].Embeddings, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![1][1].Metadata, Is.Null);
		Assert.That(result.Data![1][1].Document, Is.Null);
	}

	[Test]
	public async Task SimpleQueryMultipleIncludeAll()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Query(new CollectionQueryRequest()
		{
			QueryEmbeddings = [Embeddings1, Embeddings2],
			Include = ["distances", "embeddings", "metadatas", "documents"],
		});
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data!.Count, Is.EqualTo(2));
		Assert.That(result.Data![0].Count, Is.EqualTo(2));
		Assert.That(result.Data![0].Select(x => x.Distance), Has.Some.Not.EqualTo(0));
		Assert.That(result.Data![0][0].Embeddings, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![0][0].Metadata, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![0][0].Document, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![0][1].Embeddings, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![0][1].Metadata, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![0][1].Document, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![1].Count, Is.EqualTo(2));
		Assert.That(result.Data![1].Select(x => x.Distance), Has.Some.Not.EqualTo(0));
		Assert.That(result.Data![1][0].Embeddings, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![1][0].Metadata, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![1][0].Document, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![1][1].Embeddings, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![1][1].Metadata, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data![1][1].Document, Is.Not.Null.And.Not.Empty);
	}

	[Test]
	public async Task QuerySingleNResults1()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Query(new CollectionQueryRequest()
		{
			QueryEmbeddings = [Embeddings1],
			Include = ["distances", "embeddings"],
			NResults = 1,
		});
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data!.Count, Is.EqualTo(1));
		Assert.That(result.Data![0].Count, Is.EqualTo(1));
		Assert.That(result.Data![0][0].Distance, Is.EqualTo(0));
		Assert.That(result.Data![0][0].Embeddings, Is.EqualTo(Embeddings1));
		Assert.That(result.Data![0][0].Metadata, Is.Null);
		Assert.That(result.Data![0][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhere()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Query(new CollectionQueryRequest()
		{
			QueryEmbeddings = [Embeddings1, Embeddings2],
			Where = new Dictionary<string, object> { { MetadataKey2, new Dictionary<string, object> { { "$lt", Metadata2[MetadataKey2] } } } },
			Include = ["distances"],
		});
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data!.Count, Is.EqualTo(2));
		Assert.That(result.Data![0].Count, Is.EqualTo(1));
		Assert.That(result.Data![0][0].Distance, Is.EqualTo(0));
		Assert.That(result.Data![0][0].Id, Is.EqualTo(Id1));
		Assert.That(result.Data![0][0].Embeddings, Is.Null);
		Assert.That(result.Data![0][0].Metadata, Is.Null);
		Assert.That(result.Data![0][0].Document, Is.Null);
		Assert.That(result.Data![1].Count, Is.EqualTo(1));
		Assert.That(result.Data![1][0].Distance, Is.GreaterThan(0));
		Assert.That(result.Data![1][0].Id, Is.EqualTo(Id1));
		Assert.That(result.Data![1][0].Embeddings, Is.Null);
		Assert.That(result.Data![1][0].Metadata, Is.Null);
		Assert.That(result.Data![1][0].Document, Is.Null);
	}

	[Test]
	public async Task QueryWithWhereDocument()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Query(new CollectionQueryRequest()
		{
			QueryEmbeddings = [Embeddings1, Embeddings2],
			WhereDocument = new Dictionary<string, object> { { "$not_contains", Doc2[^1] } },
			Include = ["distances"],
		});
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data!.Count, Is.EqualTo(2));
		Assert.That(result.Data![0].Count, Is.EqualTo(1));
		Assert.That(result.Data![0][0].Distance, Is.EqualTo(0));
		Assert.That(result.Data![0][0].Id, Is.EqualTo(Id1));
		Assert.That(result.Data![0][0].Embeddings, Is.Null);
		Assert.That(result.Data![0][0].Metadata, Is.Null);
		Assert.That(result.Data![0][0].Document, Is.Null);
		Assert.That(result.Data![1].Count, Is.EqualTo(1));
		Assert.That(result.Data![1][0].Distance, Is.GreaterThan(0));
		Assert.That(result.Data![1][0].Id, Is.EqualTo(Id1));
		Assert.That(result.Data![1][0].Embeddings, Is.Null);
		Assert.That(result.Data![1][0].Metadata, Is.Null);
		Assert.That(result.Data![1][0].Document, Is.Null);
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
		var collectionResponse = await client.CreateCollection(new CreateCollectionRequest { Name = name });
		Assert.That(collectionResponse.Success, Is.True);
		var collection = collectionResponse.Data!;
		var collectionClient = new ChromaDBCollectionClient(collection, httpClient);
		var addResponse = await collectionClient.Add(new CollectionAddRequest()
		{
			Ids = [Id1, Id2],
			Embeddings = [Embeddings1, Embeddings2],
			Metadatas = [Metadata1, Metadata2],
			Documents = [Doc1, Doc2],
		});
		Assert.That(addResponse.Success, Is.True);
		return collectionClient;
	}
}
