using ChromaDB.Client.Common.Attributes;

namespace ChromaDB.Client.Models;

[ChromaPostRoute(Endpoint = "reset", Source = typeof(Reset), RequestType = typeof(Reset), ResponseType = typeof(bool))]
public class Reset
{ }
