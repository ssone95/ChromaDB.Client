# Search

The servers that serve the Search API: [COMPATIBILITY.md](COMPATIBILITY.md#chroma-cloud).

```csharp
var results = await collectionClient.SearchAsync(new ChromaSearch
{
	Where = ChromaWhereOperator.GreaterThanOrEqual("year", 2021),
	WhereDocument = ChromaWhereDocumentOperator.Contains("apple"),
	Rank = ChromaRank.Knn(new([0.1f, 0.2f, 0.3f])),
	Limit = 10,
	Select = [ChromaSearchKeys.Document, ChromaSearchKeys.Score, "title"],
});
foreach (var result in results)
{
	Console.WriteLine($"{result.Id} {result.Score} {result.Document} {result.Metadata?["title"]}");
}
```

`ChromaSearch` holds the filters, which combine with `$and`, the ids, the ranking, the page and the fields to return. Without `Select`, a search returns only the ids. The records with the lowest score come first, but with `ChromaRank.HybridRrf` as the whole rank, whose results come with the fused score, positive, highest first. Inside an expression, like `HybridRrf(...) * 0.5`, the score comes as Chroma sends it, lowest first.

`ChromaRank` builds the ranking:

- `Knn` ranks by distance from a vector;
- `Value`, `Sum`, `Multiply`, `Max`, `Min`, `Abs`, `Exp`, `Log` and the operators `+ - * /` combine rankings;
- `Rrf` fuses several rankings, built as the Python client builds it.

```csharp
var fused = ChromaRank.Rrf([ChromaRank.Knn(vector1, returnRank: true), ChromaRank.Knn(vector2, returnRank: true)]);
var perCategory = new ChromaSearchGroupBy(ChromaSearchAggregate.MinK(3, ChromaSearchKeys.Score), "category");
var many = await collectionClient.SearchAsync([new ChromaSearch { Rank = fused, Limit = 5 }, new ChromaSearch { GroupBy = perCategory, Rank = fused }]);
```

`ChromaSearchGroupBy` keeps, for each value of the metadata keys, the records the aggregate chooses. Several searches go in one request, and their results come back in order. `ToString()` of a `ChromaRank` returns its JSON, where a text query of `SparseKnn` appears as text; `SearchAsync` sends its sparse vector.

`SearchAsync(search, ChromaReadLevel.IndexOnly)` leaves out the records not indexed yet. `ChromaSearchAggregate.MinK` keeps the records with the lowest values of the keys, the best ranked with `ChromaSearchKeys.Score`, and `MaxK` those with the highest.

## Hybrid search with BM25

```csharp
var collection = await client.CreateCollectionAsync(new ChromaCollectionDefinition("articles")
{
	Schema = new ChromaCollectionSchema().WithSparseVectorIndex("doc_bm25", ChromaSearchKeys.Document, bm25: true, new ChromaBm25().Reference),
});
var collectionClient = client.GetCollectionClient(collection);
await collectionClient.AddAsync(new ChromaRecords(ids) { Embeddings = embeddings, Documents = documents });
var results = await collectionClient.SearchAsync(new ChromaSearch
{
	Rank = ChromaRank.Rrf([ChromaRank.Knn(queryEmbedding, returnRank: true), ChromaRank.SparseKnn(queryText, "doc_bm25", returnRank: true)]),
	Limit = 10,
	Select = [ChromaSearchKeys.Document, ChromaSearchKeys.Score],
});
```

- **What `ChromaBm25` computes:** the BM25 vectors as the Python client computes them, `chroma_bm25` with the Snowball English stemmer of snowballstemmer 3.1.1. The same text gives the same indices and values in .NET and in Python, so a collection written by one is searched by the other.
- **How it was tested:** against the Python client on more than 5,000 texts, with the characters of every Unicode script that Python 3.13 knows, on .NET 8 and on .NET Framework, outside the CI. On every change, `ChromaBm25Tests` compares the vectors of a few texts with those of Python. It follows the Unicode rules of Python from its own tables, not those of the runtime.
- **What the client computes, as the Python client does:**
  - In `AddAsync`, `UpdateAsync` and `UpsertAsync`, the vectors of each sparse vector index of the schema with a source key and `chroma_bm25`, from the document or from the text in the metadata key, with the settings of the schema. An empty text gets an empty vector, which replaces the vector of the old text, as with the Python client. A record whose metadata already has the key keeps its vector. The records and the metadata you pass do not change.
  - In `SearchAsync`, the vector of the text of `SparseKnn(queryText, key)`, with the function of the index of the key.
  - The schema comes with the collection, from `CreateCollectionAsync` or `GetCollectionAsync`. A collection client created from an id alone has no schema, so a text query throws a `ChromaException`. So does a function other than `chroma_bm25`, unless the metadata has the vectors.
- **By hand:** `new ChromaBm25()` with the same settings, or `Bm25Function` of the index, gives the vectors to put in the metadata or in `SparseKnn`. `Embed(text)` returns the vector of a text, and `Reference` declares the function in the schema.
- **Other functions:** `ChromaEmbeddingFunctionReference.Known(name, config)` declares another function that the clients of Chroma know. The client only declares it.
- **Records without the terms of the query:** Chroma Cloud ranks them too, among the `limit` of `SparseKnn`, with the score 1, one minus the dot product. With `returnRank` they take the next positions, so in `Rrf` they get points from the sparse part as well. `ChromaRank.HybridRrf(embedding, text, key, limit)` fuses the dense search and the BM25 search of the text so that only the records with a term of the text get points from it; the others keep the order of the dense search.
- **License:** the license of the stemmer is in [THIRD-PARTY-NOTICES.md](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/THIRD-PARTY-NOTICES.md).
