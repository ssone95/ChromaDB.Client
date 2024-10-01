using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Responses;

namespace ChromaDB.Client.Common.Mappers;

public static class CollectionEntryMapper
{
	public static List<CollectionEntry> Map(this CollectionEntriesGetResponse response)
	{
		return response.Ids
			.Select((id, i) => new CollectionEntry(id)
			{
				Embeddings = response.Embeddings?[i],
				Metadata = response.Metadatas?[i],
				Document = response.Documents?[i],
				Uris = response.Uris?[i],
				Data = response.Data,
			})
			.ToList();
	}
}
