using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Models.Responses;
using ChromaDB.Client.Services.Implementations;
using ChromaDB.Client.Services.Interfaces;

namespace ChromaDB.Client.Console;

internal class Program
{
	static async Task Main(string[] args)
	{
		try
		{
			ConfigurationOptions configOptions = new(uri: "http://localhost:8000/api/v1/");
			using IChromaDBHttpClient httpClient = new ChromaDBHttpClient(configOptions);
			using IChromaDBClient client = new ChromaDBClient(configOptions, httpClient);
			
			BaseResponse<List<Collection>> collections = await client.GetCollections(database: "test", tenant: "nedeljko");

			BaseResponse<Collection> collection1 = await client.GetCollectionByName("string5", database: "test", tenant: "nedeljko");

			IChromaCollectionClient string5Client = new ChromaCollectionFactory(configOptions)
				.Create(collection1.Data!, httpClient);

			BaseResponse<List<CollectionEntry>> getResponse = await string5Client.Get(new CollectionGetRequest()
			{
				Ids = new List<string>() { "340a36ad-c38a-406c-be38-250174aee5a4" },
				Include = new List<string>() { "metadatas", "documents", "embeddings" }
			});

			BaseResponse<CollectionEntriesQueryResponse> queryResponse = await string5Client.Query(new CollectionQueryRequest()
			{
				Ids = new List<string>() { "340a36ad-c38a-406c-be38-250174aee5a4" },
				Include = new List<string>() { "metadatas", "documents", "embeddings" },
				QueryEmbeddings = new List<List<float>>()
				{
					new List<float>() { 1f, 0.5f, 0f, -0.5f, -1f }
				}
			});
		}
		catch (Exception ex)
		{
			System.Console.WriteLine($"{ex}");
		}
	}
}
