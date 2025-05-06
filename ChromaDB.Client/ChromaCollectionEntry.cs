namespace ChromaDB.Client.Models;

public class ChromaCollectionEntry
{
	public string Id { get; }
	public ReadOnlyMemory<float>? Embeddings { get; init; }
	public Dictionary<string, object>? Metadata { get; init; }
	public string? Document { get; init; }
	public List<string?>? Uris { get; init; }

	public ChromaCollectionEntry(string id)
	{
		Id = id;
	}
}
