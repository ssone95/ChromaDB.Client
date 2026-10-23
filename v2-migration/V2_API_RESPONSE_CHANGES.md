# ChromaDB API v1 vs v2 Response Format Changes

## Overview

During the v2 migration, I discovered that the ChromaDB v2 API returns different JSON response formats compared to v1. These changes required modifications to the response models and mapper code.

## The Problem

When initially testing against the v2 API, the client failed with deserialization errors:
```
JSON deserialization for type 'CollectionEntriesQueryResponse' was missing required properties including: 'data'.
```

After inspecting the actual v2 API responses, I found two major differences from v1.

## Changes Explained

### 1. CollectionEntriesQueryResponse.cs - Distances Field Type

**The Code Change:**
```csharp
// OLD (v1):
[JsonPropertyName("distances")]
public required List<ReadOnlyMemory<float>> Distances { get; init; }

// NEW (v2):
[JsonPropertyName("distances")]
public required List<List<float>>? Distances { get; init; }
```

**v1 API Response:**
```json
{
  "data": {
    "ids": [["doc1", "doc2"]],
    "distances": [[0.0, 0.17]],
    "metadatas": [[{"source": "test1"}, {"source": "test2"}]],
    "documents": [["First document", "Second document"]]
  }
}
```

**v2 API Response:**
```json
{
  "ids": [["doc1", "doc2"]],
  "distances": [[0.0, 0.17]],
  "metadatas": [[{"source": "test1"}, {"source": "test2"}]],
  "documents": [["First document", "Second document"]]
}
```

**Why the Change:**
- v2 API returns distances as `List<List<float>>` (nested lists) to properly represent the structure: each query embedding returns a list of distances
- The old `List<ReadOnlyMemory<float>>` type didn't match the JSON structure
- Made nullable (`?`) because v2 doesn't always include this field when not requested via `include` parameter

---

### 2. CollectionEntriesQueryResponse.cs - Data Field

**The Code Change:**
```csharp
// OLD (v1):
[JsonPropertyName("data")]
public required dynamic? Data { get; init; }

// NEW (v2):
[JsonPropertyName("data")]
public dynamic? Data { get; init; }  // Removed "required"
```

**Why the Change:**
- **v1** wrapped response fields in a `data` object at the root level
- **v2** returns fields directly at the root level (no `data` wrapper)
- The `required` keyword caused deserialization to fail when the field was missing in v2 responses
- By removing `required`, we allow v2 responses to deserialize successfully while the field remains available for backward compatibility if needed

---

### 3. CollectionEntriesGetResponse.cs - Data Field

**The Code Change:**
```csharp
// OLD (v1):
[JsonPropertyName("data")]
public required dynamic? Data { get; init; }

// NEW (v2):
[JsonPropertyName("data")]
public dynamic? Data { get; init; }  // Removed "required"
```

**Why the Change:**
- Same reason as above - v2 API eliminated the `data` wrapper
- Get requests in v2 return results directly at the root level

**v1 API Response (Get):**
```json
{
  "data": {
    "ids": ["doc1"],
    "embeddings": [[1.0, 0.5, 0.0, -0.5, -1.0]],
    "metadatas": [{"source": "test"}],
    "documents": ["First document"]
  }
}
```

**v2 API Response (Get):**
```json
{
  "ids": ["doc1"],
  "embeddings": [[1.0, 0.5, 0.0, -0.5, -1.0]],
  "metadatas": [{"source": "test"}],
  "documents": ["First document"]
}
```

---

### 4. CollectionQueryEntryMapper.cs - Distance Access

**The Code Change:**
```csharp
// OLD (v1):
public static List<List<ChromaCollectionQueryEntry>> Map(this CollectionEntriesQueryResponse response)
{
    return response.Ids
        .Select((_, i) => response.Ids[i]
            .Select((id, j) => new ChromaCollectionQueryEntry(id)
            {
                Distance = response.Distances[i].Span[j],  // ← OLD
                Metadata = response.Metadatas?[i][j],
                ...
            })
            .ToList())
        .ToList();
}

// NEW (v2):
public static List<List<ChromaCollectionQueryEntry>> Map(this CollectionEntriesQueryResponse response)
{
    return response.Ids
        .Select((_, i) => response.Ids[i]
            .Select((id, j) => new ChromaCollectionQueryEntry(id)
            {
                Distance = response.Distances?[i][j] ?? 0f,  // ← NEW
                Metadata = response.Metadatas?[i][j],
                ...
            })
            .ToList())
        .ToList();
}
```

**Why the Change:**
- Old code used `.Span[j]` because `Distances` was `List<ReadOnlyMemory<float>>`
- New code uses direct indexing `[i][j]` because `Distances` is now `List<List<float>>`
- Added null-coalescing operator `?? 0f` for safety when distances are not included in the response

---

## Summary of API Differences

| Aspect | v1 API | v2 API |
|--------|--------|--------|
| Response wrapper | `{"data": {...}}` | Direct fields at root |
| Distances type | Flat array | Nested lists `[[...]]` |
| Required `data` field | Yes | No (field omitted) |
| URL structure | Query parameters | Hierarchical paths |

## Discovery Process

These changes were discovered through:

1. **Initial testing** - Integration tests failed with deserialization errors
2. **API inspection** - Used `curl` to examine actual v2 API responses:
   ```bash
   curl -X POST http://localhost:8000/api/v2/tenants/default_tenant/databases/default_database/collections/{id}/query \
     -H "Content-Type: application/json" \
     -d '{"query_embeddings":[[1.0,0.5,0.0,-0.5,-1.0]],"n_results":2}' | jq
   ```
3. **Comparison** - Compared v2 responses to expected v1 format
4. **Iterative fixes** - Adjusted models and mappers until all 13 integration tests passed

## Impact on Public API

✅ **No breaking changes to the public API surface**

These are internal implementation details. The public API methods like `Query()`, `Get()`, etc. work exactly the same way - users don't need to change their code (except for updating the base URI).

## Testing Validation

All changes were validated through:
- ✅ 13 integration tests passing
- ✅ README sample working correctly
- ✅ Real-world ChromaEmbeddingCache implementation
- ✅ ChunkEval integration successful

## Actual v2 API Response Examples

### Query Response (Actual)
```json
{
  "ids": [
    [
      "doc1",
      "doc2"
    ]
  ],
  "embeddings": null,
  "documents": [
    [
      "First document",
      "Second document"
    ]
  ],
  "uris": null,
  "metadatas": [
    [
      {
        "source": "test1"
      },
      {
        "source": "test2"
      }
    ]
  ],
  "distances": [
    [
      0.0,
      0.16999999
    ]
  ]
}
```

### Get Response (Actual)
```json
{
  "ids": [
    "doc1"
  ],
  "embeddings": [
    [1.0, 0.5, 0.0, -0.5, -1.0]
  ],
  "documents": [
    "First document"
  ],
  "uris": null,
  "metadatas": [
    {
      "source": "test1"
    }
  ]
}
```

Note: No `data` wrapper in either response!

## Conclusion

The v2 API simplified its response format by:
1. Removing the `data` wrapper object
2. Using proper nested list structures for multi-query results

These changes make the API cleaner and more predictable, though they required compatibility updates in the client to handle the new format.
