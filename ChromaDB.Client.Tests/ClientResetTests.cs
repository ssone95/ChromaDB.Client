using ChromaDB.Client.Tests.TestContainer;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

public class ClientResetTests
{
	[TestFixture]
	public class DefaultSettings : ChromaTestsBase
	{
		[Test]
		public async Task ResetSimple()
		{
			var client = new ChromaClient(ConfigurationOptions, HttpClient);
			await Assert.ThatAsync(client.Reset, Throws.InstanceOf<ChromaException>().With.Message.Contains("ALLOW_RESET"));
		}
	}

	[TestFixture]
	public class AllowReset : ChromaTestsBase
	{
		[Test]
		public async Task ResetSimple()
		{
			var client = new ChromaClient(ConfigurationOptions, HttpClient);
			var result = await client.Reset();
			Assert.That(result, Is.True);
		}

		protected override ChromaDBBuilder ConfigureContainer(ChromaDBBuilder builder)
			=> builder
				.WithEnvironment("ALLOW_RESET", "TRUE");
	}
}