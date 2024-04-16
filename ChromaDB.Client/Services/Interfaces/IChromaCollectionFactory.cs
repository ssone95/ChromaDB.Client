using ChromaDB.Client.Models;

namespace ChromaDB.Client.Services.Interfaces;

public interface IChromaCollectionFactory
{
	IChromaCollectionClient Create(Collection collection);
	IChromaCollectionClient Create(Collection collection, IChromaDBHttpClient httpClient);
}
