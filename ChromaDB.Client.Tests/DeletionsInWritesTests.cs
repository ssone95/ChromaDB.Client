using System.Net;
using System.Text.Json;
using ChromaDB.Client.Models;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

// In an update or an upsert, a null value or an empty list deletes a key, a null document deletes the document with
// NullDocumentsDelete, and a text that goes takes its sparse vectors along. A null goes to every record, whether it has the key or not,
// without a read, and so does the empty document of a deletion; the client reads the records first only on Chroma Cloud beyond its quota
// of keys. The keys the metadata does not have stay.
[TestFixture]
public class DeletionsInWritesTests
{
	static readonly ReadOnlyMemory<float> Embedding = new([1f, 0f]);
	const string Bm25 = """{"type":"known","name":"chroma_bm25","config":{}}""";
	const string Schema = """{"defaults":{},"keys":{"doc_bm25":{"sparse_vector":{"sparse_vector_index":{"enabled":true,"config":{"embedding_function":""" + Bm25 + ""","source_key":"#document"}}}},"title_bm25":{"sparse_vector":{"sparse_vector_index":{"enabled":true,"config":{"embedding_function":""" + Bm25 + ""","source_key":"title"}}}}}}""";

	// Every tested Chroma takes a null for a key the record does not have, also in an upsert that creates the record, and leaves the record
	// without it: the write does not depend on a read that another writer could change before it goes.
	[Test]
	public async Task NullsGoWithoutARead()
	{
		var server = new FakeServer("""{"ids":["a","b"],"metadatas":[{"k":1,"gone":2},{"other":1}]}""");
		await Client(server).UpsertAsync(new ChromaRecords(["a", "b", "new"])
		{
			Embeddings = [Embedding, Embedding, Embedding],
			Metadatas =
			[
				new Dictionary<string, object> { ["k"] = null!, ["gone"] = new List<int>(), ["missing"] = null!, ["x"] = 1 },
				new Dictionary<string, object> { ["k"] = Array.Empty<string>(), ["y"] = 2 },
				new Dictionary<string, object> { ["k"] = null! },
			],
		});
		Assert.That(server.Paths, Is.EqualTo(new[] { "pre-flight-checks", "upsert" }));
		var metadatas = server.Bodies[1].GetProperty("metadatas");
		Assert.That(metadatas[0].GetRawText(), Is.EqualTo("""{"x":1,"k":null,"gone":null,"missing":null}"""));
		Assert.That(metadatas[1].GetRawText(), Is.EqualTo("""{"y":2,"k":null}"""));
		Assert.That(metadatas[2].GetRawText(), Is.EqualTo("""{"k":null}"""));
	}

	[Test]
	public async Task UpdateToo()
	{
		var server = new FakeServer("""{"ids":["a"],"metadatas":[{"k":1}]}""");
		await Client(server).UpdateAsync(new ChromaRecords(["a"]) { Metadatas = [new Dictionary<string, object> { ["k"] = null!, ["j"] = null! }] });
		Assert.That(server.Paths, Is.EqualTo(new[] { "pre-flight-checks", "update" }));
		Assert.That(server.Bodies[1].GetProperty("metadatas")[0].GetRawText(), Is.EqualTo("""{"k":null,"j":null}"""));
	}

	// When no record has anything to delete, the client sends the nulls of none and keeps the metadata of the caller.
	[Test]
	public async Task NothingToDeleteReadsNothing()
	{
		var server = new FakeServer("""{"ids":[]}""");
		await Client(server).UpsertAsync(new ChromaRecords(["a"]) { Embeddings = [Embedding], Metadatas = [new Dictionary<string, object> { ["k"] = 1 }], Documents = [null!] });
		Assert.That(server.Paths, Is.EqualTo(new[] { "pre-flight-checks", "upsert" }));
		Assert.That(server.Bodies[1].GetProperty("documents")[0].ValueKind, Is.EqualTo(JsonValueKind.Null));
	}

	// Chroma Cloud counts a null against its quota of 32 keys: when the nulls would take the metadata of a record beyond it, the client
	// reads that record and sends the nulls only to the keys it has. A record within the quota needs no read.
	[Test]
	public async Task BeyondTheQuotaOfKeysOfChromaCloud()
	{
		var server = new FakeServer("""{"ids":["a"],"metadatas":[{"k0":0,"gone":1}]}""");
		var many = Enumerable.Range(0, 31).ToDictionary(i => $"k{i}", i => (object)(long)i);
		many["gone"] = null!;
		many["missing"] = null!;
		await Client(server, uri: "https://api.trychroma.com").UpsertAsync(new ChromaRecords(["a", "b"])
		{
			Embeddings = [Embedding, Embedding],
			Metadatas = [many, new Dictionary<string, object> { ["k"] = null! }],
		});
		Assert.That(server.Paths, Is.EqualTo(new[] { "pre-flight-checks", "get", "upsert" }));
		Assert.That(server.Bodies[1].GetProperty("ids").EnumerateArray().Select(x => x.GetString()), Is.EqualTo(new[] { "a" }));
		var metadatas = server.Bodies[2].GetProperty("metadatas");
		Assert.That(metadatas[0].EnumerateObject().Count(), Is.EqualTo(32));
		Assert.That(metadatas[0].GetProperty("gone").ValueKind, Is.EqualTo(JsonValueKind.Null));
		Assert.That(metadatas[0].TryGetProperty("missing", out _), Is.False);
		Assert.That(metadatas[1].GetRawText(), Is.EqualTo("""{"k":null}"""));
	}

	// The read is only for Chroma Cloud, and only beyond its quota: 33 keys with the nulls on a single server, and exactly 32 on Chroma
	// Cloud, go without a read.
	[TestCase("http://localhost:8000", 31)]
	[TestCase("https://api.trychroma.com", 30)]
	public async Task NoReadWithinTheQuotaOrOnASingleServer(string uri, int values)
	{
		var server = new FakeServer("""{"ids":["a"],"metadatas":[{"k0":0,"gone":1}]}""");
		var metadata = Enumerable.Range(0, values).ToDictionary(i => $"k{i}", i => (object)(long)i);
		metadata["gone"] = null!;
		metadata["missing"] = null!;
		await Client(server, uri: uri).UpsertAsync(new ChromaRecords(["a"]) { Embeddings = [Embedding], Metadatas = [metadata] });
		Assert.That(server.Paths, Is.EqualTo(new[] { "pre-flight-checks", "upsert" }));
		Assert.That(server.Bodies[1].GetProperty("metadatas")[0].EnumerateObject().Count(), Is.EqualTo(values + 2));
	}

	// Chroma has no deletion of a document: the client writes an empty one, which a collection client deletes in one of three ways it
	// chooses. Without one, a null document with NullDocumentsDelete throws before any request, with the three ways, also from a client
	// made by name, which has not read its collection yet; without NullDocumentsDelete, or without a null document, the write goes.
	[TestCase(true)]
	[TestCase(false)]
	public async Task NullDocumentsDeleteWithoutAWayToDeleteDocuments(bool upsert)
	{
		var server = new FakeServer("""{"ids":[]}""");
		var byName = new ChromaClient(new ChromaConfigurationOptions("http://localhost:8000"), new HttpClient(server)).GetCollectionClient("c");
		ChromaRecords Records(string?[] documents, bool nullDocumentsDelete) => new(["a", "b"])
		{
			Embeddings = upsert ? [Embedding, Embedding] : null,
			Documents = documents,
			NullDocumentsDelete = nullDocumentsDelete,
		};
		Task Write(ChromaCollectionClient client, ChromaRecords records) => upsert ? client.UpsertAsync(records) : client.UpdateAsync(records);
		foreach (var client in new[] { Client(server), byName })
		{
			var ex = Assert.ThrowsAsync<ArgumentException>(() => Write(client, Records([null, "doc b"], true)));
			Assert.That(ex!.Message, Does.StartWith("NullDocumentsDelete needs a way to tell a deleted document from an empty one: Chroma cannot delete a document, so the client writes an empty one. Choose one on the collection client: "));
			Assert.That(ex.Message, Does.Contain("WithDocumentCopyKey(key)").And.Contain("WithDocumentDeletionKey(key)").And.Contain("WithEmptyDocumentDeletions()"));
			Assert.That(ex.ParamName, Is.EqualTo("records"));
		}
		Assert.That(server.Paths, Is.Empty);
		await Write(Client(server), Records([null, "doc b"], false));
		await Write(Client(server), Records(["doc a", "doc b"], true));
		Assert.That(server.Paths.Count(path => path is "upsert" or "update"), Is.EqualTo(2));
	}

	// A collection client chooses once how it deletes documents: a second call of any of the three methods throws, also on a client of
	// WithMetadataValues, which keeps the choice. The message says which way the client has.
	[Test]
	public void OneWayToDeleteDocumentsPerClient([Values("copy", "mark", "empty")] string first, [Values("copy", "mark", "empty")] string second)
	{
		var client = Way(Client(new FakeServer("""{"ids":[]}""")), first);
		var chosen = first switch { "copy" => "WithDocumentCopyKey(\"text\")", "mark" => "WithDocumentDeletionKey(\"deleted\")", _ => "WithEmptyDocumentDeletions()" };
		var ex = Assert.Throws<InvalidOperationException>(() => Way(client, second));
		Assert.That(ex!.Message, Does.StartWith($"This collection client already deletes documents with {chosen}"));
		Assert.That(() => Way(client.WithMetadataValues(ChromaMetadataValues.Exact), second), Throws.InvalidOperationException);
	}

	// The same method with another key throws too; a null key throws the ArgumentNullException of the key first.
	[Test]
	public void AnotherKeyThrowsToo()
	{
		var client = Client(new FakeServer("""{"ids":[]}"""));
		Assert.That(() => client.WithDocumentCopyKey("text").WithDocumentCopyKey("other"), Throws.InvalidOperationException);
		Assert.That(() => client.WithDocumentDeletionKey("deleted").WithDocumentDeletionKey("gone"), Throws.InvalidOperationException);
		Assert.That(() => client.WithDocumentCopyKey("text").WithDocumentDeletionKey(null!), Throws.ArgumentNullException);
	}

	// A client of WithMetadataValues deletes documents in the way of the client it comes from.
	[TestCase("mark", """{"deleted":true}""")]
	[TestCase("empty", "null")]
	public async Task MetadataValuesKeepTheWay(string way, string metadata)
	{
		var server = new FakeServer("""{"ids":[]}""");
		var client = Way(Client(server), way).WithMetadataValues(ChromaMetadataValues.Exact);
		await client.UpsertAsync(new ChromaRecords(["a"]) { Embeddings = [Embedding], Documents = [null!], NullDocumentsDelete = true });
		var upsert = server.Bodies[server.Paths.IndexOf("upsert")];
		Assert.That(upsert.GetProperty("documents")[0].GetString(), Is.EqualTo(""));
		Assert.That(upsert.GetProperty("metadatas").ValueKind == JsonValueKind.Null ? "null" : upsert.GetProperty("metadatas")[0].GetRawText(), Is.EqualTo(metadata));
	}

	static ChromaCollectionClient Way(ChromaCollectionClient client, string way)
		=> way switch
		{
			"copy" => client.WithDocumentCopyKey("text"),
			"mark" => client.WithDocumentDeletionKey("deleted"),
			_ => client.WithEmptyDocumentDeletions(),
		};

	// With a document copy key the copy tells a deleted document from an empty one: the client writes an empty document and the null of
	// its copy to every record whose document goes, without a read.
	[TestCase(true)]
	[TestCase(false)]
	public async Task NullDocumentsDeleteWithADocumentCopyKey(bool upsert)
	{
		var server = new FakeServer("""{"ids":["a"],"metadatas":[{"text":"doc a"}],"documents":["doc a"]}""");
		var client = Client(server).WithDocumentCopyKey("text");
		var records = new ChromaRecords(["a", "b", "c"])
		{
			Embeddings = upsert ? [Embedding, Embedding, Embedding] : null,
			Documents = [null!, null!, "new"],
			NullDocumentsDelete = true,
		};
		await (upsert ? client.UpsertAsync(records) : client.UpdateAsync(records));
		Assert.That(server.Paths, Is.EqualTo(new[] { "pre-flight-checks", upsert ? "upsert" : "update" }));
		var write = server.Bodies[1];
		Assert.That(write.GetProperty("documents").EnumerateArray().Select(x => x.GetString()), Is.EqualTo(new[] { "", "", "new" }));
		Assert.That(write.GetProperty("metadatas").EnumerateArray().Select(x => x.GetRawText()), Is.EqualTo(new[] { """{"text":null}""", """{"text":null}""", """{"text":"new"}""" }));
	}

	// With a document deletion key the client marks a deleted document instead of copying every one: an empty document with true under
	// the key, and the null of the mark for any other document, without a read. A null document that stays keeps its mark, and an add
	// writes no mark.
	[TestCase(true)]
	[TestCase(false)]
	public async Task NullDocumentsDeleteWithADocumentDeletionKey(bool upsert)
	{
		var server = new FakeServer("""{"ids":[]}""");
		var client = Client(server).WithDocumentDeletionKey("deleted");
		var records = new ChromaRecords(["a", "b", "c"])
		{
			Embeddings = upsert ? [Embedding, Embedding, Embedding] : null,
			Metadatas = [null, new Dictionary<string, object> { ["k"] = 1L }, null],
			Documents = [null!, "", "new"],
			NullDocumentsDelete = true,
		};
		await (upsert ? client.UpsertAsync(records) : client.UpdateAsync(records));
		Assert.That(server.Paths, Is.EqualTo(new[] { "pre-flight-checks", upsert ? "upsert" : "update" }));
		var write = server.Bodies[1];
		Assert.That(write.GetProperty("documents").EnumerateArray().Select(x => x.GetString()), Is.EqualTo(new[] { "", "", "new" }));
		Assert.That(write.GetProperty("metadatas").EnumerateArray().Select(x => x.GetRawText()), Is.EqualTo(new[] { """{"deleted":true}""", """{"k":1,"deleted":null}""", """{"deleted":null}""" }));
		await client.UpdateAsync(new ChromaRecords(["a"]) { Documents = [null!] });
		Assert.That(server.Bodies[^1].GetProperty("metadatas").ValueKind, Is.EqualTo(JsonValueKind.Null));
		await client.AddAsync(new ChromaRecords(["d"]) { Embeddings = [Embedding], Documents = [""] });
		Assert.That(server.Bodies[^1].GetProperty("metadatas").ValueKind, Is.EqualTo(JsonValueKind.Null));
	}

	// With empty document deletions the client writes an empty document, and the null of the sparse vector of the document, without a
	// read and without any key of its own.
	[TestCase(true)]
	[TestCase(false)]
	public async Task NullDocumentsDeleteWithEmptyDocumentDeletions(bool upsert)
	{
		var server = new FakeServer("""{"ids":[]}""");
		var client = Client(server, Schema).WithEmptyDocumentDeletions();
		var records = new ChromaRecords(["a", "b"])
		{
			Embeddings = upsert ? [Embedding, Embedding] : null,
			Metadatas = [null, new Dictionary<string, object> { ["k"] = 1L }],
			Documents = [null!, null!],
			NullDocumentsDelete = true,
		};
		await (upsert ? client.UpsertAsync(records) : client.UpdateAsync(records));
		Assert.That(server.Paths, Is.EqualTo(new[] { "pre-flight-checks", upsert ? "upsert" : "update" }));
		var write = server.Bodies[1];
		Assert.That(write.GetProperty("documents").EnumerateArray().Select(x => x.GetString()), Is.EqualTo(new[] { "", "" }));
		Assert.That(write.GetProperty("metadatas").EnumerateArray().Select(x => x.GetRawText()), Is.EqualTo(new[] { """{"doc_bm25":null}""", """{"k":1,"doc_bm25":null}""" }));
	}

	// The sparse vectors of a text that goes are deleted too, unless the caller gives them; an empty text has no vector.
	[Test]
	public async Task SparseVectorsOfATextThatGoes()
	{
		var server = new FakeServer("""{"ids":["a","b"],"metadatas":[{"title":"T","title_bm25":1,"doc_bm25":1},{"title":"U","title_bm25":1}],"documents":["doc a","doc b"]}""");
		await Client(server, Schema).WithDocumentCopyKey("text").UpsertAsync(new ChromaRecords(["a", "b"])
		{
			Embeddings = [Embedding, Embedding],
			Metadatas = [new Dictionary<string, object> { ["title"] = null! }, new Dictionary<string, object> { ["title"] = null!, ["title_bm25"] = new ChromaSparseVector([1], [0.5f]) }],
			Documents = [null!, ""],
			NullDocumentsDelete = true,
		});
		var upsert = server.Bodies[server.Paths.IndexOf("upsert")];
		var metadatas = upsert.GetProperty("metadatas");
		Assert.That(metadatas[0].GetRawText(), Is.EqualTo("""{"title":null,"text":null,"doc_bm25":null,"title_bm25":null}"""));
		Assert.That(metadatas[1].GetProperty("title").ValueKind, Is.EqualTo(JsonValueKind.Null));
		Assert.That(metadatas[1].GetProperty("title_bm25").GetProperty("indices")[0].GetInt32(), Is.EqualTo(1));
		Assert.That(metadatas[1].TryGetProperty("doc_bm25", out _), Is.False);
		Assert.That(upsert.GetProperty("documents")[0].GetString(), Is.EqualTo(""));
	}

	// Without metadata, a document that goes takes its sparse vector along; a null goes to a stored record without metadata too.
	[Test]
	public async Task DocumentThatGoesWithoutMetadata()
	{
		var server = new FakeServer("""{"ids":["a","b"],"metadatas":[{"doc_bm25":1},null],"documents":["doc a","doc b"]}""");
		await Client(server, Schema).WithDocumentCopyKey("text").UpsertAsync(new ChromaRecords(["a"]) { Embeddings = [Embedding], Documents = [null!], NullDocumentsDelete = true });
		Assert.That(server.Bodies[server.Paths.IndexOf("upsert")].GetProperty("metadatas")[0].GetRawText(), Is.EqualTo("""{"text":null,"doc_bm25":null}"""));
		await Client(server).UpsertAsync(new ChromaRecords(["b"]) { Embeddings = [Embedding], Metadatas = [new Dictionary<string, object> { ["k"] = null! }] });
		Assert.That(server.Bodies.Last().GetProperty("metadatas")[0].GetRawText(), Is.EqualTo("""{"k":null}"""));
	}

	// The copy of each document under the document copy key replaces what the metadata has there; a document too long for Chroma Cloud
	// goes without it, and only there.
	[TestCase("http://localhost:8000", true)]
	[TestCase("https://api.trychroma.com", false)]
	public async Task DocumentCopyInAnAdd(string uri, bool longCopied)
	{
		var server = new FakeServer("""{"ids":[]}""");
		var longText = new string('a', ChromaCloudQuotas.MaxMetadataValueBytes + 1);
		await Client(server, uri: uri).WithDocumentCopyKey("text").AddAsync(new ChromaRecords(["a", "b", "c"])
		{
			Embeddings = [Embedding, Embedding, Embedding],
			Metadatas = [new Dictionary<string, object> { ["text"] = "old", ["k"] = 1 }, null!, null!],
			Documents = ["short", longText, null!],
		});
		var metadatas = server.Bodies[server.Paths.IndexOf("add")].GetProperty("metadatas");
		Assert.That(metadatas[0].GetRawText(), Is.EqualTo("""{"text":"short","k":1}"""));
		Assert.That(metadatas[1].ValueKind == JsonValueKind.Object && metadatas[1].TryGetProperty("text", out _), Is.EqualTo(longCopied));
		Assert.That(metadatas[2].ValueKind, Is.EqualTo(JsonValueKind.Null));
	}

	// In an upsert a document without its copy deletes the stored one: one too long for Chroma Cloud, and one deleted; a null document that
	// stays keeps its copy.
	[TestCase(true)]
	[TestCase(false)]
	public async Task DocumentCopyInAnUpsert(bool nullDocumentsDelete)
	{
		var server = new FakeServer("""{"ids":["a","b"],"metadatas":[{"text":"old a"},{"text":"old b"}],"documents":["old a","old b"]}""");
		var longText = new string('a', ChromaCloudQuotas.MaxMetadataValueBytes + 1);
		await Client(server, uri: "https://api.trychroma.com").WithDocumentCopyKey("text").UpsertAsync(new ChromaRecords(["a", "b", "c"])
		{
			Embeddings = [Embedding, Embedding, Embedding],
			Documents = [longText, null!, "new c"],
			NullDocumentsDelete = nullDocumentsDelete,
		});
		var upsert = server.Bodies[server.Paths.IndexOf("upsert")];
		var metadatas = upsert.GetProperty("metadatas");
		Assert.That(metadatas[0].GetRawText(), Is.EqualTo("""{"text":null}"""));
		Assert.That(nullDocumentsDelete ? metadatas[1].GetRawText() : metadatas[1].ValueKind.ToString(), Is.EqualTo(nullDocumentsDelete ? """{"text":null}""" : "Null"));
		Assert.That(metadatas[2].GetRawText(), Is.EqualTo("""{"text":"new c"}"""));
	}

	[Test]
	public async Task NoDocumentsNoCopy()
	{
		var server = new FakeServer("""{"ids":[]}""");
		await Client(server).WithDocumentCopyKey("text").UpsertAsync(new ChromaRecords(["a"]) { Embeddings = [Embedding] });
		Assert.That(server.Paths, Is.EqualTo(new[] { "pre-flight-checks", "upsert" }));
		Assert.That(server.Bodies[1].GetProperty("metadatas").ValueKind, Is.EqualTo(JsonValueKind.Null));
	}

	// A list of metadata that are all null goes as no metadata.
	[Test]
	public async Task AllMetadatasNull()
	{
		var server = new FakeServer("""{"ids":[]}""");
		await Client(server).UpsertAsync(new ChromaRecords(["a", "b"]) { Embeddings = [Embedding, Embedding], Metadatas = [null, null] });
		Assert.That(server.Bodies[server.Paths.IndexOf("upsert")].GetProperty("metadatas").ValueKind, Is.EqualTo(JsonValueKind.Null));
	}

	// Records without metadata whose documents get no copy send no metadata.
	[Test]
	public async Task NoCopyNoMetadata()
	{
		var server = new FakeServer("""{"ids":[]}""");
		await Client(server).WithDocumentCopyKey("text").AddAsync(new ChromaRecords(["a"]) { Embeddings = [Embedding], Documents = [null!] });
		Assert.That(server.Bodies[server.Paths.IndexOf("add")].GetProperty("metadatas").ValueKind, Is.EqualTo(JsonValueKind.Null));
	}

	static ChromaCollectionClient Client(HttpMessageHandler handler, string? schema = null, string uri = "http://localhost:8000")
		=> new(new ChromaCollection("c") { Id = Guid.NewGuid(), SchemaJson = schema is null ? null : JsonDocument.Parse(schema).RootElement },
			new ChromaConfigurationOptions(uri), new HttpClient(handler));

	// Answers pre-flight-checks with a batch size, the reads with the stored records, and the writes with nothing; records the requests.
	sealed class FakeServer(string stored) : HttpMessageHandler
	{
		public List<string> Paths { get; } = [];
		public List<JsonElement> Bodies { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var path = request.RequestUri!.AbsolutePath;
			Paths.Add(path.Substring(path.LastIndexOf('/') + 1));
			Bodies.Add(request.Content is null ? default : JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken)).RootElement.Clone());
			var answer = path.EndsWith("/version") ? "\"1.5.9\""
				: path.EndsWith("/pre-flight-checks") ? """{"max_batch_size":100,"supports_base64_encoding":false}"""
				: path.EndsWith("/get") ? stored
				: "{}";
			return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer) };
		}
	}
}
