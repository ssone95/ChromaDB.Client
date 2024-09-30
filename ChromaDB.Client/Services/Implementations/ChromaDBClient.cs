using ChromaDB.Client.Common.Helpers;
using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Services.Interfaces;

namespace ChromaDB.Client.Services.Implementations;

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

	public async Task<BaseResponse<List<Collection>>> ListCollections(string? tenant = null, string? database = null)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{tenant}", tenant)
			.Insert("{database}", database);
		return await _httpClient.Get<List<Collection>>("collections?tenant={tenant}&database={database}", requestParams);
	}

	public async Task<BaseResponse<Collection>> GetCollection(string name, string? tenant = null, string? database = null)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{collectionName}", name)
			.Insert("{tenant}", tenant)
			.Insert("{database}", database);
		return await _httpClient.Get<Collection>("collections/{collectionName}?tenant={tenant}&database={database}", requestParams);
	}

	public async Task<BaseResponse<Heartbeat>> Heartbeat()
	{
		return await _httpClient.Get<Heartbeat>("", new RequestQueryParams());
	}

	public async Task<BaseResponse<Collection>> CreateCollection(DBCreateCollectionRequest request, string? tenant = null, string? database = null)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{tenant}", tenant)
			.Insert("{database}", database);
		return await _httpClient.Post<DBCreateCollectionRequest, Collection>("collections?tenant={tenant}&database={database}", request, requestParams);
	}

	public async Task<BaseResponse<Collection>> GetOrCreateCollection(DBGetOrCreateCollectionRequest request, string? tenant = null, string? database = null)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{tenant}", tenant)
			.Insert("{database}", database);
		return await _httpClient.Post<DBGetOrCreateCollectionRequest, Collection>("collections?tenant={tenant}&database={database}", request, requestParams);
	}

	public async Task<BaseResponse<BaseResponse.None>> DeleteCollection(string name, string? tenant = null, string? database = null)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{collectionName}", name)
			.Insert("{tenant}", tenant)
			.Insert("{database}", database);
		return await _httpClient.Delete<BaseResponse.None>("collections/{collectionName}?tenant={tenant}&database={database}", requestParams);
	}

	public async Task<BaseResponse<string>> GetVersion()
	{
		return await _httpClient.Get<string>("version", new RequestQueryParams());
	}

	public async Task<BaseResponse<bool>> Reset()
	{
		return await _httpClient.Post<Reset, bool>("reset", null, new RequestQueryParams());
	}

	public async Task<BaseResponse<int>> CountCollections(string? tenant = null, string? database = null)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		RequestQueryParams requestParams = new RequestQueryParams()
			.Insert("{tenant}", tenant)
			.Insert("{database}", database);
		return await _httpClient.Get<int>("count_collections?tenant={tenant}&database={database}", requestParams);
	}
}
