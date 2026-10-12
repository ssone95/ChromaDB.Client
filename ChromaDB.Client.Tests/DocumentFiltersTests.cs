using System.Net;
using ChromaDB.Client.Models;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

// KD-56 and KD-57: on Chroma Cloud a filter on the documents gives the wrong records, also in a delete: a $regex whose pattern takes an
// empty text finds every record and its $not_regex none, and, once Chroma Cloud has indexed the records, a $contains of fewer than 3
// characters finds none and its $not_contains every record. Such a filter throws on Chroma Cloud before any request, in every read and
// delete, also inside $and, $or and Not, and so does a pattern that the client cannot read, which Chroma rejects. The client reads a
// pattern as Chroma does, with the syntax of the Rust crate regex (RegexMinimumLengthTests). Every other filter goes, and every filter
// goes on a single server.
[TestFixture]
public class DocumentFiltersTests
{
	const string Cloud = "https://api.trychroma.com";
	const string Local = "http://localhost:8000";
	static readonly ReadOnlyMemory<float> Embedding = new([1f, 0f]);
	static readonly string[] Operations = ["get", "get with where", "query", "delete", "delete by filters", "search", "search with where document"];

	static IEnumerable<ChromaWhereDocumentOperator> WrongOnChromaCloud()
	{
		yield return ChromaWhereDocumentOperator.Regex("^$");
		yield return ChromaWhereDocumentOperator.Regex("^");
		yield return ChromaWhereDocumentOperator.Regex("^.*$");
		yield return ChromaWhereDocumentOperator.NotRegex("^$");
		yield return ChromaWhereDocumentOperator.Contains("xyz") & ChromaWhereDocumentOperator.Regex("x*");
		yield return ChromaWhereDocumentOperator.Not(ChromaWhereDocumentOperator.Regex("^$") | ChromaWhereDocumentOperator.Contains("xyz"));
		// Written in a way that .NET reads otherwise than Chroma, or not at all: Chroma 1.5.9 finds an empty document with them.
		yield return ChromaWhereDocumentOperator.Regex("^[[:alpha:]]*$");
		yield return ChromaWhereDocumentOperator.Regex("^[a[b]]*$");
		yield return ChromaWhereDocumentOperator.Regex("^[]a[:alpha:]]*$");
		yield return ChromaWhereDocumentOperator.Regex("^[^]a[:alpha:]]*$");
		yield return ChromaWhereDocumentOperator.Regex("^(?U)a*$");
		yield return ChromaWhereDocumentOperator.Regex("(?P<a>x*)");
		yield return ChromaWhereDocumentOperator.Regex("^\\b{start-half}$");
		// In verbose mode the blanks do not count, and x+ ? is (x+)?.
		yield return ChromaWhereDocumentOperator.Regex("(?x) a* ");
		yield return ChromaWhereDocumentOperator.Regex("(?x)a+ ?");
		// Patterns that the client cannot read, which Chroma rejects.
		yield return ChromaWhereDocumentOperator.Regex("\\Z");
		yield return ChromaWhereDocumentOperator.Regex("(?=)");
		// Texts of fewer than 3 characters, as Chroma counts them: a character outside the BMP is one.
		yield return ChromaWhereDocumentOperator.Contains("");
		yield return ChromaWhereDocumentOperator.Contains("a");
		yield return ChromaWhereDocumentOperator.Contains('a');
		yield return ChromaWhereDocumentOperator.Contains("ab");
		yield return ChromaWhereDocumentOperator.NotContains("ab");
		yield return ChromaWhereDocumentOperator.Contains("αβ");
		yield return ChromaWhereDocumentOperator.Contains("😀😀");
		yield return ChromaWhereDocumentOperator.Contains("  ");
		yield return ChromaWhereDocumentOperator.Contains("abc") & ChromaWhereDocumentOperator.NotContains("x");
		yield return ChromaWhereDocumentOperator.Not(ChromaWhereDocumentOperator.Contains("ab") | ChromaWhereDocumentOperator.Regex("xyz"));
	}

	static IEnumerable<ChromaWhereDocumentOperator> RightOnChromaCloud()
	{
		yield return ChromaWhereDocumentOperator.Regex("^x$");
		yield return ChromaWhereDocumentOperator.Regex(".");
		yield return ChromaWhereDocumentOperator.Regex("(?s).");
		yield return ChromaWhereDocumentOperator.NotRegex("(?s).");
		yield return ChromaWhereDocumentOperator.NotRegex("^.+$");
		yield return ChromaWhereDocumentOperator.Regex("ab");
		// A ']' right after '[' or '[^' and an escaped '[' are characters of the class, not a nested class.
		yield return ChromaWhereDocumentOperator.Regex("^[]a]+$");
		yield return ChromaWhereDocumentOperator.Regex("^[^]]$");
		yield return ChromaWhereDocumentOperator.Regex("[\\[]x*");
		yield return ChromaWhereDocumentOperator.Regex("a{2,}");
		// Written in a way that only Chroma reads, needing a character.
		yield return ChromaWhereDocumentOperator.Regex("^[[:alpha:]]+$");
		yield return ChromaWhereDocumentOperator.Regex("(?P<a>x)");
		yield return ChromaWhereDocumentOperator.Regex("\\b{start}x");
		yield return ChromaWhereDocumentOperator.Regex("(?x) a b # c");
		yield return ChromaWhereDocumentOperator.Contains("abc");
		yield return ChromaWhereDocumentOperator.NotContains("abc");
		yield return ChromaWhereDocumentOperator.Contains("αβγ");
		yield return ChromaWhereDocumentOperator.Contains("😀😀😀");
		yield return ChromaWhereDocumentOperator.Contains("abc") & ChromaWhereDocumentOperator.Regex("x");
	}

	[Test]
	public void ThrowsOnChromaCloud([ValueSource(nameof(WrongOnChromaCloud))] ChromaWhereDocumentOperator filter, [ValueSource(nameof(Operations))] string operation)
	{
		var server = new Server();
		var ex = Assert.ThrowsAsync<ArgumentException>(() => Run(Client(server, Cloud), operation, filter));
		Assert.That(ex!.Message, Does.StartWith("On Chroma Cloud the ").And.Contain("gives the wrong records"));
		Assert.That(server.Paths, Is.Empty);
	}

	[Test]
	public async Task GoesOnChromaCloud([ValueSource(nameof(RightOnChromaCloud))] ChromaWhereDocumentOperator filter, [ValueSource(nameof(Operations))] string operation)
	{
		var server = new Server();
		await Run(Client(server, Cloud), operation, filter);
		Assert.That(server.Paths, Does.Contain(operation.Split(' ')[0]));
	}

	[Test]
	public async Task GoesOnASingleServer([ValueSource(nameof(WrongOnChromaCloud))] ChromaWhereDocumentOperator filter, [ValueSource(nameof(Operations))] string operation)
	{
		var server = new Server();
		await Run(Client(server, Local), operation, filter);
		Assert.That(server.Paths, Does.Contain(operation.Split(' ')[0]));
	}

	// The message names the filter and what is right instead.
	[TestCase("^$", "the pattern \"^$\" of $regex", "it takes an empty text")]
	[TestCase("\\Z", "the pattern \"\\Z\" of $regex", "the client cannot read it")]
	public void TheMessageOfAPattern(string pattern, string filter, string reason)
	{
		var ex = Assert.ThrowsAsync<ArgumentException>(() => Client(new Server(), Cloud).GetAsync(whereDocument: ChromaWhereDocumentOperator.Regex(pattern)));
		Assert.That(ex!.Message, Does.Contain(filter).And.Contain(reason).And.Contain("Regex(\"(?s).\")").And.Contain("NotRegex(\"(?s).\")"));
		Assert.That(ex.ParamName, Is.EqualTo("whereDocument"));
	}

	[Test]
	public void TheMessageOfAText()
	{
		var ex = Assert.ThrowsAsync<ArgumentException>(() => Client(new Server(), Cloud).GetAsync(whereDocument: ChromaWhereDocumentOperator.NotContains("ab")));
		Assert.That(ex!.Message, Does.Contain("the text \"ab\" of $not_contains").And.Contain("fewer than 3 characters").And.Contain("$regex"));
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
