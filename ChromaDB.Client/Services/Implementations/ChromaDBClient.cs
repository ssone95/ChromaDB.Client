using ChromaDB.Client.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChromaDB.Client.Services.Implementations
{
    public class ChromaDBClient : IChromaDBClient, IDisposable
	{
        private HttpClient _httpClient;
        public ChromaDBClient(string baseAddress)
        {
			_httpClient = new HttpClient()
            {
                BaseAddress = new Uri(baseAddress),
            };

		}
        public ChromaDBClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        private object _disposeLock = new object();
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
	}
}
