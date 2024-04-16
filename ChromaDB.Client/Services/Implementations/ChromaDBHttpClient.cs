using ChromaDB.Client.Services.Interfaces;

namespace ChromaDB.Client.Services.Implementations;

public class ChromaDBHttpClient : HttpClient, IChromaDBHttpClient
{
	private readonly ConfigurationOptions _config;

	public ChromaDBHttpClient(ConfigurationOptions configurationOptions)
	{
		_config = configurationOptions;
		BaseAddress = _config.Uri;
	}
}
