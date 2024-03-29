﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ChromaDB.Client.Models.Requests
{
	public class CollectionGetRequest
	{
		[JsonPropertyName("ids")]
		public required List<Guid> Ids { get; init; }

		[JsonPropertyName("where")]
		public IDictionary<string, object>? Where { get; init; }

		[JsonPropertyName("where_document")]
		public IDictionary<string, object>? WhereDocument { get; init; }

		[JsonPropertyName("sort")]
		public string? Sort { get; init; }

		[JsonPropertyName("limit")]
		public int? Limit { get; init; }

		[JsonPropertyName("offset")]
		public int? Offset { get; init; }

		[JsonPropertyName("include")]
		public required List<string> Include { get; set; }
	}
}
