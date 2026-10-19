using ChromaDB.Client.Models;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class BatchSplittingTests : ChromaTestsBase
{
	// One record beyond the max_batch_size of the server: up to Chroma 1.0.13 a single request fails, in batches it works.
	[Test]
	public async Task AddAndDeleteBeyondTheMaxBatchSize()
	{
		Assume.That(PreFlightChecksSupported, Is.True, "Chroma 0.4.10 has no pre-flight-checks, so the client does not know its limit.");
		var options = TestMaxBatchSize is { } limit ? BaseConfigurationOptions.WithBatchSplitting(limit) : BaseConfigurationOptions.WithBatchSplitting();
		var client = new ChromaClient(options, HttpClient);
		var count = (await client.GetPreFlightChecksAsync()).MaxBatchSize + 1;
		var collectionClient = client.GetCollectionClient(await client.CreateCollectionAsync($"collection{Random.Shared.Next()}"));
		var ids = Enumerable.Range(0, count).Select(i => $"r{i}").ToList();

		await collectionClient.AddAsync(new ChromaRecords(ids) { Embeddings = Enumerable.Repeat(new ReadOnlyMemory<float>([0.5f, 0.5f]), count).ToList() });
		Assert.That(await collectionClient.CountAsync(), Is.EqualTo(count));
		await collectionClient.DeleteAsync(ids);
		Assert.That(await collectionClient.CountAsync(), Is.EqualTo(0));
	}

	// DeleteAllAsync deletes a page of the batch size at a time, until none is left.
	[Test]
	public async Task DeleteAllInPages()
	{
		var client = new ChromaClient(BaseConfigurationOptions.WithBatchSplitting(1000), HttpClient);
		var collectionClient = client.GetCollectionClient(await client.CreateCollectionAsync($"collection{Random.Shared.Next()}"));
		var ids = Enumerable.Range(0, 3000).Select(i => $"r{i}").ToList();
		await collectionClient.AddAsync(new ChromaRecords(ids) { Embeddings = Enumerable.Repeat(new ReadOnlyMemory<float>([0.5f, 0.5f]), 3000).ToList() });

		Assert.That(await collectionClient.DeleteAllAsync(), Is.EqualTo(3000));
		Assert.That(await collectionClient.CountAsync(), Is.EqualTo(0));
		Assert.That(await collectionClient.DeleteAllAsync(), Is.EqualTo(0));
	}

	// Chroma Cloud answers at most 300 records per read and rejects a larger limit: a fresh client with WithBatchSplitting(1000), on a
	// collection another client filled, learns it from the first get, reads all the records and deletes them all.
	[Test]
	public async Task QuotaOfTheReadsOfChromaCloud()
	{
		Assume.That(ChromaCloud, Is.True, "Only Chroma Cloud has the quota of 300 records per read.");
		var filler = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var collection = await filler.CreateCollectionAsync($"collection{Random.Shared.Next()}");
		var ids = Enumerable.Range(0, 350).Select(i => $"r{i}").ToList();
		await filler.GetCollectionClient(collection).AddAsync(new ChromaRecords(ids) { Embeddings = Enumerable.Repeat(new ReadOnlyMemory<float>([0.5f, 0.5f]), 350).ToList() });

		var reader = new ChromaClient(BaseConfigurationOptions.WithBatchSplitting(1000), NewHttpClient()).GetCollectionClient(collection);
		Assert.That((await reader.GetAsync(include: ChromaGetInclude.None)).Count, Is.EqualTo(350));
		var deleter = new ChromaClient(BaseConfigurationOptions.WithBatchSplitting(1000), NewHttpClient()).GetCollectionClient(collection);
		Assert.That(await deleter.DeleteAllAsync(), Is.EqualTo(350));
		Assert.That(await filler.GetCollectionClient(collection).CountAsync(), Is.EqualTo(0));
	}

	// Chroma Cloud rejects a get of more ids than its quota of 300, with a limit or without: a fresh client with WithBatchSplitting(1000)
	// learns it and reads the ids in batches.
	[TestCase(350)]
	[TestCase(null)]
	public async Task QuotaOfAReadByIdsOfChromaCloud(int? limit)
	{
		Assume.That(ChromaCloud, Is.True, "Only Chroma Cloud has the quota of 300 records per read.");
		var filler = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var collection = await filler.CreateCollectionAsync($"collection{Random.Shared.Next()}");
		var ids = Enumerable.Range(0, 350).Select(i => $"r{i}").ToList();
		await filler.GetCollectionClient(collection).AddAsync(new ChromaRecords(ids) { Embeddings = Enumerable.Repeat(new ReadOnlyMemory<float>([0.5f, 0.5f]), 350).ToList() });

		var reader = new ChromaClient(BaseConfigurationOptions.WithBatchSplitting(1000), NewHttpClient()).GetCollectionClient(collection);
		Assert.That((await reader.GetAsync(ids, limit: limit, include: ChromaGetInclude.None)).Select(x => x.Id), Is.EquivalentTo(ids));
	}

	// More ids than the batch size of the caller: its batches go beyond the quota of Chroma Cloud, and the client reads them again smaller.
	[Test]
	public async Task QuotaOfReadsByIdsInBatchesOfChromaCloud()
	{
		Assume.That(ChromaCloud, Is.True, "Only Chroma Cloud has the quota of 300 records per read.");
		var filler = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var collection = await filler.CreateCollectionAsync($"collection{Random.Shared.Next()}");
		var ids = Enumerable.Range(0, 1001).Select(i => $"r{i}").ToList();
		await filler.GetCollectionClient(collection).AddAsync(new ChromaRecords(ids) { Embeddings = Enumerable.Repeat(new ReadOnlyMemory<float>([0.5f, 0.5f]), 1001).ToList() });

		var reader = new ChromaClient(BaseConfigurationOptions.WithBatchSplitting(1000), NewHttpClient()).GetCollectionClient(collection);
		Assert.That((await reader.GetAsync(ids, include: ChromaGetInclude.None)).Select(x => x.Id), Is.EquivalentTo(ids));
	}

	// Get in pages of the batch size: all the records, a limit and an offset across pages, and ids beyond the batch size.
	[Test]
	public async Task GetBeyondTheBatchSize()
	{
		var client = new ChromaClient(BaseConfigurationOptions.WithBatchSplitting(3), HttpClient);
		var collectionClient = client.GetCollectionClient(await client.CreateCollectionAsync($"collection{Random.Shared.Next()}"));
		var ids = Enumerable.Range(0, 7).Select(i => $"r{i}").ToList();
		await collectionClient.AddAsync(new ChromaRecords(ids) { Embeddings = Enumerable.Repeat(new ReadOnlyMemory<float>([0.5f, 0.5f]), 7).ToList() });

		var all = await collectionClient.GetAsync();
		Assert.That(all.Select(x => x.Id), Is.EquivalentTo(ids));
		var page = await collectionClient.GetAsync(limit: 5, offset: 1);
		Assert.That(page.Select(x => x.Id), Is.EqualTo(all.Skip(1).Take(5).Select(x => x.Id)));
		Assert.That((await collectionClient.GetAsync(ids)).Select(x => x.Id), Is.EquivalentTo(ids));
	}

	// Chroma Cloud declares a max_batch_size of 1000 but takes 300 records per write: by default the client sends 300 at a time; with a
	// larger limit of the caller, the rejected batch and the rest go in batches of the quota that its message tells.
	[TestCase(null)]
	[TestCase(1000)]
	public async Task QuotaOfRecordsOfChromaCloud(int? maxBatchSize)
	{
		Assume.That(ChromaCloud, Is.True, "Only Chroma Cloud has the quota of 300 records.");
		var options = maxBatchSize is { } limit ? BaseConfigurationOptions.WithBatchSplitting(limit) : BaseConfigurationOptions;
		var client = new ChromaClient(options, HttpClient);
		var collectionClient = client.GetCollectionClient(await client.CreateCollectionAsync($"collection{Random.Shared.Next()}"));
		var ids = Enumerable.Range(0, 301).Select(i => $"r{i}").ToList();
		await collectionClient.AddAsync(new ChromaRecords(ids) { Embeddings = Enumerable.Repeat(new ReadOnlyMemory<float>([0.5f, 0.5f]), 301).ToList() });
		Assert.That(await collectionClient.CountAsync(), Is.EqualTo(301));
		Assert.That((await collectionClient.GetAsync()).Count, Is.EqualTo(301));
	}
}
