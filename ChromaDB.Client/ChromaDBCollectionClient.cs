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

	public async Task<Response<List<CollectionEntry>>> Get(List<string>? ids = null, IDictionary<string, object>? where = null, IDictionary<string, object>? whereDocument = null, int? limit = null, int? offset = null, List<string>? include = null)
	{
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		CollectionGetRequest request = new CollectionGetRequest()
		{
			Ids = ids,
			Where = where,
			WhereDocument = whereDocument,
			Limit = limit,
			Offset = offset,
			Include = include ?? ["metadatas", "documents"],
		};
		Response<CollectionEntriesGetResponse> response = await _httpClient.Post<CollectionGetRequest, CollectionEntriesGetResponse>("collections/{collection_id}/get", request, requestParams);
		List<CollectionEntry> entries = response.Data?.Map() ?? [];
		return new Response<List<CollectionEntry>>(response.StatusCode, entries, response.ErrorMessage);
	}

	public async Task<Response<List<List<CollectionQueryEntry>>>> Query(CollectionQueryRequest request)
	{
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		var response = await _httpClient.Post<CollectionQueryRequest, CollectionEntriesQueryResponse>("collections/{collection_id}/query", request, requestParams);
		List<List<CollectionQueryEntry>> entries = response.Data?.Map() ?? [];
		return new Response<List<List<CollectionQueryEntry>>>(response.StatusCode, entries, response.ErrorMessage);
	}

	public async Task<Response<Response.Empty>> Add(List<string> ids, List<List<float>>? embeddings = null, List<IDictionary<string, object>>? metadatas = null, List<string>? documents = null)
	{
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		CollectionAddRequest request = new CollectionAddRequest()
		{
			Ids = ids,
			Embeddings = embeddings,
			Metadatas = metadatas,
			Documents = documents,
		};
		return await _httpClient.Post<CollectionAddRequest, Response.Empty>("collections/{collection_id}/add", request, requestParams);
	}

	public async Task<Response<Response.Empty>> Update(CollectionUpdateRequest request)
	{
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		return await _httpClient.Post<CollectionUpdateRequest, Response.Empty>("collections/{collection_id}/update", request, requestParams);
	}

	public async Task<Response<Response.Empty>> Upsert(CollectionUpsertRequest request)
	{
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		return await _httpClient.Post<CollectionUpsertRequest, Response.Empty>("collections/{collection_id}/upsert", request, requestParams);
	}

	public async Task<Response<Response.Empty>> Delete(List<string> ids, IDictionary<string, object>? where = null, IDictionary<string, object>? whereDocument = null)
	{
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		CollectionDeleteRequest request = new CollectionDeleteRequest()
		{
			Ids = ids,
			Where = where,
			WhereDocument = whereDocument,
		};
		return await _httpClient.Post<CollectionDeleteRequest, Response.Empty>("collections/{collection_id}/delete", request, requestParams);
	}

	public async Task<Response<int>> Count()
	{
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		return await _httpClient.Get<int>("collections/{collection_id}/count", requestParams);
	}

	public async Task<Response<List<CollectionEntry>>> Peek(int? limit = 10)
	{
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		CollectionPeekRequest request = new CollectionPeekRequest()
		{
			Limit = limit,
		};
		Response<CollectionEntriesGetResponse> response = await _httpClient.Post<CollectionPeekRequest, CollectionEntriesGetResponse>("collections/{collection_id}/get", request, requestParams);
		List<CollectionEntry> entries = response.Data?.Map() ?? [];
		return new Response<List<CollectionEntry>>(response.StatusCode, entries, response.ErrorMessage);
	}

	public async Task<Response<Response.Empty>> Modify(string? name = null, IDictionary<string, object>? metadata = null)
	{
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		CollectionModifyRequest request = new CollectionModifyRequest()
		{
			Name = name,
			Metadata = metadata,
		};
		return await _httpClient.Put<CollectionModifyRequest, Response.Empty>("collections/{collection_id}", request, requestParams);
	}
}
