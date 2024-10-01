using ChromaDB.Client;
using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Models.Responses;
using ChromaDB.Client.Services.Implementations;
using ChromaDB.Client.Services.Interfaces;

ConfigurationOptions configOptions = new(uri: "http://localhost:8000/api/v1/");
using IChromaDBHttpClient httpClient = new ChromaDBHttpClient(configOptions);
IChromaDBClient client = new ChromaDBClient(configOptions, httpClient);

Console.WriteLine((await client.GetVersion()).Data);

BaseResponse<Collection> collectionResponse = await client.GetOrCreateCollection(new() { Name = "string5" });

IChromaDBCollectionClient string5Client = new ChromaDBCollectionClient(collectionResponse.Data!, httpClient);

BaseResponse<List<CollectionEntry>> getResponse = await string5Client.Get(new CollectionGetRequest()
{
	Ids = ["340a36ad-c38a-406c-be38-250174aee5a4"],
	Include = ["metadatas", "documents", "embeddings"],
});
if (getResponse.Success)
{
	foreach (var entry in getResponse.Data!)
	{
		Console.WriteLine(entry.Id);
	}
}

BaseResponse<CollectionEntriesQueryResponse> queryResponse = await string5Client.Query(new CollectionQueryRequest()
{
	Ids = ["340a36ad-c38a-406c-be38-250174aee5a4"],
	Include = ["metadatas", "documents", "embeddings"],
	QueryEmbeddings =
	[
		[1f, 0.5f, 0f, -0.5f, -1f]
	],
});
if (queryResponse.Success)
{
	foreach (var id in queryResponse.Data!.Ids)
	{
		Console.WriteLine(id);
	}
}
