namespace ChromaDB.Client.Models;

/// <summary>
/// The records to add, update or upsert. A new field of Chroma becomes a new property here, without changing the
/// methods.
/// </summary>
public class ChromaRecords
{
	/// <summary>
	/// The ids of the records.
	/// </summary>
	public IReadOnlyList<string> Ids { get; }
	/// <summary>
	/// The embeddings of the records, in the order of the ids.
	/// </summary>
	public IReadOnlyList<ReadOnlyMemory<float>>? Embeddings { get; init; }
	/// <summary>
	/// The metadatas of the records, in the order of the ids; null for a record without metadata. In <c>UpdateAsync</c> and
	/// <c>UpsertAsync</c> a null value, written <c>null!</c> as the type does not allow it, or an empty list deletes the key, and the
	/// sparse vectors the client computes from its text; the keys the metadata does not have stay. <c>AddAsync</c> rejects both.
	/// <c>ChromaMetadataConvert.ToMetadata</c> builds a metadata with null values from .NET values.
	/// </summary>
	public IReadOnlyList<IReadOnlyDictionary<string, object>?>? Metadatas { get; init; }
	/// <summary>
	/// The documents of the records, in the order of the ids; null for a record without a document. In <c>UpdateAsync</c> and
	/// <c>UpsertAsync</c> a null document keeps the stored one, as in Chroma 1.x and Chroma Cloud, unless <c>NullDocumentsDelete</c>; the
	/// 0.x servers delete it.
	/// </summary>
	public IReadOnlyList<string?>? Documents { get; init; }
	/// <summary>
	/// Whether a null document deletes the stored document in <c>UpdateAsync</c> and <c>UpsertAsync</c>, and the sparse vectors the
	/// client computes from it, instead of keeping it as Chroma 1.x does. Chroma 1.x has no deletion of a document: the client writes an empty
	/// one, which only the copy of a client with <c>WithDocumentCopyKey</c> tells from an empty text, and reads back as null. A client
	/// without it throws an <c>ArgumentException</c> for a null document, before any request.
	/// </summary>
	public bool NullDocumentsDelete { get; init; }
	/// <summary>
	/// The URIs of the records, in the order of the ids. A record without a URI has null at its position.
	/// </summary>
	public IReadOnlyList<string?>? Uris { get; init; }

	/// <summary>
	/// Creates the records with the ids.
	/// </summary>
	/// <param name="ids">The ids of the records.</param>
	public ChromaRecords(IReadOnlyList<string> ids)
	{
		Ids = ids;
	}
}
