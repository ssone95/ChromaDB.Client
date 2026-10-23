# Migration Guide: v1.x to v2.0

## Overview

ChromaDB.Client v2.0 has been updated to support the ChromaDB v2 API. This is a **breaking change** that requires updating your base URI configuration.

## What Changed?

ChromaDB has migrated from API v1 to v2, with the primary change being the URL structure:

**v1 API:**
```
http://localhost:8000/api/v1/
```

**v2 API:**
```
http://localhost:8000/api/v2/
```

The v2 API uses a hierarchical URL structure where tenant and database are part of the path instead of query parameters:

- **v1**: `/api/v1/collections?tenant={t}&database={d}`
- **v2**: `/api/v2/tenants/{t}/databases/{d}/collections`

## Migration Steps

### Step 1: Update Your ChromaDB Server

Ensure your ChromaDB server supports the v2 API. The v2 API is available in recent versions of ChromaDB.

### Step 2: Update Your Configuration

Change your `ChromaConfigurationOptions` URI from `/api/v1/` to `/api/v2/`:

**Before (v1.x):**
```csharp
var configOptions = new ChromaConfigurationOptions(uri: "http://localhost:8000/api/v1/");
```

**After (v2.0):**
```csharp
var configOptions = new ChromaConfigurationOptions(uri: "http://localhost:8000/api/v2/");
```

### Step 3: Update Package Reference

Update your NuGet package reference to v2.0.0 or later:

```xml
<PackageReference Include="ChromaDB.Client" Version="2.0.0" />
```

Or via the .NET CLI:
```bash
dotnet add package ChromaDB.Client --version 2.0.0
```

## What Stays the Same?

✅ **All API methods remain unchanged** - No code changes needed beyond the configuration  
✅ **Request/response models** - All data structures remain the same  
✅ **Method signatures** - All methods work exactly as before  
✅ **Functionality** - All features work identically  

## Example: Complete Migration

**Before (v1.x):**
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

**After (v2.0):**
```csharp
using ChromaDB.Client;

// Only change: /api/v1/ → /api/v2/
var configOptions = new ChromaConfigurationOptions(uri: "http://localhost:8000/api/v2/");
using var httpClient = new HttpClient();
var client = new ChromaClient(configOptions, httpClient);

// Everything else remains the same
var collection = await client.GetOrCreateCollection("my_collection");
var collectionClient = new ChromaCollectionClient(collection, configOptions, httpClient);

await collectionClient.Add(
    ["doc1"], 
    embeddings: [new([1f, 0.5f, 0f, -0.5f, -1f])]
);
```

## Troubleshooting

### "404 Not Found" Errors

If you get 404 errors after upgrading, you're likely still pointing to the v1 API endpoint:
- ✅ Check your URI contains `/api/v2/` (not `/api/v1/`)
- ✅ Verify your ChromaDB server supports v2 API

### Server Version Compatibility

The v2 API is supported in ChromaDB server versions 0.4.0 and later. Check your server version:

```csharp
var version = await client.GetVersion();
Console.WriteLine($"ChromaDB Server Version: {version}");
```

## Breaking Changes Summary

| Change | Impact | Action Required |
|--------|--------|-----------------|
| API endpoint | High | Update URI from `/api/v1/` to `/api/v2/` |
| URL structure | None | Handled internally by the client |
| Request/response | None | No changes needed |

## Need Help?

If you encounter issues during migration:

1. Verify your ChromaDB server version supports v2 API
2. Double-check your configuration URI uses `/api/v2/`
3. Review the error messages - they will indicate if there's a server compatibility issue
4. Open an issue on GitHub if you need assistance

## Rollback

If you need to rollback to v1 API:

1. Downgrade to ChromaDB.Client v1.x: `dotnet add package ChromaDB.Client --version 1.0.1`
2. Revert your configuration URI to `/api/v1/`
