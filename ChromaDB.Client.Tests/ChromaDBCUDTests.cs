using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Services.Implementations;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class ChromaDBCUDTests : ChromaDBTestsBase
{
	[Test]
	public async Task AddJustIds()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Add(new CollectionAddRequest()
		{
			Ids = [$"{Guid.NewGuid()}"],
		});
		Assert.That(result.Success, Is.True);
	}

	[Test]
	public async Task AddWithEmbeddings()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Add(new CollectionAddRequest()
		{
			Ids = [$"{Guid.NewGuid()}"],
			Embeddings = [[1f, 0.5f, 0f, -0.5f, -1f]],
		});
		Assert.That(result.Success, Is.True);
	}

	[Test]
	public async Task AddWithMetadatas()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Add(new CollectionAddRequest()
		{
			Ids = [$"{Guid.NewGuid()}"],
			Metadatas = [new Dictionary<string, object>
			{
				{ "key", "value" },
				{ "key2", 10 },
			}],
		});
		Assert.That(result.Success, Is.True);
	}

	[Test]
	public async Task AddWithDocuments()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Add(new CollectionAddRequest()
		{
			Ids = [$"{Guid.NewGuid()}"],
			Documents = ["test"],
		});
		Assert.That(result.Success, Is.True);
	}

	[Test]
	public async Task AddWithAll()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Add(new CollectionAddRequest()
		{
			Ids = [$"{Guid.NewGuid()}"],
			Embeddings = [[1f, 0.5f, 0f, -0.5f, -1f]],
			Metadatas = [new Dictionary<string, object>
			{
				{ "key", "value" },
				{ "key2", 10 },
			}],
			Documents = ["test"],
		});
		Assert.That(result.Success, Is.True);
	}

	[Test]
	public async Task UpdateNothing()
	{
		var id = $"{Guid.NewGuid()}";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		await client.Add(new CollectionAddRequest()
		{
			Ids = [id],
		});
		var result = await client.Update(new CollectionUpdateRequest()
		{
			Ids = [id],
		});
		Assert.That(result.Success, Is.True);
	}

	[Test]
	public async Task UpdateEmbeddings()
	{
		var id = $"{Guid.NewGuid()}";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		await client.Add(new CollectionAddRequest()
		{
			Ids = [id],
		});
		var result = await client.Update(new CollectionUpdateRequest()
		{
			Ids = [id],
			Embeddings = [[1f, 0.5f, 0f, -0.5f, -1f]],
		});
		Assert.That(result.Success, Is.True);
	}

	[Test]
	public async Task UpdateMetadatas()
	{
		var id = $"{Guid.NewGuid()}";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		await client.Add(new CollectionAddRequest()
		{
			Ids = [id],
		});
		var result = await client.Update(new CollectionUpdateRequest()
		{
			Ids = [id],
			Metadatas = [new Dictionary<string, object>
			{
				{ "key", "value" },
				{ "key2", 10 },
			}],
		});
		Assert.That(result.Success, Is.True);
	}

	[Test]
	public async Task UpdateDocuments()
	{
		var id = $"{Guid.NewGuid()}";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		await client.Add(new CollectionAddRequest()
		{
			Ids = [id],
		});
		var result = await client.Update(new CollectionUpdateRequest()
		{
			Ids = [id],
			Documents = ["test"],
		});
		Assert.That(result.Success, Is.True);
	}

	[Test]
	public async Task UpdateAll()
	{
		var id = $"{Guid.NewGuid()}";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		await client.Add(new CollectionAddRequest()
		{
			Ids = [id],
		});
		var result = await client.Update(new CollectionUpdateRequest()
		{
			Ids = [id],
			Embeddings = [[1f, 0.5f, 0f, -0.5f, -1f]],
			Metadatas = [new Dictionary<string, object>
			{
				{ "key", "value" },
				{ "key2", 10 },
			}],
			Documents = ["test"],
		});
		Assert.That(result.Success, Is.True);
	}

	[Test]
	public async Task UpsertJustIds()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Upsert(new CollectionUpsertRequest()
		{
			Ids = [$"{Guid.NewGuid()}"],
		});
		Assert.That(result.Success, Is.True);
	}

	[Test]
	public async Task UpsertWithEmbeddings()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Upsert(new CollectionUpsertRequest()
		{
			Ids = [$"{Guid.NewGuid()}"],
			Embeddings = [[1f, 0.5f, 0f, -0.5f, -1f]],
		});
		Assert.That(result.Success, Is.True);
	}

	[Test]
	public async Task UpsertWithMetadatas()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Upsert(new CollectionUpsertRequest()
		{
			Ids = [$"{Guid.NewGuid()}"],
			Metadatas = [new Dictionary<string, object>
			{
				{ "key", "value" },
				{ "key2", 10 },
			}],
		});
		Assert.That(result.Success, Is.True);
	}

	[Test]
	public async Task UpsertWithDocuments()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Upsert(new CollectionUpsertRequest()
		{
			Ids = [$"{Guid.NewGuid()}"],
			Documents = ["test"],
		});
		Assert.That(result.Success, Is.True);
	}

	[Test]
	public async Task AUpsertithAll()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = await Init(httpClient);
		var result = await client.Upsert(new CollectionUpsertRequest()
		{
			Ids = [$"{Guid.NewGuid()}"],
			Embeddings = [[1f, 0.5f, 0f, -0.5f, -1f]],
			Metadatas = [new Dictionary<string, object>
			{
				{ "key", "value" },
				{ "key2", 10 },
			}],
			Documents = ["test"],
		});
		Assert.That(result.Success, Is.True);
	}

	async Task<ChromaCollectionClient> Init(ChromaDBHttpClient httpClient)
	{
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var collectionResponse = await client.GetOrCreateCollection(new DBGetOrCreateCollectionRequest { Name = "cud_tests" });
		Assert.That(collectionResponse.Success, Is.True);
		var collection = collectionResponse.Data!;
		return new ChromaCollectionClient(collection, httpClient);
	}
}
