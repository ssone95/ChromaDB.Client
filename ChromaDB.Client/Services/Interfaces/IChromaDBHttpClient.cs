namespace ChromaDB.Client.Services.Interfaces;

public interface IChromaDBHttpClient : IDisposable
{
	public Uri? BaseUri { get; set; }

	public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation = default);
}
