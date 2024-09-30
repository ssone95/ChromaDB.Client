using ChromaDB.Client.Services.Implementations;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class ChromaDBTests : ChromaDBTestsBase
{
	[Test]
	public async Task NoAuth()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		await Assert.ThatAsync(client.Heartbeat, Throws.Nothing);
	}

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

	[Test]
	public async Task GetVersionSimple()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var result = await client.GetVersion();
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Is.Not.Null.And.Not.Empty);
		Assert.That(result.Data, Does.Match(@"\d+\.\d+"));
	}
}
