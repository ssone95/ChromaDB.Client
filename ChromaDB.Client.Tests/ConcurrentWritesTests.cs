using ChromaDB.Client.Models;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

// Two writes of the same record at the same time leave the record of one of them: a write that deletes a key does not depend on a read
// that another writer can change before the write goes.
[TestFixture]
public class ConcurrentWritesTests : ChromaTestsBase
{
	static readonly ReadOnlyMemory<float> Embedding = new([1f, 0f]);

	// The write of B lands after A started and before A writes: A writes last, so the record is the one A wrote, without the key that A
	// deletes and that B added.
	[TestCase(true)]
	[TestCase(false)]
	public async Task AWriteThatDeletesAKeyAfterAnotherThatAddsIt(bool upsert)
	{
		var options = BaseConfigurationOptions.WithMetadataValues(ChromaMetadataValues.Exact);
		var b = new ChromaClient(options, HttpClient).GetCollectionClient(await new ChromaClient(options, HttpClient).CreateCollectionAsync($"collection{Random.Shared.Next()}"));
		await b.AddAsync(new ChromaRecords(["r"]) { Embeddings = [Embedding], Metadatas = [new Dictionary<string, object> { ["s"] = "0" }] });
		var hook = new BeforeTheWrite(() => b.UpsertAsync(new ChromaRecords(["r"]) { Embeddings = [Embedding], Metadatas = [new Dictionary<string, object> { ["s"] = "B", ["x"] = 2L }] }));
		using var httpClient = new HttpClient(hook);
		var a = new ChromaCollectionClient(b.Collection, options, httpClient);
		var deletion = new Dictionary<string, object> { ["s"] = "A", ["x"] = null! };
		await (upsert
			? a.UpsertAsync(new ChromaRecords(["r"]) { Embeddings = [Embedding], Metadatas = [deletion] })
			: a.UpdateAsync(new ChromaRecords(["r"]) { Metadatas = [deletion] }));
		Assert.That(hook.Ran, Is.True);
		Assert.That((await b.GetAsync("r", include: ChromaGetInclude.Metadatas))!.Metadata, Is.EquivalentTo(new Dictionary<string, object> { ["s"] = "A" }));
	}

	// With a document copy key, the write of B, which gives the record a document, lands after A started and before A writes: A writes
	// last, so the record is the one A wrote, without a document.
	[TestCase(true)]
	[TestCase(false)]
	public async Task AWriteThatDeletesTheDocumentAfterAnotherThatWritesOne(bool upsert)
	{
		var options = BaseConfigurationOptions.WithMetadataValues(ChromaMetadataValues.Exact);
		var collection = await new ChromaClient(options, HttpClient).CreateCollectionAsync($"collection{Random.Shared.Next()}");
		var b = new ChromaCollectionClient(collection, options, HttpClient).WithDocumentCopyKey("text");
		await b.AddAsync(new ChromaRecords(["r"]) { Embeddings = [Embedding], Metadatas = [new Dictionary<string, object> { ["s"] = "0" }] });
		var hook = new BeforeTheWrite(() => b.UpsertAsync(new ChromaRecords(["r"]) { Embeddings = [Embedding], Metadatas = [new Dictionary<string, object> { ["s"] = "B" }], Documents = ["B"] }));
		using var httpClient = new HttpClient(hook);
		var a = new ChromaCollectionClient(collection, options, httpClient).WithDocumentCopyKey("text");
		var deletion = new ChromaRecords(["r"])
		{
			Embeddings = upsert ? [Embedding] : null,
			Metadatas = [new Dictionary<string, object> { ["s"] = "A" }],
			Documents = [null!],
			NullDocumentsDelete = true,
		};
		await (upsert ? a.UpsertAsync(deletion) : a.UpdateAsync(deletion));
		Assert.That(hook.Ran, Is.True);
		var record = (await b.GetAsync("r", include: ChromaGetInclude.Metadatas | ChromaGetInclude.Documents))!;
		Assert.That(record.Metadata, Is.EquivalentTo(new Dictionary<string, object> { ["s"] = "A" }));
		Assert.That(record.Document, Is.Null);
	}

	// Writes of the same record at the same time, half of them deleting the key that the others add: the record left is one of the two.
	[Test]
	public async Task ConcurrentWritesLeaveOneOfThem()
	{
		var options = BaseConfigurationOptions.WithMetadataValues(ChromaMetadataValues.Exact);
		var client = new ChromaClient(options, HttpClient);
		var collection = client.GetCollectionClient(await client.CreateCollectionAsync($"collection{Random.Shared.Next()}"));
		var mixed = new List<string>();
		for (var round = 0; round < 20; round++)
		{
			await collection.UpsertAsync(new ChromaRecords(["r"]) { Embeddings = [Embedding], Metadatas = [new Dictionary<string, object> { ["s"] = "0" }] });
			await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() => collection.UpsertAsync(new ChromaRecords(["r"])
			{
				Embeddings = [Embedding],
				Metadatas = [i % 2 == 0 ? new Dictionary<string, object> { ["s"] = "A", ["x"] = null! } : new Dictionary<string, object> { ["s"] = "B", ["x"] = 2L }],
			}))));
			var metadata = (await collection.GetAsync("r", include: ChromaGetInclude.Metadatas))!.Metadata!;
			if (!(metadata["s"] is "A" && !metadata.ContainsKey("x") || metadata["s"] is "B" && metadata.TryGetValue("x", out var x) && x is 2L))
			{
				mixed.Add($"round {round}: {string.Join(", ", metadata.Select(pair => $"{pair.Key}={pair.Value}"))}");
			}
		}
		Assert.That(mixed, Is.Empty);
	}

	// Runs the other write once, just before the first write of the client goes: after the read that the client may do first.
	sealed class BeforeTheWrite(Func<Task> otherWrite) : DelegatingHandler(new HttpClientHandler())
	{
		public bool Ran { get; private set; }

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var path = request.RequestUri!.AbsolutePath;
			if (!Ran && (path.EndsWith("/upsert", StringComparison.Ordinal) || path.EndsWith("/update", StringComparison.Ordinal)))
			{
				Ran = true;
				await otherWrite();
			}
			return await base.SendAsync(request, cancellationToken);
		}
	}
}
