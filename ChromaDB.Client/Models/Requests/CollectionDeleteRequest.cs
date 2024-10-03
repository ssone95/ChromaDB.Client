using System.Text.Json.Serialization;

namespace ChromaDB.Client.Models.Requests;

internal class CollectionDeleteRequest
{
	[JsonPropertyName("ids")]
	public required List<string> Ids { get; init; }

	[JsonPropertyName("where")]
	public IDictionary<string, object>? Where { get; init; }

	[JsonPropertyName("where_document")]
	public IDictionary<string, object>? WhereDocument { get; init; }
}
