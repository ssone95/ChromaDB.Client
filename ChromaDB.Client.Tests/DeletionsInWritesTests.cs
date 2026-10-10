using System.Net;
using System.Text.Json;
using ChromaDB.Client.Models;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

// In an update or an upsert, a null value or an empty list deletes a key, a null document deletes the document with
// NullDocumentsDelete, and a text that goes takes its sparse vectors along. A null goes to every record, whether it has the key or not,
// without a read; the client reads the records first only to delete documents, and on Chroma Cloud beyond its quota of keys. The keys
// the metadata does not have stay.
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

	// Chroma has no deletion of a document: the client writes an empty one, for a record that has a document.
	[Test]
	public async Task NullDocumentsDelete()
	{
		var server = new FakeServer("""{"ids":["a","b"],"metadatas":[null,null],"documents":["doc a",""]}""");
		await Client(server).UpsertAsync(new ChromaRecords(["a", "b", "c"]) { Embeddings = [Embedding, Embedding, Embedding], Documents = [null!, null!, "new"], NullDocumentsDelete = true });
		Assert.That(server.Paths, Is.EqualTo(new[] { "pre-flight-checks", "get", "upsert" }));
		Assert.That(server.Bodies[1].GetProperty("include").EnumerateArray().Select(x => x.GetString()), Is.EqualTo(new[] { "metadatas", "documents" }));
		Assert.That(server.Bodies[1].GetProperty("ids").EnumerateArray().Select(x => x.GetString()), Is.EqualTo(new[] { "a", "b" }));
		Assert.That(server.Bodies[2].GetProperty("documents").EnumerateArray().Select(x => x.ValueKind == JsonValueKind.Null ? null : x.GetString()), Is.EqualTo(new[] { "", null, "new" }));
	}

	// The sparse vectors of a text that goes are deleted too, unless the caller gives them; an empty text has no vector.
	[Test]
	public async Task SparseVectorsOfATextThatGoes()
	{
		var server = new FakeServer("""{"ids":["a","b"],"metadatas":[{"title":"T","title_bm25":1,"doc_bm25":1},{"title":"U","title_bm25":1}],"documents":["doc a","doc b"]}""");
		await Client(server, Schema).UpsertAsync(new ChromaRecords(["a", "b"])
		{
			Embeddings = [Embedding, Embedding],
			Metadatas = [new Dictionary<string, object> { ["title"] = null! }, new Dictionary<string, object> { ["title"] = null!, ["title_bm25"] = new ChromaSparseVector([1], [0.5f]) }],
			Documents = [null!, ""],
			NullDocumentsDelete = true,
		});
		var upsert = server.Bodies[server.Paths.IndexOf("upsert")];
		var metadatas = upsert.GetProperty("metadatas");
		Assert.That(metadatas[0].GetRawText(), Is.EqualTo("""{"title":null,"doc_bm25":null,"title_bm25":null}"""));
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
		await Client(server, Schema).UpsertAsync(new ChromaRecords(["a"]) { Embeddings = [Embedding], Documents = [null!], NullDocumentsDelete = true });
		Assert.That(server.Bodies[server.Paths.IndexOf("upsert")].GetProperty("metadatas")[0].GetRawText(), Is.EqualTo("""{"doc_bm25":null}"""));
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
