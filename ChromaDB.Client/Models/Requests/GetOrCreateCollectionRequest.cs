namespace ChromaDB.Client.Models.Requests;

public class GetOrCreateCollectionRequest : GetOrCreateCollectionRequestBase
{
	protected override bool GetOrCreate { get; } = true;
}
