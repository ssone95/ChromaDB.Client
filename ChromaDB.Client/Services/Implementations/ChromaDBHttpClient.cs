using ChromaDB.Client.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ChromaDB.Client.Services.Implementations
{
	public class ChromaDBHttpClient : HttpClient, IChromaDBHttpClient
	{
		public Uri? BaseUri
		{
			get { return base.BaseAddress; }
			set { base.BaseAddress = value; }
		}

		private readonly ConfigurationOptions _config;

		public ChromaDBHttpClient(ConfigurationOptions configurationOptions)
		{
			_config = configurationOptions;
			BaseUri = _config.Uri;
		}

		public override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			return await base.SendAsync(request, cancellationToken);
		}

		#region Dispose
		private object _disposeLock = new();
		private bool _disposed = false;
		public new void Dispose()
		{
			lock (_disposeLock)
			{
				if (!_disposed)
				{
					base.Dispose();
					// Left as non-nullable on purpose, but disabled the warning here as we want to have IDisposable implemented and resource cleanup done immediately
					_disposed = true;
				}
			}
		}
		#endregion
	}
}
