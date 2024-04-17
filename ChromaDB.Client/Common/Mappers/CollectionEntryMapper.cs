using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Responses;

namespace ChromaDB.Client.Common.Mappers;

public static class CollectionEntryMapper
{
	public static List<CollectionEntry> Map(this CollectionEntriesResponse response)
	{
		return response.Ids
			.Select((id, i) => new CollectionEntry(id)
			{
				Data = response.Data,
				Embeddings = response.Embeddings?[i] ?? null,
				Metadata = response.Metadatas?[i] ?? null,
				Uris = response.Uris?[i] ?? null
			})
			.ToList();
	}
}
