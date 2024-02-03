using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Responses;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChromaDB.Client.Common.Mappers
{
	public static class CollectionEntryMapper
	{
		public static List<CollectionEntry> Map(this CollectionEntriesResponse response)
		{
			List<CollectionEntry> entries = new();
			List<Guid> ids = response.Ids;
			try
			{
				for (int i = 0; i < ids.Count; i++)
				{
					entries.Add(new CollectionEntry()
					{
						Id = ids[i],
						Data = response.Data,
						Embeddings = response.Embeddings?.ElementAt(i) ?? null,
						Metadata = response.Metadatas?.ElementAt(i) ?? null,
						Uris = response.Uris?.ElementAt(i) ?? null
					});
				}
			}
			catch (Exception ex)
			{
				throw;
			}
			return entries;
		}
	}
}
