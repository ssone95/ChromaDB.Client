using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Services.Implementations;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class ChromaDBCollectionTests : ChromaDBTestsBase
{
	[Test]
	public async Task CreateCollectionWithoutMetadata()
	{
		var name = $"collection{Random.Shared.Next()}";

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var result = await client.CreateCollection(new DBCreateCollectionRequest()
		{
			Name = name,
		});
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Is.Not.Null);
		Assert.That(result.Data.Name, Is.EqualTo(name));
	}

	[Test]
	public async Task CreateCollectionWithMetadata()
	{
		var name = $"collection{Random.Shared.Next()}";
		var metadata = new Dictionary<string, object>()
		{
			{ "test", "foo" },
			{ "test2", 10 },
		};

		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		var result = await client.CreateCollection(new DBCreateCollectionRequest()
		{
			Name = name,
			Metadata = metadata,
		});
		Assert.That(result.Success, Is.True);
		Assert.That(result.Data, Is.Not.Null);
		Assert.That(result.Data.Name, Is.EqualTo(name));
		Assert.That(result.Data.Metadata, Is.Not.Null);
		Assert.That(result.Data.Metadata["test"], Is.EqualTo(metadata["test"]));
		Assert.That(result.Data.Metadata["test2"], Is.EqualTo(metadata["test2"]));
	}

	[Test]
	public async Task CreateCollectionAlreadyExists()
	{
		using var httpClient = new ChromaDBHttpClient(ConfigurationOptions);
		var client = new ChromaDBClient(ConfigurationOptions, httpClient);
		await client.CreateCollection(new DBCreateCollectionRequest()
		{
			Name = $"collection_exists",
		});
		var result = await client.CreateCollection(new DBCreateCollectionRequest()
		{
			Name = $"collection_exists",
		});
		Assert.That(result.Success, Is.False);
		Assert.That(result.ReasonPhrase, Is.Not.Null.And.Not.Empty);
	}
}
