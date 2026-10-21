# Collections

## Collections and records

```csharp
var collections = await client.ListCollectionsAsync();
var page = await client.ListCollectionsAsync(limit: 10, offset: 20);
var count = await client.CountCollectionsAsync();
await collectionClient.ModifyAsync(name: "renamed", metadata: new Dictionary<string, object> { ["owner"] = "me" });
var first = await collectionClient.PeekAsync(5);
await client.DeleteCollectionAsync("renamed");
var deleted = await client.DeleteCollectionIfExistsAsync("renamed");   // false: it no longer exists
var heartbeat = await client.HeartbeatAsync();
var checks = await client.GetPreFlightChecksAsync();   // MaxBatchSize, SupportsBase64Encoding
var identity = await client.GetUserIdentityAsync();    // UserId, Tenant, Databases
var database = await client.GetDatabaseAsync("my_database");
```

`ModifyAsync` changes the name or the metadata of a collection. `PeekAsync` returns its first records. `DeleteCollectionIfExistsAsync` takes a missing collection as deleted already, which the client recognizes as `CollectionExistsAsync` does (KD-43 in [docs/COMPATIBILITY.md](COMPATIBILITY.md)).

`DeleteCollectionAsync`, `DeleteCollectionIfExistsAsync` and `DeleteDatabaseAsync` send one request, as Chroma does. If your records have lists in their metadata, pass `deleteRecordsFirst: true`: on Chroma 1.x, except Chroma Cloud, the client then deletes the records first, in batches. That takes about two requests for every 5,461 records, about 370 for a million. If it stops halfway, the collection keeps part of its records or none of them: delete it again. If the server returns records after their delete, the client throws a `ChromaException` and keeps the collection, instead of deleting it with them.

The lists of the records of a deleted collection: KD-12 in [COMPATIBILITY.md](COMPATIBILITY.md#known-defects-of-the-servers).

## Tenants and databases

```csharp
await client.CreateTenantAsync("my_tenant");
await client.CreateDatabaseAsync("my_database", tenant: "my_tenant");

var options = new ChromaConfigurationOptions(uri: "http://localhost:8000", tenant: "my_tenant", database: "my_database");
```

The collections created with these options belong to that tenant and database.

```csharp
var databases = await client.ListDatabasesAsync(tenant: "my_tenant");
var page = await client.ListDatabasesAsync(limit: 10, offset: 20, tenant: "my_tenant");
await client.DeleteDatabaseAsync("my_database", tenant: "my_tenant");
```

[docs/COMPATIBILITY.md](COMPATIBILITY.md) lists the servers that have `ListDatabasesAsync` and `DeleteDatabaseAsync`.

## Collections by id

```csharp
var collection = await client.GetCollectionByIdAsync(id);
```

`GetCollectionByIdAsync` looks for the id in the tenant and database of the options, or in the ones you pass. [docs/COMPATIBILITY.md](COMPATIBILITY.md) lists the servers that have it.

A `ChromaClient` hands out the clients for the records of its collections, with its options and `HttpClient`, without sending a request. The id and the name are enough:

```csharp
var collectionClient = client.GetCollectionClient(collection);
var sameCollection = client.GetCollectionClient(collectionId, "my_collection");
var byName = client.GetCollectionClient("my_collection");   // whichever collection has the name

// or without a ChromaClient; the tenant and database come from the options
var standalone = new ChromaCollectionClient(collectionId, "my_collection", options, httpClient);
```

A collection client made by name reads the collection before its first request, which `GetCollectionAsync` returns. When a request fails on that id, it reads the collection again: if the name has another id, as when the collection was deleted and created again elsewhere, the operation runs again, once, on that collection, and otherwise the failure stands. The servers tell a collection gone in their own ways ([docs/COMPATIBILITY.md](COMPATIBILITY.md)), so the client compares the ids.

## Collections created when missing

Two ways give a client for the records of a collection that is created when the name has none:

```csharp
// When the first operation goes: no request now
var lazy = client.GetCollectionClient("my_collection", createIfMissing: true);

// Now: the request goes in this call
var now = await client.GetOrCreateCollectionClientAsync("my_collection");

// With the metadata, the configuration or the schema of a new collection
var defined = client.GetCollectionClient(new ChromaCollectionDefinition("my_collection") { Metadata = metadata }, createIfMissing: true);
```

- `GetCollectionClient(name, createIfMissing: true)` sends no request. Its first operation gets or creates the collection, with one request that the operations which start before it ends share; each of them keeps its own cancellation. An error of that request comes from the first operation, and the next one tries again.
- `GetOrCreateCollectionClientAsync(name)` sends the request now, so its error comes from that call; then the client works as the other one.
- Both keep the collection. When a request fails because the collection was deleted elsewhere, they get or create it again, empty, with the same definition, and run the operation again, once: never a write that went in part, which throws with the number of records that went.
- With `createIfMissing: false`, the client is the one of `GetCollectionClient(name)`, which never creates the collection.
- The client does not take the embedding generator of a definition: give it with `WithEmbeddingGenerator`.
