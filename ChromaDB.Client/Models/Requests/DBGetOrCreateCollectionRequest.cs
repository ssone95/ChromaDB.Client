namespace ChromaDB.Client.Models.Requests;

public class DBGetOrCreateCollectionRequest : DBGetOrCreateCollectionRequestBase
{
	protected override bool GetOrCreate { get; } = true;
}
