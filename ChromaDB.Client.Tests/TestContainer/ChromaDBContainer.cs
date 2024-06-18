using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

namespace ChromaDB.Client.Tests.TestContainer;

public class ChromaDBContainer : DockerContainer
{
	public ChromaDBContainer(IContainerConfiguration configuration)
		: base(configuration)
	{ }
}
