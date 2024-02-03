using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ChromaDB.Client.Models
{
	
	public class Database
	{
		[JsonPropertyName("id")]
		public Guid Id { get; set; }

		[JsonPropertyName("name")]
		public string Name { get; init; }

		[JsonPropertyName("tenant")]
		public string? Tenant { get; set; }

		public Database(string name)
		{
			Name = name;
		}
	}
}
