# Queries and filters

## Querying some records only

```csharp
var results = await collectionClient.QueryAsync(new ReadOnlyMemory<float>([1f, 0.5f, 0f]), nResults: 1, ids: ["a", "c"]);
var same = await collectionClient.QueryAsync(new ChromaQuery([new([1f, 0.5f, 0f])]) { Ids = ["a", "c"], NResults = 1 });
```

`ChromaQuery` holds the query embeddings, the number of results, the filters, what to include and the ids to search among, and the offset: Chroma has no offset in queries, so the client asks for the skipped records too and leaves them out. An id without a record is left out: `QueryAsync` asks again with the ids that have one (KD-24 in [docs/COMPATIBILITY.md](COMPATIBILITY.md)). When a result falls outside the ids, `QueryAsync` throws a `ChromaException` instead of returning it (KD-36). With `ExpectedSpace`, `QueryAsync` throws an `InvalidOperationException` before the query when the collection has another space, whose distances would not be the expected ones.

## Reading records with GetAsync

`GetAsync` throws an `ArgumentOutOfRangeException` for a negative limit or offset, and returns no record for a limit of 0, without a get: Chroma 0.6.3 would take 0 as no limit. A client made by name still reads its collection first, so a collection that does not exist throws, as in the other reads. It reads more records than the batch size in pages: pages of the batch size from the offset, until the limit or the last record. Ids beyond the batch size go in batches, each id once, with the limit and the offset applied to all of them together. The pages are separate requests, so records written in between can be read twice or missed.

## Filters

`ToString()` of a `ChromaWhereOperator` or a `ChromaWhereDocumentOperator` returns the JSON the client sends:

```csharp
var where = ChromaWhereOperator.Equal("year", 2026) & ChromaWhereOperator.In("lang", "en", "it");
Console.WriteLine(where); // {"$and":[{"year":{"$eq":2026}},{"lang":{"$in":["en","it"]}}]}
```

- `ChromaWhereOperator` has `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual`, `In`, `NotIn`, `Contains` and `NotContains`, combined with `&` and `|`.
- `ChromaWhereDocumentOperator` has `Contains`, `NotContains`, `Regex` and `NotRegex`.
- A chain of the same operator, like `a & b & c` or one built in a loop, goes as one list, `{"$and":[a,b,c]}`, as the Python client writes it.
- `Not` negates a filter. Chroma has no `$not`, so the negation goes into the operators: `$eq` becomes `$ne`, `$gt` becomes `$lte`, `$in` becomes `$nin`, `$contains` becomes `$not_contains`, `$regex` becomes `$not_regex`, and `$and` becomes `$or` of the negations. `$ne`, `$nin` and `$not_contains` match the records without the key, `$ne` and `$nin` from Chroma 0.5.15, and a comparison like `$lte` does not: `Not(GreaterThan("k", 5))` leaves out the records without `k`, as `GreaterThan("k", 5)` does.
- `ChromaWhereOperator` also filters the ids, with `Equal` and `In` on `ChromaSearchKeys.Id`, and the documents, with `Document(filter)`, as the where clause of the Search API does. Get, query and delete take neither in their `where`: the client sends them as the ids and the `where_document` of the request, which takes them only joined to the other conditions with `&`, and throws a `NotSupportedException` otherwise, like for an id inside an `|`.
- `ChromaWhereOperator.All` matches every record and `None` no record, which Chroma has no filter for: the client sends no `where` for `All`, and no request for `None`, so a read with it returns nothing and a delete deletes nothing. `&` and `|` simplify with them, and each is a single instance, which `Not` also returns. `In` without values is `None`, and `NotIn` without values `All` (KD-46 in [docs/COMPATIBILITY.md](COMPATIBILITY.md)).

### Very long filters

A single Chroma server limits how deep a list of filters can go: [Other behaviors of the servers](COMPATIBILITY.md#other-behaviors-of-the-servers) in COMPATIBILITY.md, and KD-4.

So the client counts that depth, the length of the lists along the deepest path of the filter. Beyond 900, it splits each list of n filters into ⌈√n⌉ lists with the same meaning.

`ChromaWhereDocumentOperator.Regex` and `NotRegex` filter the documents with a regular expression; [docs/COMPATIBILITY.md](COMPATIBILITY.md) lists the versions.

```csharp
var apples = await collectionClient.GetAsync(whereDocument: ChromaWhereDocumentOperator.Regex("^apple"));
```
