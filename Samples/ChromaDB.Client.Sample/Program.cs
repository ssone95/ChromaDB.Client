using System.Diagnostics;
using ChromaDB.Client;

var configOptions = new ConfigurationOptions(uri: "http://localhost:8000/api/v1/");
using var httpClient = new ChromaDBHttpClient(configOptions);
var client = new ChromaDBClient(configOptions, httpClient);

Console.WriteLine((await client.GetVersion()).Data);

var getOrCreateResponse = await client.GetOrCreateCollection("string5");
Trace.Assert(getOrCreateResponse.Success);

var string5Client = new ChromaDBCollectionClient(getOrCreateResponse.Data, httpClient);

var addResponse = await string5Client.Add(["340a36ad-c38a-406c-be38-250174aee5a4"], embeddings: [[1f, 0.5f, 0f, -0.5f, -1f]]);
Trace.Assert(addResponse.Success);

var getResponse = await string5Client.Get(["340a36ad-c38a-406c-be38-250174aee5a4"], include: ["metadatas", "documents", "embeddings"]);
if (getResponse.Success)
{
	foreach (var entry in getResponse.Data)
	{
		Console.WriteLine($"ID: {entry.Id}");
	}
}

var queryResponse = await string5Client.Query([[1f, 0.5f, 0f, -0.5f, -1f], [1.5f, 0f, 2f, -1f, -1.5f]],
	include: ["metadatas", "distances"]);
if (queryResponse.Success)
{
	foreach (var item in queryResponse.Data)
	{
		foreach (var entry in item)
		{
			Console.WriteLine($"ID: {entry.Id} | Distance: {entry.Distance}");
		}
	}
}
