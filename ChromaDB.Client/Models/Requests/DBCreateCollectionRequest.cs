namespace ChromaDB.Client.Models.Requests;

public class DBCreateCollectionRequest : DBGetOrCreateCollectionRequestBase
{
	protected override bool GetOrCreate { get; } = false;
}
