using System.Text.Json.Serialization;

namespace ChromaDB.Client.Models;

public class Collection
{
	[JsonPropertyName("id")]
	public Guid Id { get; init; }

	[JsonPropertyName("name")]
	public string Name { get; }

	[JsonPropertyName("metadata")]
	public IDictionary<string, object>? Metadata { get; init; }

	[JsonPropertyName("tenant")]
	public string? Tenant { get; init; }

	[JsonPropertyName("database")]
	public string? Database { get; init; }

	public Collection(string name)
	{
		Name = name;
	}
}
