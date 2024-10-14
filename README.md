# ChromaDB.Client

_ChromaDB.Client_ is a C# cross-platform library for communication with Chroma vector database. Chroma is the AI-native open-source vector database. Chroma makes it easy to build LLM apps by making knowledge, facts, and skills pluggable for LLMs.

With _ChromaDB.Client_, you can easily connect to a Chroma instance, create and manage collections, perform CRUD operations on the data in the collections, and execute other available operations such as nearest neighbor search and filtering.

## Example

```csharp
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
```

## Status

[![NuGet Downloads](https://img.shields.io/nuget/dt/ChromaDB.Client)](https://www.nuget.org/packages/ChromaDB.Client/)
[![NuGet](https://img.shields.io/nuget/v/ChromaDB.Client)](https://www.nuget.org/packages/ChromaDB.Client/)
[![NuGet Prerelease](https://img.shields.io/nuget/vpre/ChromaDB.Client)](https://www.nuget.org/packages/ChromaDB.Client/)
[![License](https://img.shields.io/github/license/ssone95/ChromaDB.Client)](https://github.com/ssone95/ChromaDB.Client/LICENSE)
[![CI](https://img.shields.io/github/actions/workflow/status/ssone95/ChromaDB.Client/ci.yml)](https://github.com/ssone95/ChromaDB.Client/actions/workflows/ci.yml)