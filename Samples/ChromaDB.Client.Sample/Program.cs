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

BaseResponse<Collection> createCollectionResponse = await client.CreateCollection(new DBCreateCollectionRequest()
{
	Name = "string5",
}, database: "test", tenant: "nedeljko");

BaseResponse<List<Collection>> collections = await client.ListCollections(database: "test", tenant: "nedeljko");

BaseResponse<Collection> collection1 = await client.GetCollection("string5", database: "test", tenant: "nedeljko");

IChromaCollectionClient string5Client = new ChromaCollectionClient(collection1.Data!, httpClient);

BaseResponse<List<CollectionEntry>> getResponse = await string5Client.Get(new CollectionGetRequest()
{
	Ids = ["340a36ad-c38a-406c-be38-250174aee5a4"],
	Include = ["metadatas", "documents", "embeddings"],
});

BaseResponse<CollectionEntriesQueryResponse> queryResponse = await string5Client.Query(new CollectionQueryRequest()
{
	Ids = ["340a36ad-c38a-406c-be38-250174aee5a4"],
	Include = ["metadatas", "documents", "embeddings"],
	QueryEmbeddings =
	[
		[1f, 0.5f, 0f, -0.5f, -1f]
	],
});
