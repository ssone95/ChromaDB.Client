using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Responses;

namespace ChromaDB.Client.Common;

internal static class CollectionQueryEntryMapper
{
	public static List<List<ChromaCollectionQueryEntry>> Map(this CollectionEntriesQueryResponse response)
	{
		return response.Ids
			.Select((_, i) => response.Ids[i]
				.Select((id, j) => new ChromaCollectionQueryEntry(id)
				{
					Distance = response.Distances[i].Span[j],
					Metadata = response.Metadatas?[i][j],
					Embeddings = response.Embeddings?[i][j],
					Document = response.Documents?[i][j],
					Uris = response.Uris?[i][j],
				})
				.ToList())
			.ToList();
	}
}
