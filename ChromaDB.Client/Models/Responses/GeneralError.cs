using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ChromaDB.Client.Models.Responses
{
	public class GeneralError
	{
		[JsonPropertyName("error")]
		public string? ErrorMessage { get; set; }
	}
}
