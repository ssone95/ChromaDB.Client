#if !CHROMA_CLIENT_NETSTANDARD2_0
using ChromaDB.Client.Models;
using Microsoft.Extensions.AI;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

// The embeddings computed by a generator, on the server: the records written with their documents only are found by the query texts. A new
// collection with the generator of Chroma declares "default" on Chroma 1.x, and on 0.x goes without.
[TestFixture]
public class EmbeddingGeneratorServerTests : ChromaTestsBase
{
	[Test]
	public async Task DocumentsAndQueryTexts()
	{
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var collection = await client.CreateCollectionAsync(new ChromaCollectionDefinition($"collection{Random.Shared.Next()}"));
		var records = client.GetCollectionClient(collection).WithEmbeddingGenerator(new LetterGenerator());
		await records.AddAsync(["a", "b"], documents: ["apple", "banana"]);
		await records.UpsertAsync(["c"], documents: ["cherry"]);
		await records.UpdateAsync(["b"], documents: ["blueberry"]);
		Assert.That((await records.QueryAsync("avocado", nResults: 1)).Single().Id, Is.EqualTo("a"));
		var nearest = await records.QueryTextsAsync(["bean", "coconut"], nResults: 1);
		Assert.That(nearest.Select(x => (x.Single().Id, x.Single().Document)), Is.EqualTo(new[] { ("b", "blueberry"), ("c", "cherry") }));
		var stored = await records.GetAsync(ids: ["c"], include: ChromaGetInclude.Embeddings);
		Assert.That(stored.Single().Embedding!.Value.ToArray(), Is.EqualTo(LetterGenerator.Embed("cherry")));
	}

	[Test]
	public async Task DefaultEmbeddingFunction()
	{
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var collection = await client.CreateCollectionAsync(new ChromaCollectionDefinition($"collection{Random.Shared.Next()}") { EmbeddingGenerator = new LetterGenerator("chroma", "all-MiniLM-L6-v2") });
		var read = await client.GetCollectionAsync(collection.Name);
		string? Declared(ChromaCollection c)
			=> c.ConfigurationJson is { ValueKind: System.Text.Json.JsonValueKind.Object } configuration
				&& configuration.TryGetProperty("embedding_function", out var function) && function.ValueKind == System.Text.Json.JsonValueKind.Object
				? function.GetProperty("type").GetString() + " " + function.GetProperty("name").GetString() : null;
		// Chroma 1.0.0 to 1.0.5 take the embedding function without reporting it, as the space (docs/COMPATIBILITY.md).
		Assert.That((Declared(collection), Declared(read)), Is.EqualTo(IsChroma0 || !ConfigurationSpaceReported ? (null, null) : ("known default", "known default")));
	}

	// One dimension for each letter from a to e, by the first letter of the text, and one for the rest: texts that start with the same
	// letter are nearest.
	sealed class LetterGenerator(string provider = "test", string model = "letters") : IEmbeddingGenerator<string, Embedding<float>>
	{
		public static float[] Embed(string text)
		{
			var embedding = new float[6];
			embedding[text.Length > 0 && text[0] is >= 'a' and <= 'e' ? text[0] - 'a' : 5] = 1f;
			return embedding;
		}

		public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
			=> Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(values.Select(text => new Embedding<float>(Embed(text)))));

		public object? GetService(Type serviceType, object? serviceKey = null)
			=> serviceType == typeof(EmbeddingGeneratorMetadata) ? new EmbeddingGeneratorMetadata(provider, null, model, 6) : null;

		public void Dispose() { }
	}
}
#endif
