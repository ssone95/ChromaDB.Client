﻿using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;

namespace ChromaDB.Client.Services.Interfaces;

public interface IChromaDBClient
{
	Task<BaseResponse<Collection>> GetCollection(string name, string? tenant = null, string? database = null);
	Task<BaseResponse<List<Collection>>> ListCollections(string? tenant = null, string? database = null);
	Task<BaseResponse<Heartbeat>> Heartbeat();
	Task<BaseResponse<Collection>> CreateCollection(DBCreateCollectionRequest request, string? tenant = null, string? database = null);
	Task<BaseResponse<Collection>> GetOrCreateCollection(DBGetOrCreateCollectionRequest request, string? tenant = null, string? database = null);
	Task<BaseResponse<BaseResponse.None>> DeleteCollection(string name, string? tenant = null, string? database = null);
	Task<BaseResponse<string>> GetVersion();
	Task<BaseResponse<bool>> Reset();
	Task<BaseResponse<int>> CountCollections(string? tenant = null, string? database = null);
}
