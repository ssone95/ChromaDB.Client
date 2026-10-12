# Chroma Cloud

[COMPATIBILITY.md](COMPATIBILITY.md#chroma-cloud) has the quotas and the behaviors of Chroma Cloud.

```csharp
var options = new ChromaConfigurationOptions(uri: "https://api.trychroma.com").WithChromaToken(apiKey);
var client = await new ChromaClient(options, httpClient).WithTenantAndDatabaseFromIdentityAsync();
```

`WithTenantAndDatabaseFromIdentityAsync` takes the tenant and the database from the credentials, as the `CloudClient` of the Python client does:

- an API key for one database gives both;
- an API key for a whole tenant gives only the tenant, so the database goes in the options;
- a tenant or database set in the options, other than the default ones, must match the one the key gives, when it gives one;
- a single Chroma server always answers with `default_tenant` and `default_database`.

These operations exist only on Chroma Cloud; a single Chroma server answers them with an error.

```csharp
var copy = await collectionClient.ForkAsync("my_collection_copy");
var forks = await collectionClient.ForkCountAsync();
var status = await collectionClient.GetIndexingStatusAsync();
```

`ForkAsync` copies a collection with its records under a new name. `GetIndexingStatusAsync` tells how many writes are indexed.

On every server, `CountAsync(ChromaReadLevel.IndexOnly)` counts only the records already indexed. Chroma Cloud indexes them later, so right after a write the count can be lower, even 0, while `CountAsync()` already sees them. A single server indexes them at once and gives the same count.

```csharp
await collectionClient.AddAsync(ids, embeddings: embeddings);                  // 6 records
var all = await collectionClient.CountAsync();                                 // 6
var indexed = await collectionClient.CountAsync(ChromaReadLevel.IndexOnly);    // on Chroma Cloud, from 0 to 6 until they are indexed
```

```csharp
var (attached, created) = await collectionClient.AttachFunctionAsync(ChromaFunctions.Statistics, "my_stats", "my_stats_output");
var function = await collectionClient.GetAttachedFunctionAsync("my_stats");
await collectionClient.DetachFunctionAsync("my_stats", deleteOutputCollection: true);
```

The functions of Chroma Cloud, `ChromaFunctions.Statistics` and `ChromaFunctions.RecordCounter`, run on the records of a collection and write their results to an output collection.

```csharp
await client.UpdateTenantAsync("my_tenant", resourceName: "my_org");
var collection = await client.GetCollectionByCrnAsync("my_org:my_database:my_collection");
```

`UpdateTenantAsync` sets the resource name of a tenant, which `GetTenantAsync` returns as `ResourceName`. `GetCollectionByCrnAsync` gets a collection by its Chroma Resource Name.

`GetCollectionByCrnAsync` on Chroma Cloud: KD-26 in [COMPATIBILITY.md](COMPATIBILITY.md#known-defects-of-the-servers).

## Batches on Chroma Cloud

So on Chroma Cloud, at `*.trychroma.com`, the client uses 300 when no limit is given. Behind a proxy or another address, `WithChromaCloud()` on the options says the server is Chroma Cloud, for this and the other rules of Chroma Cloud. A server may reject a batch over its quota of records before writing any of it, as Chroma Cloud does with "current usage of 301 exceeds limit of 300". The client then sends that batch, the rest and the next writes in batches of the quota. A raised quota needs its limit:

```csharp
var options = new ChromaConfigurationOptions(uri: "https://api.trychroma.com").WithChromaToken(apiKey)
	.WithTenant(tenant).WithDatabase(database)
	.WithBatchSplitting(maxBatchSize: 1000); // a quota raised to 1000 records
```

`ChromaCloudQuotas` holds the default quotas of a Chroma Cloud tenant, as its documentation lists them: 300 records per request, 8,182 bytes per metadata value, 16,384 per document, 32 metadata keys of at most 36 bytes, 8 predicates per filter. A single Chroma server has none of them. Chroma Cloud creates a collection whose schema names a key beyond 36 bytes, and then rejects every write with that key, so on Chroma Cloud `CreateCollectionAsync` and `GetOrCreateCollectionAsync` throw an `ArgumentException` before the request. Chroma Cloud counts the nulls of an update or an upsert against the 32 keys too: a write with nulls that would take a record beyond them throws an `ArgumentException` before it goes, unless `WithReadsBeyondKeyQuota()`, which reads the record first ([metadata.md](metadata.md)). A record with more than 32 values and no null goes, and Chroma Cloud rejects it with `422`.
