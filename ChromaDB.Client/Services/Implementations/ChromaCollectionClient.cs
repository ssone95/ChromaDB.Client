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
			.Add("{collection_id}", _collection.Id);
		BaseResponse<CollectionEntriesResponse> response = await _httpClient.Post<Collection, CollectionGetRequest, CollectionEntriesResponse>(request, requestParams);
		List<CollectionEntry> entries = response.Data?.Map() ?? [];
		return new BaseResponse<List<CollectionEntry>>(entries, response.StatusCode, response.ReasonPhrase);
	}

	public async Task<BaseResponse<CollectionEntriesQueryResponse>> Query(CollectionQueryRequest request)
	{
		RequestQueryParams requestParams = new RequestQueryParams()
			.Add("{collection_id}", _collection.Id);
		return await _httpClient.Post<Collection, CollectionQueryRequest, CollectionEntriesQueryResponse>(request, requestParams);
	}

	public async Task<BaseResponse<BaseResponse.None>> Add(CollectionAddRequest request)
	{
		RequestQueryParams requestParams = new RequestQueryParams()
			.Add("{collection_id}", _collection.Id);
		return await _httpClient.Post<Collection, CollectionAddRequest, BaseResponse.None>(request, requestParams);
	}
}
