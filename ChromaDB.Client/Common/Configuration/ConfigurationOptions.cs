using ChromaDB.Client.Common.Exceptions;

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
    
    public ConfigurationOptions(string uri, string? defaultTenant = null, string? defaultDatabase = null) : this(new Uri(uri), defaultTenant, defaultDatabase)
    {
        if (string.IsNullOrEmpty(uri)) throw new ChromaDBGeneralException($"{this.GetType()}: Argument {nameof(uri)} cannot be null!");
        if (!uri.EndsWith("/")) throw new ChromaDBGeneralException($"{this.GetType()}: Argument {nameof(uri)} must end with /");
    }

    public ConfigurationOptions(Uri uri, string? defaultTenant = null, string? defaultDatabase = null) 
    {
        Uri = uri;
        Tenant = defaultTenant;
        Database = defaultDatabase;
    }
}
