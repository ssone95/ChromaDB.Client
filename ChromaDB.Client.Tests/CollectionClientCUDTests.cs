using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class CollectionClientCUDTests : ChromaTestsBase
{
	[Test]
	public async Task AddJustIds()
	{
		var client = await Init();
		await client.Add([$"{Guid.NewGuid()}"]);
	}

	[Test]
	public async Task AddWithEmbeddings()
	{
		var client = await Init();
		await client.Add([$"{Guid.NewGuid()}"],
			embeddings: [new([1f, 0.5f, 0f, -0.5f, -1f])]);
	}

	[Test]
	public async Task AddWithMetadatas()
	{
		var client = await Init();
		await client.Add([$"{Guid.NewGuid()}"],
			metadatas: [new Dictionary<string, object>
			{
				{ "key", "value" },
				{ "key2", 10 },
			}]);
	}

	[Test]
	public async Task AddWithDocuments()
	{
		var client = await Init();
		await client.Add([$"{Guid.NewGuid()}"],
			documents: ["test"]);
	}

	[Test]
	public async Task AddWithAll()
	{
		var client = await Init();
		await client.Add([$"{Guid.NewGuid()}"],
			embeddings: [new([1f, 0.5f, 0f, -0.5f, -1f])],
			metadatas: [new Dictionary<string, object>
			{
				{ "key", "value" },
				{ "key2", 10 },
			}],
			documents: ["test"]);
	}

	[Test]
	public async Task UpdateNothing()
	{
		var id = $"{Guid.NewGuid()}";

		var client = await Init();
		await client.Add([id]);
		await client.Update([id]);
	}

	[Test]
	public async Task UpdateEmbeddings()
	{
		var id = $"{Guid.NewGuid()}";

		var client = await Init();
		await client.Add([id]);
		await client.Update([id],
			embeddings: [new([1f, 0.5f, 0f, -0.5f, -1f])]);
	}

	[Test]
	public async Task UpdateMetadatas()
	{
		var id = $"{Guid.NewGuid()}";

		var client = await Init();
		await client.Add([id]);
		await client.Update([id],
			metadatas: [new Dictionary<string, object>
			{
				{ "key", "value" },
				{ "key2", 10 },
			}]);
	}

	[Test]
	public async Task UpdateDocuments()
	{
		var id = $"{Guid.NewGuid()}";

		var client = await Init();
		await client.Add([id]);
		await client.Update([id],
			documents: ["test"]);
	}

	[Test]
	public async Task UpdateAll()
	{
		var id = $"{Guid.NewGuid()}";

		var client = await Init();
		await client.Add([id]);
		await client.Update([id],
			embeddings: [new([1f, 0.5f, 0f, -0.5f, -1f])],
			metadatas: [new Dictionary<string, object>
			{
				{ "key", "value" },
				{ "key2", 10 },
			}],
			documents: ["test"]);
	}

	[Test]
	public async Task UpsertJustIds()
	{
		var client = await Init();
		await client.Upsert([$"{Guid.NewGuid()}"]);
	}

	[Test]
	public async Task UpsertWithEmbeddings()
	{
		var client = await Init();
		await client.Upsert([$"{Guid.NewGuid()}"],
			embeddings: [new([1f, 0.5f, 0f, -0.5f, -1f])]);
	}

	[Test]
	public async Task UpsertWithMetadatas()
	{
		var client = await Init();
		await client.Upsert([$"{Guid.NewGuid()}"],
			metadatas: [new Dictionary<string, object>
			{
				{ "key", "value" },
				{ "key2", 10 },
			}]);
	}

	[Test]
	public async Task UpsertWithDocuments()
	{
		var client = await Init();
		await client.Upsert([$"{Guid.NewGuid()}"],
			documents: ["test"]);
	}

	[Test]
	public async Task UpsertWithAll()
	{
		var client = await Init();
		await client.Upsert([$"{Guid.NewGuid()}"],
			embeddings: [new([1f, 0.5f, 0f, -0.5f, -1f])],
			metadatas: [new Dictionary<string, object>
			{
				{ "key", "value" },
				{ "key2", 10 },
			}],
			documents: ["test"]);
	}

	[Test]
	public async Task DeleteByIdExisting()
	{
		var id = $"{Guid.NewGuid()}";

		var client = await Init();
		await client.Add([id]);
		await client.Delete([id]);
	}

	[Test]
	public async Task DeleteByIdNonExisting()
	{
		var id = $"{Guid.NewGuid()}";

		var client = await Init();
		await client.Delete([id]);
	}

	[Test]
	public async Task DeleteByMultipleIds()
	{
		var id1 = $"{Guid.NewGuid()}";
		var id2 = $"{Guid.NewGuid()}";

		var client = await Init();
		await client.Add([id1, id2]);
		await client.Delete([id1, id2]);
	}

	[Test]
	public async Task DeleteByMultipleIdsOneNonExisting()
	{
		var id1 = $"{Guid.NewGuid()}";
		var id2 = $"{Guid.NewGuid()}";

		var client = await Init();
		await client.Add([id1]);
		await client.Delete([id1, id2]);
	}

	[Test]
	public async Task DeleteByMultipleIdsWithWhere()
	{
		var id1 = $"{Guid.NewGuid()}";
		var id2 = $"{Guid.NewGuid()}";

		var client = await Init();
		await client.Add([id1, id2],
			metadatas: [new Dictionary<string, object>
			{
			}, new Dictionary<string, object>
			{
				{ "key", "value" },
			}]);
		await client.Delete([id1, id2],
			where: ChromaWhere.Equal("key", "value"));
	}

	[Test]
	public async Task DeleteByMultipleIdsWithWhereDocument()
	{
		var id1 = $"{Guid.NewGuid()}";
		var id2 = $"{Guid.NewGuid()}";

		var client = await Init();
		await client.Add([id1, id2],
			documents: ["Doc1", "Doc2"]);
		await client.Delete([id1, id2],
			whereDocument: ChromaWhereDocument.Contains("2"));
	}

	async Task<ChromaCollectionClient> Init()
	{
		var name = $"collection{Random.Shared.Next()}";
		var client = new ChromaClient(ConfigurationOptions, HttpClient);
		var collection = await client.GetOrCreateCollection(name);
		return new ChromaCollectionClient(collection, ConfigurationOptions, HttpClient);
	}
}
