using System.Text.Json.Serialization;
using ChromaDB.Client.Common.Attributes;

namespace ChromaDB.Client.Models;

[ChromaGetRoute(Endpoint = "", Source = typeof(Heartbeat), ResponseType = typeof(Heartbeat))]
public class Heartbeat
{
	[JsonPropertyName("nanosecond heartbeat")]
	public long NanosecondHeartbeat { get; set; }
}
