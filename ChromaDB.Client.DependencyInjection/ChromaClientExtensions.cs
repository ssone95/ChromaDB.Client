using Microsoft.Extensions.DependencyInjection;

namespace ChromaDB.Client.DependencyInjection;

public static class ChromaClientExtensions
{
	public static void AddChromaClient(this IServiceCollection services, Func<ChromaConfigurationOptions?, ChromaConfigurationOptions>? configurationOptions = null)
	{
		configurationOptions ??= DefaultConfigurationOptions;

		ChromaConfigurationOptions options = new();
		options = configurationOptions(options);

		services.AddScoped<ChromaClient>();
		services.AddHttpClient<ChromaClient>(o =>
		{
			o.BaseAddress = options.Uri;
		});
	}

	private static ChromaConfigurationOptions DefaultConfigurationOptions(ChromaConfigurationOptions? options = null)
		=> options ?? new ChromaConfigurationOptions();
}