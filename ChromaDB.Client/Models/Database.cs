using System.Text.Json.Serialization;

namespace ChromaDB.Client.Models;

public class Database
{
	[JsonPropertyName("id")]
	public Guid Id { get; init; }

	[JsonPropertyName("name")]
	public string Name { get; }

	[JsonPropertyName("tenant")]
	public string? Tenant { get; init; }

	public Database(string name)
	{
		Name = name;
	}
}
