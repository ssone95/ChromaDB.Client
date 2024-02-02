using ChromaDB.Client.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChromaDB.Client.Services.Interfaces
{
	public interface IChromaDBClient : IDisposable
	{
		Task<BaseResponse<Collection>> GetCollectionByName(string name, string? tenant = null, string? database = null);
		Task<BaseResponse<List<Collection>>> GetCollections(string? tenant = null, string? database = null);
	}
}
