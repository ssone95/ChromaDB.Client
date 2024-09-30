using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Models.Responses;

namespace ChromaDB.Client.Services.Interfaces;

public interface IChromaCollectionClient
{
	Task<BaseResponse<List<CollectionEntry>>> Get(CollectionGetRequest request);
	Task<BaseResponse<CollectionEntriesQueryResponse>> Query(CollectionQueryRequest request);
	Task<BaseResponse<BaseResponse.None>> Add(CollectionAddRequest request);
	Task<BaseResponse<BaseResponse.None>> Update(CollectionUpdateRequest request);
	Task<BaseResponse<BaseResponse.None>> Upsert(CollectionUpsertRequest request);
	Task<BaseResponse<BaseResponse.None>> Delete(CollectionDeleteRequest request);
	Task<BaseResponse<int>> Count();
}
