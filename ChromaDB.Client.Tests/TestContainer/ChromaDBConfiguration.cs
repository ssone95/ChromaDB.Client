using Docker.DotNet.Models;
using DotNet.Testcontainers.Configurations;

namespace ChromaDB.Client.Tests.TestContainer;

public class ChromaDBConfiguration : ContainerConfiguration
{
	public ChromaDBConfiguration()
	{ }

	public ChromaDBConfiguration(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
		: base(resourceConfiguration)
	{ }

	public ChromaDBConfiguration(IContainerConfiguration resourceConfiguration)
		: base(resourceConfiguration)
	{ }

	public ChromaDBConfiguration(ChromaDBConfiguration resourceConfiguration)
		: this(new ChromaDBConfiguration(), resourceConfiguration)
	{ }

	public ChromaDBConfiguration(ChromaDBConfiguration oldValue, ChromaDBConfiguration newValue)
		: base(oldValue, newValue)
	{ }
}