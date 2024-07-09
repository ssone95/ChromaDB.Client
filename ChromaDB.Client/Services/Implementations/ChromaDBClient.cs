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
			.Add("{tenant}", tenant)
			.Add("{database}", database);
		return await _httpClient.Get<Collection, List<Collection>>(requestParams);
	}

	public async Task<BaseResponse<Collection>> GetCollection(string name, string? tenant = null, string? database = null)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		RequestQueryParams requestParams = new RequestQueryParams()
			.Add("{collectionName}", name)
			.Add("{tenant}", tenant)
			.Add("{database}", database);
		return await _httpClient.Get<Collection, Collection>(requestParams);
	}

	public async Task<BaseResponse<Heartbeat>> Heartbeat()
	{
		return await _httpClient.Get<Heartbeat, Heartbeat>(new RequestQueryParams());
	}

	public async Task<BaseResponse<Collection>> CreateCollection(DBCreateCollectionRequest request, string? tenant = null, string? database = null)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		RequestQueryParams requestParams = new RequestQueryParams()
			.Add("{tenant}", tenant)
			.Add("{database}", database);
		return await _httpClient.Post<Collection, DBCreateCollectionRequest, Collection>(request, requestParams);
	}

	public async Task<BaseResponse<Collection>> GetOrCreateCollection(DBGetOrCreateCollectionRequest request, string? tenant = null, string? database = null)
	{
		tenant = tenant is not null and not [] ? tenant : _currentTenant.Name;
		database = database is not null and not [] ? database : _currentDatabase.Name;
		RequestQueryParams requestParams = new RequestQueryParams()
			.Add("{tenant}", tenant)
			.Add("{database}", database);
		return await _httpClient.Post<Collection, DBGetOrCreateCollectionRequest, Collection>(request, requestParams);
	}
}
