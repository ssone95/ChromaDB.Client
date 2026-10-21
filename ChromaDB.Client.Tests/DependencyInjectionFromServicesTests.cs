using System.Net;
using ChromaDB.Client.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

// AddChromaClientFromServices and AddKeyedChromaClientFromServices: the options come from the services when the client is resolved.
[TestFixture]
public class DependencyInjectionFromServicesTests
{
	sealed record Settings(string Uri);

	// The options are made once, when the client or the options are resolved, from services registered also after the client.
	[Test]
	public void TheOptionsComeFromTheServices()
	{
		var services = new ServiceCollection();
		var calls = 0;
		services.AddChromaClientFromServices(serviceProvider =>
		{
			calls++;
			return new ChromaConfigurationOptions(serviceProvider.GetRequiredService<Settings>().Uri);
		});
		services.AddSingleton(new Settings("http://chroma.example:8000"));
		Assert.That(calls, Is.Zero);
		using var provider = services.BuildServiceProvider();
		var client = provider.GetRequiredService<ChromaClient>();
		Assert.That(client.Options.Uri, Is.EqualTo(new Uri("http://chroma.example:8000")));
		Assert.That(provider.GetRequiredService<ChromaConfigurationOptions>().Uri, Is.EqualTo(new Uri("http://chroma.example:8000")));
		Assert.That(provider.GetRequiredService<ChromaClient>(), Is.SameAs(client));
		Assert.That(calls, Is.EqualTo(1));
	}

	// The HttpClient of the client goes through the builder, as with AddChromaClient.
	[Test]
	public async Task TheHttpClientGoesThroughTheBuilder()
	{
		var handler = new Recorder();
		var services = new ServiceCollection();
		services.AddSingleton(new Settings("http://chroma.example:8000"));
		services.AddChromaClientFromServices(serviceProvider => new ChromaConfigurationOptions(serviceProvider.GetRequiredService<Settings>().Uri),
			builder => builder.ConfigurePrimaryHttpMessageHandler(() => handler));
		using var provider = services.BuildServiceProvider();
		await provider.GetRequiredService<ChromaClient>().HeartbeatAsync();
		Assert.That(handler.Uris.Single(), Is.EqualTo(new Uri("http://chroma.example:8000/api/v2/heartbeat")));
	}

	[Test]
	public async Task KeyedClientsFromTheServices()
	{
		var first = new Recorder();
		var second = new Recorder();
		var services = new ServiceCollection();
		services.AddKeyedSingleton("first", new Settings("http://first:8000"));
		services.AddKeyedSingleton("second", new Settings("http://second:8000"));
		services.AddKeyedChromaClientFromServices("first", serviceProvider => new ChromaConfigurationOptions(serviceProvider.GetRequiredKeyedService<Settings>("first").Uri),
			builder => builder.ConfigurePrimaryHttpMessageHandler(() => first));
		services.AddKeyedChromaClientFromServices("second", serviceProvider => new ChromaConfigurationOptions(serviceProvider.GetRequiredKeyedService<Settings>("second").Uri),
			builder => builder.ConfigurePrimaryHttpMessageHandler(() => second));
		using var provider = services.BuildServiceProvider();
		await provider.GetRequiredKeyedService<ChromaClient>("first").HeartbeatAsync();
		await provider.GetRequiredKeyedService<ChromaClient>("second").HeartbeatAsync();
		Assert.That(first.Uris.Single().Host, Is.EqualTo("first"));
		Assert.That(second.Uris.Single().Host, Is.EqualTo("second"));
		Assert.That(provider.GetRequiredKeyedService<ChromaConfigurationOptions>("second").Uri.Host, Is.EqualTo("second"));
	}

	[Test]
	public void WrongArguments()
	{
		var services = new ServiceCollection();
		Assert.That(() => services.AddChromaClientFromServices(null!), Throws.ArgumentNullException);
		Assert.That(() => services.AddKeyedChromaClientFromServices(null!, _ => new ChromaConfigurationOptions()), Throws.ArgumentNullException);
		Assert.That(() => services.AddKeyedChromaClientFromServices("k", null!), Throws.ArgumentNullException);
		services.AddChromaClientFromServices(_ => null!);
		using var provider = services.BuildServiceProvider();
		Assert.That(() => provider.GetRequiredService<ChromaClient>(), Throws.InvalidOperationException.With.Message.Contains("returned null"));
	}

	[Test]
	public void RegistrationsReturnTheServices()
	{
		var services = new ServiceCollection();
		Assert.That(services.AddChromaClientFromServices(_ => new ChromaConfigurationOptions()), Is.SameAs(services));
		Assert.That(services.AddKeyedChromaClientFromServices("k", _ => new ChromaConfigurationOptions(), _ => { }), Is.SameAs(services));
	}

	// Answers every request with a heartbeat, and records the URIs.
	sealed class Recorder : HttpMessageHandler
	{
		public List<Uri> Uris { get; } = [];

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Uris.Add(request.RequestUri!);
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"nanosecond heartbeat":1}""") });
		}
	}
}
