using ChromaDB.Client.Common.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ChromaDB.Client.Models
{
	[ChromaRoute("GET", TargetEndpoint = "collections/{collectionName}?tenant={tenant}&database={database}", Source = typeof(Collection), ResponseType = typeof(Collection))]
	[ChromaRoute("GET", TargetEndpoint = "collections?tenant={tenant}&database={database}", Source = typeof(Collection), ResponseType = typeof(List<Collection>))]
	public class Collection
	{
		[JsonPropertyName("id")]
		public Guid Id { get; set; }
		
		[JsonPropertyName("name")]
		public string Name { get; set; }
		
		[JsonPropertyName("metadata")]
		public IDictionary<string, object>? Metadata { get; set; }
		
		[JsonPropertyName("tenant")]
		public string? Tenant { get; set; }

		[JsonPropertyName("database")]
		public string? Database { get; set; }

		public Collection(string name) 
		{
			Name = name;
		}
	}
}
