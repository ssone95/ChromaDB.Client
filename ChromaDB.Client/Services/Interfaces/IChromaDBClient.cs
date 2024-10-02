using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;

namespace ChromaDB.Client.Services.Interfaces;

public interface IChromaDBClient
{
	Task<Response<Collection>> GetCollection(string name, string? tenant = null, string? database = null);
	Task<Response<List<Collection>>> ListCollections(string? tenant = null, string? database = null);
	Task<Response<Heartbeat>> Heartbeat();
	Task<Response<Collection>> CreateCollection(CreateCollectionRequest request, string? tenant = null, string? database = null);
	Task<Response<Collection>> GetOrCreateCollection(GetOrCreateCollectionRequest request, string? tenant = null, string? database = null);
	Task<Response<Response.Empty>> DeleteCollection(string name, string? tenant = null, string? database = null);
	Task<Response<string>> GetVersion();
	Task<Response<bool>> Reset();
	Task<Response<int>> CountCollections(string? tenant = null, string? database = null);
}
