using System.Text.Json.Serialization;

namespace ChromaDB.Client.Models.Responses;

public class GeneralError
{
	[JsonPropertyName("error")]
	public string? ErrorMessage { get; init; }
}
