using ChromaDB.Client.Models;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

// Values that a server would store changed, drop, or fail on: the client stops them before the request.
[TestFixture]
public class RequestValuesTests
{
	static readonly ReadOnlyMemory<float> Embedding = new([1f, 0f]);

	// UTF-8 has no form for a lone half of a surrogate pair: System.Text.Json would send U+FFFD in its place.
	[Test]
	public void LoneSurrogates()
	{
		Assert.That(() => ChromaWhereOperator.Equal("k", "a\uD800b").ToString(), Throws.ArgumentException.With.Message.Contains("U+D800"));
		Assert.That(() => ChromaWhereOperator.Equal("k\uDC00", "x").ToString(), Throws.ArgumentException.With.Message.Contains("U+DC00"));
		Assert.That(() => ChromaWhereDocumentOperator.Contains("\uD83D").ToString(), Throws.ArgumentException);
		Assert.That(ChromaWhereOperator.Equal("k", "\U0001F600").ToString(), Does.Contain("$eq"));
	}

	[Test]
	public void LoneSurrogatesInRecords()
	{
		var collection = Collection(new PreFlightOnly());
		Assert.That(() => collection.AddAsync(["a\uD800"], [Embedding]), Throws.ArgumentException);
		Assert.That(() => collection.AddAsync(["a"], [Embedding], documents: ["\uDFFF"]), Throws.ArgumentException);
		Assert.That(() => collection.AddAsync(["a"], [Embedding], [new Dictionary<string, object> { ["k\uD800"] = 1L }]), Throws.ArgumentException);
		Assert.That(() => collection.GetAsync(ids: ["a\uD800"]), Throws.ArgumentException);
	}

	[Test]
	public void LoneSurrogatesInNames()
	{
		using var client = new ChromaClient(new ChromaConfigurationOptions("http://localhost:8000"), new HttpClient(new PreFlightOnly()));
		Assert.That(() => client.GetCollectionAsync("c\uD800"), Throws.ArgumentException);
	}

	// Chroma stores integers as 64-bit signed numbers: a larger ulong would become a float.
	[Test]
	public void ULongs()
	{
		Assert.That(() => ChromaWhereOperator.Equal("k", ulong.MaxValue).ToString(), Throws.InstanceOf<ArgumentOutOfRangeException>());
		Assert.That(ChromaWhereOperator.Equal("k", (ulong)long.MaxValue).ToString(), Is.EqualTo("{\"k\":{\"$eq\":9223372036854775807}}"));
		var collection = Collection(new PreFlightOnly());
		Assert.That(() => collection.AddAsync(["a"], [Embedding], [new Dictionary<string, object> { ["k"] = (ulong)long.MaxValue + 1 }]), Throws.InstanceOf<ArgumentOutOfRangeException>());
	}

	// An empty list in the metadata of a record: Chroma 0.6.3 and 1.5.9 drop the key without an error, 1.0 to 1.4
	// reject it, Chroma Cloud stores it; the Python client rejects it. In an update or an upsert it deletes the key, as a null does: the
	// write goes without a read.
	[Test]
	public void EmptyListsInRecords()
	{
		var empty = new Dictionary<string, object> { ["tags"] = new List<string>(), ["x"] = 1L };
		Assert.That(() => Collection(new NoRequests()).AddAsync(["a"], [Embedding], [empty]), Throws.ArgumentException.With.Message.Contains("\"tags\""));
		var collection = Collection(new PreFlightOnly());
		Assert.That(() => collection.UpsertAsync(["a"], [Embedding], [empty]), Throws.InvalidOperationException.With.Message.Contains("/upsert"));
		Assert.That(() => collection.UpdateAsync(["a"], metadatas: [new Dictionary<string, object> { ["n"] = Array.Empty<long>() }]), Throws.InvalidOperationException.With.Message.Contains("/update"));
	}

	// A null value deletes the key in an update or an upsert, on every tested Chroma, and the write goes without a read. An add has none to
	// delete: Chroma 0.x drops the key and 1.x rejects the request, so AddAsync rejects it before.
	[Test]
	public void NullValuesInRecords()
	{
		var withNull = new Dictionary<string, object> { ["a"] = null!, ["b"] = 1L };
		Assert.That(() => Collection(new NoRequests()).AddAsync(["a"], [Embedding], [withNull]), Throws.ArgumentException.With.Message.Contains("\"a\""));
		var collection = Collection(new PreFlightOnly());
		Assert.That(() => collection.UpdateAsync(["a"], metadatas: [withNull]), Throws.InvalidOperationException.With.Message.Contains("/update"));
		Assert.That(() => collection.UpsertAsync(["a"], [Embedding], [withNull]), Throws.InvalidOperationException.With.Message.Contains("/upsert"));
	}

	// No Chroma stores a list in the metadata of a collection: 0.x and 1.0 to 1.4 reject it, Chroma Cloud answers 500
	// and 1.5.9 closes the connection.
	[Test]
	public void ListsInCollectionMetadata()
	{
		using var client = new ChromaClient(new ChromaConfigurationOptions("http://localhost:8000"), new HttpClient(new NoRequests()));
		var metadata = new Dictionary<string, object> { ["tags"] = new[] { "a" } };
		Assert.That(() => client.CreateCollectionAsync("c", metadata), Throws.ArgumentException.With.Message.Contains("\"tags\""));
		Assert.That(() => client.GetOrCreateCollectionAsync("c", metadata), Throws.ArgumentException);
		Assert.That(() => client.GetOrCreateCollectionAsync(new ChromaCollectionDefinition("c") { Metadata = new Dictionary<string, object> { ["e"] = new List<int>() } }), Throws.ArgumentException);
		Assert.That(() => Collection(new NoRequests()).ModifyAsync(metadata: metadata), Throws.ArgumentException);
	}

	// A byte[] goes as a base64 string, not as a list; a list that can be read once is not read by the checks, so it
	// goes whole. The write reaches its request, which the handler stops.
	[Test]
	public void ValuesThatAreNotLists()
	{
		var collection = Collection(new PreFlightOnly());
		Assert.That(() => collection.AddAsync(["a"], [Embedding], [new Dictionary<string, object> { ["b"] = Array.Empty<byte>(), ["c"] = new byte[] { 1, 2 } }]), Throws.InvalidOperationException.With.Message.Contains("/add"));
		var once = new ReadOnce(["x", "y"]);
		Assert.That(() => collection.AddAsync(["a"], [Embedding], [new Dictionary<string, object> { ["tags"] = once }]), Throws.InvalidOperationException.With.Message.Contains("/add"));
		Assert.That(once.Reads, Is.EqualTo(1));
		using var client = new ChromaClient(new ChromaConfigurationOptions("http://localhost:8000"), new HttpClient(new NoRequests()));
		Assert.That(() => client.CreateCollectionAsync("c", new Dictionary<string, object> { ["b"] = new byte[] { 1 } }), Throws.InvalidOperationException.With.Message.Contains("/collections"));
	}

	// A delete with an empty list of ids and no filter would go with no condition: rejected, as DeleteAsync(ChromaDelete) does.
	[Test]
	public void DeleteWithoutIds()
	{
		Assert.That(() => Collection(new NoRequests()).DeleteAsync(new List<string>()), Throws.ArgumentException);
	}

	// Lists of another length than the ids would be cut differently in each batch: rejected before any request.
	[Test]
	public void ListsOfAnotherLength()
	{
		var collection = Collection(new NoRequests());
		Assert.That(() => collection.AddAsync(["a", "b"], [Embedding]), Throws.ArgumentException.With.Message.Contains("embeddings"));
		Assert.That(() => collection.UpdateAsync(["a"], documents: ["x", "y"]), Throws.ArgumentException.With.Message.Contains("documents"));
		Assert.That(() => collection.UpsertAsync(["a", "b"], [Embedding, Embedding], [new Dictionary<string, object> { ["k"] = 1L }]), Throws.ArgumentException.With.Message.Contains("metadatas"));
		Assert.That(() => collection.AddAsync(new ChromaRecords(["a"]) { Embeddings = [Embedding], Uris = [] }), Throws.ArgumentException.With.Message.Contains("uris"));
	}

	// A client made by name reads the collection on the first call: the checks come before, without any request.
	[Test]
	public void ChecksBeforeTheCollectionIsRead()
	{
		using var client = new ChromaClient(new ChromaConfigurationOptions("http://localhost:8000"), new HttpClient(new NoRequests()));
		var collection = client.GetCollectionClient("c");
		Assert.That(() => collection.AddAsync(["a", "b"], [Embedding]), Throws.ArgumentException);
		Assert.That(() => collection.UpdateAsync(["a"], documents: ["x", "y"]), Throws.ArgumentException);
		Assert.That(() => collection.UpsertAsync(["a"], [new ReadOnlyMemory<float>([float.NaN])]), Throws.ArgumentException);
		Assert.That(() => collection.DeleteAsync(new List<string>()), Throws.ArgumentException);
	}

	// NaN and infinity: JSON numbers cannot carry them, and base64 embeddings get the same check.
	[Test]
	public void NonFiniteEmbeddingsInBase64()
	{
		var collection = Collection(new PreFlightOnly(base64: true));
		Assert.That(() => collection.AddAsync(["a"], [new ReadOnlyMemory<float>([1f, float.NaN])]), Throws.ArgumentException);
		Assert.That(() => collection.UpsertAsync(["a"], [new ReadOnlyMemory<float>([float.PositiveInfinity, 0f])]), Throws.ArgumentException);
	}

	sealed class ReadOnce(IEnumerable<string> items) : IEnumerable<string>
	{
		public int Reads { get; private set; }

		public IEnumerator<string> GetEnumerator()
		{
			if (++Reads > 1) throw new InvalidOperationException("Read twice.");
			return items.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
	}

	static ChromaCollectionClient Collection(HttpMessageHandler handler)
		=> new(Guid.NewGuid(), "c", new ChromaConfigurationOptions("http://localhost:8000"), new HttpClient(handler));

	sealed class NoRequests : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			=> throw new InvalidOperationException("Request to " + request.RequestUri!.AbsolutePath);
	}

	// Answers the questions about the server, so that a write gets as far as its request.
	sealed class PreFlightOnly(bool base64 = false) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var path = request.RequestUri!.AbsolutePath;
			if (path.EndsWith("/pre-flight-checks", StringComparison.Ordinal))
			{
				return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{\"max_batch_size\":100,\"supports_base64_encoding\":" + (base64 ? "true" : "false") + "}") });
			}
			if (path.EndsWith("/version", StringComparison.Ordinal))
			{
				return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("\"1.0.0\"") });
			}
			throw new InvalidOperationException("Request to " + path);
		}
	}
}
