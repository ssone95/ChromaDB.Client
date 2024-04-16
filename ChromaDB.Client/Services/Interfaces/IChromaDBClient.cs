using ChromaDB.Client.Models;

namespace ChromaDB.Client.Services.Interfaces;

public interface IChromaDBClient : IDisposable
{
	Task<BaseResponse<Collection>> GetCollectionByName(string name, string? tenant = null, string? database = null);
	Task<BaseResponse<List<Collection>>> GetCollections(string? tenant = null, string? database = null);
}
