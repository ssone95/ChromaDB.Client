using ChromaDB.Client.Common;

namespace ChromaDB.Client;

public class ChromaConfigurationOptions
{
	public Uri Uri { get; init; }
	public string? Tenant { get; init; }
	public string? Database { get; init; }
	public string? ChromaToken { get; init; }

	public ChromaConfigurationOptions(Uri uri, string? defaultTenant = null, string? defaultDatabase = null, string? chromaToken = null)
	{
		Uri = uri;
		Tenant = defaultTenant;
		Database = defaultDatabase;
		ChromaToken = chromaToken;
	}

	public ChromaConfigurationOptions(string uri, string? defaultTenant = null, string? defaultDatabase = null, string? chromaToken = null)
		: this(new Uri(uri), defaultTenant, defaultDatabase, chromaToken)
	{ }

	public ChromaConfigurationOptions()
		: this(ClientConstants.DefaultUri)
	{ }
}
