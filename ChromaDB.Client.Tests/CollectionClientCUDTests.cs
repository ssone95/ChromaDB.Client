using ChromaDB.Client.Models;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class CollectionClientCUDTests : ChromaTestsBase
{
	[Test]
	public async Task AddJustIds()
	{
		var client = await Init();
		await WithoutEmbeddings(() => client.AddAsync([$"{Guid.NewGuid()}"]));
	}

	[Test]
	public async Task AddWithEmbeddings()
	{
		var client = await Init();
		await client.AddAsync([$"{Guid.NewGuid()}"],
			embeddings: [new([1f, 0.5f, 0f, -0.5f, -1f])]);
	}

	[Test]
	public async Task AddWithMetadatas()
	{
		var client = await Init();
		await WithoutEmbeddings(() => client.AddAsync([$"{Guid.NewGuid()}"],
			metadatas: [new Dictionary<string, object>
			{
				{ "key", "value" },
				{ "key2", 10 },
			}]));
	}

	[Test]
	public async Task AddWithDocuments()
	{
		var client = await Init();
		await WithoutEmbeddings(() => client.AddAsync([$"{Guid.NewGuid()}"],
			documents: ["test"]));
	}

	[Test]
	public async Task AddWithAll()
	{
		var client = await Init();
		await client.AddAsync([$"{Guid.NewGuid()}"],
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
		await client.AddAsync([id], embeddings: Embeddings(1));
		await client.UpdateAsync([id]);
	}

	[Test]
	public async Task UpdateEmbeddings()
	{
		var id = $"{Guid.NewGuid()}";

		var client = await Init();
		await client.AddAsync([id], embeddings: Embeddings(1));
		await client.UpdateAsync([id],
			embeddings: [new([1f, 0.5f, 0f, -0.5f, -1f])]);
	}

	[Test]
	public async Task UpdateMetadatas()
	{
		var id = $"{Guid.NewGuid()}";

		var client = await Init();
		await client.AddAsync([id], embeddings: Embeddings(1));
		await client.UpdateAsync([id],
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
		await client.AddAsync([id], embeddings: Embeddings(1));
		await client.UpdateAsync([id],
			documents: ["test"]);
	}

	[Test]
	public async Task UpdateAll()
	{
		var id = $"{Guid.NewGuid()}";

		var client = await Init();
		await client.AddAsync([id], embeddings: Embeddings(1));
		await client.UpdateAsync([id],
			embeddings: [new([1f, 0.5f, 0f, -0.5f, -1f])],
			metadatas: [new Dictionary<string, object>
			{
				{ "key", "value" },
				{ "key2", 10 },
			}],
			documents: ["test"]);
	}

	// Without NullDocumentsDelete the client sends a null document as it is: Chroma 1.0.0 and later and Chroma Cloud keep the stored
	// document, as in an update without documents, and every 0.x server deletes it.
	[Test]
	public async Task NullDocumentInAnUpdateAndAnUpsert()
	{
		var client = await Init();
		await client.AddAsync(["a", "b", "c"], embeddings: Embeddings(3), documents: ["doc a", "doc b", "doc c"]);
		var metadata = new Dictionary<string, object> { ["k"] = 2 };
		await client.UpdateAsync(new ChromaRecords(["a"]) { Metadatas = [metadata], Documents = [null] });
		await client.UpsertAsync(new ChromaRecords(["b"]) { Embeddings = Embeddings(1), Metadatas = [metadata], Documents = [null] });
		await client.UpdateAsync(["c"], metadatas: [metadata]);
		var documents = (await client.GetAsync(include: ChromaGetInclude.Documents)).OrderBy(x => x.Id).Select(x => x.Document);
		Assert.That(documents, Is.EqualTo(NullDocumentKept ? new[] { "doc a", "doc b", "doc c" } : new[] { null, null, "doc c" }));
	}

	[Test]
	public async Task UpsertJustIds()
	{
		var client = await Init();
		await WithoutEmbeddings(() => client.UpsertAsync([$"{Guid.NewGuid()}"]));
	}

	[Test]
	public async Task UpsertWithEmbeddings()
	{
		var client = await Init();
		await client.UpsertAsync([$"{Guid.NewGuid()}"],
			embeddings: [new([1f, 0.5f, 0f, -0.5f, -1f])]);
	}

	[Test]
	public async Task UpsertWithMetadatas()
	{
		var client = await Init();
		await WithoutEmbeddings(() => client.UpsertAsync([$"{Guid.NewGuid()}"],
			metadatas: [new Dictionary<string, object>
			{
				{ "key", "value" },
				{ "key2", 10 },
			}]));
	}

	[Test]
	public async Task UpsertWithDocuments()
	{
		var client = await Init();
		await WithoutEmbeddings(() => client.UpsertAsync([$"{Guid.NewGuid()}"],
			documents: ["test"]));
	}

	[Test]
	public async Task UpsertWithAll()
	{
		var client = await Init();
		await client.UpsertAsync([$"{Guid.NewGuid()}"],
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
		await client.AddAsync([id], embeddings: Embeddings(1));
		await client.DeleteAsync([id]);
	}

	[Test]
	public async Task DeleteByIdNonExisting()
	{
		var id = $"{Guid.NewGuid()}";

		var client = await Init();
		await client.DeleteAsync([id]);
	}

	[Test]
	public async Task DeleteByMultipleIds()
	{
		var id1 = $"{Guid.NewGuid()}";
		var id2 = $"{Guid.NewGuid()}";

		var client = await Init();
		await client.AddAsync([id1, id2], embeddings: Embeddings(2));
		await client.DeleteAsync([id1, id2]);
	}

	[Test]
	public async Task DeleteByMultipleIdsOneNonExisting()
	{
		var id1 = $"{Guid.NewGuid()}";
		var id2 = $"{Guid.NewGuid()}";

		var client = await Init();
		await client.AddAsync([id1], embeddings: Embeddings(1));
		await client.DeleteAsync([id1, id2]);
	}

	[Test]
	public async Task DeleteByMultipleIdsWithWhere()
	{
		var id1 = $"{Guid.NewGuid()}";
		var id2 = $"{Guid.NewGuid()}";

		var client = await Init();
		await client.AddAsync([id1, id2],
			embeddings: Embeddings(2),
			metadatas: [new Dictionary<string, object>
			{
				{ "key", "other" },
			}, new Dictionary<string, object>
			{
				{ "key", "value" },
			}]);
		await client.DeleteAsync([id1, id2],
			where: ChromaWhereOperator.Equal("key", "value"));
	}

	[Test]
	public async Task DeleteByMultipleIdsWithWhereDocument()
	{
		var id1 = $"{Guid.NewGuid()}";
		var id2 = $"{Guid.NewGuid()}";

		var client = await Init();
		await client.AddAsync([id1, id2],
			embeddings: Embeddings(2),
			documents: ["Doc1", "Doc2"]);
		// Three characters tell the documents apart: on Chroma Cloud a $contains of fewer characters throws (KD-57).
		await client.DeleteAsync([id1, id2],
			whereDocument: ChromaWhereDocumentOperator.Contains("oc2"));
	}

	[Test]
	public async Task AddWithInconsistentDimensions()
	{
		Assume.That(EmbeddingDimensionsChecked, Is.True, "Before Chroma 0.5.20 the server accepts embeddings of different dimensions.");
		var client = await Init();
		var exception = Assert.ThrowsAsync<ChromaException>(() => client.AddAsync(["a", "b"], embeddings: [new([1f, 2f]), new([1f, 2f, 3f])]));
		Assert.That(exception!.Message, Does.Contain("dimension").IgnoreCase.And.Not.Contain("{"));
	}

	[Test]
	public async Task RecordsWithUris()
	{
		Assume.That(UrisSupported, Is.True, "Chroma 0.4.15 and earlier do not return the URIs of the records.");
		var client = await Init();
		await client.AddAsync(new ChromaRecords(["a", "b"]) { Embeddings = Embeddings(2), Uris = ["file://a", null] });
		await client.UpdateAsync(new ChromaRecords(["b"]) { Uris = ["file://b2"] });
		await client.UpsertAsync(new ChromaRecords(["c"]) { Embeddings = Embeddings(1), Uris = ["file://c"] });
		var entries = await client.GetAsync(["a", "b", "c"], include: ChromaGetInclude.Uris);
		Assert.That(entries.ToDictionary(x => x.Id, x => x.Uri), Is.EquivalentTo(new Dictionary<string, string?> { ["a"] = "file://a", ["b"] = "file://b2", ["c"] = "file://c" }));
		var nearest = await client.QueryAsync(new ReadOnlyMemory<float>([1f, 0.5f, 0f, -0.5f, -1f]), nResults: 3, include: ChromaQueryInclude.Uris);
		Assert.That(nearest.Select(x => x.Uri), Is.EquivalentTo(new[] { "file://a", "file://b2", "file://c" }));
	}

	// Chroma 0.4.10 to 0.4.15 reject "uris" in include with the validation errors of FastAPI: the message is theirs.
	[Test]
	public async Task UrisOnAServerWithoutThem()
	{
		Assume.That(UrisSupported, Is.False, "This server returns the URIs of the records.");
		var client = await Init();
		await Assert.ThatAsync(() => client.GetAsync(include: ChromaGetInclude.Uris),
			Throws.InstanceOf<ChromaException>().With.Message.StartsWith("body.include.0").And.Message.Contains("documents"));
	}

	async Task<ChromaCollectionClient> Init()
	{
		var name = $"collection{Random.Shared.Next()}";
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var collection = await client.GetOrCreateCollectionAsync(name);
		return new ChromaCollectionClient(collection, BaseConfigurationOptions, HttpClient);
	}
}
