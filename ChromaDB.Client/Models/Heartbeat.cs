using System.Text.Json.Serialization;

namespace ChromaDB.Client.Models;

public class Heartbeat
{
	[JsonPropertyName("nanosecond heartbeat")]
	public long NanosecondHeartbeat { get; set; }
}
