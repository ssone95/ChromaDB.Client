namespace ChromaDB.Client;

public class ConfigurationOptions
{
    public string DatabaseName { get; init; }
    public string TenantName { get; init; }
    public Uri Uri { get; init; }

    public ConfigurationOptions() 
    {
        DatabaseName = ClientConstants.DefaultDatabase;
        TenantName = ClientConstants.DefaultTenant;
        Uri = new Uri(ClientConstants.DefaultUri);
    }

    public ConfigurationOptions(string uri, string database = ClientConstants.DefaultDatabase, string tenant = ClientConstants.DefaultTenant) : this(new Uri(uri)) 
    {
        DatabaseName = database;
        TenantName = tenant;
    }

    public ConfigurationOptions(Uri uri, string database = ClientConstants.DefaultDatabase, string tenant = ClientConstants.DefaultTenant) : this(uri) 
    {
        DatabaseName = database;
        TenantName = tenant;
    }
    
    public ConfigurationOptions(string uri) : this(new Uri(uri))
    {
    }

    public ConfigurationOptions(Uri uri) 
    {
        DatabaseName = ClientConstants.DefaultDatabase;
        TenantName = ClientConstants.DefaultTenant;
        Uri = uri;
    }
}
