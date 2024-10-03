using ChromaDB.Client.Common.Constants;

namespace ChromaDB.Client;

public class ConfigurationOptions
{
	public Uri Uri { get; init; }
	public string? Tenant { get; init; }
	public string? Database { get; init; }

	public ConfigurationOptions()
	{
		Uri = new Uri(ClientConstants.DefaultUri);
	}

	public ConfigurationOptions(Uri uri, string? defaultTenant = null, string? defaultDatabase = null)
	{
		Uri = uri;
		Tenant = defaultTenant;
		Database = defaultDatabase;
	}

	public ConfigurationOptions(string uri, string? defaultTenant = null, string? defaultDatabase = null)
		: this(new Uri(uri), defaultTenant, defaultDatabase)
	{ }
}
