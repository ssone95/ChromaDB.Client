﻿using System.Text.Json.Serialization;

namespace ChromaDB.Client.Models.Requests;

internal class CollectionUpsertRequest
{
	[JsonPropertyName("ids")]
	public required List<string> Ids { get; init; }

	[JsonPropertyName("embeddings")]
	public List<List<float>>? Embeddings { get; init; }

	[JsonPropertyName("metadatas")]
	public List<Dictionary<string, object>>? Metadatas { get; init; }

	[JsonPropertyName("documents")]
	public List<string>? Documents { get; init; }
}
