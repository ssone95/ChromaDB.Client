namespace ChromaDB.Client.Models.Requests;

public class CreateCollectionRequest : GetOrCreateCollectionRequestBase
{
	protected override bool GetOrCreate { get; } = false;
}
