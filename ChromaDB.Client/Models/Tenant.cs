using System.Text.Json.Serialization;

namespace ChromaDB.Client.Models;

public class Tenant
{
	[JsonPropertyName("name")]
	public string Name { get; init; }

	public Tenant(string name)
	{
		Name = name;
	}
}
