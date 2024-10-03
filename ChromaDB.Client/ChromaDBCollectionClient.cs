using ChromaDB.Client.Common.Helpers;
using ChromaDB.Client.Common.Mappers;
using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Models.Responses;

namespace ChromaDB.Client;

public class ChromaDBCollectionClient : IChromaDBCollectionClient
{
	private readonly Collection _collection;
	private readonly IChromaDBHttpClient _httpClient;

	public ChromaDBCollectionClient(Collection collection, IChromaDBHttpClient httpClient)
	{
		_collection = collection;
		_httpClient = httpClient;
	}

	public Collection Collection => _collection;

	public async Task<Response<List<CollectionEntry>>> Get(List<string>? ids = null, Dictionary<string, object>? where = null, Dictionary<string, object>? whereDocument = null, int? limit = null, int? offset = null, List<string>? include = null)
	{
		var requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		var request = new CollectionGetRequest()
		{
			Ids = ids,
			Where = where,
			WhereDocument = whereDocument,
			Limit = limit,
			Offset = offset,
			Include = include ?? ["metadatas", "documents"],
		};
		var response = await _httpClient.Post<CollectionGetRequest, CollectionEntriesGetResponse>("collections/{collection_id}/get", request, requestParams);
		var entries = response.Data?.Map() ?? [];
		return new Response<List<CollectionEntry>>(response.StatusCode, entries, response.ErrorMessage);
	}

	public async Task<Response<List<List<CollectionQueryEntry>>>> Query(List<List<float>> queryEmbeddings, int nResults = 10, Dictionary<string, object>? where = null, Dictionary<string, object>? whereDocument = null, List<string>? include = null)
	{
		var requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		var request = new CollectionQueryRequest()
		{
			QueryEmbeddings = queryEmbeddings,
			NResults = nResults,
			Where = where,
			WhereDocument = whereDocument,
			Include = include ?? ["metadatas", "documents", "distances"],
		};
		var response = await _httpClient.Post<CollectionQueryRequest, CollectionEntriesQueryResponse>("collections/{collection_id}/query", request, requestParams);
		var entries = response.Data?.Map() ?? [];
		return new Response<List<List<CollectionQueryEntry>>>(response.StatusCode, entries, response.ErrorMessage);
	}

	public async Task<Response<Response.Empty>> Add(List<string> ids, List<List<float>>? embeddings = null, List<Dictionary<string, object>>? metadatas = null, List<string>? documents = null)
	{
		var requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		var request = new CollectionAddRequest()
		{
			Ids = ids,
			Embeddings = embeddings,
			Metadatas = metadatas,
			Documents = documents,
		};
		return await _httpClient.Post<CollectionAddRequest, Response.Empty>("collections/{collection_id}/add", request, requestParams);
	}

	public async Task<Response<Response.Empty>> Update(List<string> ids, List<List<float>>? embeddings = null, List<Dictionary<string, object>>? metadatas = null, List<string>? documents = null)
	{
		var requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		var request = new CollectionUpdateRequest()
		{
			Ids = ids,
			Embeddings = embeddings,
			Metadatas = metadatas,
			Documents = documents,
		};
		return await _httpClient.Post<CollectionUpdateRequest, Response.Empty>("collections/{collection_id}/update", request, requestParams);
	}

	public async Task<Response<Response.Empty>> Upsert(List<string> ids, List<List<float>>? embeddings = null, List<Dictionary<string, object>>? metadatas = null, List<string>? documents = null)
	{
		var requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		var request = new CollectionUpsertRequest()
		{
			Ids = ids,
			Embeddings = embeddings,
			Metadatas = metadatas,
			Documents = documents,
		};
		return await _httpClient.Post<CollectionUpsertRequest, Response.Empty>("collections/{collection_id}/upsert", request, requestParams);
	}

	public async Task<Response<Response.Empty>> Delete(List<string> ids, Dictionary<string, object>? where = null, Dictionary<string, object>? whereDocument = null)
	{
		var requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		var request = new CollectionDeleteRequest()
		{
			Ids = ids,
			Where = where,
			WhereDocument = whereDocument,
		};
		return await _httpClient.Post<CollectionDeleteRequest, Response.Empty>("collections/{collection_id}/delete", request, requestParams);
	}

	public async Task<Response<int>> Count()
	{
		var requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		return await _httpClient.Get<int>("collections/{collection_id}/count", requestParams);
	}

	public async Task<Response<List<CollectionEntry>>> Peek(int limit = 10)
	{
		var requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		var request = new CollectionPeekRequest()
		{
			Limit = limit,
		};
		var response = await _httpClient.Post<CollectionPeekRequest, CollectionEntriesGetResponse>("collections/{collection_id}/get", request, requestParams);
		var entries = response.Data?.Map() ?? [];
		return new Response<List<CollectionEntry>>(response.StatusCode, entries, response.ErrorMessage);
	}

	public async Task<Response<Response.Empty>> Modify(string? name = null, Dictionary<string, object>? metadata = null)
	{
		var requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		var request = new CollectionModifyRequest()
		{
			Name = name,
			Metadata = metadata,
		};
		return await _httpClient.Put<CollectionModifyRequest, Response.Empty>("collections/{collection_id}", request, requestParams);
	}
}
