using System.Net;
using ChromaDB.Client.Models;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

// KD-56: on Chroma Cloud a $regex on the documents whose pattern takes an empty text finds every record, also in a delete, and its
// $not_regex none. A filter whose pattern the client cannot tell needs a character throws on Chroma Cloud before any request, in every
// read and delete, also inside $and, $or and Not: one that takes an empty text, one that .NET cannot read, and one that .NET reads
// otherwise than Chroma, which reads it with the Rust crate regex. A pattern that needs a character goes, and every pattern goes on a
// single server.
[TestFixture]
public class DocumentRegexesTests
{
	const string Cloud = "https://api.trychroma.com";
	const string Local = "http://localhost:8000";
	static readonly ReadOnlyMemory<float> Embedding = new([1f, 0f]);
	static readonly string[] Operations = ["get", "get with where", "query", "delete", "delete by filters", "search", "search with where document"];

	static IEnumerable<ChromaWhereDocumentOperator> MayTakeAnEmptyText()
	{
		yield return ChromaWhereDocumentOperator.Regex("^$");
		yield return ChromaWhereDocumentOperator.Regex("^");
		yield return ChromaWhereDocumentOperator.Regex("^.*$");
		yield return ChromaWhereDocumentOperator.NotRegex("^$");
		yield return ChromaWhereDocumentOperator.Contains("x") & ChromaWhereDocumentOperator.Regex("x*");
		yield return ChromaWhereDocumentOperator.Not(ChromaWhereDocumentOperator.Regex("^$") | ChromaWhereDocumentOperator.Contains("x"));
		// Chroma 1.5.9 finds an empty document with these, while .NET reads them otherwise or not at all.
		yield return ChromaWhereDocumentOperator.Regex("^[[:alpha:]]*$");
		yield return ChromaWhereDocumentOperator.Regex("^[a[b]]*$");
		// A ']' right after '[' or '[^' is a character of the class: the POSIX class after it is inside the class.
		yield return ChromaWhereDocumentOperator.Regex("^[]a[:alpha:]]*$");
		yield return ChromaWhereDocumentOperator.Regex("^[^]a[:alpha:]]*$");
		yield return ChromaWhereDocumentOperator.Regex("^(?U)a*$");
		yield return ChromaWhereDocumentOperator.Regex("(?P<a>x*)");
		yield return ChromaWhereDocumentOperator.Regex("^\\b{start-half}$");
		// The client cannot tell: .NET cannot read the first, which Chroma reads as one that needs a character, and takes "" with the others,
		// which Chroma rejects as syntax errors.
		yield return ChromaWhereDocumentOperator.Regex("(?P<a>x)");
		yield return ChromaWhereDocumentOperator.Regex("\\Z");
		yield return ChromaWhereDocumentOperator.Regex("(?=)");
	}

	static IEnumerable<ChromaWhereDocumentOperator> NeedACharacter()
	{
		yield return ChromaWhereDocumentOperator.Regex("^x$");
		yield return ChromaWhereDocumentOperator.Regex(".");
		yield return ChromaWhereDocumentOperator.NotRegex("^.+$");
		yield return ChromaWhereDocumentOperator.NotRegex(".") & ChromaWhereDocumentOperator.Contains("");
		// A ']' right after '[' or '[^' and an escaped '[' are characters of the class, not a nested class.
		yield return ChromaWhereDocumentOperator.Regex("^[]a]+$");
		yield return ChromaWhereDocumentOperator.Regex("^[^]]$");
		yield return ChromaWhereDocumentOperator.Regex("[\\[]x*");
		yield return ChromaWhereDocumentOperator.Regex("a{2,}");
	}

	[Test]
	public void ThrowsOnChromaCloud([ValueSource(nameof(MayTakeAnEmptyText))] ChromaWhereDocumentOperator filter, [ValueSource(nameof(Operations))] string operation)
	{
		var server = new Server();
		var ex = Assert.ThrowsAsync<ArgumentException>(() => Run(Client(server, Cloud), operation, filter));
		Assert.That(ex!.Message, Does.StartWith("The client cannot tell that the pattern").And.Contain("NotRegex(\".\")"));
		Assert.That(server.Paths, Is.Empty);
	}

	[Test]
	public async Task GoesOnChromaCloud([ValueSource(nameof(NeedACharacter))] ChromaWhereDocumentOperator filter, [ValueSource(nameof(Operations))] string operation)
	{
		var server = new Server();
		await Run(Client(server, Cloud), operation, filter);
		Assert.That(server.Paths, Does.Contain(operation.Split(' ')[0]));
	}

	[Test]
	public async Task GoesOnASingleServer([ValueSource(nameof(MayTakeAnEmptyText))] ChromaWhereDocumentOperator filter, [ValueSource(nameof(Operations))] string operation)
	{
		var server = new Server();
		await Run(Client(server, Local), operation, filter);
		Assert.That(server.Paths, Does.Contain(operation.Split(' ')[0]));
	}

	// A filter that get rejects, a condition on the documents joined with |, fails the task as before: the check leaves it to the get, on
	// Chroma Cloud as on a single server.
	[TestCase(Cloud)]
	[TestCase(Local)]
	public void AFilterThatTheGetRejectsFailsItsTask(string uri)
	{
		var where = ChromaWhereOperator.Document(ChromaWhereDocumentOperator.Regex("^$")) | ChromaWhereOperator.Equal("k", 1L);
		Task? get = null;
		Assert.That(() => { get = Client(new Server(), uri).GetAsync(where: where); }, Throws.Nothing);
		Assert.That(() => get!, Throws.InstanceOf<NotSupportedException>());
	}

	static Task Run(ChromaCollectionClient client, string operation, ChromaWhereDocumentOperator filter) => operation switch
	{
		"get" => client.GetAsync(whereDocument: filter),
		"get with where" => client.GetAsync(where: ChromaWhereOperator.Document(filter)),
		"query" => client.QueryAsync(new ChromaQuery([Embedding]) { WhereDocument = filter }),
		"delete" => client.DeleteAsync(["a"], whereDocument: filter),
		"delete by filters" => client.DeleteAsync(new ChromaDelete { WhereDocument = filter }),
		"search" => client.SearchAsync(new ChromaSearch { Where = ChromaWhereOperator.Document(filter), Limit = 5 }),
		_ => client.SearchAsync(new ChromaSearch { WhereDocument = filter, Limit = 5 }),
	};

	static ChromaCollectionClient Client(HttpMessageHandler handler, string uri)
		=> new(new ChromaCollection("c") { Id = Guid.NewGuid() }, new ChromaConfigurationOptions(uri), new HttpClient(handler));

	// Answers each request with an empty result, and records the last segment of its path.
	sealed class Server : HttpMessageHandler
	{
		public List<string> Paths { get; } = [];

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var path = request.RequestUri!.AbsolutePath;
			var last = path[(path.LastIndexOf('/') + 1)..];
			Paths.Add(last);
			var answer = last switch
			{
				"version" => "\"1.5.9\"",
				"pre-flight-checks" => """{"max_batch_size":100,"supports_base64_encoding":false}""",
				"get" => """{"ids":[],"include":[]}""",
				"query" => """{"ids":[[]],"include":[]}""",
				"search" => """{"ids":[[]]}""",
				"delete" => """{"deleted":0}""",
				_ => "{}",
			};
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer) });
		}
	}
}
