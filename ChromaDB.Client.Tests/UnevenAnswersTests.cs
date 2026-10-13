using System.Net;
using ChromaDB.Client.Models;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

// An answer to a read whose lists do not have one item for every record, as the get of Chroma 1.5.9 without the embedding of a record
// while other clients write and delete the same records: the read goes again, as after an error that can pass, and after the last
// attempt a ChromaException says what the answer had, instead of an ArgumentOutOfRangeException from the lists.
[TestFixture]
public class UnevenAnswersTests
{
	static readonly ChromaReadRetries NoDelay = new(4, TimeSpan.Zero, TimeSpan.Zero);
	static readonly string[] Reads = ["get", "get with ids", "peek", "query", "search"];

	[Test]
	public async Task TheReadGoesAgain([ValueSource(nameof(Reads))] string read)
	{
		var server = new Server(uneven: 2);
		Assert.That(await Run(Client(server, NoDelay), read), Is.EqualTo(1));
		Assert.That(server.Reads, Is.EqualTo(3));
	}

	[Test]
	public void AfterTheLastAttemptAChromaException([ValueSource(nameof(Reads))] string read)
	{
		var server = new Server(uneven: int.MaxValue);
		var ex = Assert.ThrowsAsync<ChromaException>(() => Run(Client(server, NoDelay), read));
		Assert.That(ex!.Message, Does.StartWith("The answer of the server to a ").And.Contain("one item for every record"));
		Assert.That(server.Reads, Is.EqualTo(4));
	}

	[Test]
	public void WithoutAttempts([ValueSource(nameof(Reads))] string read)
	{
		var server = new Server(uneven: int.MaxValue);
		Assert.ThrowsAsync<ChromaException>(() => Run(Client(server, ChromaReadRetries.None), read));
		Assert.That(server.Reads, Is.EqualTo(1));
	}

	// The message names the list that is short.
	[Test]
	public void TheMessage()
	{
		var ex = Assert.ThrowsAsync<ChromaException>(() => Client(new Server(uneven: int.MaxValue), ChromaReadRetries.None).GetAsync(["a"], include: ChromaGetInclude.Embeddings));
		Assert.That(ex!.Message, Is.EqualTo("The answer of the server to a get has 1 id and 0 embeddings, while it has one item for every record in each list."));
	}

	// A query has one list of each for every query embedding.
	[Test]
	public void AQueryWithoutTheListOfAnEmbedding()
	{
		var server = new FixedServer("""{"ids":[["a"]],"distances":[],"include":["distances"]}""");
		var ex = Assert.ThrowsAsync<ChromaException>(() => Client(server, ChromaReadRetries.None).QueryAsync(new ChromaQuery([new([1f, 0f])]) { Include = ChromaQueryInclude.Distances }));
		Assert.That(ex!.Message, Is.EqualTo("The answer of the server to a query has 1 lists of ids and 0 lists of distances, while it has one list of each for every query embedding."));
	}

	// A query has no null list for a query embedding with records: it is uneven, unlike a search.
	[Test]
	public void AQueryWithANullList()
	{
		var server = new FixedServer("""{"ids":[["a"]],"embeddings":[null],"include":["embeddings"]}""");
		var ex = Assert.ThrowsAsync<ChromaException>(() => Client(server, ChromaReadRetries.None).QueryAsync(new ChromaQuery([new([1f, 0f])]) { Include = ChromaQueryInclude.Embeddings }));
		Assert.That(ex!.Message, Is.EqualTo("The answer of the server to a query has 1 id and 0 embeddings, while it has one item for every record in each list."));
	}

	// A query has one list of ids for every query embedding, and a search for every search: fewer is uneven, not fewer results.
	[Test]
	public void AQueryWithoutTheIdsOfAnEmbedding()
	{
		var server = new FixedServer("""{"ids":[],"include":["distances"]}""");
		var ex = Assert.ThrowsAsync<ChromaException>(() => Client(server, ChromaReadRetries.None).QueryAsync(new ChromaQuery([new([1f, 0f])]) { Include = ChromaQueryInclude.Distances }));
		Assert.That(ex!.Message, Is.EqualTo("The answer of the server to a query has 0 lists of ids for 1 query embedding, while it has one list of each for every query embedding."));
	}

	[Test]
	public void ASearchWithoutTheIdsOfASearch()
	{
		var server = new FixedServer("""{"ids":[["a"]]}""");
		var ex = Assert.ThrowsAsync<ChromaException>(() => Client(server, ChromaReadRetries.None).SearchAsync([new ChromaSearch { Limit = 5 }, new ChromaSearch { Limit = 3 }]));
		Assert.That(ex!.Message, Is.EqualTo("The answer of the server to a search has 1 list of ids for 2 searches, while it has one list of each for every search."));
	}

	// A search answers null for a list it does not select: that answer is whole.
	[Test]
	public async Task ASearchWithANullList()
	{
		var server = new FixedServer("""{"ids":[["a"]],"documents":[null],"embeddings":[null],"metadatas":[null],"scores":[[0.5]]}""");
		Assert.That((await Client(server, ChromaReadRetries.None).SearchAsync(new ChromaSearch { Limit = 5 })).Single().Id, Is.EqualTo("a"));
	}

	// The records the read returns.
	static async Task<int> Run(ChromaCollectionClient client, string read) => read switch
	{
		"get" => (await client.GetAsync(include: ChromaGetInclude.Embeddings)).Count,
		"get with ids" => (await client.GetAsync(["a"], include: ChromaGetInclude.Embeddings | ChromaGetInclude.Metadatas)).Count,
		"peek" => (await client.PeekAsync()).Count,
		"query" => (await client.QueryAsync(new ChromaQuery([new([1f, 0f])]) { Include = ChromaQueryInclude.Distances | ChromaQueryInclude.Embeddings })).Single().Count,
		_ => (await client.SearchAsync(new ChromaSearch { Limit = 5 })).Count,
	};

	static ChromaCollectionClient Client(HttpMessageHandler handler, ChromaReadRetries retries)
		=> new(new ChromaCollection("c") { Id = Guid.NewGuid() }, new ChromaConfigurationOptions("https://api.trychroma.com").WithReadRetries(retries), new HttpClient(handler));

	// Answers every read with the same answer.
	sealed class FixedServer(string answer) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var path = request.RequestUri!.AbsolutePath;
			var last = path[(path.LastIndexOf('/') + 1)..];
			var text = last switch
			{
				"version" => "\"1.5.9\"",
				"pre-flight-checks" => """{"max_batch_size":100,"supports_base64_encoding":false}""",
				_ => answer,
			};
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(text) });
		}
	}

	// Answers the first reads with a record and a list without its item, and then with the record whole.
	sealed class Server(int uneven) : HttpMessageHandler
	{
		public int Reads { get; private set; }

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var path = request.RequestUri!.AbsolutePath;
			var last = path[(path.LastIndexOf('/') + 1)..];
			var bad = last is "get" or "query" or "search" && ++Reads <= uneven;
			var answer = last switch
			{
				"version" => "\"1.5.9\"",
				"pre-flight-checks" => """{"max_batch_size":100,"supports_base64_encoding":false}""",
				"get" => bad
					? """{"ids":["a"],"embeddings":[],"metadatas":[{"k":1}],"documents":["d"],"include":["embeddings","metadatas","documents"]}"""
					: """{"ids":["a"],"embeddings":[[1.0,0.0]],"metadatas":[{"k":1}],"documents":["d"],"include":["embeddings","metadatas","documents"]}""",
				"query" => bad
					? """{"ids":[["a"]],"distances":[[]],"embeddings":[[[1.0,0.0]]],"include":["distances","embeddings"]}"""
					: """{"ids":[["a"]],"distances":[[0.5]],"embeddings":[[[1.0,0.0]]],"include":["distances","embeddings"]}""",
				"search" => bad
					? """{"ids":[["a"]],"scores":[[]]}"""
					: """{"ids":[["a"]],"scores":[[0.5]]}""",
				_ => "{}",
			};
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer) });
		}
	}
}
