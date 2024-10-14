using System.Text.Json.Serialization;

namespace ChromaDB.Client.Models;

public class ChromaHeartbeat
{
	[JsonPropertyName("nanosecond heartbeat")]
	public long NanosecondHeartbeat { get; set; }
}
