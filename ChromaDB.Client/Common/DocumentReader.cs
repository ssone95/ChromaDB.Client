namespace ChromaDB.Client.Common;

// How a client with a document copy key or a document deletion key reads the documents. Chroma cannot delete a document, so for one
// deleted with NullDocumentsDelete the client wrote an empty one: an empty document is a deleted one when its copy is not in the metadata,
// or when the metadata has its mark, true under the deletion key. The metadata, read for it, goes away from the results that did not ask
// for it. The default reads the documents and the metadata as they are.
internal readonly struct DocumentReader
{
	private readonly string? _key;
	private readonly bool _mark;
	private readonly bool _dropMetadata;

	public DocumentReader(string key, bool mark, bool keepMetadata)
	{
		_key = key;
		_mark = mark;
		_dropMetadata = !keepMetadata;
	}

	// The metadata of a record without keys, when the metadata is read: empty, not null.
	public static readonly IReadOnlyDictionary<string, object> NoKeys = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object>(new Dictionary<string, object>());

	// Whether the documents need the metadata.
	public bool ReadsMetadata => _key is not null;

	public string? Document(string? document, IReadOnlyDictionary<string, object>? metadata)
		=> _key is null || document is not "" ? document
			: _mark ? (metadata is not null && metadata.TryGetValue(_key, out var mark) && mark is true ? null : document)
			: metadata?.ContainsKey(_key) == true ? document : null;

	public IReadOnlyDictionary<string, object>? Metadata(IReadOnlyDictionary<string, object>? metadata)
		=> _dropMetadata ? null : metadata;
}
