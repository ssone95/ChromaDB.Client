using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ChromaDB.Client.Models
{
	public class Tenant
	{
		[JsonPropertyName("name")]
		public string Name { get; init; }

		public Tenant(string name)
		{
			Name = name;
		}
	}
}
