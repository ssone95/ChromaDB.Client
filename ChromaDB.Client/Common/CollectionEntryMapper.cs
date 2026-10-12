using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Responses;

namespace ChromaDB.Client.Common;

internal static class CollectionEntryMapper
{
	public static List<ChromaCollectionEntry> Map(this CollectionEntriesGetResponse response, DocumentReader copy = default)
	{
		return response.Ids
			.Select((id, i) => new ChromaCollectionEntry(id)
			{
				Embedding = response.Embeddings?[i],
				Metadata = copy.Metadata(response.Metadatas is { } metadatas ? metadatas[i] ?? DocumentReader.NoKeys : null),
				Document = copy.Document(response.Documents?[i], response.Metadatas?[i]),
				Uri = response.Uris?[i],
			})
			.ToList();
	}
}
