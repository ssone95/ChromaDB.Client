using System.Net;
using ChromaDB.Client.Models;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

// A read changes nothing, so after an error that can pass it goes again, with the exponential backoff with full jitter of AWS: Chroma
// 1.5.9 answers 500 to a get with embeddings while other clients write and delete the same records. The errors that can pass are an
// answer 429, 500, 502, 503 or 504 and a request that fails on the network; a missing collection, an answer 4xx and a write never go
// again. ChromaConfigurationOptions.ReadRetries sets the attempts and the delays, and ChromaReadRetries.None turns them off.
[TestFixture]
public class ReadRetriesTests
{
	const string Cloud = "https://api.trychroma.com";
	const string Local = "http://localhost:8000";
	const string FindingId = """{"error":"InternalError","message":"Error executing plan: Internal error: Error finding id"}""";
	const string SegmentReader = """{"error":"InternalError","message":"Error executing plan: Internal error: Error creating hnsw segment reader: Nothing found on disk"}""";
	static readonly ChromaReadRetries NoDelay = new(4, TimeSpan.Zero, TimeSpan.Zero);
	static readonly string[] Reads = ["get", "get with ids", "peek", "query", "search", "count", "count at a read level"];

	[Test]
	public async Task AReadGoesAgainAfterAnErrorOfTheServer([ValueSource(nameof(Reads))] string read, [Values(FindingId, SegmentReader)] string error)
	{
		var server = new Server(fail: 2, (HttpStatusCode.InternalServerError, error));
		await Run(Client(server, Cloud, NoDelay), read);
		Assert.That(server.Reads, Is.EqualTo(3));
	}

	[TestCase(429)]
	[TestCase(500)]
	[TestCase(502)]
	[TestCase(503)]
	[TestCase(504)]
	public async Task TheStatusesThatCanPass(int status)
	{
		var server = new Server(fail: 1, ((HttpStatusCode)status, "{}"));
		await Run(Client(server, Local, NoDelay), "get");
		Assert.That(server.Reads, Is.EqualTo(2));
	}

	[Test]
	public async Task ARequestThatFailsOnTheNetwork()
	{
		var server = new Server(fail: 1, default) { Throw = true };
		await Run(Client(server, Local, NoDelay), "get");
		Assert.That(server.Reads, Is.EqualTo(2));
	}

	// The error of the last attempt stands.
	[Test]
	public void AfterTheLastAttemptTheErrorStands([ValueSource(nameof(Reads))] string read)
	{
		var server = new Server(fail: int.MaxValue, (HttpStatusCode.InternalServerError, FindingId));
		var ex = Assert.ThrowsAsync<ChromaException>(() => Run(Client(server, Cloud, NoDelay), read));
		Assert.That(ex!.Message, Does.Contain("Error finding id"));
		Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
		Assert.That(server.Reads, Is.EqualTo(4));
	}

	[TestCase(400)]
	[TestCase(404)]
	[TestCase(422)]
	public void AClientErrorDoesNotGoAgain(int status)
	{
		var server = new Server(fail: int.MaxValue, ((HttpStatusCode)status, """{"error":"InvalidArgumentError","message":"wrong"}"""));
		Assert.ThrowsAsync<ChromaException>(() => Run(Client(server, Local, NoDelay), "get"));
		Assert.That(server.Reads, Is.EqualTo(1));
	}

	// Chroma 0.x answers 500 for a collection that does not exist.
	[Test]
	public void AMissingCollectionDoesNotGoAgain()
	{
		var server = new Server(fail: int.MaxValue, (HttpStatusCode.InternalServerError, """{"error":"InvalidCollection","message":"Collection c does not exist."}"""));
		Assert.ThrowsAsync<ChromaException>(() => Run(Client(server, Local, NoDelay), "get"));
		Assert.That(server.Reads, Is.EqualTo(1));
	}

	// The get that a write makes first goes again, as the one of SkipUnchangedEmbeddings; the write goes once.
	[Test]
	public async Task TheReadOfAWriteGoesAgain()
	{
		var server = new Server(fail: 1, (HttpStatusCode.InternalServerError, FindingId));
		var options = new ChromaConfigurationOptions(Local).WithReadRetries(NoDelay).WithUpsertStrategy(ChromaUpsertStrategy.SkipUnchangedEmbeddings);
		var client = new ChromaCollectionClient(new ChromaCollection("c") { Id = Guid.NewGuid() }, options, new HttpClient(server));
		await client.UpsertAsync(new ChromaRecords(["a"]) { Embeddings = [new([1f, 0f])] });
		Assert.That(server.Reads, Is.EqualTo(2));
		Assert.That(server.Paths.Count(x => x == "upsert"), Is.EqualTo(1));
	}

	// A timeout of the HttpClient does not go again.
	[Test]
	public void ATimeoutDoesNotGoAgain()
	{
		var server = new Server(fail: 0, default) { Hang = TimeSpan.FromSeconds(30) };
		var client = new ChromaCollectionClient(new ChromaCollection("c") { Id = Guid.NewGuid() }, new ChromaConfigurationOptions(Local).WithReadRetries(NoDelay), new HttpClient(server) { Timeout = TimeSpan.FromMilliseconds(200) });
		var ex = Assert.ThrowsAsync<ChromaException>(() => client.GetAsync());
		Assert.That(ex!.InnerException, Is.InstanceOf<TaskCanceledException>());
		Assert.That(server.Reads, Is.EqualTo(1));
	}

	// A write that failed may have gone: it never goes again.
	[TestCase("add")]
	[TestCase("update")]
	[TestCase("upsert")]
	[TestCase("delete")]
	public void AWriteDoesNotGoAgain(string write)
	{
		var server = new Server(fail: int.MaxValue, (HttpStatusCode.InternalServerError, FindingId)) { Failing = write };
		var client = Client(server, Local, NoDelay);
		var records = new ChromaRecords(["a"]) { Embeddings = [new([1f, 0f])] };
		Assert.ThrowsAsync<ChromaException>(() => write switch
		{
			"add" => client.AddAsync(records),
			"update" => client.UpdateAsync(records),
			"upsert" => client.UpsertAsync(records),
			_ => client.DeleteAsync(["a"]),
		});
		Assert.That(server.Writes, Is.EqualTo(1));
	}

	[Test]
	public void NoneTurnsThemOff()
	{
		var server = new Server(fail: 1, (HttpStatusCode.InternalServerError, FindingId));
		Assert.ThrowsAsync<ChromaException>(() => Run(Client(server, Local, ChromaReadRetries.None), "get"));
		Assert.That(server.Reads, Is.EqualTo(1));
	}

	// The default goes again too, with its delays.
	[Test]
	public async Task TheDefault()
	{
		var server = new Server(fail: 1, (HttpStatusCode.InternalServerError, FindingId));
		await new ChromaCollectionClient(new ChromaCollection("c") { Id = Guid.NewGuid() }, new ChromaConfigurationOptions(Local), new HttpClient(server)).GetAsync();
		Assert.That(server.Reads, Is.EqualTo(2));
		Assert.That(new ChromaConfigurationOptions(Local).ReadRetries, Is.SameAs(ChromaReadRetries.Default));
		Assert.That((ChromaReadRetries.Default.MaxAttempts, ChromaReadRetries.Default.BaseDelay, ChromaReadRetries.Default.MaxDelay), Is.EqualTo((4, TimeSpan.FromMilliseconds(50), TimeSpan.FromSeconds(1))));
		Assert.That((ChromaReadRetries.None.MaxAttempts, ChromaReadRetries.None.BaseDelay, ChromaReadRetries.None.MaxDelay), Is.EqualTo((1, TimeSpan.Zero, TimeSpan.Zero)));
	}

	// The setting stays through the other options.
	[Test]
	public void TheSettingStays()
		=> Assert.That(new ChromaConfigurationOptions(Local).WithReadRetries(ChromaReadRetries.None).WithTenant("t").WithBatchSplitting(false).ReadRetries, Is.SameAs(ChromaReadRetries.None));

	// And through WithMetadataValues of a client made by name, as a provider of Microsoft.Extensions.VectorData makes it.
	[Test]
	public void TheSettingStaysThroughWithMetadataValues()
	{
		var server = new Server(fail: int.MaxValue, (HttpStatusCode.InternalServerError, FindingId));
		var client = new ChromaClient(new ChromaConfigurationOptions(Local).WithReadRetries(ChromaReadRetries.None), new HttpClient(server));
		var collection = client.GetCollectionClient("c").WithMetadataValues(ChromaMetadataValues.Exact);
		Assert.ThrowsAsync<ChromaException>(() => collection.GetAsync());
		Assert.That(server.Reads, Is.EqualTo(1));
	}

	// Full jitter: a random time between zero and the smaller of the largest delay and the base times 2 to the attempts before.
	[TestCase(0, 0.0, 0)]
	[TestCase(0, 0.5, 50)]
	[TestCase(0, 1.0, 100)]
	[TestCase(1, 1.0, 200)]
	[TestCase(3, 1.0, 800)]
	[TestCase(4, 1.0, 1000)]
	[TestCase(4, 0.5, 500)]
	[TestCase(62, 1.0, 1000)]
	[TestCase(1000, 1.0, 1000)]
	public void TheDelay(int retry, double random, int milliseconds)
		=> Assert.That(new ChromaReadRetries(5, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)).Delay(retry, random), Is.EqualTo(TimeSpan.FromMilliseconds(milliseconds)));

	[TestCase(0, 0, 0)]
	[TestCase(1, -1, 0)]
	[TestCase(1, 0, -1)]
	[TestCase(1, 10, 5)]
	public void WrongSettings(int attempts, int baseDelay, int maxDelay)
		=> Assert.That(() => new ChromaReadRetries(attempts, TimeSpan.FromMilliseconds(baseDelay), TimeSpan.FromMilliseconds(maxDelay)), Throws.InstanceOf<ArgumentOutOfRangeException>());

	// A cancellation during the delay after a failed attempt stops the read at once: no other attempt goes.
	[Test]
	public void ACancellationBetweenTheAttempts()
	{
		using var cancellation = new CancellationTokenSource();
		var server = new Server(fail: 1, (HttpStatusCode.InternalServerError, FindingId)) { OnFail = () => cancellation.CancelAfter(TimeSpan.FromMilliseconds(200)) };
		var client = Client(server, Local, new ChromaReadRetries(2, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10)));
		var watch = System.Diagnostics.Stopwatch.StartNew();
		Assert.That(() => client.GetAsync(cancellationToken: cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
		Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)), "the delay stops at the cancellation");
		Assert.That(server.Reads, Is.EqualTo(1));
	}

	static Task Run(ChromaCollectionClient client, string read) => read switch
	{
		"get" => client.GetAsync(),
		"get with ids" => client.GetAsync(["a"], include: ChromaGetInclude.Embeddings),
		"peek" => client.PeekAsync(),
		"query" => client.QueryAsync(new ChromaQuery([new([1f, 0f])])),
		"search" => client.SearchAsync(new ChromaSearch { Limit = 5 }),
		"count" => client.CountAsync(),
		_ => client.CountAsync(ChromaReadLevel.IndexOnly),
	};

	static ChromaCollectionClient Client(HttpMessageHandler handler, string uri, ChromaReadRetries retries)
		=> new(new ChromaCollection("c") { Id = Guid.NewGuid() }, new ChromaConfigurationOptions(uri).WithReadRetries(retries), new HttpClient(handler));

	// Answers the first requests of the reads, or of the write named in Failing, with the error, or throws, and then with an empty result.
	sealed class Server(int fail, (HttpStatusCode Status, string Body) error) : HttpMessageHandler
	{
		static readonly string[] ReadPaths = ["get", "query", "search", "count"];
		public string? Failing { get; init; }
		public bool Throw { get; init; }
		public Action? OnFail { get; init; }
		public TimeSpan Hang { get; init; }
		public List<string> Paths { get; } = [];
		public int Reads { get; private set; }
		public int Writes { get; private set; }

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var path = request.RequestUri!.AbsolutePath;
			var last = path[(path.LastIndexOf('/') + 1)..];
			Paths.Add(last);
			var counted = Failing is null ? ReadPaths.Contains(last) : last == Failing;
			var count = counted ? Failing is null ? ++Reads : ++Writes : 0;
			if (counted && Hang > TimeSpan.Zero)
			{
				await Task.Delay(Hang, cancellationToken);
			}
			if (counted && count <= fail)
			{
				OnFail?.Invoke();
				if (Throw)
				{
					throw new HttpRequestException("The connection was reset.");
				}
				return new HttpResponseMessage(error.Status) { Content = new StringContent(error.Body) };
			}
			var answer = last switch
			{
				"version" => "\"1.5.9\"",
				"pre-flight-checks" => """{"max_batch_size":100,"supports_base64_encoding":false}""",
				"get" => """{"ids":[],"include":[]}""",
				"query" => """{"ids":[[]],"include":[]}""",
				"search" => """{"ids":[[]]}""",
				"count" => "0",
				"delete" => """{"deleted":0}""",
				"c" => """{"id":"11111111-2222-3333-4444-555555555555","name":"c"}""",
				_ => "{}",
			};
			return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer) };
		}
	}
}
