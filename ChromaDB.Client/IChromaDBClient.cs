using ChromaDB.Client.Models;

namespace ChromaDB.Client;

public interface IChromaDBClient
{
	Task<Response<Collection>> GetCollection(string name, string? tenant = null, string? database = null);
	Task<Response<List<Collection>>> ListCollections(string? tenant = null, string? database = null);
	Task<Response<Heartbeat>> Heartbeat();
	Task<Response<Collection>> CreateCollection(string name, IDictionary<string, object>? metadata = null, string? tenant = null, string? database = null);
	Task<Response<Collection>> GetOrCreateCollection(string name, IDictionary<string, object>? metadata = null, string? tenant = null, string? database = null);
	Task<Response<Response.Empty>> DeleteCollection(string name, string? tenant = null, string? database = null);
	Task<Response<string>> GetVersion();
	Task<Response<bool>> Reset();
	Task<Response<int>> CountCollections(string? tenant = null, string? database = null);
}
