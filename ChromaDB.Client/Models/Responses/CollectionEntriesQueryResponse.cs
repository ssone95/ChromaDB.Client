using System.Text.Json.Serialization;

namespace ChromaDB.Client.Models.Responses
{
    public class CollectionEntriesQueryResponse
	{
		[JsonPropertyName("ids")]
		public required List<List<string>> Ids { get; set; }

		[JsonPropertyName("embeddings")]
		public required List<List<List<float>>> Embeddings { get; set; }

		[JsonPropertyName("metadatas")]
		public required List<List<Dictionary<string, object>>> Metadatas { get; set; }

		[JsonPropertyName("uris")]
		public required List<List<string?>> Uris { get; set; }

		[JsonPropertyName("data")]
		public required dynamic Data { get; set; }

		[JsonPropertyName("distances")]
        public required List<List<float>> Distances { get; set; }

    }
}
