using System.Net;
using System.Text.Json;
using ChromaDB.Client.Models;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class QueryIdsTests
{
	static readonly ReadOnlyMemory<float> Embedding = new([1f, 0f]);

	[Test]
	public async Task QuerySendsIds()
	{
		var handler = new RecordingHandler("""{"ids":[["a"]]}""");
		await Client(handler).QueryAsync(new ChromaQuery([Embedding]) { Ids = ["a", "c"] });
		Assert.That(handler.Body.GetProperty("ids").EnumerateArray().Select(x => x.GetString()), Is.EqualTo(new[] { "a", "c" }));
	}

	[Test]
	public async Task QueryWithoutIdsDoesNotSendThem()
	{
		var handler = new RecordingHandler("""{"ids":[["a"]]}""");
		await Client(handler).QueryAsync(Embedding);
		Assert.That(handler.Body.TryGetProperty("ids", out _), Is.False);
	}

	[Test]
	public async Task QuerySendsTheOtherFields()
	{
		var handler = new RecordingHandler("""{"ids":[["a"]]}""");
		await Client(handler).QueryAsync(new ChromaQuery([Embedding]) { NResults = 3, Where = ChromaWhereOperator.Equal("k", 1), WhereDocument = ChromaWhereDocumentOperator.Contains("x"), Include = ChromaQueryInclude.Documents });
		Assert.That(handler.Body.GetProperty("n_results").GetInt32(), Is.EqualTo(3));
		Assert.That(handler.Body.GetProperty("where").GetProperty("k").GetProperty("$eq").GetInt32(), Is.EqualTo(1));
		Assert.That(handler.Body.GetProperty("where_document").GetProperty("$contains").GetString(), Is.EqualTo("x"));
		Assert.That(handler.Body.GetProperty("include").EnumerateArray().Select(x => x.GetString()), Is.EqualTo(new[] { "documents" }));
	}

	// Chroma 0.x ignores the ids: a result outside them must not reach the caller.
	[Test]
	public async Task ResultOutsideTheIdsThrows()
	{
		var handler = new RecordingHandler("""{"ids":[["a"],["a","b"]]}""");
		await Assert.ThatAsync(() => Client(handler).QueryAsync(new ChromaQuery([Embedding, Embedding]) { Ids = ["a", "c"] }), Throws.InstanceOf<ChromaException>().With.Message.Contains("outside the ids"));
	}

	[Test]
	public async Task ResultsAmongTheIdsAreReturned()
	{
		var handler = new RecordingHandler("""{"ids":[["c","a"]]}""");
		var result = await Client(handler).QueryAsync(new ChromaQuery([Embedding]) { Ids = ["a", "c"] });
		Assert.That(result.Single().Select(x => x.Id), Is.EqualTo(new[] { "c", "a" }));
	}

	// An empty list keeps no record: any result is outside it.
	[Test]
	public async Task EmptyIdsWithResultsThrows()
	{
		var handler = new RecordingHandler("""{"ids":[["a"]]}""");
		await Assert.ThatAsync(() => Client(handler).QueryAsync(new ChromaQuery([Embedding]) { Ids = [] }), Throws.InstanceOf<ChromaException>());
	}

	// Chroma has no offset in queries: the client asks for the skipped records too, and leaves them out.
	[Test]
	public async Task OffsetAsksForTheSkippedRecordsAndLeavesThemOut()
	{
		var handler = new RecordingHandler("""{"ids":[["a","b","c"],["d","e"]]}""");
		var result = await Client(handler).QueryAsync(new ChromaQuery([Embedding, Embedding]) { NResults = 2, Offset = 1 });
		Assert.That(handler.Body.GetProperty("n_results").GetInt32(), Is.EqualTo(3));
		Assert.That(result.Select(entries => entries.Select(x => x.Id)), Is.EqualTo(new[] { new[] { "b", "c" }, new[] { "e" } }));
	}

	// The results and the offset go together in n_results, which cannot go beyond the largest int.
	[Test]
	public async Task OffsetBeyondTheLargestNumberOfResultsThrows()
	{
		var handler = new RecordingHandler("""{"ids":[["a"]]}""");
		await Assert.ThatAsync(() => Client(handler).QueryAsync(new ChromaQuery([Embedding]) { NResults = int.MaxValue, Offset = 1 }), Throws.InstanceOf<ArgumentOutOfRangeException>());
		Assert.That(handler.Body.ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Undefined));
	}

	[Test]
	public void NegativeOffsetThrows()
		=> Assert.That(() => new ChromaQuery([Embedding]) { Offset = -1 }, Throws.InstanceOf<ArgumentOutOfRangeException>());

	const string FindingIdError = """{"error":"InternalError","message":"Error executing plan: Internal error: Error finding id"}""";

	// Chroma 1.x fails on an id without a record: the query goes again with the ids the server has, in their order.
	[Test]
	public async Task MissingIdIsLeftOut()
	{
		var handler = new ScriptedHandler("""{"ids":["c","a"]}""", (HttpStatusCode.InternalServerError, FindingIdError), (HttpStatusCode.OK, """{"ids":[["c","a"]]}"""));
		var result = await Client(handler).QueryAsync(new ChromaQuery([Embedding]) { Ids = ["a", "missing", "c"], NResults = 3, Where = ChromaWhereOperator.Equal("k", 1), Include = ChromaQueryInclude.Distances });
		Assert.That(result.Single().Select(x => x.Id), Is.EqualTo(new[] { "c", "a" }));
		Assert.That(handler.Requests.Select(x => x.Path), Is.EqualTo(new[] { "query", "pre-flight-checks", "get", "query" }));
		var get = handler.Requests[2].Body;
		Assert.That(get.GetProperty("ids").EnumerateArray().Select(x => x.GetString()), Is.EqualTo(new[] { "a", "missing", "c" }));
		Assert.That(get.GetProperty("include").EnumerateArray(), Is.Empty);
		Assert.That(get.TryGetProperty("where", out var where) && where.ValueKind != JsonValueKind.Null, Is.False);
		var retry = handler.Requests[3].Body;
		Assert.That(retry.GetProperty("ids").EnumerateArray().Select(x => x.GetString()), Is.EqualTo(new[] { "a", "c" }));
		Assert.That(retry.GetProperty("n_results").GetInt32(), Is.EqualTo(3));
		Assert.That(retry.GetProperty("where").GetProperty("k").GetProperty("$eq").GetInt32(), Is.EqualTo(1));
		Assert.That(retry.GetProperty("include").EnumerateArray().Select(x => x.GetString()), Is.EqualTo(new[] { "distances" }));
	}

	[Test]
	public async Task OnlyMissingIdsHaveNoResults()
	{
		var handler = new ScriptedHandler("""{"ids":[]}""", (HttpStatusCode.InternalServerError, FindingIdError));
		var result = await Client(handler).QueryAsync(new ChromaQuery([Embedding, Embedding]) { Ids = ["missing"] });
		Assert.That(result, Has.Count.EqualTo(2));
		Assert.That(result, Has.All.Empty);
		Assert.That(handler.Requests.Select(x => x.Path), Is.EqualTo(new[] { "query", "pre-flight-checks", "get" }));
	}

	// When every id has a record, the error has another cause, which can pass: the query goes again with ReadRetries, and without
	// attempts the error stands at once.
	[Test]
	public async Task InternalErrorWithAllTheIdsIsThrown()
	{
		var handler = new ScriptedHandler("""{"ids":["a","c"]}""", (HttpStatusCode.InternalServerError, FindingIdError));
		await Assert.ThatAsync(() => Client(handler, ChromaReadRetries.None).QueryAsync(new ChromaQuery([Embedding]) { Ids = ["a", "c"] }),
			Throws.InstanceOf<ChromaException>().With.Message.Contains("Error finding id").And.Property(nameof(ChromaException.StatusCode)).EqualTo(HttpStatusCode.InternalServerError));
		Assert.That(handler.Requests.Select(x => x.Path), Is.EqualTo(new[] { "query", "pre-flight-checks", "get" }));
	}

	[Test]
	public async Task InternalErrorWithAllTheIdsGoesAgain()
	{
		var handler = new ScriptedHandler("""{"ids":["a","c"]}""", (HttpStatusCode.InternalServerError, FindingIdError), (HttpStatusCode.InternalServerError, FindingIdError), (HttpStatusCode.OK, """{"ids":[["c","a"]]}"""));
		var result = await Client(handler, new ChromaReadRetries(4, TimeSpan.Zero, TimeSpan.Zero)).QueryAsync(new ChromaQuery([Embedding]) { Ids = ["a", "c"] });
		Assert.That(result.Single().Select(x => x.Id), Is.EqualTo(new[] { "c", "a" }));
		Assert.That(handler.Requests.Select(x => x.Path), Is.EqualTo(new[] { "query", "pre-flight-checks", "get", "query", "query" }));
	}

	// Without ids, the error goes again with ReadRetries (ReadRetriesTests), and without attempts it stands at once.
	[Test]
	public async Task InternalErrorWithoutIdsIsThrown()
	{
		var handler = new ScriptedHandler("""{"ids":[]}""", (HttpStatusCode.InternalServerError, FindingIdError));
		await Assert.ThatAsync(() => Client(handler, ChromaReadRetries.None).QueryAsync(Embedding), Throws.InstanceOf<ChromaException>().With.Message.Contains("Error finding id"));
		Assert.That(handler.Requests.Select(x => x.Path), Is.EqualTo(new[] { "query" }));
	}

	[Test]
	public async Task InternalErrorWithEmptyIdsIsThrown()
	{
		var handler = new ScriptedHandler("""{"ids":[]}""", (HttpStatusCode.InternalServerError, FindingIdError));
		await Assert.ThatAsync(() => Client(handler, ChromaReadRetries.None).QueryAsync(new ChromaQuery([Embedding]) { Ids = [] }), Throws.InstanceOf<ChromaException>().With.Message.Contains("Error finding id"));
		Assert.That(handler.Requests.Select(x => x.Path), Is.EqualTo(new[] { "query" }));
	}

	[Test]
	public async Task OtherErrorWithIdsIsThrown()
	{
		var handler = new ScriptedHandler("""{"ids":[]}""", (HttpStatusCode.BadRequest, """{"error":"InvalidArgumentError","message":"bad"}"""));
		await Assert.ThatAsync(() => Client(handler).QueryAsync(new ChromaQuery([Embedding]) { Ids = ["missing"] }), Throws.InstanceOf<ChromaException>().With.Message.EqualTo("bad"));
		Assert.That(handler.Requests.Select(x => x.Path), Is.EqualTo(new[] { "query" }));
	}

	static ChromaCollectionClient Client(HttpMessageHandler handler, ChromaReadRetries? retries = null)
	{
		var collection = new ChromaCollection("collection") { Id = Guid.NewGuid() };
		return new ChromaCollectionClient(collection, new ChromaConfigurationOptions("http://localhost:8000").WithReadRetries(retries ?? ChromaReadRetries.Default), new HttpClient(handler));
	}

	sealed class RecordingHandler(string response) : HttpMessageHandler
	{
		public JsonElement Body { get; private set; }

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement.Clone();
			return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) };
		}
	}

	// Answers the queries in turn, the gets with getResponse and pre-flight-checks with a batch size, and records the requests.
	sealed class ScriptedHandler(string getResponse, params (HttpStatusCode Status, string Body)[] queryResponses) : HttpMessageHandler
	{
		int _queries;

		public List<(string Path, JsonElement Body)> Requests { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var path = request.RequestUri!.AbsolutePath.Split('/').Last();
			var body = request.Content is null ? default : JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken)).RootElement.Clone();
			Requests.Add((path, body));
			var (status, response) = path switch
			{
				"pre-flight-checks" => (HttpStatusCode.OK, """{"max_batch_size":100}"""),
				"get" => (HttpStatusCode.OK, getResponse),
				_ => queryResponses[_queries++],
			};
			return new HttpResponseMessage(status) { Content = new StringContent(response) };
		}
	}
}
