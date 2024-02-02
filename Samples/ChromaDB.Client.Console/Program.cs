using ChromaDB.Client.Common.Helpers;
using ChromaDB.Client.Models;
using ChromaDB.Client.Services.Implementations;
using ChromaDB.Client.Services.Interfaces;

namespace ChromaDB.Client.Console
{
	internal class Program
	{
		static async Task Main(string[] args)
		{


			try
			{
				using IChromaDBClient client = new ChromaDBClient(new HttpClient(), new ConfigurationOptions(uri: "http://localhost:8000/api/v1/"));
				
				BaseResponse<List<Collection>> collections = await client.GetCollections(database: "test", tenant: "nedeljko");


				BaseResponse<Collection> collection1 = await client.GetCollectionByName("string5", database: "test", tenant: "nedeljko");
				BaseResponse<Collection> collection11 = await client.GetCollectionByName("string11", database: "test", tenant: "nedeljko");


				client.Dispose();
			}
			catch (Exception ex)
			{
				System.Console.WriteLine($"{ex}");
			}
		}
	}
}
