using System.Text.Json.Serialization;

namespace ChromaDB.Client.Models.Requests;

public class DBCreateCollectionRequest
{
	[JsonPropertyName("name")]
	public required string Name { get; init; }

	[JsonPropertyName("metadata")]
	public IDictionary<string, object>? Metadata { get; init; }
}
