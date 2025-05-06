using ChromaDB.Client.Common;
using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Models.Responses;

namespace ChromaDB.Client;

public class ChromaCollectionClient
{
	private readonly ChromaCollection _collection;
	private readonly HttpClient _httpClient;
	private readonly ChromaTenant _currentTenant;
	private readonly ChromaDatabase _currentDatabase;

	public ChromaCollectionClient(ChromaCollection collection, ChromaConfigurationOptions options, HttpClient httpClient)
	{
		_collection = collection;
		_httpClient = httpClient;
		_currentTenant = options.Tenant is not null and not []
			? new ChromaTenant(options.Tenant)
			: ClientConstants.DefaultTenant;
		_currentDatabase = options.Database is not null and not []
			? new ChromaDatabase(options.Database)
			: ClientConstants.DefaultDatabase;

		if (_httpClient.BaseAddress != options.Uri)
		{
			_httpClient.BaseAddress = options.Uri;
		}
	}

	public ChromaCollection Collection => _collection;

	public async Task<ChromaCollectionEntry?> Get(string id, ChromaWhereOperator? where = null, ChromaWhereDocumentOperator? whereDocument = null, ChromaGetInclude? include = null)
		=> (await Get([id], where: where, whereDocument: whereDocument, include: include)).FirstOrDefault();

	public async Task<List<ChromaCollectionEntry>> Get(List<string>? ids = null, ChromaWhereOperator? where = null, ChromaWhereDocumentOperator? whereDocument = null, int? limit = null, int? offset = null, ChromaGetInclude? include = null)
	{
		var requestParams = new RequestQueryParams()
			.Insert("{tenant}", _currentTenant.Name)
			.Insert("{database}", _currentDatabase.Name)
			.Insert("{collection_id}", _collection.Id);

		var request = new CollectionGetRequest()
		{
			Ids = ids,
			Where = where?.ToWhere(),
			WhereDocument = whereDocument?.ToWhereDocument(),
			Limit = limit,
			Offset = offset,
			Include = (include ?? ChromaGetInclude.Metadatas | ChromaGetInclude.Documents).ToInclude(),
		};

		var response = await _httpClient.Post<CollectionGetRequest, CollectionEntriesGetResponse>(
			"tenants/{tenant}/databases/{database}/collections/{collection_id}/get",
			request,
			requestParams);

		return response.Map() ?? [];
	}

	public async Task<List<ChromaCollectionQueryEntry>> Query(ReadOnlyMemory<float> queryEmbeddings, int nResults = 10, ChromaWhereOperator? where = null, ChromaWhereDocumentOperator? whereDocument = null, ChromaQueryInclude? include = null)
		=> (await Query([queryEmbeddings], nResults: nResults, where: where, whereDocument: whereDocument, include: include)).FirstOrDefault() ?? [];

	public async Task<List<List<ChromaCollectionQueryEntry>>> Query(List<ReadOnlyMemory<float>> queryEmbeddings, int nResults = 10, ChromaWhereOperator? where = null, ChromaWhereDocumentOperator? whereDocument = null, ChromaQueryInclude? include = null)
	{
		var requestParams = new RequestQueryParams()
			.Insert("{tenant}", _currentTenant.Name)
			.Insert("{database}", _currentDatabase.Name)
			.Insert("{collection_id}", _collection.Id);

		var request = new CollectionQueryRequest()
		{
			QueryEmbeddings = queryEmbeddings,
			NResults = nResults,
			Where = where?.ToWhere(),
			WhereDocument = whereDocument?.ToWhereDocument(),
			Include = (include ?? ChromaQueryInclude.Metadatas | ChromaQueryInclude.Documents | ChromaQueryInclude.Distances).ToInclude(),
		};

		var response = await _httpClient.Post<CollectionQueryRequest, CollectionEntriesQueryResponse>(
			"tenants/{tenant}/databases/{database}/collections/{collection_id}/query",
			request,
			requestParams);

		return response.Map() ?? [];
	}

	public async Task Add(List<string> ids, List<ReadOnlyMemory<float>>? embeddings = null, List<Dictionary<string, object>>? metadatas = null, List<string>? documents = null)
	{
		var requestParams = new RequestQueryParams()
			.Insert("{tenant}", _currentTenant.Name)
			.Insert("{database}", _currentDatabase.Name)
			.Insert("{collection_id}", _collection.Id);

		var request = new CollectionAddRequest()
		{
			Ids = ids,
			Embeddings = embeddings,
			Metadatas = metadatas,
			Documents = documents,
		};

		await _httpClient.Post(
			"tenants/{tenant}/databases/{database}/collections/{collection_id}/add",
			request,
			requestParams);
	}

	public async Task Update(List<string> ids, List<ReadOnlyMemory<float>>? embeddings = null, List<Dictionary<string, object>>? metadatas = null, List<string>? documents = null)
	{
		var requestParams = new RequestQueryParams()
			.Insert("{tenant}", _currentTenant.Name)
			.Insert("{database}", _currentDatabase.Name)
			.Insert("{collection_id}", _collection.Id);

		var request = new CollectionUpdateRequest()
		{
			Ids = ids,
			Embeddings = embeddings,
			Metadatas = metadatas,
			Documents = documents,
		};

		await _httpClient.Post(
			"tenants/{tenant}/databases/{database}/collections/{collection_id}/update",
			request,
			requestParams);
	}

	public async Task Upsert(List<string> ids, List<ReadOnlyMemory<float>>? embeddings = null, List<Dictionary<string, object>>? metadatas = null, List<string>? documents = null)
	{
		var requestParams = new RequestQueryParams()
			.Insert("{tenant}", _currentTenant.Name)
			.Insert("{database}", _currentDatabase.Name)
			.Insert("{collection_id}", _collection.Id);

		var request = new CollectionUpsertRequest()
		{
			Ids = ids,
			Embeddings = embeddings,
			Metadatas = metadatas,
			Documents = documents,
		};

		await _httpClient.Post(
			"tenants/{tenant}/databases/{database}/collections/{collection_id}/upsert",
			request,
			requestParams);
	}

	public async Task Delete(List<string> ids, ChromaWhereOperator? where = null, ChromaWhereDocumentOperator? whereDocument = null)
	{
		var requestParams = new RequestQueryParams()
			.Insert("{tenant}", _currentTenant.Name)
			.Insert("{database}", _currentDatabase.Name)
			.Insert("{collection_id}", _collection.Id);

		var request = new CollectionDeleteRequest()
		{
			Ids = ids,
			Where = where?.ToWhere(),
			WhereDocument = whereDocument?.ToWhereDocument(),
		};

		await _httpClient.Post(
			"tenants/{tenant}/databases/{database}/collections/{collection_id}/delete",
			request,
			requestParams);
	}

	public async Task<int> Count()
	{
		var requestParams = new RequestQueryParams()
			.Insert("{tenant}", _currentTenant.Name)
			.Insert("{database}", _currentDatabase.Name)
			.Insert("{collection_id}", _collection.Id);

		return await _httpClient.Get<int>(
			"tenants/{tenant}/databases/{database}/collections/{collection_id}/count",
			requestParams);
	}

	public async Task<List<ChromaCollectionEntry>> Peek(int limit = 10)
	{
		var requestParams = new RequestQueryParams()
			.Insert("{tenant}", _currentTenant.Name)
			.Insert("{database}", _currentDatabase.Name)
			.Insert("{collection_id}", _collection.Id);

		var request = new CollectionPeekRequest()
		{
			Limit = limit,
		};

		var response = await _httpClient.Post<CollectionPeekRequest, CollectionEntriesGetResponse>(
			"tenants/{tenant}/databases/{database}/collections/{collection_id}/get",
			request,
			requestParams);

		return response.Map() ?? [];
	}

	public async Task Modify(string? name = null, Dictionary<string, object>? metadata = null)
	{
		var requestParams = new RequestQueryParams()
			.Insert("{tenant}", _currentTenant.Name)
			.Insert("{database}", _currentDatabase.Name)
			.Insert("{collection_id}", _collection.Id);

		var request = new CollectionModifyRequest()
		{
			Name = name,
			Metadata = metadata,
		};

		await _httpClient.Put(
			"tenants/{tenant}/databases/{database}/collections/{collection_id}",
			request,
			requestParams);
	}
}
