using ChromaDB.Client.Common;
using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;

namespace ChromaDB.Client;

public class ChromaClient
{
	private readonly HttpClient _httpClient;
	private readonly ChromaTenant _currentTenant;
	private readonly ChromaDatabase _currentDatabase;

	public ChromaClient(ChromaConfigurationOptions options, HttpClient httpClient)
	{
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
		if (options.ChromaToken is not null and not [])
		{
			_httpClient.DefaultRequestHeaders.Remove(ClientConstants.ChromaTokenHeader);
			_httpClient.DefaultRequestHeaders.Add(ClientConstants.ChromaTokenHeader, options.ChromaToken);
		}
	}

	public async Task<List<ChromaCollection>> ListCollections(string? tenant = null, string? database = null)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		var requestParams = new RequestQueryParams()
			.Insert("{tenant}", tenant)
			.Insert("{database}", database);
		return await _httpClient.Get<List<ChromaCollection>>("tenants/{tenant}/databases/{database}/collections", requestParams);
	}

	public async Task<ChromaCollection> GetCollection(string name, string? tenant = null, string? database = null)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		var requestParams = new RequestQueryParams()
			.Insert("{collection_id}", name) // it seems to be a chroma bug: https://github.com/chroma-core/chroma/issues/4456
			.Insert("{tenant}", tenant)
			.Insert("{database}", database);
		return await _httpClient.Get<ChromaCollection>("tenants/{tenant}/databases/{database}/collections/{collection_id}", requestParams);
	}

	public async Task<ChromaHeartbeat> Heartbeat()
	{
		return await _httpClient.Get<ChromaHeartbeat>("heartbeat", new RequestQueryParams());
	}

	public async Task<ChromaCollection> CreateCollection(string name, Dictionary<string, object>? metadata = null, string? tenant = null, string? database = null)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		var requestParams = new RequestQueryParams()
			.Insert("{tenant}", tenant)
			.Insert("{database}", database);
		var request = new CreateCollectionRequest()
		{
			Name = name,
			Metadata = metadata
		};
		return await _httpClient.Post<CreateCollectionRequest, ChromaCollection>("tenants/{tenant}/databases/{database}/collections", request, requestParams);
	}

	public async Task<ChromaCollection> GetOrCreateCollection(string name, Dictionary<string, object>? metadata = null, string? tenant = null, string? database = null)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		var requestParams = new RequestQueryParams()
			.Insert("{tenant}", tenant)
			.Insert("{database}", database);
		var request = new GetOrCreateCollectionRequest()
		{
			Name = name,
			Metadata = metadata
		};
		return await _httpClient.Post<GetOrCreateCollectionRequest, ChromaCollection>("tenants/{tenant}/databases/{database}/collections", request, requestParams);
	}

	public async Task DeleteCollection(string name, string? tenant = null, string? database = null)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		var requestParams = new RequestQueryParams()
			.Insert("{collection_id}", name)
			.Insert("{tenant}", tenant)
			.Insert("{database}", database);
		await _httpClient.Delete("tenants/{tenant}/databases/{database}/collections/{collection_id}", requestParams);
	}

	public async Task<string> GetVersion()
	{
		return await _httpClient.Get<string>("version", new RequestQueryParams());
	}

	public async Task<bool> Reset()
	{
		return await _httpClient.Post<ResetRequest, bool>("reset", null, new RequestQueryParams());
	}

	public async Task<int> CountCollections(string? tenant = null, string? database = null)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		var requestParams = new RequestQueryParams()
			.Insert("{tenant}", tenant)
			.Insert("{database}", database);
		return await _httpClient.Get<int>("tenants/{tenant}/databases/{database}/collections_count", requestParams);
	}
}
