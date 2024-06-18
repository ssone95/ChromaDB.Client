using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;

namespace ChromaDB.Client.Tests.TestContainer;

public class ChromaDBBuilder : ContainerBuilder<ChromaDBBuilder, ChromaDBContainer, ChromaDBConfiguration>
{
	public const string ChromaDBImage = "chromadb/chroma:latest";
	public const int ChromaDBPort = 8000;

	protected override ChromaDBConfiguration DockerResourceConfiguration { get; }

	public ChromaDBBuilder()
		: this(new ChromaDBConfiguration())
	{
		DockerResourceConfiguration = Init().DockerResourceConfiguration;
	}

	private ChromaDBBuilder(ChromaDBConfiguration dockerResourceConfiguration)
		: base(dockerResourceConfiguration)
	{
		DockerResourceConfiguration = dockerResourceConfiguration;
	}

	public override ChromaDBContainer Build()
	{
		Validate();
		return new ChromaDBContainer(DockerResourceConfiguration);
	}

	protected override ChromaDBBuilder Init()
	{
		return base.Init()
			.WithImage(ChromaDBImage)
			.WithPortBinding(ChromaDBPort)
			.WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(ChromaDBPort));
	}

	protected override ChromaDBBuilder Clone(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
	{
		return Merge(DockerResourceConfiguration, new ChromaDBConfiguration(resourceConfiguration));
	}

	protected override ChromaDBBuilder Merge(ChromaDBConfiguration oldValue, ChromaDBConfiguration newValue)
	{
		return new ChromaDBBuilder(new ChromaDBConfiguration(oldValue, newValue));
	}

	protected override ChromaDBBuilder Clone(IContainerConfiguration resourceConfiguration)
	{
		return Merge(DockerResourceConfiguration, new ChromaDBConfiguration(resourceConfiguration));
	}
}