namespace ChromaDB.Client.Services.Interfaces;

public interface IChromaDBHttpClient : IDisposable
{
	public Uri? BaseAddress { get; set; }

	public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation = default);
}
