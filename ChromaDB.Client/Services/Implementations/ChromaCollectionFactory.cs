using ChromaDB.Client.Models;
using ChromaDB.Client.Services.Interfaces;

namespace ChromaDB.Client.Services.Implementations;

public class ChromaCollectionFactory : IChromaCollectionFactory
{
	private readonly ConfigurationOptions _config;

	public ChromaCollectionFactory(ConfigurationOptions configurationOptions)
	{
		_config = configurationOptions;
	}

	public IChromaCollectionClient Create(Collection collection, IChromaDBHttpClient httpClient)
	{
		return new ChromaCollectionClient(collection, httpClient);
	}

	public IChromaCollectionClient Create(Collection collection)
	{
		throw new NotImplementedException();
	}
}
