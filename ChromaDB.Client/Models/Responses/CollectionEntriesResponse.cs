using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ChromaDB.Client.Models.Responses
{
    public class CollectionEntriesResponse
    {
        [JsonPropertyName("ids")]
        public required List<string> Ids { get; set; }

        [JsonPropertyName("embeddings")]
        public required List<List<float>> Embeddings { get; set; }

        [JsonPropertyName("metadatas")]
        public required List<Dictionary<string, object>> Metadatas { get; set; }

        [JsonPropertyName("uris")]
        public required List<List<string?>> Uris { get; set; }

        [JsonPropertyName("data")]
        public required dynamic Data { get; set; }
    }
}
