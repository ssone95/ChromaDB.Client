using System.Net;
using System.Text.Json;
using ChromaDB.Client.Models;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

// Chroma 1.5 keeps the lists in the metadata of the records of a deleted collection or database, and gives them to the next records
// it stores: with deleteRecordsFirst, on Chroma 1.x the client deletes the records first, a page of the batch size at a time.
[TestFixture]
public class DeleteRecordsFirstTests
{
	const string Id = "11111111-2222-3333-4444-555555555555";
	const string OtherId = "66666666-7777-8888-9999-000000000000";

	// By default each deletion is one request, as in Chroma.
	[Test]
	public async Task OneRequestByDefault()
	{
		var server = new FakeServer("1.0.0", "{}");
		var client = Client(server);
		await client.DeleteCollectionAsync("c");
		await client.DeleteCollectionIfExistsAsync("c");
		await client.DeleteDatabaseAsync("d");
		Assert.That(server.Requests.Select(x => x.Request), Is.EqualTo(new[]
		{
			"DELETE tenants/default_tenant/databases/default_database/collections/c",
			"DELETE tenants/default_tenant/databases/default_database/collections/c",
			"DELETE tenants/default_tenant/databases/d",
		}));
	}

	[Test]
	public async Task DeleteCollectionDeletesTheRecordsFirst()
	{
		var server = new FakeServer("1.0.0", $$"""{"id":"{{Id}}","name":"c","tenant":"t2","database":"d2"}""", """["a","b"]""", """["c"]""", "[]");
		await Client(server).DeleteCollectionAsync("c", tenant: "t2", database: "d2", deleteRecordsFirst: true);
		var records = $"tenants/t2/databases/d2/collections/{Id}";
		Assert.That(server.Requests.Select(x => x.Request), Is.EqualTo(new[]
		{
			"GET version",
			"GET tenants/t2/databases/d2/collections/c",
			"GET pre-flight-checks",
			$"POST {records}/get",
			$"POST {records}/delete",
			$"POST {records}/get",
			$"POST {records}/delete",
			$"POST {records}/get",
			"DELETE tenants/t2/databases/d2/collections/c",
		}));
		var get = server.Requests[3].Body;
		Assert.That(get.GetProperty("limit").GetInt32(), Is.EqualTo(2));
		Assert.That(get.GetProperty("include").EnumerateArray(), Is.Empty);
		Assert.That(Ids(server.Requests[4].Body), Is.EqualTo(new[] { "a", "b" }));
		Assert.That(Ids(server.Requests[6].Body), Is.EqualTo(new[] { "c" }));
	}

	[Test]
	public async Task DeleteCollectionOnChroma0()
	{
		var server = new FakeServer("0.6.3", "{}");
		await Client(server).DeleteCollectionAsync("c", deleteRecordsFirst: true);
		Assert.That(server.Requests.Select(x => x.Request), Is.EqualTo(new[] { "GET version", "DELETE tenants/default_tenant/databases/default_database/collections/c" }));
	}

	[Test]
	public async Task DeleteCollectionOnChromaCloud()
	{
		var server = new FakeServer("1.0.0", "{}");
		await Client(server, "https://api.trychroma.com").DeleteCollectionAsync("c", deleteRecordsFirst: true);
		Assert.That(server.Requests.Select(x => x.Request), Is.EqualTo(new[] { "DELETE tenants/default_tenant/databases/default_database/collections/c" }));
	}

	// A page that comes back after its delete would come back forever: the client throws, and the collection stays with its records.
	[Test]
	public async Task RecordsThatStayStopTheDeletes()
	{
		var server = new FakeServer("1.0.0", $$"""{"id":"{{Id}}","name":"c"}""", """["a"]""", """["a"]""");
		await Assert.ThatAsync(() => Client(server).DeleteCollectionAsync("c", deleteRecordsFirst: true), Throws.InstanceOf<ChromaException>().With.Message.Contains("not deleted"));
		var records = $"tenants/default_tenant/databases/default_database/collections/{Id}";
		Assert.That(server.Requests.Select(x => x.Request), Is.EqualTo(new[]
		{
			"GET version",
			"GET tenants/default_tenant/databases/default_database/collections/c",
			"GET pre-flight-checks",
			$"POST {records}/get",
			$"POST {records}/delete",
			$"POST {records}/get",
		}));
	}

	[Test]
	public async Task DeleteDatabaseDeletesTheRecordsOfEachCollection()
	{
		var server = new FakeServer("1.0.0", $$"""[{"id":"{{Id}}","name":"x","tenant":"t","database":"d"},{"id":"{{OtherId}}","name":"y","tenant":"t","database":"d"}]""",
			"""["a"]""", "[]", """["b"]""", "[]");
		await Client(server).DeleteDatabaseAsync("d", tenant: "t", deleteRecordsFirst: true);
		Assert.That(server.Requests.Select(x => x.Request), Is.EqualTo(new[]
		{
			"GET version",
			"GET tenants/t/databases/d/collections",
			"GET pre-flight-checks",
			$"POST tenants/t/databases/d/collections/{Id}/get",
			$"POST tenants/t/databases/d/collections/{Id}/delete",
			$"POST tenants/t/databases/d/collections/{Id}/get",
			$"POST tenants/t/databases/d/collections/{OtherId}/get",
			$"POST tenants/t/databases/d/collections/{OtherId}/delete",
			$"POST tenants/t/databases/d/collections/{OtherId}/get",
			"DELETE tenants/t/databases/d",
		}));
	}

	[Test]
	public async Task DeleteDatabaseOnChroma0()
	{
		var server = new FakeServer("0.6.3", "{}");
		await Client(server).DeleteDatabaseAsync("d", deleteRecordsFirst: true);
		Assert.That(server.Requests.Select(x => x.Request), Is.EqualTo(new[] { "GET version", "DELETE tenants/default_tenant/databases/d" }));
	}

	// A collection client made by name whose collection is created again under the name after the first page: the error says how many
	// records went, and no delete reaches the new collection.
	[Test]
	public async Task DeleteAllStopsOnACollectionCreatedAgain()
	{
		var server = new RecreatedServer();
		var collectionClient = Client(server).GetCollectionClient("c");
		await collectionClient.CountAsync();
		await Assert.ThatAsync(() => collectionClient.DeleteAllAsync(), Throws.InstanceOf<ChromaException>().With.Message.StartsWith("2 records were deleted before the error"));
		Assert.That(server.Requests.Where(x => x.EndsWith("/delete")), Is.EqualTo(new[] { $"POST tenants/default_tenant/databases/default_database/collections/{Id}/delete" }));
	}

	// Chroma Cloud rejects a get with a limit beyond its quota of 300, "'Limit value'": a client with WithBatchSplitting(1000) learns it and
	// reads that page again, in GetAsync and in DeleteAllAsync, and no delete goes twice.
	[Test]
	public async Task TheQuotaOfTheReads()
	{
		var server = new QuotaServer(350);
		var client = new ChromaCollectionClient(new ChromaCollection("c") { Id = Guid.Parse(Id) }, new ChromaConfigurationOptions("https://api.trychroma.com").WithBatchSplitting(1000), new HttpClient(server));
		Assert.That((await client.GetAsync(include: ChromaGetInclude.None)).Count, Is.EqualTo(350));
		Assert.That(server.Limits, Is.EqualTo(new[] { 1000, 300, 300 }));

		var fresh = new ChromaCollectionClient(new ChromaCollection("c") { Id = Guid.Parse(Id) }, new ChromaConfigurationOptions("https://api.trychroma.com").WithBatchSplitting(1000), new HttpClient(server));
		server.Limits.Clear();
		Assert.That(await fresh.DeleteAllAsync(), Is.EqualTo(350));
		Assert.That(server.Limits, Is.EqualTo(new[] { 1000, 300, 300, 300 }));
		Assert.That(server.Deleted, Is.EqualTo(Enumerable.Range(0, 350).Select(i => $"r{i}")));
	}

	// Chroma Cloud rejects a get of more ids than its quota: with a limit beyond it, "'Limit value'", and without a limit, "'Number of
	// records'". A client with WithBatchSplitting(1000) learns it and reads the ids in batches.
	[TestCase(350)]
	[TestCase(null)]
	public async Task TheQuotaOfAReadByIds(int? limit)
	{
		var server = new QuotaServer(350);
		var client = new ChromaCollectionClient(new ChromaCollection("c") { Id = Guid.Parse(Id) }, new ChromaConfigurationOptions("https://api.trychroma.com").WithBatchSplitting(1000), new HttpClient(server));
		var ids = Enumerable.Range(0, 350).Select(i => $"r{i}").ToList();
		Assert.That((await client.GetAsync(ids, limit: limit, include: ChromaGetInclude.None)).Select(x => x.Id), Is.EqualTo(ids));
		Assert.That(server.IdCounts, Is.EqualTo(new[] { 350, 300, 50 }));
	}

	// More ids than the batch size go in batches; the first, beyond the quota of Chroma Cloud, gets 422: the client learns the quota and
	// reads that batch again, smaller, keeping the batches read.
	[Test]
	public async Task TheQuotaOfAReadByIdsInBatches()
	{
		var server = new QuotaServer(1001);
		var client = new ChromaCollectionClient(new ChromaCollection("c") { Id = Guid.Parse(Id) }, new ChromaConfigurationOptions("https://api.trychroma.com").WithBatchSplitting(1000), new HttpClient(server));
		var ids = Enumerable.Range(0, 1001).Select(i => $"r{i}").ToList();
		Assert.That((await client.GetAsync(ids, include: ChromaGetInclude.None)).Select(x => x.Id), Is.EqualTo(ids));
		Assert.That(server.IdCounts, Is.EqualTo(new[] { 1000, 300, 300, 300, 101 }));
	}

	// A tenant of Chroma Cloud whose quota of the reads is higher than the one of the deletes: the delete of a page beyond it gets 422 and
	// deletes nothing, and the client reads a smaller page. Every record goes, each id once.
	[Test]
	public async Task TheQuotaOfTheDeletes()
	{
		var server = new QuotaServer(350, readQuota: 1000, deleteQuota: 300);
		var client = new ChromaCollectionClient(new ChromaCollection("c") { Id = Guid.Parse(Id) }, new ChromaConfigurationOptions("https://api.trychroma.com").WithBatchSplitting(1000), new HttpClient(server));
		Assert.That(await client.DeleteAllAsync(), Is.EqualTo(350));
		Assert.That(server.Limits, Is.EqualTo(new[] { 1000, 300, 300, 300 }));
		Assert.That(server.Deleted, Is.EqualTo(Enumerable.Range(0, 350).Select(i => $"r{i}")));
	}

	// Holds the records r0 to r{count - 1}, answers a get with a limit beyond the quota of the reads, and a delete of more ids than the
	// quota of the deletes, with the 422 of Chroma Cloud, and records the limits asked and the ids deleted.
	sealed class QuotaServer(int count, int readQuota = 300, int deleteQuota = 300) : HttpMessageHandler
	{
		readonly List<string> _ids = Enumerable.Range(0, count).Select(i => $"r{i}").ToList();

		public List<int> Limits { get; } = [];
		public List<int> IdCounts { get; } = [];
		public List<string> Deleted { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var path = request.RequestUri!.AbsolutePath;
			var body = request.Content is null ? default : JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken)).RootElement.Clone();
			if (path.EndsWith("/pre-flight-checks"))
			{
				return Answer(HttpStatusCode.OK, """{"max_batch_size":1000}""");
			}
			if (path.EndsWith("/get"))
			{
				int? limit = body.TryGetProperty("limit", out var asked) && asked.ValueKind == JsonValueKind.Number ? asked.GetInt32() : null;
				var offset = body.TryGetProperty("offset", out var start) && start.ValueKind == JsonValueKind.Number ? start.GetInt32() : 0;
				var requested = body.TryGetProperty("ids", out var given) && given.ValueKind == JsonValueKind.Array ? given.EnumerateArray().Select(x => x.GetString()!).ToList() : null;
				if (limit is { } value)
				{
					Limits.Add(value);
				}
				if (requested is not null)
				{
					IdCounts.Add(requested.Count);
				}
				if (limit > readQuota)
				{
					return Answer((HttpStatusCode)422, $$"""{"error":"ChromaError","message":"Quota exceeded: 'Limit value' exceeded quota limit for action 'Get': current usage of {{limit}} exceeds limit of {{readQuota}}. https://trychroma.com/request-quota-increase"}""");
				}
				if (requested is not null && limit is null && requested.Count > readQuota)
				{
					return Answer((HttpStatusCode)422, $$"""{"error":"ChromaError","message":"Quota exceeded: 'Number of records' exceeded quota limit for action 'Get': current usage of {{requested.Count}} exceeds limit of {{readQuota}}. https://trychroma.com/request-quota-increase"}""");
				}
				var found = requested is null ? _ids : _ids.Where(requested.Contains).ToList();
				return Answer(HttpStatusCode.OK, JsonSerializer.Serialize(new { ids = found.Skip(offset).Take(limit ?? int.MaxValue).ToList() }));
			}
			if (path.EndsWith("/delete"))
			{
				var ids = Ids(body).Select(x => x!).ToList();
				if (ids.Count > deleteQuota)
				{
					return Answer((HttpStatusCode)422, $$"""{"error":"ChromaError","message":"Quota exceeded: 'Number of records' exceeded quota limit for action 'Delete': current usage of {{ids.Count}} exceeds limit of {{deleteQuota}}. https://trychroma.com/request-quota-increase"}""");
				}
				Deleted.AddRange(ids);
				_ids.RemoveAll(ids.Contains);
				return Answer(HttpStatusCode.OK, "{}");
			}
			return Answer(HttpStatusCode.OK, "\"1.5.9\"");
		}

		static HttpResponseMessage Answer(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body) };
	}

	static ChromaClient Client(HttpMessageHandler handler, string uri = "http://localhost:8000")
		=> new(new ChromaConfigurationOptions(uri), new HttpClient(handler));

	static IEnumerable<string?> Ids(JsonElement body) => body.GetProperty("ids").EnumerateArray().Select(x => x.GetString());

	// Answers the version, the collection or the collections with found, pre-flight-checks with a batch size of 2, and each get with
	// the next page of ids; records the requests, without the prefix of the API.
	// The collection c has the id Id until its second page, which answers 404: from then on c is a new collection, with OtherId.
	sealed class RecreatedServer : HttpMessageHandler
	{
		bool _recreated;
		int _pages;

		public List<string> Requests { get; } = [];

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var path = request.RequestUri!.AbsolutePath.Substring("/api/v2/".Length);
			Requests.Add($"{request.Method} {path}");
			var (status, answer) = path switch
			{
				"version" => (HttpStatusCode.OK, "\"1.0.0\""),
				"pre-flight-checks" => (HttpStatusCode.OK, """{"max_batch_size":2}"""),
				_ when path.EndsWith("/count") => (HttpStatusCode.OK, "4"),
				_ when path.EndsWith("/delete") => (HttpStatusCode.OK, "{}"),
				_ when path.EndsWith("/get") && path.Contains(OtherId) => (HttpStatusCode.OK, """{"ids":["x","y"]}"""),
				_ when path.EndsWith("/get") && _pages++ == 0 => (HttpStatusCode.OK, """{"ids":["a","b"]}"""),
				_ when path.EndsWith("/get") => Recreate(),
				_ => (HttpStatusCode.OK, $$"""{"id":"{{(_recreated ? OtherId : Id)}}","name":"c"}"""),
			};
			return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(answer) });
		}

		(HttpStatusCode, string) Recreate()
		{
			_recreated = true;
			return (HttpStatusCode.NotFound, """{"error":"NotFoundError","message":"Collection does not exist."}""");
		}
	}

	sealed class FakeServer(string version, string found, params string[] pages) : HttpMessageHandler
	{
		int _page;

		public List<(string Request, JsonElement Body)> Requests { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var path = request.RequestUri!.AbsolutePath.Substring("/api/v2/".Length);
			var body = request.Content is null ? default : JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken)).RootElement.Clone();
			Requests.Add(($"{request.Method} {path}", body));
			var answer = path switch
			{
				"version" => $"\"{version}\"",
				"pre-flight-checks" => """{"max_batch_size":2}""",
				_ when path.EndsWith("/get") => $$"""{"ids":{{pages[_page++]}}}""",
				_ when path.EndsWith("/delete") => "{}",
				_ => found,
			};
			return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer) };
		}
	}
}
