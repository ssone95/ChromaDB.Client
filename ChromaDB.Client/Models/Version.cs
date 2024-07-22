using ChromaDB.Client.Common.Attributes;

namespace ChromaDB.Client.Models;

[ChromaGetRoute(Endpoint = "version", Source = typeof(Version), ResponseType = typeof(string))]
public class Version
{ }
