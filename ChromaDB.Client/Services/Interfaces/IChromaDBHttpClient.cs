using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChromaDB.Client.Services.Interfaces
{
	public interface IChromaDBHttpClient : IDisposable
	{
		public Uri? BaseUri { get; set; }

		public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation = default);
	}
}
