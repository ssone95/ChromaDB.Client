# Dependency injection

`ChromaDotNet.Client.DependencyInjection` adds the registration for `Microsoft.Extensions.DependencyInjection`:

- `AddChromaClient` registers the `ChromaClient` as a singleton, so singletons can take it too. `AddKeyedChromaClient` registers more than one, under different keys, for other servers, tenants or databases. Each key gets its own `HttpClient`, also keys with the same text like `1` and `"1"`; a null key throws an `ArgumentNullException`.
- The client's `HttpClient` sends each request through the current handler of `IHttpClientFactory`. The factory renews the handler after its lifetime, two minutes by default, so a DNS change of the server address is picked up on every target framework. After two minutes the client also asks again what it learned about the server, like its version.
- An overload configures that `HttpClient`, for example with a resilience handler, a proxy or a timeout: `services.AddChromaClient(_ => options, builder => builder.AddStandardResilienceHandler())`.
- `serviceProvider.CreateChromaClient(options, httpClientName)` creates a client the same way when it is kept elsewhere, like in an integration with an `HttpClient` name of its own.
- `AddChromaClientFromServices` and `AddKeyedChromaClientFromServices` make the options from the services, when the client or its options are first resolved: so the address can come from `IConfiguration` or `IOptions` of the application, also registered after the call. The function runs once, and a null it returns throws an `InvalidOperationException`.

```csharp
services.AddChromaClientFromServices(serviceProvider =>
	new ChromaConfigurationOptions(serviceProvider.GetRequiredService<IConfiguration>()["Chroma:Uri"]!));
services.AddKeyedChromaClientFromServices("archive", serviceProvider =>
	new ChromaConfigurationOptions(serviceProvider.GetRequiredService<IOptions<ArchiveSettings>>().Value.ChromaUri),
	builder => builder.AddStandardResilienceHandler());
```
