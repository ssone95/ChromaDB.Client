using ChromaDB.Client.Common.Helpers;
using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChromaDB.Client.Services.Implementations
{
    public class ChromaDBClient : IChromaDBClient
	{
        private readonly ConfigurationOptions _config;
        private IChromaDBHttpClient _httpClient;
        private Tenant _currentTenant = ClientConstants.DefaultTenant;
        private Database _currentDatabase = ClientConstants.DefaultDatabase;

        public ChromaDBClient(ConfigurationOptions options, IChromaDBHttpClient httpClient)
        {
            _config = options;
            _httpClient = httpClient;

            if (!string.IsNullOrEmpty(options.Tenant))
            {
                _currentTenant = new Tenant(options.Tenant);
            }

            if (!string.IsNullOrEmpty(options.Database))
            {
                _currentDatabase = new Database(options.Database);
            }
		}

        public ChromaDBClient(IChromaDBHttpClient httpClient, ConfigurationOptions options)
        {
            _config = options;
            _httpClient = httpClient;
            _httpClient.BaseUri = _config.Uri;
        }

        public async Task<BaseResponse<List<Collection>>> GetCollections(string? tenant = null, string? database = null)
        {
            string tenantName = !string.IsNullOrEmpty(tenant) ? tenant : _currentTenant.Name;
            string dbName = !string.IsNullOrEmpty(database) ? database : _currentDatabase.Name;

			var requestParams = new RequestQueryParams().Add("{tenant}", tenantName).Add("{database}", dbName);
			return await _httpClient.Get<Collection, List<Collection>>(requestParams);
		}

		public async Task<BaseResponse<Collection>> GetCollectionByName(string name, string? tenant = null, string? database = null)
		{
			string tenantName = !string.IsNullOrEmpty(tenant) ? tenant : _currentTenant.Name;
			string dbName = !string.IsNullOrEmpty(database) ? database : _currentDatabase.Name;

			var requestParams = new RequestQueryParams().Add("{collectionName}", name)
                .Add("{tenant}", tenantName)
                .Add("{database}", dbName);
			return await _httpClient.Get<Collection, Collection>(requestParams);
		}

		#region Dispose
		private object _disposeLock = new();
        private bool _disposed = false;
		public void Dispose()
		{
            lock (_disposeLock)
            {
                if (!_disposed && _httpClient != null)
                {
                    _httpClient.Dispose();
                    // Left as non-nullable on purpose, but disabled the warning here as we want to have IDisposable implemented and resource cleanup done immediately
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
					_httpClient = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
					_disposed = true;
                }
            }
		}
		#endregion
	}
}
