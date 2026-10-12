using System.Net;
using ChromaDB.Client.Models;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

[TestFixture]
public class MetadataTests : ChromaTestsBase
{
	static readonly ReadOnlyMemory<float> Embedding1 = new([1f, 0f]);
	static readonly ReadOnlyMemory<float> Embedding2 = new([0f, 1f]);

	[Test]
	public async Task StringsStayStrings()
	{
		var client = await Init(BaseConfigurationOptions.WithMetadataValues(ChromaMetadataValues.Exact));
		await client.AddAsync(new ChromaRecords(["a"]) { Embeddings = [Embedding1], Metadatas = [new Dictionary<string, object> { ["date"] = "2026-10-04", ["text"] = "t" }] });
		var metadata = (await client.GetAsync("a", include: ChromaGetInclude.Metadatas))!.Metadata!;
		Assert.That(metadata["date"], Is.EqualTo("2026-10-04"));
		Assert.That(metadata["text"], Is.EqualTo("t"));
	}

	// A whole double goes as 2.0, as the Python client writes it: Chroma keeps it a float, and a list of doubles in a
	// filter is not a list of ints and floats, which Chroma rejects.
	[Test]
	public async Task WholeDoublesStayFloats()
	{
		var client = await Init(BaseConfigurationOptions.WithMetadataValues(ChromaMetadataValues.Exact));
		await client.AddAsync(new ChromaRecords(["a", "b"])
		{
			Embeddings = [Embedding1, Embedding2],
			Metadatas = [new Dictionary<string, object> { ["d"] = 2.0, ["f"] = 3f, ["m"] = 4m }, new Dictionary<string, object> { ["d"] = 2.25 }],
		});
		var metadata = (await client.GetAsync("a", include: ChromaGetInclude.Metadatas))!.Metadata!;
		Assert.That(metadata["d"], Is.InstanceOf<double>().And.EqualTo(2.0));
		Assert.That(metadata["f"], Is.InstanceOf<double>().And.EqualTo(3.0));
		Assert.That(metadata["m"], Is.InstanceOf<double>().And.EqualTo(4.0));
		var found = await client.GetAsync(where: ChromaWhereOperator.In("d", 2.0, 2.25), include: ChromaGetInclude.None);
		Assert.That(found.Select(e => e.Id), Is.EquivalentTo(new[] { "a", "b" }));
		// Stored as an int, 2 would not be under 2.2 for Chroma.
		found = await client.GetAsync(where: ChromaWhereOperator.LessThan("d", 2.2), include: ChromaGetInclude.None);
		Assert.That(found.Select(e => e.Id), Is.EqualTo(new[] { "a" }));
	}

	// A null deletes the key in an update and in an upsert on every tested Chroma, and a record left without keys comes back with
	// Metadata null; AddAsync rejects a null before the request.
	[Test]
	public async Task NullValuesDeleteKeys()
	{
		var client = await Init(BaseConfigurationOptions.WithMetadataValues(ChromaMetadataValues.Exact));
		await client.AddAsync(new ChromaRecords(["a", "b"])
		{
			Embeddings = [Embedding1, Embedding2],
			Metadatas = [new Dictionary<string, object> { ["x"] = 1L, ["y"] = 2L }, new Dictionary<string, object> { ["x"] = 1L }],
		});
		await client.UpdateAsync(new ChromaRecords(["a"]) { Metadatas = [new Dictionary<string, object> { ["x"] = null! }] });
		await client.UpsertAsync(new ChromaRecords(["b"]) { Embeddings = [Embedding2], Metadatas = [new Dictionary<string, object> { ["x"] = null! }] });
		var records = (await client.GetAsync(["a", "b"], include: ChromaGetInclude.Metadatas)).ToDictionary(x => x.Id, x => x.Metadata);
		Assert.That(records["a"], Is.EquivalentTo(new Dictionary<string, object> { ["y"] = 2L }));
		// A record without keys comes back with an empty metadata.
		Assert.That(records["b"], Is.Not.Null.And.Empty);
		Assert.That(() => client.AddAsync(new ChromaRecords(["c"]) { Embeddings = [Embedding1], Metadatas = [new Dictionary<string, object> { ["x"] = null! }] }), Throws.ArgumentException);
	}

	[Test]
	public async Task DatesStayStringsByDefault()
	{
		var client = await Init(BaseConfigurationOptions);
		await client.AddAsync(new ChromaRecords(["a"]) { Embeddings = [Embedding1], Metadatas = [new Dictionary<string, object> { ["date"] = "2026-10-04" }] });
		Assert.That((await client.GetAsync("a", include: ChromaGetInclude.Metadatas))!.Metadata!["date"], Is.EqualTo("2026-10-04"));
	}

	[Test]
	public async Task DatesAreInferredWithInferred()
	{
		var client = await Init(BaseConfigurationOptions.WithMetadataValues(ChromaMetadataValues.Inferred));
		await client.AddAsync(new ChromaRecords(["a"]) { Embeddings = [Embedding1], Metadatas = [new Dictionary<string, object> { ["date"] = "2026-10-04" }] });
		Assert.That((await client.GetAsync("a", include: ChromaGetInclude.Metadatas))!.Metadata!["date"], Is.EqualTo(new DateTime(2026, 10, 4)));
	}

	[Test]
	public async Task ListsInMetadata()
	{
		var client = await Init(BaseConfigurationOptions.WithMetadataValues(ChromaMetadataValues.Exact));
		var records = new ChromaRecords(["a"])
		{
			Embeddings = [Embedding1],
			Metadatas = [new Dictionary<string, object> { ["texts"] = new List<string> { "x", "y" }, ["array"] = new[] { "z" }, ["ints"] = new List<int> { 1, 2 }, ["floats"] = new List<double> { 1.5, 2.25 }, ["bools"] = new List<bool> { true, false } }],
		};
		if (IsChroma0)
		{
			// The server would store the record without its lists: the client stops before sending it.
			var ex = Assert.ThrowsAsync<ChromaException>(() => client.AddAsync(records));
			Assert.That(ex!.StatusCode, Is.Null);
			Assert.That(await client.CountAsync(), Is.EqualTo(0));
			return;
		}
		if (!MetadataListsSupported)
		{
			var ex = Assert.ThrowsAsync<ChromaException>(() => client.AddAsync(records));
			Assert.That(ex!.StatusCode, Is.EqualTo((HttpStatusCode)422));
			return;
		}
		await client.AddAsync(records);
		var metadata = (await client.GetAsync("a", include: ChromaGetInclude.Metadatas))!.Metadata!;
		Assert.That(metadata["texts"], Is.EqualTo(new List<object> { "x", "y" }));
		Assert.That(metadata["array"], Is.EqualTo(new List<object> { "z" }));
		Assert.That(metadata["ints"], Is.EqualTo(new List<object> { 1L, 2L }));
		Assert.That(metadata["floats"], Is.EqualTo(new List<object> { 1.5, 2.25 }));
		Assert.That(metadata["bools"], Is.EqualTo(new List<object> { true, false }));
		// A whole double goes as 1.0: Chroma finds 1 only in a list of ints, and 1.0 only in a list of doubles.
		await client.AddAsync(new ChromaRecords(["b"]) { Embeddings = [Embedding2], Metadatas = [new Dictionary<string, object> { ["floats"] = new List<double> { 1.0, 2.5 } }] });
		var found = await client.GetAsync(where: ChromaWhereOperator.Contains("floats", 1.0), include: ChromaGetInclude.None);
		Assert.That(found.Select(e => e.Id), Is.EqualTo(new[] { "b" }));
	}

	[Test]
	public async Task UpsertAndUpdateListsInMetadata()
	{
		Assume.That(MetadataListsSupported, Is.True, "Chroma 1.4.1 and earlier do not store lists in metadata.");
		var client = await Init(BaseConfigurationOptions.WithMetadataValues(ChromaMetadataValues.Exact));
		await client.UpsertAsync(new ChromaRecords(["a"]) { Embeddings = [Embedding1], Metadatas = [new Dictionary<string, object> { ["texts"] = new[] { "x" } }] });
		await client.UpdateAsync(new ChromaRecords(["a"]) { Metadatas = [new Dictionary<string, object> { ["texts"] = new[] { "y", "z" } }] });
		Assert.That((await client.GetAsync("a", include: ChromaGetInclude.Metadatas))!.Metadata!["texts"], Is.EqualTo(new List<object> { "y", "z" }));
	}

	[Test]
	public async Task ContainsAndNotContains()
	{
		Assume.That(MetadataListsSupported, Is.True, "Chroma 1.4.1 and earlier do not store lists in metadata.");
		var client = await Init(BaseConfigurationOptions);
		await client.AddAsync(new ChromaRecords(["a", "b"])
		{
			Embeddings = [Embedding1, Embedding2],
			Metadatas = [new Dictionary<string, object> { ["texts"] = new[] { "x", "y" }, ["ints"] = new[] { 1, 2 } }, new Dictionary<string, object> { ["texts"] = new[] { "z" }, ["ints"] = new[] { 3 } }],
		});
		Assert.That((await client.GetAsync(where: ChromaWhereOperator.Contains("texts", "x"))).Select(x => x.Id), Is.EqualTo(new[] { "a" }));
		Assert.That((await client.GetAsync(where: ChromaWhereOperator.NotContains("texts", "x"))).Select(x => x.Id), Is.EqualTo(new[] { "b" }));
		Assert.That((await client.GetAsync(where: ChromaWhereOperator.Contains("ints", 3))).Select(x => x.Id), Is.EqualTo(new[] { "b" }));
		Assert.That((await client.QueryAsync(Embedding2, where: ChromaWhereOperator.Contains("texts", "y"))).Select(x => x.Id), Is.EqualTo(new[] { "a" }));
	}

	// The 0.x servers know only $gt, $gte, $lt, $lte, $ne, $eq, $in and $nin in metadata filters.
	[Test]
	public async Task ContainsOnChroma0Throws()
	{
		Assume.That(IsChroma0, Is.True, "Chroma 1.x has $contains in metadata filters.");
		var client = await Init(BaseConfigurationOptions);
		await client.AddAsync(new ChromaRecords(["a"]) { Embeddings = [Embedding1], Metadatas = [new Dictionary<string, object> { ["text"] = "x" }] });
		var ex = Assert.ThrowsAsync<ChromaException>(() => client.GetAsync(where: ChromaWhereOperator.Contains("text", "x")));
		Assert.That(ex!.Message, Does.Contain("$contains"));
	}

	// A null deletes a key and a null document with NullDocumentsDelete, from a client with a document copy key, the document, which
	// comes back null; a key the metadata does not have stays, and a new record gets the nulls too, which Chroma stores without the keys.
	[Test]
	public async Task DeletionsInAnUpsert()
	{
		var client = (await Init(BaseConfigurationOptions.WithMetadataValues(ChromaMetadataValues.Exact))).WithDocumentCopyKey("text");
		var metadata = new Dictionary<string, object> { ["k"] = 1L, ["keep"] = 2L };
		if (MetadataListsSupported)
		{
			metadata["tags"] = new[] { "x" };
		}
		await client.AddAsync(new ChromaRecords(["a"]) { Embeddings = [Embedding1], Metadatas = [metadata], Documents = ["doc"] });
		var upsert = new Dictionary<string, object> { ["k"] = null!, ["x"] = 3L };
		if (MetadataListsSupported)
		{
			upsert["tags"] = Array.Empty<string>();
		}
		await client.UpsertAsync(new ChromaRecords(["a", "b"])
		{
			Embeddings = [Embedding1, Embedding2],
			Metadatas = [upsert, new Dictionary<string, object> { ["k"] = null! }],
			Documents = [null!, null!],
			NullDocumentsDelete = true,
		});
		var records = (await client.GetAsync(["a", "b"], include: ChromaGetInclude.Metadatas | ChromaGetInclude.Documents)).ToDictionary(x => x.Id);
		Assert.That(records["a"].Metadata, Is.EquivalentTo(new Dictionary<string, object> { ["keep"] = 2L, ["x"] = 3L }));
		Assert.That(records["a"].Document, Is.Null);
		Assert.That(records["b"].Metadata, Is.Not.Null.And.Empty);
		Assert.That(records["b"].Document, Is.Null);
	}

	// Chroma Cloud counts a null against its quota of 32 keys: a record with 31 values, the null of a key it has and the null of one it
	// does not have goes, as the client reads it first and sends only the null of the key it has.
	[Test]
	public async Task NullsBeyondTheQuotaOfKeysOfChromaCloud()
	{
		Assume.That(ChromaCloud, Is.True, "Only Chroma Cloud has the quota of 32 keys.");
		var client = await Init(BaseConfigurationOptions.WithMetadataValues(ChromaMetadataValues.Exact));
		var values = Enumerable.Range(0, 31).ToDictionary(i => $"k{i}", i => (object)(long)i);
		await client.AddAsync(new ChromaRecords(["a"]) { Embeddings = [Embedding1], Metadatas = [new Dictionary<string, object>(values) { ["gone"] = 1L }] });
		await client.UpsertAsync(new ChromaRecords(["a"]) { Embeddings = [Embedding1], Metadatas = [new Dictionary<string, object>(values) { ["gone"] = null!, ["missing"] = null! }] });
		Assert.That((await client.GetAsync("a", include: ChromaGetInclude.Metadatas))!.Metadata, Is.EquivalentTo(values));
	}

	// Exactly 32 keys with the nulls, which the client sends without a read: Chroma Cloud takes them.
	[Test]
	public async Task NullsWithinTheQuotaOfKeysOfChromaCloud()
	{
		Assume.That(ChromaCloud, Is.True, "Only Chroma Cloud has the quota of 32 keys.");
		var client = await Init(BaseConfigurationOptions.WithMetadataValues(ChromaMetadataValues.Exact));
		var values = Enumerable.Range(0, 30).ToDictionary(i => $"k{i}", i => (object)(long)i);
		await client.AddAsync(new ChromaRecords(["a"]) { Embeddings = [Embedding1], Metadatas = [new Dictionary<string, object>(values) { ["gone"] = 1L }] });
		await client.UpsertAsync(new ChromaRecords(["a"]) { Embeddings = [Embedding1], Metadatas = [new Dictionary<string, object>(values) { ["gone"] = null!, ["missing"] = null! }] });
		Assert.That((await client.GetAsync("a", include: ChromaGetInclude.Metadatas))!.Metadata, Is.EquivalentTo(values));
	}

	// The copy of the document under the document copy key lets a where filter find the whole text. A document comes back as it was
	// written: empty, or null when it was deleted, which takes its copy along; so does a record that had no document, and a new one.
	[Test]
	public async Task DocumentCopy()
	{
		var client = (await Init(BaseConfigurationOptions.WithMetadataValues(ChromaMetadataValues.Exact))).WithDocumentCopyKey("text");
		await client.AddAsync(new ChromaRecords(["a", "b", "c"]) { Embeddings = [Embedding1, Embedding2, Embedding1], Documents = ["apple pie", "banana split", null!] });
		var found = await client.GetAsync(where: ChromaWhereOperator.Equal("text", "apple pie"), include: ChromaGetInclude.None);
		Assert.That(found.Select(x => x.Id), Is.EqualTo(new[] { "a" }));
		await client.UpsertAsync(new ChromaRecords(["a", "b", "c", "d"]) { Embeddings = [Embedding1, Embedding2, Embedding1, Embedding2], Documents = [null!, "", null!, null!], NullDocumentsDelete = true });
		var records = (await client.GetAsync(include: ChromaGetInclude.Documents)).ToDictionary(x => x.Id);
		Assert.That((records["a"].Document, records["b"].Document, records["c"].Document, records["d"].Document), Is.EqualTo(((string?)null, "", (string?)null, (string?)null)));
		var record = (await client.GetAsync("a", include: ChromaGetInclude.Metadatas | ChromaGetInclude.Documents))!;
		Assert.That(record.Metadata, Is.Not.Null.And.Empty);
		Assert.That(record.Document, Is.Null);
	}

	// With a document deletion key a deleted document comes back null, also of a record that had none and of a new one, and an empty one
	// comes back empty; the mark stays in the metadata, and goes away when the record gets a document again.
	[Test]
	public async Task DocumentDeletionKey()
	{
		var client = (await Init(BaseConfigurationOptions.WithMetadataValues(ChromaMetadataValues.Exact))).WithDocumentDeletionKey("deleted");
		await client.AddAsync(new ChromaRecords(["a", "b", "c"]) { Embeddings = [Embedding1, Embedding2, Embedding1], Documents = ["apple pie", "banana split", null!] });
		await client.UpsertAsync(new ChromaRecords(["a", "b", "c", "d"]) { Embeddings = [Embedding1, Embedding2, Embedding1, Embedding2], Documents = [null!, "", null!, null!], NullDocumentsDelete = true });
		var records = (await client.GetAsync(include: ChromaGetInclude.Documents)).ToDictionary(x => x.Id);
		Assert.That((records["a"].Document, records["b"].Document, records["c"].Document, records["d"].Document), Is.EqualTo(((string?)null, "", (string?)null, (string?)null)));
		var found = (await client.QueryAsync(new ChromaQuery([Embedding1]) { NResults = 4, Include = ChromaQueryInclude.Documents }))[0].ToDictionary(x => x.Id);
		Assert.That((found["a"].Document, found["b"].Document), Is.EqualTo(((string?)null, "")));
		Assert.That((await client.GetAsync("a", include: ChromaGetInclude.Metadatas))!.Metadata, Is.EquivalentTo(new Dictionary<string, object> { ["deleted"] = true }));
		await client.UpdateAsync(new ChromaRecords(["a"]) { Documents = ["again"] });
		var again = (await client.GetAsync("a", include: ChromaGetInclude.Metadatas | ChromaGetInclude.Documents))!;
		Assert.That(again.Document, Is.EqualTo("again"));
		Assert.That(again.Metadata, Is.Not.Null.And.Empty);
	}

	// With empty document deletions a deleted document comes back empty, also of a record that had none and of a new one, and the
	// records get no key.
	[Test]
	public async Task EmptyDocumentDeletions()
	{
		var client = (await Init(BaseConfigurationOptions.WithMetadataValues(ChromaMetadataValues.Exact))).WithEmptyDocumentDeletions();
		await client.AddAsync(new ChromaRecords(["a", "b"]) { Embeddings = [Embedding1, Embedding2], Documents = ["apple pie", null!] });
		await client.UpsertAsync(new ChromaRecords(["a", "b", "c"]) { Embeddings = [Embedding1, Embedding2, Embedding1], Documents = [null!, null!, null!], NullDocumentsDelete = true });
		var records = (await client.GetAsync(include: ChromaGetInclude.Documents | ChromaGetInclude.Metadatas)).ToDictionary(x => x.Id);
		Assert.That((records["a"].Document, records["b"].Document, records["c"].Document), Is.EqualTo(("", "", "")));
		Assert.That(records.Values.Select(x => x.Metadata), Is.All.Not.Null.And.All.Empty);
	}

	// The values of ChromaMetadataConvert come back as they were written, and a filter with a converted value finds the same instant
	// at another offset.
	[Test]
	public async Task ConvertedValues()
	{
		var client = await Init(BaseConfigurationOptions.WithMetadataValues(ChromaMetadataValues.Exact));
		var opened = new DateTimeOffset(2026, 10, 5, 13, 0, 0, TimeSpan.FromHours(2));
		var updated = new DateTime(2026, 10, 5, 11, 0, 0, DateTimeKind.Local);
		var metadata = new Dictionary<string, object>
		{
			["opened"] = ChromaMetadataConvert.ToMetadataValue(opened)!,
			["updated"] = ChromaMetadataConvert.ToMetadataValue(updated)!,
			["count"] = ChromaMetadataConvert.ToMetadataValue(3)!,
		};
		if (MetadataListsSupported)
		{
			metadata["days"] = ChromaMetadataConvert.ToMetadataValue(new[] { opened, opened.AddDays(1) })!;
		}
		await client.AddAsync(new ChromaRecords(["a"]) { Embeddings = [Embedding1], Metadatas = [metadata] });
		var read = (await client.GetAsync("a", include: ChromaGetInclude.Metadatas))!.Metadata!;
		Assert.That(ChromaMetadataConvert.FromMetadataValue(read["opened"], typeof(DateTimeOffset)), Is.EqualTo(opened));
		var readUpdated = (DateTime)ChromaMetadataConvert.FromMetadataValue(read["updated"], typeof(DateTime))!;
		Assert.That((readUpdated, readUpdated.Kind), Is.EqualTo((updated, DateTimeKind.Local)));
		Assert.That(ChromaMetadataConvert.FromMetadataValue(read["count"], typeof(int)), Is.EqualTo(3));
		var sameInstant = ChromaMetadataConvert.ToMetadataValue(opened.ToUniversalTime())!;
		Assert.That((await client.GetAsync(where: ChromaWhereOperator.Equal("opened", sameInstant), include: ChromaGetInclude.None)).Select(x => x.Id), Is.EqualTo(new[] { "a" }));
		if (MetadataListsSupported)
		{
			Assert.That(ChromaMetadataConvert.FromMetadataValue(read["days"], typeof(List<DateTimeOffset>)), Is.EqualTo(new List<DateTimeOffset> { opened, opened.AddDays(1) }));
			Assert.That((await client.GetAsync(where: ChromaWhereOperator.Contains("days", sameInstant), include: ChromaGetInclude.None)).Select(x => x.Id), Is.EqualTo(new[] { "a" }));
		}
	}

	// Chroma 1.5 keeps the lists of the records of a deleted collection or database, and gives them to the next records it stores, in
	// any collection: with deleteRecordsFirst the client deletes the records first.
	[Test]
	public async Task DeletedCollectionLeavesNoLists()
	{
		Assume.That(MetadataListsSupported, Is.True, "Chroma 1.4.1 and earlier do not store lists in metadata.");
		var deleted = await AddRecordsWithLists(BaseConfigurationOptions);
		await new ChromaClient(BaseConfigurationOptions, HttpClient).DeleteCollectionAsync(deleted.Collection.Name, deleteRecordsFirst: true);
		await AssertNoListsInNewRecords();
	}

	[Test]
	public async Task DeletedDatabaseLeavesNoLists()
	{
		Assume.That(MetadataListsSupported, Is.True, "Chroma 1.4.1 and earlier do not store lists in metadata.");
		Assume.That(OtherTenantsAndDatabasesTested, Is.True, "A server already running may not let the tests create or look up other tenants and databases.");
		var chroma = new ChromaClient(BaseConfigurationOptions, HttpClient);
		var database = $"database{Random.Shared.Next()}";
		await chroma.CreateDatabaseAsync(database);
		await AddRecordsWithLists(BaseConfigurationOptions.WithDatabase(database));
		await AddRecordsWithLists(BaseConfigurationOptions.WithDatabase(database));
		await chroma.DeleteDatabaseAsync(database, deleteRecordsFirst: true);
		await AssertNoListsInNewRecords();
	}

	// The records of every collection go first, also of the collections beyond a page of the list: 120 collections, each with a record.
	[Test]
	public async Task DeletedDatabaseDeletesTheRecordsOfEveryCollection()
	{
		Assume.That(IsChroma1, Is.True, "Chroma 0.x stores no lists, so the records are not deleted first.");
		Assume.That(OtherTenantsAndDatabasesTested, Is.True, "A server already running may not let the tests create or look up other tenants and databases.");
		var deletes = new DeletesByCollection { InnerHandler = new HttpClientHandler() };
		using var httpClient = new HttpClient(deletes);
		var chroma = new ChromaClient(BaseConfigurationOptions, httpClient);
		var database = $"database{Random.Shared.Next()}";
		await chroma.CreateDatabaseAsync(database);
		for (var i = 0; i < 120; i++)
		{
			var collection = await chroma.CreateCollectionAsync($"collection{i:000}", database: database);
			await new ChromaCollectionClient(collection, BaseConfigurationOptions.WithDatabase(database), httpClient).AddAsync(["a"], [Embedding1]);
		}
		deletes.Collections.Clear();
		await chroma.DeleteDatabaseAsync(database, deleteRecordsFirst: true);
		Assert.That(deletes.Collections, Has.Count.EqualTo(120));
	}

	// The collections whose records a request deletes.
	sealed class DeletesByCollection : DelegatingHandler
	{
		public HashSet<string> Collections { get; } = [];

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var segments = request.RequestUri!.AbsolutePath.Split('/');
			if (segments[^1] == "delete")
			{
				lock (Collections)
				{
					Collections.Add(segments[^2]);
				}
			}
			return base.SendAsync(request, cancellationToken);
		}
	}

	async Task<ChromaCollectionClient> AddRecordsWithLists(ChromaConfigurationOptions options)
	{
		var client = await Init(options);
		await client.AddAsync(new ChromaRecords(["a", "b"])
		{
			Embeddings = [Embedding1, Embedding2],
			Metadatas = [new Dictionary<string, object> { ["texts"] = new[] { "x", "y" } }, new Dictionary<string, object> { ["ints"] = new[] { 1, 2 } }],
		});
		return client;
	}

	async Task AssertNoListsInNewRecords()
	{
		var client = await Init(BaseConfigurationOptions);
		var ids = Enumerable.Range(0, 4).Select(i => $"new{i}").ToList();
		await client.AddAsync(new ChromaRecords(ids)
		{
			Embeddings = ids.Select(_ => Embedding1).ToList(),
			Metadatas = ids.Select(_ => (IReadOnlyDictionary<string, object>)new Dictionary<string, object> { ["k"] = 1 }).ToList(),
		});
		Assert.That((await client.GetAsync(include: ChromaGetInclude.Metadatas)).Select(x => string.Join(",", x.Metadata!.Keys)), Has.All.EqualTo("k"));
		Assert.That(await client.GetAsync(where: ChromaWhereOperator.Contains("texts", "x"), include: ChromaGetInclude.None), Is.Empty);
	}

	async Task<ChromaCollectionClient> Init(ChromaConfigurationOptions options)
	{
		var collection = await new ChromaClient(options, HttpClient).CreateCollectionAsync($"collection{Random.Shared.Next()}");
		return new ChromaCollectionClient(collection, options, HttpClient);
	}
}
