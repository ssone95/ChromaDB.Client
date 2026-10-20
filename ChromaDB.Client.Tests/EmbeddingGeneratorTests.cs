#if !CHROMA_CLIENT_NETSTANDARD2_0
using System.Net;
using System.Text.Json;
using ChromaDB.Client.Models;
using Microsoft.Extensions.AI;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

// The embeddings computed by a generator, as an embedding function does in the Python client of Chroma: from the documents of the records
// given without embeddings, and from the query texts. A new collection with the generator of Chroma declares "default".
[TestFixture]
public class EmbeddingGeneratorTests
{
	static readonly ReadOnlyMemory<float> Embedding = new([9f, 9f]);

	[Test]
	public async Task WritesComputeTheEmbeddingsOfTheDocuments()
	{
		var server = new FakeServer();
		var generator = new FakeGenerator();
		var client = Client(server).WithEmbeddingGenerator(generator);
		await client.AddAsync(["a", "b"], documents: ["apple", "kiwi"]);
		await client.UpdateAsync(["a"], documents: ["banana"]);
		await client.UpsertAsync(new ChromaRecords(["c"]) { Documents = ["fig"] });
		Assert.That(generator.Calls, Is.EqualTo(new[] { new[] { "apple", "kiwi" }, new[] { "banana" }, new[] { "fig" } }));
		Assert.That(server.Embeddings("/add"), Is.EqualTo(new[] { new[] { 5f, 1f }, new[] { 4f, 1f } }));
		Assert.That(server.Embeddings("/update"), Is.EqualTo(new[] { new[] { 6f, 1f } }));
		Assert.That(server.Embeddings("/upsert"), Is.EqualTo(new[] { new[] { 3f, 1f } }));
		Assert.That(server.Body("/add").GetProperty("documents").EnumerateArray().Select(x => x.GetString()), Is.EqualTo(new[] { "apple", "kiwi" }));
	}

	// Records given with embeddings keep them, and records without documents have nothing to compute from.
	[Test]
	public async Task GivenEmbeddingsAndRecordsWithoutDocumentsGoAsTheyAre()
	{
		var server = new FakeServer();
		var generator = new FakeGenerator();
		var client = Client(server).WithEmbeddingGenerator(generator);
		await client.AddAsync(["a"], [Embedding], documents: ["apple"]);
		await client.UpdateAsync(["a"], metadatas: [new Dictionary<string, object> { ["k"] = 1L }]);
		Assert.That(generator.Calls, Is.Empty);
		Assert.That(server.Embeddings("/add"), Is.EqualTo(new[] { new[] { 9f, 9f } }));
		Assert.That(server.Body("/update").TryGetProperty("embeddings", out var embeddings) && embeddings.ValueKind != JsonValueKind.Null, Is.False);
	}

	// A null document has no text to compute from: the write throws before any request.
	[Test]
	public void NullDocumentWithoutEmbeddings()
	{
		var server = new FakeServer();
		var client = Client(server).WithEmbeddingGenerator(new FakeGenerator());
		Assert.That(() => client.UpdateAsync(["a", "b"], documents: ["apple", null!]), Throws.ArgumentException.With.Message.Contains("null document"));
		Assert.That(() => client.UpsertAsync(new ChromaRecords(["a"]) { Documents = [null], NullDocumentsDelete = true }), Throws.ArgumentException);
		Assert.That(server.Paths, Is.Empty);
	}

	[Test]
	public async Task QueryTexts()
	{
		var server = new FakeServer();
		var generator = new FakeGenerator();
		var client = Client(server).WithEmbeddingGenerator(generator);
		var one = await client.QueryAsync("apple", nResults: 3);
		Assert.That(one.Select(x => x.Id), Is.EqualTo(new[] { "r0" }));
		Assert.That(server.Body("/query").GetProperty("n_results").GetInt32(), Is.EqualTo(3));
		var two = await client.QueryTextsAsync(["apple", "kiwi"]);
		Assert.That(two.Select(x => x.Single().Id), Is.EqualTo(new[] { "r0", "r1" }));
		Assert.That(server.Embeddings("/query", "query_embeddings"), Is.EqualTo(new[] { new[] { 5f, 1f }, new[] { 4f, 1f } }));
		Assert.That(generator.Calls, Is.EqualTo(new[] { new[] { "apple" }, new[] { "apple", "kiwi" } }));
	}

	[Test]
	public void QueryTextsNeedAGenerator()
	{
		var server = new FakeServer();
		var client = Client(server);
		Assert.That(() => client.QueryAsync("apple"), Throws.InvalidOperationException.With.Message.Contains("WithEmbeddingGenerator"));
		Assert.That(() => client.QueryTextsAsync(["apple"]), Throws.InvalidOperationException);
		Assert.That(() => client.WithEmbeddingGenerator(new FakeGenerator()).QueryTextsAsync(["apple", null!]), Throws.ArgumentException);
		Assert.That(() => client.WithEmbeddingGenerator(null!), Throws.ArgumentNullException);
		Assert.That(server.Paths, Is.Empty);
	}

	// A generator that returns another number of embeddings than of texts.
	[Test]
	public void EmbeddingsForEveryText()
	{
		var client = Client(new FakeServer()).WithEmbeddingGenerator(new FakeGenerator { Extra = 1 });
		Assert.That(() => client.AddAsync(["a"], documents: ["apple"]), Throws.InvalidOperationException.With.Message.Contains("2 embeddings for 1 texts"));
		Assert.That(() => client.QueryAsync("apple"), Throws.InvalidOperationException);
	}

	// The other With methods keep the generator, and it keeps their settings.
	[Test]
	public async Task GeneratorStaysWithTheOtherSettings()
	{
		var server = new FakeServer();
		var generator = new FakeGenerator();
		var client = Client(server).WithEmbeddingGenerator(generator).WithDocumentCopyKey("text");
		Assert.That(client.EmbeddingGenerator, Is.SameAs(generator));
		await client.AddAsync(["a"], documents: ["apple"]);
		Assert.That(server.Body("/add").GetProperty("metadatas")[0].GetProperty("text").GetString(), Is.EqualTo("apple"));
		Assert.That(server.Embeddings("/add"), Is.EqualTo(new[] { new[] { 5f, 1f } }));
		Assert.That(Client(server).EmbeddingGenerator, Is.Null);
		Assert.That(Client(server).WithDocumentCopyKey("text").WithEmbeddingGenerator(generator).WithMetadataValues(ChromaMetadataValues.Exact).EmbeddingGenerator, Is.SameAs(generator));
	}

	// The generator of the default embedding function of Chroma has the provider "chroma" and the model
	// all-MiniLM-L6-v2: a new collection with it declares "default", with no settings, as the Python client of Chroma 1.5.9 stores it.
	[TestCase("chroma", "all-MiniLM-L6-v2", "1.5.9", """{"type":"known","name":"default","config":{}}""")]
	[TestCase("other", "all-MiniLM-L6-v2", "1.5.9", null)]
	[TestCase("chroma", "other", "1.5.9", null)]
	[TestCase("chroma", "all-MiniLM-L6-v2", "0.6.3", null)]
	public async Task DefaultEmbeddingFunction(string provider, string model, string version, string? declared)
	{
		var server = new FakeServer(version);
		var client = new ChromaClient(new ChromaConfigurationOptions("http://localhost:8000"), new HttpClient(server));
		var generator = new FakeGenerator(provider, model);
		await client.CreateCollectionAsync(new ChromaCollectionDefinition("c") { EmbeddingGenerator = generator });
		await client.GetOrCreateCollectionAsync(new ChromaCollectionDefinition("c") { EmbeddingGenerator = generator, Metadata = new Dictionary<string, object> { ["k"] = 1L } });
		Assert.That(server.Configurations, Is.EqualTo(new[] { declared, declared }));
	}

	// The settings of the configuration stay, and a function it declares wins over the one of the generator.
	[Test]
	public async Task DeclaredFunctionAndSettingsStay()
	{
		var server = new FakeServer();
		var client = new ChromaClient(new ChromaConfigurationOptions("http://localhost:8000"), new HttpClient(server));
		var generator = new FakeGenerator("chroma", "all-MiniLM-L6-v2");
		await client.CreateCollectionAsync(new ChromaCollectionDefinition("c") { EmbeddingGenerator = generator, Configuration = new() { Space = ChromaSpace.Cosine } });
		await client.CreateCollectionAsync(new ChromaCollectionDefinition("c") { EmbeddingGenerator = generator, Configuration = new() { EmbeddingFunction = ChromaEmbeddingFunctionReference.Known("mine") } });
		await client.CreateCollectionAsync(new ChromaCollectionDefinition("c"));
		Assert.That(server.Configurations, Is.EqualTo(new[] { """{"type":"known","name":"default","config":{}}""", """{"type":"known","name":"mine","config":{}}""", null }));
		Assert.That(server.Bodies.First(x => x.Path.EndsWith("/collections")).Body.GetProperty("metadata").GetProperty("hnsw:space").GetString(), Is.EqualTo("cosine"));
	}

	static ChromaCollectionClient Client(HttpMessageHandler handler)
		=> new(new ChromaCollection("c") { Id = Guid.NewGuid() }, new ChromaConfigurationOptions("http://localhost:8000"), new HttpClient(handler));

	// The embedding of a text: its length, and 1.
	sealed class FakeGenerator(string provider = "test", string model = "test") : IEmbeddingGenerator<string, Embedding<float>>
	{
		public List<string[]> Calls { get; } = [];
		public int Extra { get; init; }

		public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
		{
			var texts = values.ToArray();
			Calls.Add(texts);
			var embeddings = texts.Concat(Enumerable.Repeat("", Extra)).Select(text => new Embedding<float>(new[] { (float)text.Length, 1f }));
			return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
		}

		public object? GetService(Type serviceType, object? serviceKey = null)
			=> serviceType == typeof(EmbeddingGeneratorMetadata) ? new EmbeddingGeneratorMetadata(provider, null, model, 2) : null;

		public void Dispose() { }
	}

	// Answers the version, pre-flight-checks, a new collection, a query with one record per embedding, and the writes with nothing; records
	// the path and the body of each request, and the embedding function of the configuration of each new collection.
	sealed class FakeServer(string version = "1.5.9") : HttpMessageHandler
	{
		public List<(string Path, JsonElement Body)> Bodies { get; } = [];
		public List<string?> Configurations { get; } = [];
		public IEnumerable<string> Paths => Bodies.Select(x => x.Path).Where(path => !path.EndsWith("/version") && !path.EndsWith("/pre-flight-checks"));

		public JsonElement Body(string path) => Bodies.Last(x => x.Path.EndsWith(path)).Body;

		public float[][] Embeddings(string path, string name = "embeddings")
			=> Body(path).GetProperty(name).EnumerateArray().Select(x => x.EnumerateArray().Select(v => v.GetSingle()).ToArray()).ToArray();

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var path = request.RequestUri!.AbsolutePath;
			var body = request.Content is null ? default : JsonDocument.Parse(await request.Content.ReadAsStringAsync()).RootElement.Clone();
			Bodies.Add((path, body));
			if (path.EndsWith("/collections"))
			{
				Configurations.Add(body.TryGetProperty("configuration", out var configuration) && configuration.ValueKind == JsonValueKind.Object
					&& configuration.TryGetProperty("embedding_function", out var function) ? function.GetRawText() : null);
			}
			var answer = path.EndsWith("/version") ? "\"" + version + "\""
				: path.EndsWith("/pre-flight-checks") ? """{"max_batch_size":100,"supports_base64_encoding":false}"""
				: path.EndsWith("/collections") ? """{"id":"11111111-2222-3333-4444-555555555555","name":"c"}"""
				: path.EndsWith("/query") ? "{\"ids\":[" + string.Join(",", body.GetProperty("query_embeddings").EnumerateArray().Select((_, i) => "[\"r" + i + "\"]")) + "]}"
				: "{}";
			return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer) };
		}
	}
}
#endif
