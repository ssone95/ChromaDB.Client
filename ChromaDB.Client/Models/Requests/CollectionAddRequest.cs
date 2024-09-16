using System.Text.Json.Serialization;

namespace ChromaDB.Client.Models.Requests;

public class CollectionAddRequest
{
	[JsonPropertyName("ids")]
	public required List<string> Ids { get; init; }

	[JsonPropertyName("embeddings")]
	public List<List<float>>? Embeddings { get; init; }

	[JsonPropertyName("metadatas")]
	public List<IDictionary<string, object>>? Metadatas { get; init; }

	[JsonPropertyName("documents")]
	public List<string>? Documents { get; init; }
}
