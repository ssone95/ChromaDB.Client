using ChromaDB.Client.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChromaDB.Client.Services.Interfaces
{
	public interface IChromaCollectionFactory
	{
		IChromaCollectionClient Create(Collection collection);
		IChromaCollectionClient Create(Collection collection, IChromaDBHttpClient httpClient);
	}
}
