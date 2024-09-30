using System.Text.Json.Serialization;

namespace ChromaDB.Client.Models.Requests;

public class CollectionPeekRequest
{
	[JsonPropertyName("limit")]
	public int? Limit { get; init; } = 10;
}
