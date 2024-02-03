using ChromaDB.Client.Common.Helpers;
using ChromaDB.Client.Common.Mappers;
using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Models.Responses;
using ChromaDB.Client.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ChromaDB.Client.Services.Implementations
{
    public class ChromaCollectionClient : IChromaCollectionClient
	{
		private readonly Collection _collection;
		private IChromaDBHttpClient _httpClient;
		public ChromaCollectionClient(Collection collection, IChromaDBHttpClient httpClient)
		{
			_collection = collection;
			_httpClient = httpClient;
		}

		public async Task<BaseResponse<List<CollectionEntry>>> Get(CollectionGetRequest request)
		{
			var requestParams = new RequestQueryParams().Add("{collection_id}", _collection.Id.ToString());

			BaseResponse<CollectionEntriesResponse> response = await _httpClient
				.Post<Collection, CollectionGetRequest, CollectionEntriesResponse>(request, requestParams);

			List<CollectionEntry> entries = response.Data?.Map() 
				?? new List<CollectionEntry>();

			return new BaseResponse<List<CollectionEntry>>(entries, response.StatusCode, response.StatusReason);
		}

		public async Task<BaseResponse<CollectionEntriesQueryResponse>> Query(CollectionQueryRequest request)
		{
			var requestParams = new RequestQueryParams().Add("{collection_id}", _collection.Id.ToString());

			return  await _httpClient
				.Post<Collection, CollectionQueryRequest, CollectionEntriesQueryResponse>(request, requestParams);
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
