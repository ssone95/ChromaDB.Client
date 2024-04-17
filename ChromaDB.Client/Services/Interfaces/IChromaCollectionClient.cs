using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Models.Responses;

namespace ChromaDB.Client.Services.Interfaces;

public interface IChromaCollectionClient
{
	Task<BaseResponse<List<CollectionEntry>>> Get(CollectionGetRequest request);
	Task<BaseResponse<CollectionEntriesQueryResponse>> Query(CollectionQueryRequest request);
}
