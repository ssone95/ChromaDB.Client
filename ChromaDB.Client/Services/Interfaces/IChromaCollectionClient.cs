using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Models.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChromaDB.Client.Services.Interfaces
{
    public interface IChromaCollectionClient : IDisposable
	{
		Task<BaseResponse<List<CollectionEntry>>> Get(CollectionGetRequest request);
		Task<BaseResponse<CollectionEntriesQueryResponse>> Query(CollectionQueryRequest request);
	}
}
