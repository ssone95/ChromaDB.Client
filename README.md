[![ChromaDotNet](https://raw.githubusercontent.com/ChromaDotNet/.github/main/assets/logo-64.png)](https://chromadotnet.org)

# ChromaDB.Client

[![NuGet](https://img.shields.io/nuget/v/ChromaDotNet.Client)](https://www.nuget.org/packages/ChromaDotNet.Client/)
[![CI](https://img.shields.io/github/actions/workflow/status/ChromaDotNet/ChromaDB.Client/ci.yml?branch=main)](https://github.com/ChromaDotNet/ChromaDB.Client/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/ChromaDotNet/ChromaDB.Client)](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/LICENSE)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/ChromaDotNet/ChromaDB.Client/badge)](https://scorecard.dev/viewer/?uri=github.com/ChromaDotNet/ChromaDB.Client)
[![OpenSSF Best Practices](https://www.bestpractices.dev/projects/15264/badge)](https://www.bestpractices.dev/projects/15264)

_ChromaDB.Client_, published on NuGet as `ChromaDotNet.Client`, is a .NET client for Chroma and Chroma Cloud. It covers:

- collections, tenants and databases;
- writing and reading records, and nearest neighbor queries with filters;
- the Search API of Chroma Cloud, with hybrid BM25 search;
- OpenTelemetry traces and metrics.

> This is a community project, the continuation of [ssone95/ChromaDB.Client](https://github.com/ssone95/ChromaDB.Client), kept up to date with current Chroma versions. It is not affiliated with or endorsed by Chroma. See [About this fork](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/docs/about-this-fork.md).

Website: [chromadotnet.org](https://chromadotnet.org)

## Used by

Built on the client:

- [CommunityToolkit.VectorData.Chroma](https://www.nuget.org/packages/CommunityToolkit.VectorData.Chroma): the Chroma provider for Microsoft.Extensions.VectorData, in the [AI Community Toolkit](https://github.com/CommunityToolkit/AI/blob/546aa1b8e306e7518b0225341ea62cd86c916297/MEVD/src/Chroma/Chroma.csproj#L18) of the .NET Foundation. It works with Semantic Kernel and Agent Framework.
  - Cited in [Semantic Kernel](https://github.com/microsoft/semantic-kernel/blob/7e68276a4a85022dd4da255beae3263ab5cbf059/dotnet/src/VectorData/Chroma/README.md#L1).
  - Cited in [Microsoft Agent Framework](https://github.com/microsoft/agent-framework/blob/deecbcb062198f926516f89b65e5b1676e4514c2/dotnet/samples/community-projects.md#L26).
- CommunityToolkit.Aspire.Chroma: the Chroma client integration, in the [Aspire Community Toolkit](https://github.com/CommunityToolkit/Aspire/blob/523b84b27c5d019d242d95c2252046ba3619303c/src/CommunityToolkit.Aspire.Chroma/CommunityToolkit.Aspire.Chroma.csproj#L12) of the .NET Foundation.

Through CommunityToolkit.VectorData.Chroma:

- [Generative AI for Beginners .NET](https://github.com/microsoft/Generative-AI-for-beginners-dotnet/blob/359ffe5adf0cb74a397fd99af8786e14d0930ca3/samples/CoreSamples/RAGSimple-05MEAIVectorsChroma/RAGSimple-05MEAIVectorsChroma.csproj#L12): a RAG sample in the Microsoft course.
- [LangChain .NET](https://github.com/tryAGI/LangChain.Databases/blob/17c118f863ef1437b4ecaa0222081eb045b38a87/src/IntegrationTests/LangChain.Databases.IntegrationTests.csproj#L19): the integration tests of LangChain.Databases.

## Installation

```
dotnet add package ChromaDotNet.Client
```

`ChromaDotNet.Client.DependencyInjection` for `Microsoft.Extensions.DependencyInjection`: [Dependency injection](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/docs/dependency-injection.md).

## Example

The models, like `ChromaRecords`, `ChromaQuery` and `ChromaCollectionDefinition`, are in `ChromaDB.Client.Models`. `AddChromaClient` is in `ChromaDB.Client.DependencyInjection`.

```csharp
using ChromaDB.Client;
using ChromaDB.Client.Models;

var configOptions = new ChromaConfigurationOptions(uri: "http://localhost:8000/api/v2/");
using var httpClient = new HttpClient();
var client = new ChromaClient(configOptions, httpClient);

Console.WriteLine(await client.GetVersionAsync());

var string5Collection = await client.GetOrCreateCollectionAsync("string5");
var string5Client = new ChromaCollectionClient(string5Collection, configOptions, httpClient);

await string5Client.AddAsync(["340a36ad-c38a-406c-be38-250174aee5a4"], embeddings: [new([1f, 0.5f, 0f, -0.5f, -1f])]);

var getResult = await string5Client.GetAsync("340a36ad-c38a-406c-be38-250174aee5a4", include: ChromaGetInclude.Metadatas | ChromaGetInclude.Documents | ChromaGetInclude.Embeddings);
Console.WriteLine($"ID: {getResult!.Id}");

var queryData = await string5Client.QueryAsync([new([1f, 0.5f, 0f, -0.5f, -1f]), new([1.5f, 0f, 2f, -1f, -1.5f])], include: ChromaQueryInclude.Metadatas | ChromaQueryInclude.Distances);
foreach (var item in queryData)
{
	foreach (var entry in item)
	{
		Console.WriteLine($"ID: {entry.Id} | Distance: {entry.Distance}");
	}
}
```

The URI, the `HttpClient` and the use from more threads: [Connecting](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/docs/connecting.md).

## Compatibility

[docs/COMPATIBILITY.md](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/docs/COMPATIBILITY.md) lists every tested release, the differences between them and the versions the CI runs.

The [compatibility table](https://chromadotnet.org/compatibility/) shows every check of the latest release on Chroma 1.5.9 and on Chroma Cloud, on each .NET runtime and platform.

## Documentation

- [Connecting](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/docs/connecting.md): the URI, the `HttpClient`, threads, the API version, authentication and connection strings.
- [Dependency injection](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/docs/dependency-injection.md): `AddChromaClient` and `AddKeyedChromaClient`.
- [Collections](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/docs/collections.md): collections, tenants and databases, collections by id.
- [Records](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/docs/records.md): URIs, large writes, deletes and base64 embeddings.
- [Metadata values](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/docs/metadata.md): values, lists, deletions in updates and upserts, document copies, conversions.
- [Queries and filters](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/docs/queries-and-filters.md): queries, `GetAsync` in pages, where and document filters.
- [Upsert strategies](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/docs/upsert-strategies.md): `Server` and `SkipUnchangedEmbeddings`.
- [Index settings](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/docs/index-settings.md): the distance, the HNSW and SPANN settings, `ModifyConfigurationAsync`.
- [Errors](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/docs/errors.md): `ChromaException` and `CollectionExistsAsync`.
- [Mocks in tests](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/docs/mocking.md): mocking `ChromaClient` and `ChromaCollectionClient`.
- [Health, traces and metrics](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/docs/observability.md): `HealthcheckAsync` and OpenTelemetry.
- [Chroma Cloud](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/docs/chroma-cloud.md): options, identity, batches, quotas and the operations of Chroma Cloud.
- [Search](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/docs/search.md): the Search API and hybrid search with BM25.
- [Sparse vectors and schema](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/docs/sparse-vectors-and-schema.md): sparse vectors, BM25 indexes and the indexes of a schema.
- [Platforms](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/docs/platforms.md): the builds, trimming and NativeAOT.
- [About this fork](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/docs/about-this-fork.md): the original project and what this fork does.

## Migrating from ChromaDB.Client 1.x

The [migration guide](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/v2-migration/MIGRATION_GUIDE_V2.md) has the details, and the [release notes](https://github.com/ChromaDotNet/ChromaDB.Client/releases) list the changes of every version.

## Contributing

[CONTRIBUTING.md](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/CONTRIBUTING.md) and [SECURITY.md](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/SECURITY.md).

## License

The project is released under the MIT License, keeping the original copyright notice.
