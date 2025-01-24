using ChromaDB.Client.Tests.TestContainer;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

public class ClientAuthTests
{
	[TestFixture]
	public class NoAuth : ChromaTestsBase
	{
		[Test]
		public async Task Success()
		{
			var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
			await Assert.ThatAsync(() => client.CreateCollection($"collection{Random.Shared.Next()}"), Throws.Nothing);
		}
	}

	[TestFixture]
	public class ChromaTokenAuth : ChromaTestsBase
	{
		[Test]
		public async Task Success()
		{
			var client = new ChromaClient(BaseConfigurationOptions.WithChromaToken("random-ToKen"), HttpClient);
			await Assert.ThatAsync(() => client.CreateCollection($"collection{Random.Shared.Next()}"), Throws.Nothing);
		}

		[Test]
		public async Task NoToken()
		{
			var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
			await Assert.ThatAsync(() => client.CreateCollection($"collection{Random.Shared.Next()}"), Throws.InstanceOf<ChromaException>().With.Message.Contains("Forbidden"));
		}

		[Test]
		public async Task WrongToken()
		{
			var client = new ChromaClient(BaseConfigurationOptions.WithChromaToken("wrong"), HttpClient);
			await Assert.ThatAsync(() => client.CreateCollection($"collection{Random.Shared.Next()}"), Throws.InstanceOf<ChromaException>().With.Message.Contains("Forbidden"));
		}

		[Test]
		public async Task WrongTokenCasing()
		{
			var client = new ChromaClient(BaseConfigurationOptions.WithChromaToken("random-token"), HttpClient);
			await Assert.ThatAsync(() => client.CreateCollection($"collection{Random.Shared.Next()}"), Throws.InstanceOf<ChromaException>().With.Message.Contains("Forbidden"));
		}

		protected override ChromaDBBuilder ConfigureContainer(ChromaDBBuilder builder)
			=> builder
				.WithEnvironment("CHROMA_SERVER_AUTHN_CREDENTIALS", "random-ToKen")
				.WithEnvironment("CHROMA_SERVER_AUTHN_PROVIDER", "chromadb.auth.token_authn.TokenAuthenticationServerProvider")
				.WithEnvironment("CHROMA_AUTH_TOKEN_TRANSPORT_HEADER", "X-Chroma-Token");
	}
}
