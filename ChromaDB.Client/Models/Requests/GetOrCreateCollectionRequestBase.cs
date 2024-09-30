using System.Text.Json.Serialization;

namespace ChromaDB.Client.Models.Requests;

public abstract class GetOrCreateCollectionRequestBase
{
	[JsonPropertyName("name")]
	public required string Name { get; init; }

	[JsonPropertyName("metadata")]
	public IDictionary<string, object>? Metadata { get; init; }

	[JsonInclude]
	[JsonPropertyName("get_or_create")]
	protected abstract bool GetOrCreate { get; }
}
