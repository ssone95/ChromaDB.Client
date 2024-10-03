using ChromaDB.Client.Common.Constants;
using ChromaDB.Client.Common.Helpers;
using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;

namespace ChromaDB.Client;

public class ChromaDBClient : IChromaDBClient
{
	private readonly ConfigurationOptions _config;
	private readonly IChromaDBHttpClient _httpClient;

	private Tenant _currentTenant;
	private Database _currentDatabase;

	public ChromaDBClient(ConfigurationOptions options, IChromaDBHttpClient httpClient)
	{
		_config = options;
		_httpClient = httpClient;

		_currentTenant = options.Tenant is not null and not []
			? new Tenant(options.Tenant)
			: ClientConstants.DefaultTenant;
		_currentDatabase = options.Database is not null and not []
			? new Database(options.Database)
			: ClientConstants.DefaultDatabase;
	}

	public async Task<Response<List<Collection>>> ListCollections(string? tenant = null, string? database = null)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{tenant}", tenant)
			.Insert("{database}", database);
		return await _httpClient.Get<List<Collection>>("collections?tenant={tenant}&database={database}", requestParams);
	}

	public async Task<Response<Collection>> GetCollection(string name, string? tenant = null, string? database = null)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{collectionName}", name)
			.Insert("{tenant}", tenant)
			.Insert("{database}", database);
		return await _httpClient.Get<Collection>("collections/{collectionName}?tenant={tenant}&database={database}", requestParams);
	}

	public async Task<Response<Heartbeat>> Heartbeat()
	{
		return await _httpClient.Get<Heartbeat>("", new RequestQueryParams());
	}

	public async Task<Response<Collection>> CreateCollection(string name, Dictionary<string, object>? metadata = null, string? tenant = null, string? database = null)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{tenant}", tenant)
			.Insert("{database}", database);
		CreateCollectionRequest request = new CreateCollectionRequest()
		{
			Name = name,
			Metadata = metadata
		};
		return await _httpClient.Post<CreateCollectionRequest, Collection>("collections?tenant={tenant}&database={database}", request, requestParams);
	}

	public async Task<Response<Collection>> GetOrCreateCollection(string name, Dictionary<string, object>? metadata = null, string? tenant = null, string? database = null)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{tenant}", tenant)
			.Insert("{database}", database);
		GetOrCreateCollectionRequest request = new GetOrCreateCollectionRequest()
		{
			Name = name,
			Metadata = metadata
		};
		return await _httpClient.Post<GetOrCreateCollectionRequest, Collection>("collections?tenant={tenant}&database={database}", request, requestParams);
	}

	public async Task<Response<Response.Empty>> DeleteCollection(string name, string? tenant = null, string? database = null)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{collectionName}", name)
			.Insert("{tenant}", tenant)
			.Insert("{database}", database);
		return await _httpClient.Delete<Response.Empty>("collections/{collectionName}?tenant={tenant}&database={database}", requestParams);
	}

	public async Task<Response<string>> GetVersion()
	{
		return await _httpClient.Get<string>("version", new RequestQueryParams());
	}

	public async Task<Response<bool>> Reset()
	{
		return await _httpClient.Post<ResetRequest, bool>("reset", null, new RequestQueryParams());
	}

	public async Task<Response<int>> CountCollections(string? tenant = null, string? database = null)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{tenant}", tenant)
			.Insert("{database}", database);
		return await _httpClient.Get<int>("count_collections?tenant={tenant}&database={database}", requestParams);
	}
}
