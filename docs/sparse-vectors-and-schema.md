# Sparse vectors and schema

Chroma Cloud keeps sparse vectors, like the BM25 vectors of the documents, in a metadata key with a sparse vector index, which the schema of the collection declares:

```csharp
var collection = await client.CreateCollectionAsync(new ChromaCollectionDefinition("articles")
{
	Schema = new ChromaCollectionSchema().WithSparseVectorIndex("doc_bm25", ChromaSearchKeys.Document, bm25: true, ChromaEmbeddingFunctionReference.ChromaBm25()),
});
var collectionClient = client.GetCollectionClient(collection);
await collectionClient.AddAsync(new ChromaRecords(["a"])
{
	Embeddings = [embedding],
	Documents = ["apple pie"],
	Metadatas = [new Dictionary<string, object> { ["doc_bm25"] = new ChromaSparseVector([17, 4242], [0.8f, 1.1f]) }],
});
var results = await collectionClient.SearchAsync(new ChromaSearch { Rank = ChromaRank.SparseKnn(queryVector, "doc_bm25"), Limit = 10 });
```

- `ChromaSparseVector` holds the indices, in strictly ascending order, their values, and optionally the tokens. It is a metadata value, written as the Python client writes it: `{"#type": "sparse_vector", "indices": [...], "values": [...]}`.
- **Reading it back:** a sparse vector comes back as a `ChromaSparseVector`, or as a `JsonElement` with `ChromaMetadataValues.Inferred`.
- **Where it works:**
  - the servers that store them: [COMPATIBILITY.md](COMPATIBILITY.md);
  - Chroma 0.x would drop them without an error, so the client throws a `ChromaException` before sending them.
- **`ChromaCollectionSchema`** declares the indexes of a new collection:
  - With `bm25`, the server applies the inverse document frequency of BM25. A source key needs an embedding function, because Chroma Cloud rejects one without the other.
  - `ChromaEmbeddingFunctionReference.ChromaBm25()` declares the BM25 function of Chroma with the settings of its Python client, so the clients that know it compute the vectors.
  - `ChromaCollection.SparseVectorIndexes` and `ChromaCollection.SchemaJson` read it back. `EmbeddingFunctionConfig` of an index holds the settings of its function, and `Bm25Function` the `ChromaBm25` with those settings. `FindBm25Index(sourceKey)` returns the BM25 index on the text of a key, like `#document`, or null.
  - `WithBm25Index(sourceKey)` declares a BM25 index with `chroma_bm25` on the text of a metadata key or of the documents, on the key named after the source, like `title_bm25` or `document_bm25`. With `ifSupported: true` the client creates the collection without it on a server other than Chroma Cloud, which rejects it, and on Chroma Cloud when the key is longer than 36 bytes. A write that gives that key a value other than a sparse vector throws an `ArgumentException`. `FindBm25IndexAsync(sourceKey)` of a collection client finds the index, and for the document copy key also the one on the documents.
  - `ToString()` returns the JSON the client sends.
- **The indexes of the values**, like `create_index` and `delete_index` of the Python client. `WithIndex` and `WithoutIndex` turn on or off:
  - the index of the string, integer, floating-point or Boolean values (`ChromaSchemaIndex.StringInverted`, `IntInverted`, `FloatInverted`, `BoolInverted`) of a metadata key, or of every key without a setting of its own;
  - the full-text search index of the documents (`FullTextSearch`), on `#document` only.

  They are all on by default; [docs/COMPATIBILITY.md](COMPATIBILITY.md) lists the versions that apply them.

  ```csharp
  var schema = new ChromaCollectionSchema()
  	.WithoutIndex(ChromaSchemaIndex.StringInverted, "body")
  	.WithoutIndex(ChromaSchemaIndex.FullTextSearch);
  ```

- **On Chroma Cloud only:**
  - the algorithm of a sparse vector index, `WithSparseVectorIndex(key, ChromaSparseIndexAlgorithm.MaxScore, ...)`, which `ChromaSparseVectorIndex.Algorithm` reads back, for the tenants that have it;
  - a customer-managed key of Google Cloud KMS, `WithGcpCmek("projects/.../locations/.../keyRings/.../cryptoKeys/...")`, like `set_cmek` of the Python client.
- **The space with a schema:** `Configuration = new() { Space = ... }` goes in the schema, on `#embedding`, as `create_index(VectorIndexConfig(space=...))` of the Python client writes it, and not in the `hnsw:space` metadata. Chroma rejects the two together: "Cannot set both collection config and schema simultaneously".
- **Where the schema works:**
  - the versions that apply it: [COMPATIBILITY.md](COMPATIBILITY.md);
  - a single server rejects a sparse vector index, and does not get the ones added with `ifSupported`;
  - Chroma 1.0.0 to 1.2.2 and 0.6.3 create the collection without the schema. Then `CreateCollectionAsync` deletes it and throws a `ChromaException`, and `GetOrCreateCollectionAsync` throws and keeps it, since it may have existed before;
  - Chroma 1.3.0 ignores the space in a schema that turns no index on or off, and applies it when the schema also turns an index on or off; 1.3.2 and later always apply it. When the collection comes back with another space, the client does the same.
