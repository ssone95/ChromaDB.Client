using ChromaDB.Client.Common.Helpers;
using ChromaDB.Client.Common.Mappers;
using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Models.Responses;
using ChromaDB.Client.Services.Interfaces;

namespace ChromaDB.Client.Services.Implementations;

public class ChromaCollectionClient : IChromaCollectionClient
{
	private readonly Collection _collection;
	private readonly IChromaDBHttpClient _httpClient;

	public ChromaCollectionClient(Collection collection, IChromaDBHttpClient httpClient)
	{
		_collection = collection;
		_httpClient = httpClient;
	}

	public async Task<BaseResponse<List<CollectionEntry>>> Get(CollectionGetRequest request)
	{
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		BaseResponse<CollectionEntriesResponse> response = await _httpClient.Post<CollectionGetRequest, CollectionEntriesResponse>("collections/{collection_id}/get", request, requestParams);
		List<CollectionEntry> entries = response.Data?.Map() ?? [];
		return new BaseResponse<List<CollectionEntry>>(entries, response.StatusCode, response.ReasonPhrase);
	}

	public async Task<BaseResponse<CollectionEntriesQueryResponse>> Query(CollectionQueryRequest request)
	{
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		return await _httpClient.Post<CollectionQueryRequest, CollectionEntriesQueryResponse>("collections/{collection_id}/query", request, requestParams);
	}

	public async Task<BaseResponse<BaseResponse.None>> Add(CollectionAddRequest request)
	{
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		return await _httpClient.Post<CollectionAddRequest, BaseResponse.None>("collections/{collection_id}/add", request, requestParams);
	}

	public async Task<BaseResponse<BaseResponse.None>> Update(CollectionUpdateRequest request)
	{
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		return await _httpClient.Post<CollectionUpdateRequest, BaseResponse.None>("collections/{collection_id}/update", request, requestParams);
	}

	public async Task<BaseResponse<BaseResponse.None>> Upsert(CollectionUpsertRequest request)
	{
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		return await _httpClient.Post<CollectionUpsertRequest, BaseResponse.None>("collections/{collection_id}/upsert", request, requestParams);
	}

	public async Task<BaseResponse<BaseResponse.None>> Delete(CollectionDeleteRequest request)
	{
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		return await _httpClient.Post<CollectionDeleteRequest, BaseResponse.None>("collections/{collection_id}/delete", request, requestParams);
	}

	public async Task<BaseResponse<int>> Count()
	{
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		return await _httpClient.Get<int>("collections/{collection_id}/count", requestParams);
	}

	public async Task<BaseResponse<List<CollectionEntry>>> Peek(CollectionPeekRequest request)
	{
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{collection_id}", _collection.Id);
		BaseResponse<CollectionEntriesResponse> response = await _httpClient.Post<CollectionPeekRequest, CollectionEntriesResponse>("collections/{collection_id}/get", request, requestParams);
		List<CollectionEntry> entries = response.Data?.Map() ?? [];
		return new BaseResponse<List<CollectionEntry>>(entries, response.StatusCode, response.ReasonPhrase);
	}
}
