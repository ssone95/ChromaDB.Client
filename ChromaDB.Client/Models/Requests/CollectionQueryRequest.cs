using System.Text.Json.Serialization;

namespace ChromaDB.Client.Models.Requests;

public class CollectionQueryRequest
{
	[JsonPropertyName("query_embeddings")]
	public required List<List<float>>? QueryEmbeddings { get; init; }

	[JsonPropertyName("n_results")]
	public int NResults { get; init; } = 10;

	[JsonPropertyName("where")]
	public IDictionary<string, object>? Where { get; init; }

	[JsonPropertyName("where_document")]
	public IDictionary<string, object>? WhereDocument { get; init; }

	[JsonPropertyName("include")]
	public List<string> Include { get; init; } = ["metadatas", "documents", "distances"];
}
