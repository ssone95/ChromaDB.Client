using ChromaDB.Client.Services.Implementations;
using ChromaDB.Client.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ChromaDB.Client.Extensions;

public static class ChromaDBClientExtensions
{
    private static ConfigurationOptions DefaultConfigurationOptions(ConfigurationOptions? options = null) 
    {
        options ??= new ConfigurationOptions(new Uri(ClientConstants.DefaultUri)) 
        {
            DatabaseName = ClientConstants.DefaultDatabase,
            TenantName = ClientConstants.DefaultTenant
        };
        return options;
    }
    public static void AddChromaDBClient(this IServiceCollection services, Func<ConfigurationOptions?, ConfigurationOptions>? configurationOptions = null)
    {
        configurationOptions ??= DefaultConfigurationOptions;

        ConfigurationOptions options = new ();
        options = configurationOptions(options);

        services.AddScoped<IChromaDBClient, ChromaDBClient>();
        services.AddHttpClient<IChromaDBClient, ChromaDBClient>(o => 
        {
            o.BaseAddress = options.Uri;
        });
    }
}