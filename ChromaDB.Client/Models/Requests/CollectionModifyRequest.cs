using System.Text.Json.Serialization;

namespace ChromaDB.Client.Models.Requests;

public class CollectionModifyRequest
{
	[JsonPropertyName("name")]
	public string? Name { get; init; }

	[JsonPropertyName("metadata")]
	public IDictionary<string, object>? Metadata { get; init; }
}
