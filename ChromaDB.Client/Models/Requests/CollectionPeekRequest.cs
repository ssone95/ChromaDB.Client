using System.Text.Json.Serialization;

namespace ChromaDB.Client.Models.Requests;

internal class CollectionPeekRequest
{
	[JsonPropertyName("limit")]
	public int Limit { get; init; }
}
