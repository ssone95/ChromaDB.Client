using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChromaDB.Client.Models
{
	public class CollectionEntry
	{
		public string Id { get; set; }
		public List<float>? Embeddings { get; set; }
		public Dictionary<string, object>? Metadata { get; set; }
		public List<string?>? Uris { get; set; }
		public dynamic? Data { get; init; }
	}
}
