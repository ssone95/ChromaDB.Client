using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;

namespace ChromaDB.Client.Services.Interfaces;

public interface IChromaDBClient
{
	Task<BaseResponse<Collection>> GetCollectionByName(string name, string? tenant = null, string? database = null);
	Task<BaseResponse<List<Collection>>> ListCollections(string? tenant = null, string? database = null);
	Task<BaseResponse<Heartbeat>> Heartbeat();
	Task<BaseResponse<Collection>> CreateCollection(DBCreateCollectionRequest request, string? tenant = null, string? database = null);
	Task<BaseResponse<Collection>> GetOrCreateCollection(DBGetOrCreateCollectionRequest request, string? tenant = null, string? database = null);
	Task<BaseResponse<bool>> Reset();
}
