using ChromaDB.Client.Tests.TestContainer;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

public abstract class ChromaTestsBase
{
	protected static readonly HttpClient HttpClient = new();

	private ChromaDBContainer _container;
	private ChromaConfigurationOptions? _baseConfigurationOptions;

	[OneTimeSetUp]
	public async Task OneTimeSetUp()
	{
		_container = ConfigureContainer(new ChromaDBBuilder()).Build();
		await _container.StartAsync();
		_baseConfigurationOptions = new ChromaConfigurationOptions(uri: $"http://{_container.IpAddress}:{_container.GetMappedPublicPort(ChromaDBBuilder.ChromaDBPort)}/api/v2/");
	}

	[OneTimeTearDown]
	public async Task OneTimeTearDown()
	{
		_baseConfigurationOptions = null;
		await _container.DisposeAsync();
	}

	protected ChromaConfigurationOptions BaseConfigurationOptions => _baseConfigurationOptions ?? throw new InvalidOperationException();

	protected virtual ChromaDBBuilder ConfigureContainer(ChromaDBBuilder builder) => builder;
}
