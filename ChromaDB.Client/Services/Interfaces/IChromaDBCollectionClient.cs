﻿using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;

namespace ChromaDB.Client.Services.Interfaces;

public interface IChromaDBCollectionClient
{
	Collection Collection { get; }

	Task<BaseResponse<List<CollectionEntry>>> Get(CollectionGetRequest request);
	Task<BaseResponse<List<List<CollectionQueryEntry>>>> Query(CollectionQueryRequest request);
	Task<BaseResponse<BaseResponse.None>> Add(CollectionAddRequest request);
	Task<BaseResponse<BaseResponse.None>> Update(CollectionUpdateRequest request);
	Task<BaseResponse<BaseResponse.None>> Upsert(CollectionUpsertRequest request);
	Task<BaseResponse<BaseResponse.None>> Delete(CollectionDeleteRequest request);
	Task<BaseResponse<int>> Count();
	Task<BaseResponse<List<CollectionEntry>>> Peek(CollectionPeekRequest request);
	Task<BaseResponse<BaseResponse.None>> Modify(CollectionModifyRequest request);
}
