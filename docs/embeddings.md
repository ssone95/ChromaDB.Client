# Embeddings

## The embeddings of the Python client

[ChromaDotNet.Embeddings](https://www.nuget.org/packages/ChromaDotNet.Embeddings) computes the vectors of the default embedding function of Chroma, the one the Python client uses when a collection has no other: the model all-MiniLM-L6-v2, with 384 dimensions. So .NET and Python read the same collection with the same embeddings.

```
dotnet add package ChromaDotNet.Embeddings --prerelease
```

```csharp
using ChromaDB.Client;
using ChromaDB.Client.Models;
using ChromaDotNet.Embeddings;

using var embeddings = await ChromaDefaultEmbeddingGenerator.CreateAsync();
var collection = await client.GetOrCreateCollectionAsync(new ChromaCollectionDefinition("fruits") { EmbeddingGenerator = embeddings });
var fruits = client.GetCollectionClient(collection).WithEmbeddingGenerator(embeddings);

await fruits.AddAsync(["apple", "lemon"], documents: ["A crunchy apple", "A sour lemon"]);
var nearest = await fruits.QueryAsync("something sour", nResults: 1);
```

The first `CreateAsync` downloads the model into the cache of the Python client, or uses the one Python downloaded. The definition declares the function `default`, as the Python client does. The package is a preview, and runs in 64-bit processes only, not on Intel Macs: see its [repository](https://github.com/ChromaDotNet/ChromaDB.Embeddings).

The [SearchByText](https://github.com/ChromaDotNet/ChromaDB.Local/tree/main/samples/SearchByText) sample of ChromaDotNet.Local stores and searches texts this way, on a Chroma started without Docker. [Open it in a Codespace](https://codespaces.new/ChromaDotNet/ChromaDB.Local?quickstart=1).

## Embeddings from a generator

```csharp
using Microsoft.Extensions.AI;
using OpenAI;

// From Microsoft.Extensions.AI.OpenAI; any other IEmbeddingGenerator<string, Embedding<float>> works the same way.
IEmbeddingGenerator<string, Embedding<float>> generator = new OpenAIClient(apiKey).GetEmbeddingClient("text-embedding-3-small").AsIEmbeddingGenerator();
var collectionClient = client.GetCollectionClient(collection).WithEmbeddingGenerator(generator);

await collectionClient.AddAsync(["a", "b"], documents: ["apples are red", "kiwis are green"]);
var nearest = await collectionClient.QueryAsync("a red fruit", nResults: 1);
```

`WithEmbeddingGenerator` takes any `IEmbeddingGenerator<string, Embedding<float>>` of Microsoft.Extensions.AI. The client of the collection then computes the embeddings, as an embedding function does in the Python client of Chroma:

- `AddAsync`, `UpdateAsync` and `UpsertAsync` compute the embeddings of the records given with documents and without embeddings. Every record then needs a document: a null document throws an `ArgumentException` before any request.
- Records given with embeddings keep them, and records without documents go as they are.
- `QueryAsync(queryText)` and `QueryTextsAsync(queryTexts)` compute the embeddings of the query texts, as `query_texts` in the Python client. Without a generator they throw an `InvalidOperationException`.
- The other `With` methods of the collection client keep the generator. The client does not dispose it.

The builds for .NET 8 and .NET Framework have these methods. The build for .NET Standard 2.0, which applications on .NET Core 3.1 to 7 use, does not.

## The default embedding function of Chroma

The Python client of Chroma computes the embeddings with `all-MiniLM-L6-v2` when a collection has no other function, and stores the function as `default` in the configuration of the collection. With the generator of that function, a new collection declares it too:

```csharp
var collection = await client.GetOrCreateCollectionAsync(new ChromaCollectionDefinition("fruits") { EmbeddingGenerator = generator });
```

- The client recognizes the generator by its metadata: the provider `chroma` and the model `all-MiniLM-L6-v2`. Another generator declares nothing, and the collection is created as without it.
- `Configuration.EmbeddingFunction`, when set, wins over the function of the generator.
- Chroma 0.x takes no embedding function, so there the collection is created without it. Chroma 1.0.0 to 1.0.5 take it without reporting it ([docs/COMPATIBILITY.md](COMPATIBILITY.md)).
- The definition only declares the function: the client of the collection gets the generator with `WithEmbeddingGenerator`.
