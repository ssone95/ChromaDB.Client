﻿using System.Text.Json.Serialization;

namespace ChromaDB.Client.Models.Responses;

public class CollectionEntriesResponse
{
	[JsonPropertyName("ids")]
	public required List<string> Ids { get; init; }

	[JsonPropertyName("embeddings")]
	public required List<List<float>> Embeddings { get; init; }

	[JsonPropertyName("metadatas")]
	public required List<Dictionary<string, object>> Metadatas { get; init; }

	[JsonPropertyName("uris")]
	public required List<List<string?>> Uris { get; init; }

	[JsonPropertyName("data")]
	public required dynamic Data { get; init; }
}
