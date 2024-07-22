using ChromaDB.Client.Services.Implementations;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class ChromaDBHeartbeatTests : ChromaDBTestsBase
{
	[Test]
	public async Task HeartbeatSimple()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var result = await client.Heartbeat();
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Is.Not.Null);
		Assert.That(result.Data.NanosecondHeartbeat, Is.GreaterThan(0));
	}
}
