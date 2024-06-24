using ChromaDB.Client.Services.Implementations;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class ChromaDBAuthTests : ChromaDBTestsBase
{
	[Test]
	public async Task NoAuth()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		await Assert.ThatAsync(client.Heartbeat, Throws.Nothing);
	}
}
