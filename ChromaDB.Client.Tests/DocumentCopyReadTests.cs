using System.Net;
using System.Text.Json;
using ChromaDB.Client.Models;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

// A client with a document copy key reads an empty document without its copy as null: the client wrote it for a document deleted with
// NullDocumentsDelete. An empty document with its copy was written empty. A client with a document deletion key reads an empty document
// with its mark as null, and one without it as empty. The metadata goes with the documents, and away from the results that did not ask
// for it.
[TestFixture]
public class DocumentCopyReadTests
{
	static readonly ReadOnlyMemory<float> Embedding = new([1f, 0f]);

	[Test]
	public async Task Get()
	{
		var server = new FakeServer("""{"ids":["empty","deleted","text"],"metadatas":[{"text":""},{"k":1},null],"documents":["","","x"]}""");
		var client = Client(server).WithDocumentCopyKey("text");

		var documents = await client.GetAsync(include: ChromaGetInclude.Documents);
		Assert.That(server.Include(), Is.EquivalentTo(new[] { "metadatas", "documents" }));
		Assert.That(documents.Select(x => x.Document), Is.EqualTo(new[] { "", null, "x" }));
		Assert.That(documents.Select(x => x.Metadata), Is.All.Null);

		var both = await client.GetAsync(include: ChromaGetInclude.Metadatas | ChromaGetInclude.Documents);
		Assert.That(both.Select(x => x.Document), Is.EqualTo(new[] { "", null, "x" }));
		Assert.That(both[1].Metadata!["k"], Is.EqualTo(1L));

		var withoutKey = await Client(server).GetAsync(include: ChromaGetInclude.Documents);
		Assert.That(server.Include(), Is.EqualTo(new[] { "documents" }));
		Assert.That(withoutKey.Select(x => x.Document), Is.EqualTo(new[] { "", "", "x" }));
	}

	[Test]
	public async Task Query()
	{
		var server = new FakeServer("""{"ids":[["empty","deleted"]],"metadatas":[[{"text":""},null]],"documents":[["",""]],"distances":[[0.1,0.2]]}""");

		var results = (await Client(server).WithDocumentCopyKey("text").QueryAsync(new ChromaQuery([Embedding]) { Include = ChromaQueryInclude.Documents | ChromaQueryInclude.Distances }))[0];

		Assert.That(server.Include(), Is.EquivalentTo(new[] { "metadatas", "documents", "distances" }));
		Assert.That(results.Select(x => x.Document), Is.EqualTo(new[] { "", null }));
		Assert.That(results.Select(x => x.Metadata), Is.All.Null);
	}

	// A search selects the copy key with the documents, unless it selects all the metadata or the key itself.
	[TestCase(new[] { ChromaSearchKeys.Document, ChromaSearchKeys.Score }, new[] { ChromaSearchKeys.Document, ChromaSearchKeys.Score, "text" }, false)]
	[TestCase(new[] { ChromaSearchKeys.Document, "text" }, new[] { ChromaSearchKeys.Document, "text" }, true)]
	[TestCase(new[] { ChromaSearchKeys.Document, ChromaSearchKeys.Metadata }, new[] { ChromaSearchKeys.Document, ChromaSearchKeys.Metadata }, true)]
	public async Task Search(string[] select, string[] sent, bool metadataKept)
	{
		var server = new FakeServer("""{"ids":[["empty","deleted"]],"documents":[["",""]],"metadatas":[[{"text":""},{}]],"scores":[[0.1,0.2]]}""");

		var results = await Client(server).WithDocumentCopyKey("text").SearchAsync(new ChromaSearch { Select = select });

		var keys = server.Bodies[^1].GetProperty("searches")[0].GetProperty("select").GetProperty("keys").EnumerateArray().Select(x => x.GetString());
		Assert.That(keys, Is.EqualTo(sent));
		Assert.That(results.Select(x => x.Document), Is.EqualTo(new[] { "", null }));
		Assert.That(results[0].Metadata is not null, Is.EqualTo(metadataKept));
	}

	// The mark is true under the key: an empty document with it reads as null, in a get, a query and a search, and a search selects the
	// key with the documents.
	[Test]
	public async Task DocumentDeletionKey()
	{
		var get = new FakeServer("""{"ids":["empty","deleted","text","unmarked"],"metadatas":[null,{"deleted":true},{"deleted":true},{"deleted":false}],"documents":["","","x",""]}""");
		var documents = await Client(get).WithDocumentDeletionKey("deleted").GetAsync(include: ChromaGetInclude.Documents);
		Assert.That(get.Include(), Is.EquivalentTo(new[] { "metadatas", "documents" }));
		Assert.That(documents.Select(x => x.Document), Is.EqualTo(new[] { "", null, "x", "" }));
		Assert.That(documents.Select(x => x.Metadata), Is.All.Null);

		var query = new FakeServer("""{"ids":[["empty","deleted"]],"metadatas":[[null,{"deleted":true}]],"documents":[["",""]],"distances":[[0.1,0.2]]}""");
		var results = (await Client(query).WithDocumentDeletionKey("deleted").QueryAsync(new ChromaQuery([Embedding]) { Include = ChromaQueryInclude.Documents }))[0];
		Assert.That(results.Select(x => x.Document), Is.EqualTo(new[] { "", null }));

		var search = new FakeServer("""{"ids":[["empty","deleted"]],"documents":[["",""]],"metadatas":[[{},{"deleted":true}]],"scores":[[0.1,0.2]]}""");
		var found = await Client(search).WithDocumentDeletionKey("deleted").SearchAsync(new ChromaSearch { Select = [ChromaSearchKeys.Document] });
		var keys = search.Bodies[^1].GetProperty("searches")[0].GetProperty("select").GetProperty("keys").EnumerateArray().Select(x => x.GetString());
		Assert.That(keys, Is.EqualTo(new[] { ChromaSearchKeys.Document, "deleted" }));
		Assert.That(found.Select(x => x.Document), Is.EqualTo(new[] { "", null }));
		Assert.That(() => Client(get).WithDocumentDeletionKey(null!), Throws.ArgumentNullException);
	}

	// With empty document deletions a client reads the documents as they are, without the metadata.
	[Test]
	public async Task EmptyDocumentDeletions()
	{
		var server = new FakeServer("""{"ids":["empty","text"],"documents":["","x"]}""");
		var documents = await Client(server).WithEmptyDocumentDeletions().GetAsync(include: ChromaGetInclude.Documents);
		Assert.That(server.Include(), Is.EqualTo(new[] { "documents" }));
		Assert.That(documents.Select(x => x.Document), Is.EqualTo(new[] { "", "x" }));
	}

	// The copies of a client by name go to the collection it reads.
	[Test]
	public async Task ByName()
	{
		var server = new FakeServer("""{"ids":[]}""");
		using var httpClient = new HttpClient(server);
		var client = new ChromaClient(new ChromaConfigurationOptions("http://localhost:8000"), httpClient).GetCollectionClient("c").WithDocumentCopyKey("text");

		await client.AddAsync(new ChromaRecords(["a"]) { Embeddings = [Embedding], Documents = ["x"] });

		Assert.That(server.Paths, Does.Contain("c"));
		Assert.That(server.Bodies[server.Paths.IndexOf("add")].GetProperty("metadatas")[0].GetRawText(), Is.EqualTo("""{"text":"x"}"""));
		Assert.That(() => client.WithDocumentCopyKey(null!), Throws.ArgumentNullException);
	}

	// A collection client of a client that infers metadata values can read them exactly, and keeps its document copy key.
	[Test]
	public async Task MetadataValues()
	{
		var server = new FakeServer("""{"ids":["a"],"metadatas":[{"date":"2026-10-04"}],"documents":[""]}""");
		using var httpClient = new HttpClient(server);
		var inferred = new ChromaClient(new ChromaConfigurationOptions("http://localhost:8000").WithMetadataValues(ChromaMetadataValues.Inferred), httpClient)
			.GetCollectionClient("c").WithDocumentCopyKey("text");

		var exact = (await inferred.WithMetadataValues(ChromaMetadataValues.Exact).GetAsync()).Single();
		var asIs = (await inferred.GetAsync()).Single();

		Assert.That((exact.Metadata!["date"], exact.Document), Is.EqualTo(("2026-10-04", (string?)null)));
		Assert.That(asIs.Metadata!["date"], Is.InstanceOf<DateTime>());
	}

	static ChromaCollectionClient Client(HttpMessageHandler handler)
		=> new(new ChromaCollection("c") { Id = Guid.NewGuid() }, new ChromaConfigurationOptions("http://localhost:8000"), new HttpClient(handler));

	// Answers the version, pre-flight-checks with a batch size, the collection c, the reads with the records, and the writes with
	// nothing; records the requests.
	sealed class FakeServer(string records) : HttpMessageHandler
	{
		public List<string> Paths { get; } = [];
		public List<JsonElement> Bodies { get; } = [];

		public IEnumerable<string?> Include()
			=> Bodies[^1].GetProperty("include").EnumerateArray().Select(x => x.GetString());

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var path = request.RequestUri!.AbsolutePath;
			Paths.Add(path.Substring(path.LastIndexOf('/') + 1));
			Bodies.Add(request.Content is null ? default : JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken)).RootElement.Clone());
			var answer = path.EndsWith("/version") ? "\"1.5.9\""
				: path.EndsWith("/pre-flight-checks") ? """{"max_batch_size":100,"supports_base64_encoding":false}"""
				: path.EndsWith("/collections/c") ? """{"id":"11111111-2222-3333-4444-555555555555","name":"c"}"""
				: path.EndsWith("/get") || path.EndsWith("/query") || path.EndsWith("/search") ? records
				: "{}";
			return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer) };
		}
	}
}
