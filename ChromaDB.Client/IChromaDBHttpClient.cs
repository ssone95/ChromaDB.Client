namespace ChromaDB.Client;

public interface IChromaDBHttpClient : IDisposable
{
	public Uri? BaseAddress { get; set; }

	public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation = default);
}
