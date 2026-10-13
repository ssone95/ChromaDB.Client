using System.Net;
using System.Text.Json;
using ChromaDB.Client.Models;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

// A collection client made by name reads the collection before its first request, and again when a request fails on the id it read
// before: when the name has another id now, the operation runs again, once, on that collection.
[TestFixture]
public class CollectionClientByNameTests
{
	const string First = "11111111-1111-1111-1111-111111111111";
	const string Second = "22222222-2222-2222-2222-222222222222";
	const string Missing = """{"error":"NotFoundError","message":"Collection [11111111-1111-1111-1111-111111111111] does not exist."}""";

	[Test]
	public async Task ReadsTheCollectionOnce()
	{
		var server = new FakeServer(First);
		var collection = Client(server).GetCollectionClient("c");
		Assert.That((collection.Collection.Name, collection.Collection.Id), Is.EqualTo(("c", Guid.Empty)));
		Assert.That(server.Requests, Is.Empty);
		Assert.That(await collection.CountAsync(), Is.EqualTo(3));
		Assert.That(await collection.CountAsync(), Is.EqualTo(3));
		Assert.That(server.Requests, Is.EqualTo(new[] { "GET collections/c", $"GET collections/{First}/count", $"GET collections/{First}/count" }));
		Assert.That(collection.Collection.Id, Is.EqualTo(Guid.Parse(First)));
	}

	// The collection was deleted and created again elsewhere: the client reads it again and counts on the new one.
	[Test]
	public async Task ReadsTheCollectionAgainWhenItsIdIsGone()
	{
		var server = new FakeServer(First);
		var collection = Client(server).GetCollectionClient("c");
		await collection.CountAsync();
		server.Id = Second;
		server.MissingId = First;
		Assert.That(await collection.CountAsync(), Is.EqualTo(3));
		Assert.That(server.Requests.Skip(2), Is.EqualTo(new[] { $"GET collections/{First}/count", "GET collections/c", $"GET collections/{Second}/count" }));
		Assert.That(collection.Collection.Id, Is.EqualTo(Guid.Parse(Second)));
	}

	// A write that fails after some of its batches went is not run again on a collection created again under the name: those batches
	// stay in the collection that was there, and the exception says how many records went.
	[Test]
	public async Task APartialWriteIsNotRunAgain()
	{
		var server = new FakeServer(First);
		server.OnRequest = path =>
		{
			if (path.EndsWith("/add"))
			{
				(server.Id, server.MissingId) = (Second, First);
			}
		};
		var client = new ChromaClient(new ChromaConfigurationOptions("http://localhost:8000").WithBatchSplitting(1), new HttpClient(server));
		var collection = client.GetCollectionClient("c");
		await collection.CountAsync();
		await Assert.ThatAsync(() => collection.AddAsync(["a", "b"], [new([1f]), new([2f])]), Throws.InstanceOf<ChromaException>().With.Message.Contains("1 of the 2 records"));
		Assert.That(server.Requests.Where(x => x.EndsWith("/add")), Is.EqualTo(new[] { $"POST collections/{First}/add", $"POST collections/{First}/add" }));
	}

	// An id just read is not read again, and an id that stays the same keeps the failure.
	[Test]
	public async Task TheSameIdKeepsTheFailure()
	{
		var server = new FakeServer(First) { MissingId = First };
		var collection = Client(server).GetCollectionClient("c");
		await Assert.ThatAsync(() => collection.CountAsync(), Throws.InstanceOf<ChromaException>().With.Message.Contains("does not exist"));
		Assert.That(server.Requests, Is.EqualTo(new[] { "GET collections/c", $"GET collections/{First}/count" }));
		await Assert.ThatAsync(() => collection.CountAsync(), Throws.InstanceOf<ChromaException>().With.Message.Contains("does not exist"));
		Assert.That(server.Requests.Skip(2), Is.EqualTo(new[] { $"GET collections/{First}/count", "GET collections/c" }));
	}

	// Any failure counts, as the servers tell a collection gone in their own ways: Chroma 0.4 answers 500 with "coroutine raised StopIteration".
	// Without attempts, as a read goes again after a 500 before (ReadRetriesTests).
	[Test]
	public async Task AnyFailureReadsTheCollectionAgain()
	{
		var server = new FakeServer(First);
		var collection = Client(server, ChromaReadRetries.None).GetCollectionClient("c");
		await collection.CountAsync();
		server.MissingId = First;
		server.MissingAnswer = (HttpStatusCode.InternalServerError, """{"error":"RuntimeError('coroutine raised StopIteration')"}""");
		await Assert.ThatAsync(() => collection.CountAsync(), Throws.InstanceOf<ChromaException>().With.Message.EqualTo("coroutine raised StopIteration"));
		server.Id = Second;
		Assert.That(await collection.CountAsync(), Is.EqualTo(3));
		Assert.That(server.Requests.Skip(2), Is.EqualTo(new[] { $"GET collections/{First}/count", "GET collections/c", $"GET collections/{First}/count", "GET collections/c", $"GET collections/{Second}/count" }));
	}

	// When the name has no collection either, the failure of the operation stands.
	[Test]
	public async Task NoCollectionOfTheNameKeepsTheFailure()
	{
		var server = new FakeServer(First);
		var collection = Client(server).GetCollectionClient("c");
		await collection.CountAsync();
		server.MissingId = First;
		server.NameMissing = true;
		await Assert.ThatAsync(() => collection.CountAsync(), Throws.InstanceOf<ChromaException>().With.Message.Contains(First));
		Assert.That(server.Requests.Skip(2), Is.EqualTo(new[] { $"GET collections/{First}/count", "GET collections/c" }));
	}

	// Also an operation without a result, like an upsert, runs again on the collection of the name.
	[Test]
	public async Task OperationsWithoutAResult()
	{
		var server = new FakeServer(First);
		var collection = Client(server).GetCollectionClient("c");
		await collection.DeleteAsync(["a"]);
		server.Id = Second;
		server.MissingId = First;
		await collection.DeleteAsync(["a"]);
		Assert.That(server.Requests.Where(x => x.EndsWith("/delete")), Is.EqualTo(new[] { $"POST collections/{First}/delete", $"POST collections/{First}/delete", $"POST collections/{Second}/delete" }));
	}

	[Test]
	public async Task GetCollectionAsync()
	{
		var server = new FakeServer(First);
		var client = Client(server);
		var byName = client.GetCollectionClient("c");
		Assert.That((await byName.GetCollectionAsync()).Id, Is.EqualTo(Guid.Parse(First)));
		Assert.That((await byName.GetCollectionAsync()).Id, Is.EqualTo(Guid.Parse(First)));
		var byId = client.GetCollectionClient(Guid.Parse(Second), "c");
		Assert.That((await byId.GetCollectionAsync()).Id, Is.EqualTo(Guid.Parse(Second)));
		Assert.That(server.Requests, Is.EqualTo(new[] { "GET collections/c" }));
	}

	// The operation runs again from the records given: what the client computed for the collection that is gone, like the sparse
	// vectors of its schema, does not go to the new one.
	[Test]
	public async Task RunsAgainFromTheRecordsGiven()
	{
		var server = new FakeServer(First) { Schema = Bm25Schema };
		var collection = Client(server).GetCollectionClient("c");
		await collection.CountAsync();
		server.Id = Second;
		server.Schema = null;
		server.MissingId = First;
		await collection.UpsertAsync(new ChromaRecords(["a"]) { Embeddings = [new([1f, 0f])], Documents = ["apple pie"] });
		var upserts = server.Bodies.Where(x => x.Path.EndsWith("/upsert")).Select(x => x.Body).ToList();
		Assert.That(server.Bodies.Where(x => x.Path.EndsWith("/upsert")).Select(x => x.Path), Is.EqualTo(new[] { $"collections/{First}/upsert", $"collections/{Second}/upsert" }));
		Assert.That(upserts[0].GetProperty("metadatas")[0].TryGetProperty("doc_bm25", out _), Is.True);
		Assert.That(upserts[1].GetProperty("metadatas").ValueKind, Is.EqualTo(JsonValueKind.Null));
	}

	const string Bm25Schema = """{"defaults":{},"keys":{"doc_bm25":{"sparse_vector":{"sparse_vector_index":{"enabled":true,"config":{"embedding_function":{"type":"known","name":"chroma_bm25","config":{}},"source_key":"#document"}}}}}}""";

	static ChromaClient Client(HttpMessageHandler handler, ChromaReadRetries? retries = null)
		=> new(new ChromaConfigurationOptions("http://localhost:8000").WithBatchSplitting(false).WithReadRetries(retries ?? ChromaReadRetries.Default), new HttpClient(handler));

	// Answers the collection c with Id and the space l2, as Chroma 1.5.9 reports it, or as missing with NameMissing, MissingAnswer to the requests on MissingId, and 3 for a count;
	// records the requests, without the prefix of the database.
	sealed class FakeServer(string id) : HttpMessageHandler
	{
		public string Id { get; set; } = id;
		public string? MissingId { get; set; }
		public (HttpStatusCode Status, string Body) MissingAnswer { get; set; } = (HttpStatusCode.NotFound, Missing);
		public bool NameMissing { get; set; }
		public string? Schema { get; set; }
		public Action<string>? OnRequest { get; set; }
		public List<string> Requests { get; } = [];
		public List<(string Path, JsonElement Body)> Bodies { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var path = request.RequestUri!.AbsolutePath.Replace("/api/v2/tenants/default_tenant/databases/default_database/", "");
			Requests.Add($"{request.Method} {path}");
			if (request.Content is not null)
			{
				Bodies.Add((path, JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken)).RootElement.Clone()));
			}
			var (status, body) = path == "collections/c" && NameMissing ? (HttpStatusCode.NotFound, """{"error":"NotFoundError","message":"Collection [c] does not exist"}""")
				: path == "collections/c" ? (HttpStatusCode.OK, $$$"""{"id":"{{{Id}}}","name":"c","configuration_json":{"hnsw":{"space":"l2"}},"schema":{{{Schema ?? "null"}}}}""")
				: MissingId is { } missing && path.Contains(missing) ? MissingAnswer
				: path.EndsWith("/count") ? (HttpStatusCode.OK, "3")
				: path.EndsWith("/version") ? (HttpStatusCode.OK, "\"1.5.9\"")
				: (HttpStatusCode.OK, "{}");
			// After the answer: a change of the server shows from the next request.
			OnRequest?.Invoke(path);
			return new HttpResponseMessage(status) { Content = new StringContent(body) };
		}
	}
}
