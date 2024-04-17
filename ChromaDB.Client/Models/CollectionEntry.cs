namespace ChromaDB.Client.Models;

public class CollectionEntry
{
	public string Id { get; }
	public List<float>? Embeddings { get; init; }
	public Dictionary<string, object>? Metadata { get; init; }
	public List<string?>? Uris { get; init; }
	public dynamic? Data { get; init; }

	public CollectionEntry(string id)
	{
		Id = id;
	}
}
