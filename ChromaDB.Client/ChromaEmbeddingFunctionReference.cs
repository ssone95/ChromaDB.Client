#if !NETSTANDARD2_0
using Microsoft.Extensions.AI;
#endif

namespace ChromaDB.Client.Models;

/// <summary>
/// The embedding function that a collection declares for a key, <c>embedding_function</c> in its schema: the clients of Chroma that
/// know the function compute the embeddings with it. The client only declares it.
/// </summary>
public sealed class ChromaEmbeddingFunctionReference
{
	private readonly string _name;
	private readonly Dictionary<string, object> _config;

	private ChromaEmbeddingFunctionReference(string name, Dictionary<string, object> config)
	{
		_name = name;
		_config = config;
	}

	/// <summary>
	/// A function known to the clients of Chroma by its name, with its settings.
	/// </summary>
	/// <param name="name">The name of the function, like <c>chroma_bm25</c>.</param>
	/// <param name="config">The settings of the function, or null for none.</param>
	/// <returns>The reference to the function.</returns>
	public static ChromaEmbeddingFunctionReference Known(string name, IReadOnlyDictionary<string, object>? config = null)
		=> new(name, config is null ? [] : config.ToDictionary(x => x.Key, x => x.Value)); // a copy: later changes to config do not reach the schemas

	/// <summary>
	/// The BM25 function of Chroma, <c>chroma_bm25</c>, with the settings the Python client of Chroma 1.5.9 uses by default:
	/// <c>k</c> 1.2, <c>b</c> 0.75, <c>avg_doc_length</c> 256, <c>token_max_length</c> 40, <c>include_tokens</c> false.
	/// </summary>
	/// <param name="k">The saturation of the term frequency.</param>
	/// <param name="b">How much the length of the text weighs.</param>
	/// <param name="avgDocLength">The length of an average text, in tokens.</param>
	/// <param name="tokenMaxLength">The longest token kept, in characters; the longer ones are dropped.</param>
	/// <param name="includeTokens">Whether the vectors hold their tokens.</param>
	/// <returns>The reference to the function.</returns>
	public static ChromaEmbeddingFunctionReference ChromaBm25(double k = 1.2, double b = 0.75, double avgDocLength = 256, int tokenMaxLength = 40, bool includeTokens = false)
		=> new("chroma_bm25", new()
		{
			["k"] = k,
			["b"] = b,
			["avg_doc_length"] = avgDocLength,
			["token_max_length"] = tokenMaxLength,
			["include_tokens"] = includeTokens,
		});

#if !NETSTANDARD2_0
	// The generator of the embedding function of Chroma has the provider "chroma" and the model all-MiniLM-L6-v2: the function "default"
	// of the Python client, which stores it with no settings. The client recognizes it by its metadata, so that it needs no package of
	// the generator. Another generator declares nothing.
	internal static ChromaEmbeddingFunctionReference? Declared(IEmbeddingGenerator<string, Embedding<float>>? generator)
		=> generator?.GetService(typeof(EmbeddingGeneratorMetadata)) is EmbeddingGeneratorMetadata { ProviderName: "chroma", DefaultModelId: "all-MiniLM-L6-v2" }
			? Known("default")
			: null;
#endif

	internal Dictionary<string, object> ToJson()
		=> new() { ["type"] = "known", ["name"] = _name, ["config"] = _config };
}
