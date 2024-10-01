namespace ChromaDB.Client.Models;

public class CollectionQueryEntry
{
	public string Id { get; }
	public float Distance { get; init; }
	public Dictionary<string, object>? Metadata { get; init; }
	public List<float>? Embeddings { get; init; }
	public string? Document { get; init; }
	public List<string?>? Uris { get; init; }
	public dynamic? Data { get; init; }

	public CollectionQueryEntry(string id)
	{
		Id = id;
	}
}
