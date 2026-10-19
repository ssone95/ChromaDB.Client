using System.Net;
using System.Text.Json;
using ChromaDB.Client.Models;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class BatchesAndFiltersTests
{
	static readonly ReadOnlyMemory<float> Embedding = new([1f, 0f]);
	static readonly ChromaConfigurationOptions Options = new("http://localhost:8000");

	static ChromaRecords FiveRecords() => new(["a", "b", "c", "d", "e"])
	{
		Embeddings = Enumerable.Repeat(Embedding, 5).ToList(),
		Metadatas = Enumerable.Range(1, 5).Select(i => new Dictionary<string, object> { ["i"] = i }).ToList(),
		Documents = ["1", "2", "3", "4", "5"],
		Uris = ["u1", null, "u3", null, "u5"],
	};

	// With WithBatchSplitting(false) the records go in one request; pre-flight-checks is asked only for the base64 of the embeddings.
	[Test]
	public async Task OneRequestWithoutBatchSplitting()
	{
		var handler = new Handler("""{"max_batch_size":2}""");
		await Client(Options.WithBatchSplitting(false), handler).AddAsync(FiveRecords());
		Assert.That(handler.Paths.Select(Last), Is.EqualTo(new[] { "pre-flight-checks", "add" }));
		Assert.That(handler.Bodies.Single().GetProperty("ids").GetArrayLength(), Is.EqualTo(5));
	}

	// Batch splitting is on by default.
	[Test]
	public async Task BatchesByDefault()
	{
		var handler = new Handler("""{"max_batch_size":2}""");
		await Client(new ChromaConfigurationOptions("http://localhost:8000"), handler).AddAsync(FiveRecords());
		Assert.That(handler.Bodies.Select(b => b.GetProperty("ids").GetArrayLength()), Is.EqualTo(new[] { 2, 2, 1 }));
	}

	[Test]
	public async Task BatchesOfTheMaxBatchSize()
	{
		var handler = new Handler("""{"max_batch_size":2}""");
		await Client(Options.WithBatchSplitting(), handler).AddAsync(FiveRecords());
		Assert.That(handler.Paths.Select(Last), Is.EqualTo(new[] { "pre-flight-checks", "add", "add", "add" }));
		Assert.That(handler.Bodies.Select(b => Strings(b, "ids")), Is.EqualTo(new[] { new[] { "a", "b" }, new[] { "c", "d" }, new[] { "e" } }));
		Assert.That(handler.Bodies.Select(b => Strings(b, "documents")), Is.EqualTo(new[] { new[] { "1", "2" }, new[] { "3", "4" }, new[] { "5" } }));
		Assert.That(handler.Bodies.Select(b => Strings(b, "uris")), Is.EqualTo(new[] { new[] { "u1", null }, new[] { "u3", null }, new[] { "u5" } }));
		Assert.That(handler.Bodies.Select(b => b.GetProperty("metadatas").EnumerateArray().Select(m => m.GetProperty("i").GetInt32())), Is.EqualTo(new[] { new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5 } }));
		Assert.That(handler.Bodies.Select(b => b.GetProperty("embeddings").GetArrayLength()), Is.EqualTo(new[] { 2, 2, 1 }));
	}

	// The limit is asked once for all the calls of a client.
	[Test]
	public async Task UpdateUpsertAndDeleteToo()
	{
		var handler = new Handler("""{"max_batch_size":2}""");
		var client = Client(Options.WithBatchSplitting(), handler);
		await client.UpdateAsync(FiveRecords());
		await client.UpsertAsync(FiveRecords());
		await client.DeleteAsync(["a", "b", "c"], where: ChromaWhereOperator.Equal("k", 1));
		Assert.That(handler.Paths.Select(Last), Is.EqualTo(new[] { "pre-flight-checks", "update", "update", "update", "upsert", "upsert", "upsert", "delete", "delete" }));
		Assert.That(handler.Bodies.Skip(6).Select(b => Strings(b, "ids")), Is.EqualTo(new[] { new[] { "a", "b" }, new[] { "c" } }));
		Assert.That(handler.Bodies.Skip(6).Select(b => b.GetProperty("where").GetRawText()), Is.All.EqualTo("""{"k":{"$eq":1}}"""));
	}

	[Test]
	public async Task OneRequestWithinTheLimit()
	{
		var handler = new Handler("""{"max_batch_size":5}""");
		await Client(Options.WithBatchSplitting(), handler).AddAsync(FiveRecords());
		Assert.That(handler.Paths.Select(Last), Is.EqualTo(new[] { "pre-flight-checks", "add" }));
	}

	// Chroma 0.4.10 has no pre-flight-checks: the records go in one request.
	[Test]
	public async Task OneRequestWithoutPreFlightChecks()
	{
		var handler = new Handler(null);
		await Client(Options.WithBatchSplitting(), handler).AddAsync(FiveRecords());
		Assert.That(handler.Paths.Select(Last), Is.EqualTo(new[] { "pre-flight-checks", "add" }));
		Assert.That(handler.Bodies.Single().GetProperty("ids").GetArrayLength(), Is.EqualTo(5));
	}

	// A failed batch stops the others; the earlier batches stay written.
	[Test]
	public async Task FailedBatchStops()
	{
		var handler = new Handler("""{"max_batch_size":2}""") { FailAt = 2 };
		Assert.ThrowsAsync<ChromaException>(() => Client(Options.WithBatchSplitting(), handler).AddAsync(FiveRecords()));
		Assert.That(handler.Paths.Select(Last), Is.EqualTo(new[] { "pre-flight-checks", "add", "add" }));
	}

	[Test]
	public void OtherOptionsKeepBatchSplitting()
		=> Assert.That(Options.WithBatchSplitting().WithTenant("t").WithMetadataValues(ChromaMetadataValues.Exact).BatchSplitting, Is.True);

	[Test]
	public async Task FiltersAsTheJsonTheClientSends()
	{
		var where = ChromaWhereOperator.Equal("k", 1) & (ChromaWhereOperator.In("t", "a", "b") | ChromaWhereOperator.Contains("tags", "x"));
		var whereDocument = ChromaWhereDocumentOperator.Contains("foo") | ChromaWhereDocumentOperator.NotContains("bar");
		Assert.That(where.ToString(), Is.EqualTo("""{"$and":[{"k":{"$eq":1}},{"$or":[{"t":{"$in":["a","b"]}},{"tags":{"$contains":"x"}}]}]}"""));
		Assert.That(whereDocument.ToString(), Is.EqualTo("""{"$or":[{"$contains":"foo"},{"$not_contains":"bar"}]}"""));

		var handler = new Handler("""{"ids":[]}""");
		await Client(Options, handler).GetAsync(where: where, whereDocument: whereDocument);
		Assert.That(handler.Bodies.Single().GetProperty("where").GetRawText(), Is.EqualTo(where.ToString()));
		Assert.That(handler.Bodies.Single().GetProperty("where_document").GetRawText(), Is.EqualTo(whereDocument.ToString()));
	}

	// A filter given as a dictionary goes in the request as it is.
	[Test]
	public async Task FilterFromDictionaryAsItIs()
	{
		var where = ChromaWhereOperator.FromDictionary(new Dictionary<string, object>
		{
			["$and"] = new object[]
			{
				new Dictionary<string, object> { ["k"] = new Dictionary<string, object> { ["$eq"] = 1 } },
				new Dictionary<string, object> { ["t"] = new Dictionary<string, object> { ["$in"] = new object[] { "a", "b" } } },
			},
		});

		var handler = new Handler("""{"ids":[]}""");
		await Client(Options, handler).GetAsync(where: where);
		Assert.That(handler.Bodies.Single().GetProperty("where").GetRawText(), Is.EqualTo("""{"$and":[{"k":{"$eq":1}},{"t":{"$in":["a","b"]}}]}"""));
		Assert.Throws<ArgumentNullException>(() => ChromaWhereOperator.FromDictionary(null!));
	}

	// A chain of the same operator goes as one list, as the Python client writes it: nested, a chain of 32 filters went beyond the
	// 64 levels of System.Text.Json.
	[Test]
	public async Task ChainsOfTheSameOperatorInOneList()
	{
		ChromaWhereOperator Eq(int i) => ChromaWhereOperator.Equal("i", i);
		Assert.That((Eq(1) & Eq(2) & Eq(3)).ToString(), Is.EqualTo("""{"$and":[{"i":{"$eq":1}},{"i":{"$eq":2}},{"i":{"$eq":3}}]}"""));
		Assert.That((Eq(1) & (Eq(2) & Eq(3))).ToString(), Is.EqualTo((Eq(1) & Eq(2) & Eq(3)).ToString()));
		Assert.That(((Eq(1) | Eq(2)) & Eq(3) & (Eq(4) | Eq(5) | Eq(6))).ToString(),
			Is.EqualTo("""{"$and":[{"$or":[{"i":{"$eq":1}},{"i":{"$eq":2}}]},{"i":{"$eq":3}},{"$or":[{"i":{"$eq":4}},{"i":{"$eq":5}},{"i":{"$eq":6}}]}]}"""));

		// A filter used in two chains stays as it was.
		var both = Eq(1) & Eq(2);
		Assert.That(((both & Eq(3)).ToString(), (both | Eq(4)).ToString(), both.ToString()), Is.EqualTo((
			"""{"$and":[{"i":{"$eq":1}},{"i":{"$eq":2}},{"i":{"$eq":3}}]}""",
			"""{"$or":[{"$and":[{"i":{"$eq":1}},{"i":{"$eq":2}}]},{"i":{"$eq":4}}]}""",
			"""{"$and":[{"i":{"$eq":1}},{"i":{"$eq":2}}]}""")));

		ChromaWhereOperator Chain(int count, Func<ChromaWhereOperator, ChromaWhereOperator, ChromaWhereOperator> combine)
		{
			var chain = Eq(0);
			for (var i = 1; i < count; i++)
			{
				chain = combine(chain, Eq(i));
			}
			return chain;
		}
		// Up to 900 deep, the depth of the SQLite expression of a single server, one list; beyond, ⌈√n⌉ lists with the same filters in
		// the same order.
		Assert.That(Lists(Chain(900, (x, y) => x & y).ToString()), Is.EqualTo(new[] { 900 }));
		Assert.That(Lists(Chain(901, (x, y) => x & y).ToString()), Is.EqualTo(new[] { 31, 30, 30 }.Concat(Enumerable.Repeat(29, 29)).ToArray()));
		var and = Chain(10_000, (x, y) => x & y);
		Assert.That(Lists(and.ToString()), Is.EqualTo(new[] { 100 }.Concat(Enumerable.Repeat(100, 100)).ToArray()));
		Assert.That(Leaves(and.ToString()), Is.EqualTo(Enumerable.Range(0, 10_000)));
		var or = Chain(10_000, (x, y) => x || y);
		Assert.That(JsonDocument.Parse(or.ToString()).RootElement.GetProperty("$or")[99].GetProperty("$or")[99].GetRawText(), Is.EqualTo("""{"i":{"$eq":9999}}"""));
		Assert.That(Leaves(or.ToString()), Is.EqualTo(Enumerable.Range(0, 10_000)));
		// Two lists of 600 under an $or are 601 deep: they go as they are.
		var lists = Chain(600, (x, y) => x & y) | Chain(600, (x, y) => x & y);
		Assert.That(Lists(lists.ToString()), Is.EqualTo(new[] { 2, 600, 600 }));

		var document = ChromaWhereDocumentOperator.Contains("w0");
		for (var i = 1; i < 10_000; i++)
		{
			document &= ChromaWhereDocumentOperator.Contains($"w{i}");
		}
		Assert.That(Lists(document.ToString()), Is.EqualTo(new[] { 100 }.Concat(Enumerable.Repeat(100, 100)).ToArray()));

		var handler = new Handler("""{"ids":[]}""");
		await Client(Options, handler).GetAsync(where: and, whereDocument: document);
		Assert.That((handler.Bodies.Single().GetProperty("where").GetRawText(), handler.Bodies.Single().GetProperty("where_document").GetRawText()),
			Is.EqualTo((and.ToString(), document.ToString())));
	}

	// The lengths of the $and and $or lists of a filter, depth first.
	static int[] Lists(string filter)
	{
		var lengths = new List<int>();
		void Walk(JsonElement element)
		{
			if (element.ValueKind == JsonValueKind.Object && element.EnumerateObject().ToList() is [{ Name: "$and" or "$or" } list])
			{
				lengths.Add(list.Value.GetArrayLength());
				foreach (var item in list.Value.EnumerateArray())
				{
					Walk(item);
				}
			}
		}
		Walk(JsonDocument.Parse(filter).RootElement);
		return [.. lengths];
	}

	// The values of the {"i":{"$eq":n}} filters, in their order.
	static int[] Leaves(string filter)
		=> [.. System.Text.RegularExpressions.Regex.Matches(filter, """\{"i":\{"\$eq":(\d+)\}\}""").Select(x => int.Parse(x.Groups[1].Value))];

	// Every tested Chroma rejects $in and $nin without values: they are None and All, which FilterConstantsTests tests.
	[Test]
	public void InAndNotIn()
	{
		Assert.That(ChromaWhereOperator.In("k", 1).ToString(), Is.EqualTo("""{"k":{"$in":[1]}}"""));
		Assert.That(ChromaWhereOperator.NotIn("k", "a").ToString(), Is.EqualTo("""{"k":{"$nin":["a"]}}"""));
	}

	static string Last(string path) => path.Substring(path.LastIndexOf('/') + 1);

	static string?[] Strings(JsonElement body, string property)
		=> body.GetProperty(property).EnumerateArray().Select(x => x.GetString()).ToArray();

	static ChromaCollectionClient Client(ChromaConfigurationOptions options, HttpMessageHandler handler)
		=> new(new ChromaCollection("collection") { Id = Guid.NewGuid() }, options, new HttpClient(handler));

	// Answers pre-flight-checks with the given body, or 404 when it is null, and the other requests with true.
	sealed class Handler(string? preFlightChecks) : HttpMessageHandler
	{
		public List<string> Paths { get; } = [];
		public List<JsonElement> Bodies { get; } = [];
		public int FailAt { get; init; } = -1;
		int _records;

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Paths.Add(request.RequestUri!.AbsolutePath);
			if (request.RequestUri.AbsolutePath.EndsWith("/pre-flight-checks"))
			{
				return preFlightChecks is null
					? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{"detail":"Not Found"}""") }
					: new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(preFlightChecks) };
			}
			Bodies.Add(JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement.Clone());
			if (++_records == FailAt)
			{
				return new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("""{"error":"InternalError","message":"failed"}""") };
			}
			return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(request.RequestUri.AbsolutePath.EndsWith("/get") ? """{"ids":[]}""" : "true") };
		}
	}
}
