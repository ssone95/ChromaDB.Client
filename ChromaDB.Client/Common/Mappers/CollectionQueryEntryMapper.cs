﻿using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Responses;

namespace ChromaDB.Client.Common.Mappers;

public static class CollectionQueryEntryMapper
{
	public static List<List<CollectionQueryEntry>> Map(this CollectionEntriesQueryResponse response)
	{
		return response.Ids
			.Select((_, i) => response.Ids[i]
				.Select((id, j) => new CollectionQueryEntry(id)
				{
					Distance = response.Distances[i][j],
					Metadata = response.Metadatas?[i][j],
					Embeddings = response.Embeddings?[i][j],
					Document = response.Documents?[i][j],
					Uris = response.Uris?[i][j],
					Data = response.Data,
				})
				.ToList())
			.ToList();
	}
}
