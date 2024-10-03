﻿using System.Text.Json.Serialization;

namespace ChromaDB.Client.Models.Requests;

internal class CollectionModifyRequest
{
	[JsonPropertyName("name")]
	public string? Name { get; init; }

	[JsonPropertyName("metadata")]
	public Dictionary<string, object>? Metadata { get; init; }
}
