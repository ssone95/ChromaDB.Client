using ChromaDB.Client.Services.Implementations;
using ChromaDB.Client.Tests.TestContainer;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

public class ClientResetTests
{
	[TestFixture]
	public class DefaultSettings : ChromaDBTestsBase
	{
		[Test]
		public async Task ResetSimple()
		{
			using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
			var client = new ChromaDBClient(ConfigurationOptions, httpClient);
			var result = await client.Reset();
			Assert.That(result.Success, Is.False);
			Assert.That(result.ErrorMessage, Contains.Substring("ALLOW_RESET"));
		}
	}

	[TestFixture]
	public class AllowReset : ChromaDBTestsBase
	{
		[Test]
		public async Task ResetSimple()
		{
			using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
			var client = new ChromaDBClient(ConfigurationOptions, httpClient);
			var result = await client.Reset();
			Assert.That(result.Success, Is.True);
			Assert.That(result.Data, Is.True);
		}

		protected override ChromaDBBuilder ConfigureContainer(ChromaDBBuilder builder)
			=> builder
				.WithEnvironment("ALLOW_RESET", "TRUE");
	}
}