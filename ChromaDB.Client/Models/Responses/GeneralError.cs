using System.Text.Json.Serialization;

namespace ChromaDB.Client.Models.Responses;

internal class GeneralError
{
	[JsonPropertyName("error")]
	public string? Error { get; init; }
}
