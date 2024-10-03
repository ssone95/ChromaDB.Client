using System.Text.Json.Serialization;

namespace ChromaDB.Client.Models.Requests;

internal class CollectionGetRequest
{
	[JsonPropertyName("ids")]
	public List<string>? Ids { get; init; }

	[JsonPropertyName("where")]
	public Dictionary<string, object>? Where { get; init; }

	[JsonPropertyName("where_document")]
	public Dictionary<string, object>? WhereDocument { get; init; }

	[JsonPropertyName("limit")]
	public int? Limit { get; init; }

	[JsonPropertyName("offset")]
	public int? Offset { get; init; }

	[JsonPropertyName("include")]
	public required List<string> Include { get; init; }
}
