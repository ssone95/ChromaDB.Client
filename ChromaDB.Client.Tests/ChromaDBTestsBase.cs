using ChromaDB.Client.Tests.TestContainer;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

public abstract class ChromaDBTestsBase
{
	private readonly ChromaDBContainer _container = new ChromaDBBuilder().Build();
	private ConfigurationOptions? _configurationOptions;

	[OneTimeSetUp]
	public async Task OneTimeSetUp()
	{
		await _container.StartAsync();
		_configurationOptions = new ConfigurationOptions(uri: $"http://{_container.IpAddress}:{_container.GetMappedPublicPort(ChromaDBBuilder.ChromaDBPort)}/api/v1/");
	}

	[OneTimeTearDown]
	public async Task OneTimeTearDown()
	{
		_configurationOptions = null;
		await _container.DisposeAsync();
	}

	protected ChromaDBContainer Container => _container;
	protected ConfigurationOptions ConfigurationOptions => _configurationOptions ?? throw new InvalidOperationException();
}
