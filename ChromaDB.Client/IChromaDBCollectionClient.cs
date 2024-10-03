using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;

namespace ChromaDB.Client;

public interface IChromaDBCollectionClient
{
	Collection Collection { get; }

	Task<Response<List<CollectionEntry>>> Get(CollectionGetRequest request);
	Task<Response<List<List<CollectionQueryEntry>>>> Query(CollectionQueryRequest request);
	Task<Response<Response.Empty>> Add(List<string> ids, List<List<float>>? embeddings = null, List<IDictionary<string, object>>? metadatas = null, List<string>? documents = null);
	Task<Response<Response.Empty>> Update(CollectionUpdateRequest request);
	Task<Response<Response.Empty>> Upsert(CollectionUpsertRequest request);
	Task<Response<Response.Empty>> Delete(CollectionDeleteRequest request);
	Task<Response<int>> Count();
	Task<Response<List<CollectionEntry>>> Peek(CollectionPeekRequest request);
	Task<Response<Response.Empty>> Modify(CollectionModifyRequest request);
}
