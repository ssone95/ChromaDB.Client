# Errors

A failed request throws a `ChromaException`, with the original exception as `InnerException`: an error answer from the server, a network error, an answer that is not the expected JSON, or a timeout. Other exceptions, like an assembly that does not load or a disposed `HttpClient`, are not wrapped.

- `StatusCode` is the HTTP status of the answer, or null when there was no answer, like on a timeout.
- `ErrorType` is the kind of error the server names: `NotFoundError` or `InvalidArgumentError` from Chroma 1.x, `InvalidCollection` from Chroma 0.5 and 0.6, `ValueError` from the v1 API of Chroma 0.4, or null when it names none.

```csharp
if (!await client.CollectionExistsAsync("my_collection"))
{
	await client.CreateCollectionAsync("my_collection");
}
```

When a 0.x server rejects a request with validation errors, the message lists them, like `body.n_results: Input should be a valid integer`.

`CollectionExistsAsync` tells a missing collection apart from other errors on every tested server (KD-43 in [docs/COMPATIBILITY.md](COMPATIBILITY.md)). Any other error, like a bare `404` from a wrong address, throws.

## Reads that go again

A read changes nothing, so after an error that can pass it goes again: each request of `GetAsync`, `PeekAsync`, `QueryAsync`, `SearchAsync` and `CountAsync`, and the reads that a write makes first, as the get of `ChromaUpsertStrategy.SkipUnchangedEmbeddings`, the get of `ReadsBeyondKeyQuota` on Chroma Cloud and the pages of ids that `DeleteCollectionAsync` reads with `deleteRecordsFirst`. The errors that can pass are an answer `429`, `500`, `502`, `503` or `504`, other than a missing collection, a request that fails on the network, and an answer whose lists do not have one item for every record. Chroma 1.5.9 answers `500` "Error finding id" or "Error creating hnsw segment reader" to a get with embeddings while other clients write and delete the same records, or answers it without the embedding of a record, and the same get right after goes. After the last attempt the error stands; for an answer whose lists do not have one item for every record, it is a `ChromaException` that says it, like "The answer of the server to a get has 1 id and 0 embeddings". A timeout of the `HttpClient` does not go again: the `ChromaException` with its `TaskCanceledException` stands. A write never goes again: one that failed may have gone.

Before each new attempt the client waits with the exponential backoff with full jitter of AWS: a random time between zero and the smaller of the largest delay and the base delay times 2 to the attempts that went again before. `ChromaReadRetries.Default` makes 4 attempts at most, with a base delay of 50 ms and a largest delay of 1 second. `WithReadRetries` sets others, and `ChromaReadRetries.None` turns them off:

```csharp
var options = new ChromaConfigurationOptions(uri: "http://localhost:8000")
	.WithReadRetries(new ChromaReadRetries(maxAttempts: 6, baseDelay: TimeSpan.FromMilliseconds(100), maxDelay: TimeSpan.FromSeconds(2)));
```
