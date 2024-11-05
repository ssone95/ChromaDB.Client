using ChromaDB.Client.Common;

namespace ChromaDB.Client;

public class ChromaConfigurationOptions
{
	public Uri Uri { get; init; }
	public string? Tenant { get; init; }
	public string? Database { get; init; }

	public ChromaConfigurationOptions(Uri uri, string? defaultTenant = null, string? defaultDatabase = null)
	{
		Uri = uri;
		Tenant = defaultTenant;
		Database = defaultDatabase;
	}

	public ChromaConfigurationOptions(string uri, string? defaultTenant = null, string? defaultDatabase = null)
		: this(new Uri(uri), defaultTenant, defaultDatabase)
	{ }

	public ChromaConfigurationOptions()
		: this(ClientConstants.DefaultUri)
	{ }
}
