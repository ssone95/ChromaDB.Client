# ChromaDB.Client

_ChromaDB.Client_ is a .NET SDK that offers a seamless connection to the Chroma database. It allows creating and managing collections, performing CRUD operations, and executing nearest neighbor search and filtering.

## Example

```csharp
using ChromaDB.Client;

var configOptions = new ChromaConfigurationOptions(uri: "http://localhost:8000/api/v2/");
using var httpClient = new HttpClient();
var client = new ChromaClient(configOptions, httpClient);

Console.WriteLine(await client.GetVersion());

var string5Collection = await client.GetOrCreateCollection("string5");
var string5Client = new ChromaCollectionClient(string5Collection, configOptions, httpClient);

await string5Client.Add(["340a36ad-c38a-406c-be38-250174aee5a4"], embeddings: [new([1f, 0.5f, 0f, -0.5f, -1f])]);

var getResult = await string5Client.Get("340a36ad-c38a-406c-be38-250174aee5a4", include: ChromaGetInclude.Metadatas | ChromaGetInclude.Documents | ChromaGetInclude.Embeddings);
Console.WriteLine($"ID: {getResult!.Id}");

var queryData = await string5Client.Query([new([1f, 0.5f, 0f, -0.5f, -1f]), new([1.5f, 0f, 2f, -1f, -1.5f])], include: ChromaQueryInclude.Metadatas | ChromaQueryInclude.Distances);
foreach (var item in queryData)
{
	foreach (var entry in item)
	{
		Console.WriteLine($"ID: {entry.Id} | Distance: {entry.Distance}");
	}
}
```

## Status

[![NuGet Downloads](https://img.shields.io/nuget/dt/ChromaDB.Client)](https://www.nuget.org/packages/ChromaDB.Client/)
[![NuGet](https://img.shields.io/nuget/v/ChromaDB.Client)](https://www.nuget.org/packages/ChromaDB.Client/)
[![NuGet Prerelease](https://img.shields.io/nuget/vpre/ChromaDB.Client)](https://www.nuget.org/packages/ChromaDB.Client/)
[![License](https://img.shields.io/github/license/ssone95/ChromaDB.Client)](https://github.com/ssone95/ChromaDB.Client/LICENSE)
[![CI](https://img.shields.io/github/actions/workflow/status/ssone95/ChromaDB.Client/ci.yml)](https://github.com/ssone95/ChromaDB.Client/actions/workflows/ci.yml)
