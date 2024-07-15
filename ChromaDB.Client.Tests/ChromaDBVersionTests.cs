using ChromaDB.Client.Services.Implementations;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class ChromaDBVersionTests : ChromaDBTestsBase
{
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
