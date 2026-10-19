# Records

## Records with URIs

```csharp
await collectionClient.AddAsync(new ChromaRecords(["a"]) { Embeddings = [new([1f, 0.5f, 0f])], Uris = ["s3://bucket/a.png"] });

var entries = await collectionClient.GetAsync(["a"], include: ChromaGetInclude.Uris);
Console.WriteLine(entries[0].Uri);
```

`ChromaRecords` holds the ids, embeddings, metadata, documents and URIs that `AddAsync`, `UpdateAsync` and `UpsertAsync` write.

The client does not compute embeddings, so pass them yourself.

## Large writes

By default, `AddAsync`, `UpdateAsync`, `UpsertAsync` and `DeleteAsync` send their records in batches of the server's `max_batch_size`, one request after the other. The client asks `pre-flight-checks` once. Before the first batch, the client checks that the embeddings, metadatas, documents and URIs are as many as the ids, and that the embeddings hold finite numbers, and throws an `ArgumentException` otherwise. If a batch fails, the earlier ones stay written, and the `ChromaException` says how many records went. A collection client made by name then does not run the write again on a collection created again under the name. A server without `pre-flight-checks` ([docs/COMPATIBILITY.md](COMPATIBILITY.md)) gets the records in one request, and so does a server whose answer the client cannot read. `WithBatchSplitting(false)` sends them in one request, as earlier versions did.

```csharp
var options = new ChromaConfigurationOptions(uri: "http://localhost:8000").WithBatchSplitting(false);
```

`WithBatchSplitting(maxBatchSize)` uses the smaller of that limit and the server's, or that limit alone where the server declares none.

On Chroma Cloud: [Batches on Chroma Cloud](chroma-cloud.md#batches-on-chroma-cloud).

## Deleting records

```csharp
var deleted = await collectionClient.DeleteAsync(new ChromaDelete { WhereDocument = ChromaWhereDocumentOperator.Contains("draft"), Limit = 100 });
```

`ChromaDelete` holds the ids, the filters and the limit of a delete.

- Without ids it deletes by the filters only. Without ids and filters it throws an `ArgumentException`, since it would select every record.
- `DeleteAllAsync()` deletes every record of the collection, which stays, and returns how many it deleted: a page of ids at a time, as many as the batch size of the server and of Chroma Cloud, read from the start.
- As in Chroma and its Python client, the ids cannot be an empty list, and the limit needs a `where` or `where_document` filter and cannot be negative.
- Chroma 1.5.3 and later apply `Limit` and answer a count, which `DeleteAsync` returns: for a delete by ids it is the number of ids given, also those without a record. On earlier servers it returns null.
- Earlier servers ignore the limit and would delete every matching record. So before a delete with a limit, the client reads the OpenAPI description of the server once, and throws a `ChromaException` without sending the delete if the limit is not declared there.

## Embeddings in base64

Where `pre-flight-checks` declares `supports_base64_encoding`, `AddAsync`, `UpdateAsync` and `UpsertAsync` send the embeddings as base64 strings of their float32 values, about half the size of the numbers. Queries always send numbers. Elsewhere, or when `pre-flight-checks` does not answer, the embeddings go as numbers. [docs/COMPATIBILITY.md](COMPATIBILITY.md) lists the versions.
