namespace ChromaDB.Client.Models.Requests;

internal class GetOrCreateCollectionRequest : GetOrCreateCollectionRequestBase
{
	protected override bool GetOrCreate { get; } = true;
}
