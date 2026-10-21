# Migration guide: 1.x to 2.x

ChromaDotNet.Client 2.x uses the v2 API of Chroma, and its methods follow the .NET conventions. Both are **breaking changes**: the URI, the package id and the method names change.

This fork publishes the package as `ChromaDotNet.Client`; the namespaces stay `ChromaDB.Client`.

## What changed

Chroma moved from the v1 API to the v2 API. The main change is the structure of the URLs:

**v1 API:**
```
http://localhost:8000/api/v1/
```

**v2 API:**
```
http://localhost:8000/api/v2/
```

In the v2 API the tenant and the database are part of the path, instead of query parameters:

- **v1**: `/api/v1/collections?tenant={t}&database={d}`
- **v2**: `/api/v2/tenants/{t}/databases/{d}/collections`

## Migration steps

### Step 1: check the Chroma server

The v2 API is available from Chroma 0.5.16. Chroma 0.5.16 to 0.5.23 serve both v1 and v2, the v1 API of 0.6.3 fails on most requests, and 1.0 and later serve only v2.

### Step 2: update the configuration

Change the URI of `ChromaConfigurationOptions` from `/api/v1/` to `/api/v2/`:

**Before (1.x):**
```csharp
var configOptions = new ChromaConfigurationOptions(uri: "http://localhost:8000/api/v1/");
```

**After (2.x):**
```csharp
var configOptions = new ChromaConfigurationOptions(uri: "http://localhost:8000/api/v2/");
```

Just the address of the server works too: `new ChromaConfigurationOptions(uri: "http://localhost:8000")` gets `/api/v2/`. A URI with another path is used as it is.

### Step 3: update the package reference

Reference the latest `ChromaDotNet.Client`:

```bash
dotnet add package ChromaDotNet.Client
```

## What else changes

- **Namespaces:** they stay the same. Keep `using ChromaDB.Client;`, and `using ChromaDB.Client.Models;` for the models.
- **Features:** everything in 1.x still works, and there is much more: see the README.
- **Methods:** they follow the .NET conventions. The names end in `Async` (`GetOrCreateCollectionAsync`, `AddAsync`, `QueryAsync`...), they take an optional `CancellationToken`, and they take and return read-only lists and dictionaries. See the [breaking changes summary](#breaking-changes-summary).
- **Request and response models:** the same data. `ChromaCollectionQueryEntry.Distance` is a `float?`, and a record has `Embedding` and `Uri` for its embedding and its URI.

## Example: a complete migration

**Before (1.x):**
```csharp
using ChromaDB.Client;

var configOptions = new ChromaConfigurationOptions(uri: "http://localhost:8000/api/v1/");
using var httpClient = new HttpClient();
var client = new ChromaClient(configOptions, httpClient);

var collection = await client.GetOrCreateCollection("my_collection");
var collectionClient = new ChromaCollectionClient(collection, configOptions, httpClient);

await collectionClient.Add(
    ["doc1"], 
    embeddings: [new([1f, 0.5f, 0f, -0.5f, -1f])]
);
```

**After (2.x):**
```csharp
using ChromaDB.Client;

// /api/v1/ → /api/v2/
var configOptions = new ChromaConfigurationOptions(uri: "http://localhost:8000/api/v2/");
using var httpClient = new HttpClient();
var client = new ChromaClient(configOptions, httpClient);

// The methods end in Async
var collection = await client.GetOrCreateCollectionAsync("my_collection");
// The client of the collection shares the options and the HttpClient of the client
var collectionClient = client.GetCollectionClient(collection);

await collectionClient.AddAsync(
    ["doc1"], 
    embeddings: [new([1f, 0.5f, 0f, -0.5f, -1f])]
);
```

`new ChromaCollectionClient(collection, configOptions, httpClient)` still works; `client.GetCollectionClient(collection)` gives the same client without repeating the options and the `HttpClient`.

## Workarounds of 1.x to remove

Some applications kept 1.x working on Chroma 1.x with a `DelegatingHandler` that rewrote the routes from `/api/v1/` to `/api/v2/`, added the tenant and the database to the path, or read them from `auth/identity`. 2.x sends the v2 routes itself, with the tenant and the database of the options (`default_tenant` and `default_database` when they are not set), so remove such a handler. To take the tenant and the database from the credentials, as on Chroma Cloud, call `await client.WithTenantAndDatabaseFromIdentityAsync()`: it returns a client with the ones that `auth/identity` gives.

## Dependency injection

`ChromaDotNet.Client.DependencyInjection` registers the client in `Microsoft.Extensions.DependencyInjection`: `services.AddChromaClient(_ => options)`, `AddKeyedChromaClient` for more than one server, tenant or database, and `serviceProvider.CreateChromaClient(options, httpClientName)` for a client kept elsewhere. `AddChromaClientFromServices(serviceProvider => options)` makes the options from the services of the application, like `IConfiguration`, when the client is resolved. The client sends its requests through `IHttpClientFactory`, so a DNS change of the server is seen. See [docs/dependency-injection.md](../docs/dependency-injection.md).

## Behaviors to know

These are not breaking changes, but they differ from 1.x:

- **Reads go again.** After an error that can pass, like `500` or a request that fails on the network, a read goes again, up to 4 attempts by default, with a random delay of 40 ms or more; a write never goes again. `WithReadRetries(ChromaReadRetries.None)` turns it off. See [docs/errors.md](../docs/errors.md#reads-that-go-again).
- **Distances.** `ChromaCollectionQueryEntry.Distance` is `NaN` where Chroma sends no number, like the distance to an embedding beyond the range of a float on Chroma 1.x.
- **Empty metadata.** An empty metadata dictionary goes as no metadata, and every tested Chroma treats the two the same.
- **Embeddings of a query.** `QueryAsync` returns the embeddings only with `ChromaQueryInclude.Embeddings`, as in 1.x: by default it returns the metadata, the documents and the distances.

## Troubleshooting

### "404 Not Found" errors

A 404 after the upgrade usually means that the URI still points to the v1 API:

- check that the URI contains `/api/v2/`, not `/api/v1/`;
- check that the Chroma server supports the v2 API.

Chroma 1.0 and later answer the v1 routes with `410 Gone` and the message "The v1 API is deprecated. Please use /v2 apis".

### Version of the server

Chroma supports the v2 API from 0.5.16. To check the version of the server:

```csharp
var version = await client.GetVersionAsync();
Console.WriteLine($"ChromaDB Server Version: {version}");
```

## Breaking changes summary

| Change | Impact | What to do |
|--------|--------|------------|
| API endpoint | High | Change the URI from `/api/v1/` to `/api/v2/` |
| Package id | High | Reference `ChromaDotNet.Client` instead of `ChromaDB.Client` |
| URL structure | None | The client handles it |
| Request and response | Low | `ChromaCollectionQueryEntry.Distance` is `float?`, null when the query does not include `ChromaQueryInclude.Distances` |
| Method names | Medium | Add `Async` to the name of each asynchronous method: `GetOrCreateCollection` → `GetOrCreateCollectionAsync`, `Add` → `AddAsync`, `Query` → `QueryAsync` |
| Cancellation | Low | Every asynchronous method takes an optional `CancellationToken`. Rebuild, and turn a method group like `client.Heartbeat` passed as a `Func<Task>` into `() => client.HeartbeatAsync()` |
| Lists and dictionaries | Low | Parameters and results are `IReadOnlyList<T>` and `IReadOnlyDictionary<string, object>`. A `List<T>` or a collection expression still goes in, and metadata are written `new Dictionary<string, object> { ["key"] = value }` |
| Records | Low | The embedding of a record is `Embedding` and its URI is `Uri`. `Data` is gone, as no Chroma server fills it |
| Metadata values | Low | Strings come back as strings and lists as `List<object>`. `WithMetadataValues(ChromaMetadataValues.Inferred)` reads them as 1.x did, with dates as `DateTime` |
| Filters | Low | `ChromaWhere` and `ChromaWhereDocument` are gone. Use `ChromaWhereOperator` and `ChromaWhereDocumentOperator`, the types the methods take |
| Options constructor | Low | `defaultTenant:` and `defaultDatabase:` are now `tenant:` and `database:`: `new ChromaConfigurationOptions(uri, tenant: ..., database: ...)` |
| Batches | Low | Writes go in batches of the server's `max_batch_size`, and `GetAsync` reads in pages. `WithBatchSplitting(false)` sends each of them in one request |
| Errors | Low | Network errors, answers that are not the expected JSON, and timeouts throw a `ChromaException`, with the original exception as `InnerException` |

## Need help?

If something goes wrong during the migration:

1. check that the Chroma server supports the v2 API;
2. check that the URI uses `/api/v2/`;
3. read the error message: it tells when the server is not compatible;
4. open an issue on GitHub.

## Going back to 1.x

To go back to the v1 API:

1. go back to the original package, ChromaDB.Client 1.x: remove `ChromaDotNet.Client` and run `dotnet add package ChromaDB.Client --version 1.0.0`;
2. change the URI back to `/api/v1/`.
