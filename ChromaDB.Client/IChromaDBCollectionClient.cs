using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;

namespace ChromaDB.Client;

public interface IChromaDBCollectionClient
{
	Collection Collection { get; }

	Task<Response<List<CollectionEntry>>> Get(List<string>? ids = null, IDictionary<string, object>? where = null, IDictionary<string, object>? whereDocument = null, int? limit = null, int? offset = null, List<string>? include = null);
	Task<Response<List<List<CollectionQueryEntry>>>> Query(CollectionQueryRequest request);
	Task<Response<Response.Empty>> Add(List<string> ids, List<List<float>>? embeddings = null, List<IDictionary<string, object>>? metadatas = null, List<string>? documents = null);
	Task<Response<Response.Empty>> Update(CollectionUpdateRequest request);
	Task<Response<Response.Empty>> Upsert(CollectionUpsertRequest request);
	Task<Response<Response.Empty>> Delete(List<string> ids, IDictionary<string, object>? where = null, IDictionary<string, object>? whereDocument = null);
	Task<Response<int>> Count();
	Task<Response<List<CollectionEntry>>> Peek(CollectionPeekRequest request);
	Task<Response<Response.Empty>> Modify(CollectionModifyRequest request);
}
