using NUnit.Framework;

namespace ChromaDB.Client.Tests;

// GetCollectionClient(name, createIfMissing: true) and GetOrCreateCollectionClientAsync on the server.
[TestFixture]
public class CreateIfMissingServerTests : ChromaTestsBase
{
	// The first write creates the collection; deleted elsewhere, it is created again, empty, and the write goes to it.
	[Test]
	public async Task CreatedAtTheFirstWriteAndAgainAfterADeletion()
	{
		var client = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var name = $"create{Guid.NewGuid():N}";
		try
		{
			var records = client.GetCollectionClient(name, createIfMissing: true);
			Assert.That(await client.CollectionExistsAsync(name), Is.False);
			await records.AddAsync(["a"], [new([1f, 0f])]);
			Assert.That(await records.CountAsync(), Is.EqualTo(1));
			var first = records.Collection.Id;
			await client.DeleteCollectionAsync(name);
			await records.AddAsync(["b"], [new([0f, 1f])]);
			Assert.That(records.Collection.Id, Is.Not.EqualTo(first));
			Assert.That((await records.GetAsync()).Select(x => x.Id), Is.EqualTo(new[] { "b" }));
			var now = await client.GetOrCreateCollectionClientAsync(name);
			Assert.That(now.Collection.Id, Is.EqualTo(records.Collection.Id));
		}
		finally
		{
			if (await client.CollectionExistsAsync(name))
			{
				await client.DeleteCollectionAsync(name);
			}
		}
	}
}
