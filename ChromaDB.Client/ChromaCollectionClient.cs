using ChromaDB.Client.Common;
using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Models.Responses;
#if !NETSTANDARD2_0
using Microsoft.Extensions.AI;
#endif

namespace ChromaDB.Client;

/// <summary>
/// The client for the records of a collection, and for the operations on the collection itself, like <c>CountAsync</c>,
/// <c>ModifyAsync</c> and <c>ForkAsync</c>.
/// </summary>
public class ChromaCollectionClient
{
	// For a client made by name, the collection read last, which changes when the name has another id.
	private volatile ChromaCollection _collection;
	private readonly ChromaHttpClient _httpClient;
	private readonly bool _byName;
	private volatile bool _resolved;
	private readonly string _tenant;
	private readonly string _database;
	private readonly Uri _server;
	private readonly string? _documentCopyKey;
	private readonly string? _documentDeletionKey;
	private readonly bool _emptyDocumentDeletions;
#if !NETSTANDARD2_0
	private readonly IEmbeddingGenerator<string, Embedding<float>>? _embeddingGenerator;
#endif

	/// <summary>
	/// Creates a client for the records of the collection, which sends its requests with the options and the
	/// <c>HttpClient</c>. The requests go to the tenant and database of the collection, or else to those of the
	/// options.
	/// </summary>
	/// <param name="collection">The collection, as the server returned it.</param>
	/// <param name="options">The options of the client: the server, the credentials, the tenant and the database.</param>
	/// <param name="httpClient">The <c>HttpClient</c> that sends the requests; the client does not dispose it.</param>
	public ChromaCollectionClient(ChromaCollection collection, ChromaConfigurationOptions options, HttpClient httpClient)
		: this(collection, options, new ChromaHttpClient(httpClient, options))
	{ }

	// Shares the ChromaHttpClient of a ChromaClient, with what it keeps, like the version of the server.
	internal ChromaCollectionClient(ChromaCollection collection, ChromaConfigurationOptions options, ChromaHttpClient httpClient)
	{
		_collection = collection;
		_httpClient = httpClient;
		_server = options.Uri;
		// An empty tenant or database means the default, as in ChromaClient.
		_tenant = collection.Tenant is not null and not [] ? collection.Tenant
			: options.Tenant is not null and not [] ? options.Tenant
			: ClientConstants.DefaultTenantName;
		_database = collection.Database is not null and not [] ? collection.Database
			: options.Database is not null and not [] ? options.Database
			: ClientConstants.DefaultDatabaseName;
	}

	// The client of the collection with the name, whichever it is: ChromaClient.GetCollectionClient(name).
	internal ChromaCollectionClient(string name, ChromaConfigurationOptions options, ChromaHttpClient httpClient)
		: this(new ChromaCollection(name), options, httpClient)
	{
		_byName = true;
	}

	// The same client with another way of reading metadata values, a document copy key, a document deletion key or empty document
	// deletions; a client made by name starts from the collection read last, and reads it again by itself.
	private ChromaCollectionClient(ChromaCollectionClient client, ChromaHttpClient httpClient, string? documentCopyKey, string? documentDeletionKey, bool emptyDocumentDeletions)
	{
		_collection = client._collection;
		_httpClient = httpClient;
		_byName = client._byName;
		_resolved = client._resolved;
		_tenant = client._tenant;
		_database = client._database;
		_server = client._server;
		_documentCopyKey = documentCopyKey;
		_documentDeletionKey = documentDeletionKey;
		_emptyDocumentDeletions = emptyDocumentDeletions;
#if !NETSTANDARD2_0
		_embeddingGenerator = client._embeddingGenerator;
#endif
	}

#if !NETSTANDARD2_0
	// The same client with the embedding generator.
	private ChromaCollectionClient(ChromaCollectionClient client, IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
		: this(client, client._httpClient, client._documentCopyKey, client._documentDeletionKey, client._emptyDocumentDeletions)
	{
		_embeddingGenerator = embeddingGenerator;
	}
#endif

	/// <summary>
	/// Creates a client without getting the collection first: the requests on a collection need only its id, and the
	/// tenant and database of the options.
	/// </summary>
	/// <param name="collectionId">The id of the collection.</param>
	/// <param name="collectionName">The name of the collection.</param>
	/// <param name="options">The options of the client: the server, the credentials, the tenant and the database.</param>
	/// <param name="httpClient">The <c>HttpClient</c> that sends the requests; the client does not dispose it.</param>
	public ChromaCollectionClient(Guid collectionId, string collectionName, ChromaConfigurationOptions options, HttpClient httpClient)
		: this(new ChromaCollection(collectionName) { Id = collectionId }, options, httpClient)
	{ }

	/// <summary>
	/// For a mock in tests, made by a subclass or by a mocking library, as in the Azure SDKs: a client without a collection and
	/// without an <c>HttpClient</c>, whose members are all virtual. It sends no request: only the members it overrides work.
	/// </summary>
	protected ChromaCollectionClient()
	{
		_collection = null!;
		_httpClient = null!;
		_tenant = null!;
		_database = null!;
		_server = null!;
	}

	// The span and the duration of each operation on the collection.
	private Task<T> Operation<T>(string name, CancellationToken cancellationToken, Func<Task<T>> body)
		=> ChromaInstrumentation.Run(name, _collection.Name, $"{_tenant}|{_database}", _server, _byName ? () => ByName(body, cancellationToken) : body);

	private Task Operation(string name, CancellationToken cancellationToken, Func<Task> body)
		=> ChromaInstrumentation.Run(name, _collection.Name, $"{_tenant}|{_database}", _server, _byName ? () => ByName(async () => { await body(); return true; }, cancellationToken) : body);

	// A read changes nothing, so after an error that can pass it goes again, with ReadRetries: Chroma 1.5.9 answers 500 "Error finding id"
	// or "Error creating hnsw segment reader" to a get with embeddings while other clients write and delete the same records, and the
	// same get right after goes. Before each new attempt it waits a random time up to the base delay times 2 to the attempts that went
	// again before, and up to the largest delay: the full jitter of AWS, so that clients that failed together do not come back together.
	// An answer whose lists do not have one item for every record goes again too (AnswerChecks). A missing collection is not an error
	// that passes, and a write never goes again: one that failed may have gone. A read that went before, as the query of KD-24, counts
	// its attempts on from them, after the delay of the last one.
	private async Task<T> Read<T>(Func<Task<T>> read, CancellationToken cancellationToken, Func<ChromaException, bool>? handledElsewhere = null, int attemptsBefore = 0)
	{
		var retries = _httpClient.ReadRetries;
		if (attemptsBefore > 0)
		{
			await Task.Delay(retries.Delay(attemptsBefore - 1, Jitter()), cancellationToken);
		}
		for (var attempt = attemptsBefore + 1; ; attempt++)
		{
			try
			{
				return await read();
			}
			catch (ChromaException ex) when (attempt < retries.MaxAttempts && CanPass(ex) && handledElsewhere?.Invoke(ex) != true)
			{
				await Task.Delay(retries.Delay(attempt - 1, Jitter()), cancellationToken);
			}
		}
	}

	private static bool CanPass(ChromaException ex)
		=> (int?)ex.StatusCode is 429 or 500 or 502 or 503 or 504 && !ex.IsMissingCollection
			|| ex.StatusCode is null && ex.InnerException is HttpRequestException or IOException
			|| ex.UnevenAnswer;

	private static readonly Random JitterSource = new();

	private static double Jitter()
	{
		lock (JitterSource)
		{
			return JitterSource.NextDouble();
		}
	}

	// A client made by name reads the collection before its first request. When a request fails on the id it read before, it reads the
	// collection again: if the name has another id now, as when the collection was deleted and created again elsewhere, the operation
	// runs again, once, on that collection; otherwise the failure stands. The servers tell a collection gone in their own ways, like
	// Chroma 0.4 with "coroutine raised StopIteration", so the client compares the ids instead.
	private async Task<T> ByName<T>(Func<Task<T>> body, CancellationToken cancellationToken)
	{
		var resolved = _resolved;
		if (!resolved)
		{
			await Resolve(cancellationToken);
		}
		try
		{
			return await body();
		}
		catch (ChromaException ex) when (resolved && !ex.PartlyDone)
		{
			var id = _collection.Id;
			if (!await TryResolve(cancellationToken) || _collection.Id == id)
			{
				throw;
			}
		}
		return await body();
	}

	// Reads the collection again; when the name has no collection either, the failure of the operation stands.
	private async Task<bool> TryResolve(CancellationToken cancellationToken)
	{
		try
		{
			await Resolve(cancellationToken);
			return true;
		}
		catch (ChromaException)
		{
			return false;
		}
	}

	private async Task Resolve(CancellationToken cancellationToken)
	{
		var requestParams = new RequestQueryParams()
			.Insert("{collectionName}", _collection.Name)
			.Insert("{tenant}", _tenant)
			.Insert("{database}", _database);
		_collection = await _httpClient.WithSpace(await _httpClient.Get<ChromaCollection>(_httpClient.Routes.CollectionByName, requestParams, cancellationToken), cancellationToken);
		_resolved = true;
	}

	/// <summary>
	/// The collection the client works on. For a client made by name, with <c>ChromaClient.GetCollectionClient(name)</c>, the
	/// collection read last, or one with the name only before the first request.
	/// </summary>
	public virtual ChromaCollection Collection => _collection;

	/// <summary>
	/// The collection the client works on, as <c>Collection</c>. A client made by name reads it on the first call or request, and
	/// again when a request fails on the id it read before; the other clients send no request.
	/// </summary>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The collection.</returns>
	public virtual Task<ChromaCollection> GetCollectionAsync(CancellationToken cancellationToken = default)
		=> _byName ? Operation("get_collection", cancellationToken, () => Task.FromResult(_collection)) : Task.FromResult(_collection);

	/// <summary>
	/// A client of the same collection that copies each document into the metadata key, so that a <c>where</c> filter can compare
	/// the whole text, which <c>where_document</c> cannot; the copy replaces what the metadata has under the key. Chroma has no deletion
	/// of a document: for a null document with <c>ChromaRecords.NullDocumentsDelete</c> the client writes an empty one and deletes its
	/// copy, and it reads an empty document without its copy as null, so that a document comes back as it was written, empty or null.
	/// For that it reads the metadata with the documents, and leaves it out of the results that do not ask for it. It deletes a document
	/// without reading the record first, also of a record without a document, which a <c>where_document</c> filter that matches an empty
	/// text then sees as one with an empty document. Chroma Cloud takes a metadata value of at most 8,182 bytes: there a longer document
	/// goes without its copy, and an update or an upsert deletes the copy it had.
	/// A collection client chooses once how it deletes documents: the same call again on a client that made it changes nothing, and a call
	/// that would change the choice throws an <c>InvalidOperationException</c>.
	/// </summary>
	/// <param name="documentCopyKey">The metadata key of the copies.</param>
	/// <returns>The client of the same collection, with the copies.</returns>
	public virtual ChromaCollectionClient WithDocumentCopyKey(string documentCopyKey)
	{
		if (documentCopyKey is null)
		{
			throw new ArgumentNullException(nameof(documentCopyKey));
		}
		CheckWayToDeleteDocuments(documentCopyKey, null, false);
		return new(this, _httpClient, documentCopyKey, null, false);
	}

	/// <summary>
	/// A client of the same collection that marks a document deleted with <c>ChromaRecords.NullDocumentsDelete</c> under the metadata
	/// key, instead of copying every document. Chroma has no deletion of a document: for a null document with
	/// <c>NullDocumentsDelete</c> the client writes an empty one and <c>true</c> under the key, and any other document that an update or an
	/// upsert writes deletes the mark with a null, so no write reads the records first. It reads an empty document with the mark as null,
	/// and an empty one without it as empty; the mark stays in the metadata like any key. For that it reads the metadata with the
	/// documents, and leaves it out of the results that do not ask for it. It deletes a document also of a record without a document,
	/// which a <c>where_document</c> filter that matches an empty text then sees as one with an empty document. On Chroma Cloud the mark
	/// and its null count against the quota of 32 keys.
	/// A collection client chooses once how it deletes documents: the same call again on a client that made it changes nothing, and a call
	/// that would change the choice throws an <c>InvalidOperationException</c>.
	/// </summary>
	/// <param name="documentDeletionKey">The metadata key of the mark.</param>
	/// <returns>The client of the same collection, with the marks.</returns>
	public virtual ChromaCollectionClient WithDocumentDeletionKey(string documentDeletionKey)
	{
		if (documentDeletionKey is null)
		{
			throw new ArgumentNullException(nameof(documentDeletionKey));
		}
		CheckWayToDeleteDocuments(null, documentDeletionKey, false);
		return new(this, _httpClient, null, documentDeletionKey, false);
	}

	/// <summary>
	/// A client of the same collection that deletes a document with <c>ChromaRecords.NullDocumentsDelete</c> by writing an empty one,
	/// without a copy or a mark: Chroma has no deletion of a document, and the empty document reads back as an empty string, as the
	/// Python client of Chroma reads it. No write reads the records first. It deletes a document also of a record without a document,
	/// which a <c>where_document</c> filter that matches an empty text then sees as one with an empty document.
	/// A collection client chooses once how it deletes documents: the same call again on a client that made it changes nothing, and a call
	/// that would change the choice throws an <c>InvalidOperationException</c>.
	/// </summary>
	/// <returns>The client of the same collection, with the empty documents.</returns>
	public virtual ChromaCollectionClient WithEmptyDocumentDeletions()
	{
		CheckWayToDeleteDocuments(null, null, true);
		return new(this, _httpClient, null, null, true);
	}

	// A collection client chooses once how it deletes documents: the same call again changes nothing, while a call of another of
	// WithDocumentCopyKey, WithDocumentDeletionKey and WithEmptyDocumentDeletions, or of the same one with another key, throws.
	private void CheckWayToDeleteDocuments(string? copyKey, string? deletionKey, bool empty)
	{
		if (Way(_documentCopyKey, _documentDeletionKey, _emptyDocumentDeletions) is { } way
			&& (copyKey != _documentCopyKey || deletionKey != _documentDeletionKey || empty != _emptyDocumentDeletions))
		{
			throw new InvalidOperationException($"This collection client already deletes documents with {way}, and a collection client does not change that: call {Way(copyKey, deletionKey, empty)} on a new collection client of the collection.");
		}
	}

	// The way a client deletes documents, as the call that chose it; null for none.
	private static string? Way(string? copyKey, string? deletionKey, bool empty)
		=> copyKey is not null ? $"WithDocumentCopyKey(\"{copyKey}\")"
			: deletionKey is not null ? $"WithDocumentDeletionKey(\"{deletionKey}\")"
			: empty ? "WithEmptyDocumentDeletions()"
			: null;

	/// <summary>
	/// The BM25 index on the text of the source key, as <c>ChromaCollection.FindBm25Index</c> finds it in the schema of the collection;
	/// for the document copy key, also the one on the documents, which hold the whole text. Null when the collection has none.
	/// </summary>
	/// <param name="sourceKey">The key of the text: a metadata key, or <c>ChromaSearchKeys.Document</c>.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The index, or null.</returns>
	public virtual async Task<ChromaSparseVectorIndex?> FindBm25IndexAsync(string sourceKey, CancellationToken cancellationToken = default)
	{
		var collection = await GetCollectionAsync(cancellationToken);
		return collection.FindBm25Index(sourceKey) ?? (sourceKey == _documentCopyKey ? collection.FindBm25Index(ChromaSearchKeys.Document) : null);
	}

	/// <summary>
	/// A client of the same collection that reads metadata values the given way, whatever the client it comes from: same
	/// <c>HttpClient</c> and options.
	/// </summary>
	/// <param name="metadataValues">How the client reads metadata values.</param>
	/// <returns>The client of the same collection, reading metadata values that way.</returns>
	public virtual ChromaCollectionClient WithMetadataValues(ChromaMetadataValues metadataValues)
		=> new(this, _httpClient.WithMetadataValues(metadataValues), _documentCopyKey, _documentDeletionKey, _emptyDocumentDeletions);

#if !NETSTANDARD2_0
	/// <summary>
	/// A client of the same collection that computes the embeddings with the generator, as an embedding function does in the Python client
	/// of Chroma. <c>AddAsync</c>, <c>UpdateAsync</c> and <c>UpsertAsync</c> compute the embeddings of records given without them from
	/// their documents, and <c>QueryAsync</c> with a text and <c>QueryTextsAsync</c> compute those of the query texts. Records given with
	/// embeddings keep them. The builds for .NET 8 and .NET Framework have this method; the one for .NET Standard 2.0 does not.
	/// </summary>
	/// <param name="embeddingGenerator">The generator of the embeddings; the client does not dispose it.</param>
	/// <returns>The client of the same collection, with the generator.</returns>
	public virtual ChromaCollectionClient WithEmbeddingGenerator(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
	{
		if (embeddingGenerator is null)
		{
			throw new ArgumentNullException(nameof(embeddingGenerator));
		}
		return new(this, embeddingGenerator);
	}

	/// <summary>
	/// The generator of the embeddings of the client, from <c>WithEmbeddingGenerator</c>, or null for none.
	/// </summary>
	public virtual IEmbeddingGenerator<string, Embedding<float>>? EmbeddingGenerator => _embeddingGenerator;
#endif

	// The metadata key that tells a deleted document from an empty one: the document copy key, or the document deletion key.
	private string? DocumentReadKey => _documentCopyKey ?? _documentDeletionKey;

	// With a document copy key or a document deletion key, the documents are read with the metadata, which tells a deleted document
	// from an empty one.
	private DocumentReader ReadsDocuments(bool documents, bool metadata)
		=> !documents ? default
			: _documentCopyKey is { } copyKey ? new DocumentReader(copyKey, mark: false, metadata)
			: _documentDeletionKey is { } deletionKey ? new DocumentReader(deletionKey, mark: true, metadata)
			: default;

	/// <summary>
	/// Gets the record with the id, or null when the server returns none. Without <c>include</c>, the metadata and the
	/// document are included.
	/// </summary>
	/// <param name="id">The id of the record.</param>
	/// <param name="where">The filter on the metadata, or null for none.</param>
	/// <param name="whereDocument">The filter on the documents, or null for none.</param>
	/// <param name="include">What the results include, or null for the default.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The record, or null when the server returns none.</returns>
	public virtual async Task<ChromaCollectionEntry?> GetAsync(string id, ChromaWhereOperator? where = null, ChromaWhereDocumentOperator? whereDocument = null, ChromaGetInclude? include = null, CancellationToken cancellationToken = default)
		=> (await GetAsync([id], where: where, whereDocument: whereDocument, include: include, cancellationToken: cancellationToken)).FirstOrDefault();

	/// <summary>
	/// Gets the records selected by the ids and the filters, a page at a time with <c>limit</c> and <c>offset</c>. Without
	/// <c>include</c>, the metadatas and the documents are included. By default, unless <c>WithBatchSplitting(false)</c>, more
	/// records than the batch size are read in pages, ids beyond it in batches: Chroma Cloud answers at most 300 records, without
	/// an error.
	/// </summary>
	/// <param name="ids">The ids of the records, or null for all the records the filters select.</param>
	/// <param name="where">The filter on the metadata, or null for none.</param>
	/// <param name="whereDocument">The filter on the documents, or null for none.</param>
	/// <param name="limit">The most records to return, or null for all.</param>
	/// <param name="offset">How many records to skip first, or null for none.</param>
	/// <param name="include">What the results include, or null for the default.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The records.</returns>
	public virtual Task<IReadOnlyList<ChromaCollectionEntry>> GetAsync(IReadOnlyList<string>? ids = null, ChromaWhereOperator? where = null, ChromaWhereDocumentOperator? whereDocument = null, int? limit = null, int? offset = null, ChromaGetInclude? include = null, CancellationToken cancellationToken = default)
	{
		// As SearchAsync and ChromaQuery.Offset, whatever the number of ids, before the collection of a client made by name is read.
		if (limit < 0 || offset < 0)
		{
			throw new ArgumentOutOfRangeException(limit < 0 ? nameof(limit) : nameof(offset), "The limit and the offset of a get cannot be negative.");
		}
		CheckDocumentFilters(() => ChromaWhereOperator.Split(where, whereDocument, ids).WhereDocument?.ToRequestWhereDocument(), document: true, nameof(whereDocument));
		return WithRegexQuota(() => [(ChromaWhereOperator.Split(where, whereDocument, ids).WhereDocument?.ToRequestWhereDocument(), true)], Operation<IReadOnlyList<ChromaCollectionEntry>>("get", cancellationToken, async () =>
		{
			// Works around KD-13 (docs/COMPATIBILITY.md)
			// No get for a limit of 0, after a client made by name has read its collection, as with None: one that does not exist throws.
			if (limit == 0)
			{
				return [];
			}
			var split = ChromaWhereOperator.Split(where, whereDocument, ids);
			return await GetEntries(split.Ids, split.Where, split.WhereDocument, limit, offset, include, cancellationToken);
		}));
	}

	private async Task<List<ChromaCollectionEntry>> GetEntries(IReadOnlyList<string>? ids, ChromaWhereOperator? where, ChromaWhereDocumentOperator? whereDocument, int? limit, int? offset, ChromaGetInclude? include, CancellationToken cancellationToken)
	{
		if (where == ChromaWhereOperator.None)
		{
			return [];
		}
		// With batch splitting, on by default, more records than the batch size come in pages: Chroma Cloud answers at most 300 records, without
		// an error. The ids go in batches, each read whole, and the limit and the offset apply to all of them together; without ids
		// beyond the batch size, pages of the batch size follow the offset until the limit or a page that is not full.
		if (!_httpClient.BatchSplitting || await BatchSize(cancellationToken) is not { } size
			|| ids is { } few && few.Count <= size || ids is null && limit <= size)
		{
			// With ids, no more records than the ids: a limit beyond them, like 1000, goes over the quota of 300 of Chroma Cloud.
			var single = ids is { Count: > 0 } && limit > ids.Count ? ids.Count : limit;
			try
			{
				return await GetPage(ids, where, whereDocument, single, offset, include, cancellationToken);
			}
			// Chroma Cloud rejects a get beyond its quota, "'Limit value'" for the limit and, with ids and no limit, "'Number of records'"
			// for them: the client keeps the limit and reads in pages, or the ids in batches.
			catch (ChromaException ex) when (_httpClient.BatchSplitting && (LimitQuota(ex) ?? RecordsQuota(ex)) is { } quota && quota < (single ?? ids?.Count))
			{
				_httpClient.LearnRecordsLimit(quota);
				return await GetEntries(ids, where, whereDocument, limit, offset, include, cancellationToken);
			}
		}
		var entries = new List<ChromaCollectionEntry>();
		if (ids is not null)
		{
			// An id given twice would come back twice, from two batches: one request gives it once.
			var unique = ids.Distinct().ToList();
			for (var i = 0; i < unique.Count;)
			{
				var batch = unique.Skip(i).Take(size).ToList();
				try
				{
					entries.AddRange(await GetPage(batch, where, whereDocument, null, null, include, cancellationToken));
					i += batch.Count;
				}
				// A batch beyond the quota of Chroma Cloud: the client keeps the limit and reads that batch again, smaller; the batches read stay.
				catch (ChromaException ex) when ((LimitQuota(ex) ?? RecordsQuota(ex)) is { } quota && quota < batch.Count)
				{
					_httpClient.LearnRecordsLimit(quota);
					size = quota;
				}
			}
			return entries.Skip(offset ?? 0).Take(limit ?? int.MaxValue).ToList();
		}
		var start = offset ?? 0;
		while (limit is null || entries.Count < limit)
		{
			var take = limit is { } total ? Math.Min(size, total - entries.Count) : size;
			List<ChromaCollectionEntry> page;
			try
			{
				page = await GetPage(null, where, whereDocument, take, start, include, cancellationToken);
			}
			catch (ChromaException ex) when (LimitQuota(ex) is { } quota && quota < take)
			{
				_httpClient.LearnRecordsLimit(quota);
				size = quota;
				continue;
			}
			entries.AddRange(page);
			if (page.Count < take)
			{
				break;
			}
			start += take;
		}
		return entries;
	}

	/// <summary>
	/// Deletes every record of the collection, and returns how many it deleted. Chroma rejects a delete without ids or filters, so the
	/// client reads a page of ids from the start, as many as the batch size of the server and of Chroma Cloud, deletes them, and goes on
	/// until none is left. The collection stays. If the server returns the records of a page after their delete, the client throws a
	/// <c>ChromaException</c>.
	/// </summary>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>How many records it deleted.</returns>
	public virtual Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
		=> Operation("delete", cancellationToken, () => DeleteAllRecords(cancellationToken));

	// Deletes the records a page of ids at a time, a batch each, until none is left, and counts them: DeleteAllAsync, and ChromaClient
	// before it deletes the collection. A page that comes back the same was not deleted, and stops it. An error after a page that went
	// says so, and a collection client made by name does not run it again on a collection created again under the name.
	internal async Task<int> DeleteAllRecords(CancellationToken cancellationToken)
	{
		var requestParams = new RequestQueryParams()
			.Insert("{tenant}", _tenant)
			.Insert("{database}", _database)
			.Insert("{collection_id}", _collection.Id);
		var size = await BatchSize(cancellationToken);
		List<string> deleted = [];
		var count = 0;
		try
		{
			while (true)
			{
				List<string> ids;
				try
				{
					ids = (await GetPage(null, null, null, size, null, ChromaGetInclude.None, cancellationToken)).Select(entry => entry.Id).ToList();
				}
				catch (ChromaException ex) when (LimitQuota(ex) is { } quota && quota < size)
				{
					_httpClient.LearnRecordsLimit(quota);
					size = quota;
					continue;
				}
				if (ids.Count == 0)
				{
					return count;
				}
				if (ids.SequenceEqual(deleted))
				{
					throw new ChromaException($"The records were not deleted: the server returns them after their delete, like {ids[0]}. The collection stays with them.");
				}
				// Chroma Cloud rejects a delete beyond its quota of records, which can be lower than the one of the reads, before it deletes
				// any of them: the client keeps that limit and reads a smaller page.
				try
				{
					await _httpClient.Post(_httpClient.Routes.Collection + "/delete", new CollectionDeleteRequest() { Ids = ids }, requestParams, cancellationToken);
				}
				catch (ChromaException ex) when (RecordsQuota(ex) is { } quota && quota < ids.Count)
				{
					_httpClient.LearnRecordsLimit(quota);
					size = quota;
					continue;
				}
				count += ids.Count;
				deleted = ids;
			}
		}
		catch (ChromaException ex) when (count > 0)
		{
			throw new ChromaException($"{count} {(count == 1 ? "record was" : "records were")} deleted before the error: {ex.Message}", ex)
			{
				StatusCode = ex.StatusCode,
				ErrorType = ex.ErrorType,
				PartlyDone = true,
			};
		}
	}

	private async Task<List<ChromaCollectionEntry>> GetPage(IReadOnlyList<string>? ids, ChromaWhereOperator? where, ChromaWhereDocumentOperator? whereDocument, int? limit, int? offset, ChromaGetInclude? include, CancellationToken cancellationToken)
	{
		var requestParams = new RequestQueryParams()
			.Insert("{tenant}", _tenant)
			.Insert("{database}", _database)
			.Insert("{collection_id}", _collection.Id);
		var asked = include ?? ChromaGetInclude.Metadatas | ChromaGetInclude.Documents;
		var copy = ReadsDocuments(asked.HasFlag(ChromaGetInclude.Documents), asked.HasFlag(ChromaGetInclude.Metadatas));
		var request = new CollectionGetRequest()
		{
			Ids = ids,
			Where = ChromaWhereOperator.ToRequestWhere(where),
			WhereDocument = whereDocument?.ToRequestWhereDocument(),
			Limit = limit,
			Offset = offset,
			Include = (copy.ReadsMetadata ? asked | ChromaGetInclude.Metadatas : asked).ToInclude(),
		};
		var response = await Read(async () => (await _httpClient.Post<CollectionGetRequest, CollectionEntriesGetResponse>(_httpClient.Routes.Collection + "/get", request, requestParams, cancellationToken)).Checked("get"), cancellationToken);
		return response.Map(copy) ?? [];
	}

	/// <summary>
	/// Searches the <c>nResults</c> records nearest to the query embedding, among the ones with <c>ids</c> when given, as
	/// <c>ChromaQuery.Ids</c>. Without <c>include</c>, the metadatas, the documents and the distances are included.
	/// </summary>
	/// <param name="queryEmbeddings">The query embedding.</param>
	/// <param name="nResults">How many nearest records each query embedding returns.</param>
	/// <param name="where">The filter on the metadata, or null for none.</param>
	/// <param name="whereDocument">The filter on the documents, or null for none.</param>
	/// <param name="include">What the results include, or null for the default.</param>
	/// <param name="ids">The ids of the records to search among, or null for all.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The nearest records, nearest first.</returns>
	public virtual async Task<IReadOnlyList<ChromaCollectionQueryEntry>> QueryAsync(ReadOnlyMemory<float> queryEmbeddings, int nResults = 10, ChromaWhereOperator? where = null, ChromaWhereDocumentOperator? whereDocument = null, ChromaQueryInclude? include = null, IReadOnlyList<string>? ids = null, CancellationToken cancellationToken = default)
		=> (await QueryAsync([queryEmbeddings], nResults: nResults, where: where, whereDocument: whereDocument, include: include, ids: ids, cancellationToken: cancellationToken)).FirstOrDefault() ?? [];

	/// <summary>
	/// Searches the <c>nResults</c> records nearest to each query embedding, among the ones with <c>ids</c> when given, as
	/// <c>ChromaQuery.Ids</c>, and returns one list of results per embedding. Without <c>include</c>, the metadatas, the
	/// documents and the distances are included.
	/// </summary>
	/// <param name="queryEmbeddings">The query embeddings: the results are one list for each.</param>
	/// <param name="nResults">How many nearest records each query embedding returns.</param>
	/// <param name="where">The filter on the metadata, or null for none.</param>
	/// <param name="whereDocument">The filter on the documents, or null for none.</param>
	/// <param name="include">What the results include, or null for the default.</param>
	/// <param name="ids">The ids of the records to search among, or null for all.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>For each query embedding, its nearest records, nearest first.</returns>
	public virtual Task<IReadOnlyList<IReadOnlyList<ChromaCollectionQueryEntry>>> QueryAsync(IReadOnlyList<ReadOnlyMemory<float>> queryEmbeddings, int nResults = 10, ChromaWhereOperator? where = null, ChromaWhereDocumentOperator? whereDocument = null, ChromaQueryInclude? include = null, IReadOnlyList<string>? ids = null, CancellationToken cancellationToken = default)
		=> QueryAsync(new ChromaQuery(queryEmbeddings) { NResults = nResults, Where = where, WhereDocument = whereDocument, Include = include, Ids = ids }, cancellationToken);

#if !NETSTANDARD2_0
	/// <summary>
	/// Searches the <c>nResults</c> records nearest to the query text, with the embedding that the generator of
	/// <c>WithEmbeddingGenerator</c> computes from it, as <c>query_texts</c> in the Python client of Chroma. Without a generator it throws an
	/// <c>InvalidOperationException</c>. Without <c>include</c>, the metadatas, the documents and the distances are included.
	/// </summary>
	/// <param name="queryText">The query text.</param>
	/// <param name="nResults">How many nearest records the query returns.</param>
	/// <param name="where">The filter on the metadata, or null for none.</param>
	/// <param name="whereDocument">The filter on the documents, or null for none.</param>
	/// <param name="include">What the results include, or null for the default.</param>
	/// <param name="ids">The ids of the records to search among, or null for all.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The nearest records, nearest first.</returns>
	public virtual async Task<IReadOnlyList<ChromaCollectionQueryEntry>> QueryAsync(string queryText, int nResults = 10, ChromaWhereOperator? where = null, ChromaWhereDocumentOperator? whereDocument = null, ChromaQueryInclude? include = null, IReadOnlyList<string>? ids = null, CancellationToken cancellationToken = default)
	{
		if (queryText is null)
		{
			throw new ArgumentNullException(nameof(queryText));
		}
		return (await QueryTextsAsync([queryText], nResults: nResults, where: where, whereDocument: whereDocument, include: include, ids: ids, cancellationToken: cancellationToken)).FirstOrDefault() ?? [];
	}

	// Another name than QueryAsync: an overload with a list of texts would make QueryAsync([]) ambiguous, which compiles today.
	/// <summary>
	/// Searches the <c>nResults</c> records nearest to each query text, with the embeddings that the generator of
	/// <c>WithEmbeddingGenerator</c> computes from them, as <c>query_texts</c> in the Python client of Chroma, and returns one list of results
	/// per text. Without a generator it throws an <c>InvalidOperationException</c>. Without <c>include</c>, the metadatas, the documents and
	/// the distances are included.
	/// </summary>
	/// <param name="queryTexts">The query texts: the results are one list for each.</param>
	/// <param name="nResults">How many nearest records each query text returns.</param>
	/// <param name="where">The filter on the metadata, or null for none.</param>
	/// <param name="whereDocument">The filter on the documents, or null for none.</param>
	/// <param name="include">What the results include, or null for the default.</param>
	/// <param name="ids">The ids of the records to search among, or null for all.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>For each query text, its nearest records, nearest first.</returns>
	public virtual async Task<IReadOnlyList<IReadOnlyList<ChromaCollectionQueryEntry>>> QueryTextsAsync(IReadOnlyList<string> queryTexts, int nResults = 10, ChromaWhereOperator? where = null, ChromaWhereDocumentOperator? whereDocument = null, ChromaQueryInclude? include = null, IReadOnlyList<string>? ids = null, CancellationToken cancellationToken = default)
	{
		if (queryTexts is null)
		{
			throw new ArgumentNullException(nameof(queryTexts));
		}
		if (queryTexts.Any(text => text is null))
		{
			throw new ArgumentException("A query text cannot be null.", nameof(queryTexts));
		}
		var generator = _embeddingGenerator ?? throw new InvalidOperationException("The client has no embedding generator to compute the embeddings of the query texts: use WithEmbeddingGenerator, or query with embeddings.");
		var embeddings = await Embed(generator, queryTexts, cancellationToken);
		return await QueryAsync(new ChromaQuery(embeddings) { NResults = nResults, Where = where, WhereDocument = whereDocument, Include = include, Ids = ids }, cancellationToken);
	}
#endif

	/// <summary>
	/// Runs the query and returns one list of results per query embedding. The ids of the query without a record are left
	/// out, as Chroma Cloud does: Chroma 1.x fails on them, so the query goes again with the ids that have one. When the query
	/// has ids and the server searches outside them, as Chroma 0.x does, it throws a <c>ChromaException</c> instead of
	/// returning the results.
	/// </summary>
	/// <param name="query">The query: embeddings, number of results, filters, what to include and ids.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>For each query embedding, its nearest records, nearest first.</returns>
	public virtual Task<IReadOnlyList<IReadOnlyList<ChromaCollectionQueryEntry>>> QueryAsync(ChromaQuery query, CancellationToken cancellationToken = default)
	{
		CheckDocumentFilters(() => ChromaWhereOperator.Split(query.Where, query.WhereDocument, query.Ids).WhereDocument?.ToRequestWhereDocument(), document: true, nameof(query));
		return WithRegexQuota(() => [(ChromaWhereOperator.Split(query.Where, query.WhereDocument, query.Ids).WhereDocument?.ToRequestWhereDocument(), true)], Operation<IReadOnlyList<IReadOnlyList<ChromaCollectionQueryEntry>>>("query", cancellationToken, async () =>
		{
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", _tenant)
				.Insert("{database}", _database)
				.Insert("{collection_id}", _collection.Id);
			var (where, whereDocument, queryIds) = ChromaWhereOperator.Split(query.Where, query.WhereDocument, query.Ids);
			var asked = query.Include ?? ChromaQueryInclude.Metadatas | ChromaQueryInclude.Documents | ChromaQueryInclude.Distances;
			var copy = ReadsDocuments(asked.HasFlag(ChromaQueryInclude.Documents), asked.HasFlag(ChromaQueryInclude.Metadatas));
			Task<CollectionEntriesQueryResponse> Send(IReadOnlyList<string>? ids, Func<ChromaException, bool>? handledElsewhere = null, int attemptsBefore = 0)
				=> Read(async () => (await _httpClient.Post<CollectionQueryRequest, CollectionEntriesQueryResponse>(_httpClient.Routes.Collection + "/query", new CollectionQueryRequest()
				{
					QueryEmbeddings = query.QueryEmbeddings,
					NResults = query.NResults + query.Offset,
					Where = ChromaWhereOperator.ToRequestWhere(where),
					WhereDocument = whereDocument?.ToRequestWhereDocument(),
					Include = (copy.ReadsMetadata ? asked | ChromaQueryInclude.Metadatas : asked).ToInclude(),
					Ids = ids,
				}, requestParams, cancellationToken)).Checked(query.QueryEmbeddings.Count), cancellationToken, handledElsewhere, attemptsBefore);
			if (query.Offset > int.MaxValue - query.NResults)
			{
				throw new ArgumentOutOfRangeException(nameof(query), "The results and the offset of a query together go beyond the largest number of results.");
			}
			if (where == ChromaWhereOperator.None)
			{
				return query.QueryEmbeddings.Select(_ => (IReadOnlyList<ChromaCollectionQueryEntry>)[]).ToList();
			}
			if (query.ExpectedSpace is { } expected && _collection.Space is { } actual && actual != expected)
			{
				throw new InvalidOperationException(
					$"The collection has the space {ChromaSpaceNames.ToName(actual)}, not {ChromaSpaceNames.ToName(expected)}: its distances are not the ones the query expects.");
			}
			CollectionEntriesQueryResponse response;
			try
			{
				// With ids, an internal error goes at once to the workaround of KD-24, which tells an id without a record from an error that
				// can pass.
				response = await Send(queryIds, ex => queryIds is { Count: > 0 } && ex.ErrorType == "InternalError");
			}
			catch (ChromaException ex) when (queryIds is { Count: > 0 } && ex.ErrorType == "InternalError")
			{
				// Works around KD-24 (docs/COMPATIBILITY.md)
				// Chroma 1.x without filters answers 500 "Error finding id" when an id has no record, where Chroma Cloud leaves the id
				// out: the query goes again with the ids that have a record, and without any it has no results.
				var found = new HashSet<string>((await GetEntries(queryIds, null, null, null, null, ChromaGetInclude.None, cancellationToken)).Select(entry => entry.Id));
				if (queryIds.All(found.Contains))
				{
					// Every id has a record, so the error has another cause, which can pass: the query goes again, with ReadRetries, its
					// first attempt counted.
					if (_httpClient.ReadRetries.MaxAttempts == 1)
					{
						throw;
					}
					response = await Send(queryIds, attemptsBefore: 1);
				}
				else if (found.Count == 0)
				{
					return query.QueryEmbeddings.Select(_ => (IReadOnlyList<ChromaCollectionQueryEntry>)[]).ToList();
				}
				else
				{
					response = await Send(queryIds.Where(found.Contains).ToList());
				}
			}
			var result = response.Map(copy) ?? [];
			// Works around KD-36 (docs/COMPATIBILITY.md)
			// Chroma 0.x ignores the ids and searches all the records: a result outside the ids shows it. When all the results
			// are among the ids, they are also the nearest among them, so the answer is right on those servers too.
			if (queryIds is not null)
			{
				var ids = new HashSet<string>(queryIds);
				if (result.Any(entries => entries.Any(entry => !ids.Contains(entry.Id))))
				{
					throw new ChromaException("The server searched outside the ids of the query: it does not support them. Chroma 1.0.0 and later do.");
				}
			}
			return query.Offset == 0 ? result : result.Select(entries => (IReadOnlyList<ChromaCollectionQueryEntry>)entries.Skip(query.Offset).ToList()).ToList();
		}));
	}

	/// <summary>
	/// Adds the records with the ids, embeddings, metadatas and documents. Since Chroma 1.0.16 the server requires the
	/// embeddings: the client computes them from the documents with a generator from <c>WithEmbeddingGenerator</c>.
	/// </summary>
	/// <param name="ids">The ids of the records.</param>
	/// <param name="embeddings">The embeddings, one for each id, or null for none.</param>
	/// <param name="metadatas">The metadata, one for each id, or null for none. A null value throws an <c>ArgumentException</c>.</param>
	/// <param name="documents">The documents, one for each id, or null for none.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	public virtual Task AddAsync(IReadOnlyList<string> ids, IReadOnlyList<ReadOnlyMemory<float>>? embeddings = null, IReadOnlyList<IReadOnlyDictionary<string, object>>? metadatas = null, IReadOnlyList<string>? documents = null, CancellationToken cancellationToken = default)
		=> AddAsync(new ChromaRecords(ids) { Embeddings = embeddings, Metadatas = metadatas, Documents = documents }, cancellationToken);

	/// <summary>
	/// Adds the records; by default, unless <c>WithBatchSplitting(false)</c>, they go in batches of the <c>max_batch_size</c> of
	/// the server. Since Chroma 1.0.16 the server requires the embeddings: the client computes them from the documents with a generator
	/// from <c>WithEmbeddingGenerator</c>, and then every record needs a document. It computes the sparse
	/// vectors of the <c>chroma_bm25</c> indexes of the schema that have a source key, as the Python client of Chroma does.
	/// A null value in the metadata throws an <c>ArgumentException</c>: Chroma 0.x would drop the key, and 1.x rejects the request.
	/// </summary>
	/// <param name="records">The records: ids, and embeddings, metadatas, documents and URIs when given.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	public virtual Task AddAsync(ChromaRecords records, CancellationToken cancellationToken = default)
	{
		records = WithoutEmptyMetadatas(records);
		// Before the collection of a client made by name is read: records that cannot go send no request.
		ChromaRequestChecks.SameLengths(records, nameof(records));
		ChromaRequestChecks.MetadataValues(records.Metadatas, nameof(records));
		CheckDocumentsToEmbed(records);
		// The embeddings are computed once: a client made by name that runs the operation again on another collection keeps them.
		Task<ChromaRecords>? withEmbeddings = null;
		return Operation("add", cancellationToken, async () =>
		{
			var embedded = await (withEmbeddings ??= WithEmbeddings(records, cancellationToken));
			ChromaRequestChecks.NoNullValues(records.Metadatas, nameof(records));
			// A local copy: a collection client made by name may run the operation again on another collection, from the records given.
			var prepared = WithSparseVectors(WithDocumentCopies(embedded, update: false));
			await CheckListsInMetadata(prepared, cancellationToken);
			var base64 = prepared.Embeddings is not null && await _httpClient.SupportsBase64Embeddings(cancellationToken);
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", _tenant)
				.Insert("{database}", _database)
				.Insert("{collection_id}", _collection.Id);
			await InBatches(prepared, async batch =>
			{
				var request = new CollectionAddRequest()
				{
					Ids = batch.Ids,
					Embeddings = batch.Embeddings is { } embeddings ? new ChromaEmbeddings(embeddings, base64) : null,
					Metadatas = MetadatasOrNone(batch.Metadatas),
					Documents = batch.Documents,
					Uris = batch.Uris,
				};
				await _httpClient.Post(_httpClient.Routes.Collection + "/add", request, requestParams, cancellationToken);
			}, cancellationToken);
		});
	}

	/// <summary>
	/// Updates the embeddings, metadatas and documents of the records with the ids. With a generator from
	/// <c>WithEmbeddingGenerator</c>, documents given without embeddings get theirs.
	/// </summary>
	/// <param name="ids">The ids of the records.</param>
	/// <param name="embeddings">The embeddings, one for each id, or null for none.</param>
	/// <param name="metadatas">The metadata, one for each id, or null for none. A null value deletes the key on every tested Chroma:
	/// write it <c>null!</c>, as the type does not allow it.</param>
	/// <param name="documents">The documents, one for each id, or null for none.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	public virtual Task UpdateAsync(IReadOnlyList<string> ids, IReadOnlyList<ReadOnlyMemory<float>>? embeddings = null, IReadOnlyList<IReadOnlyDictionary<string, object>>? metadatas = null, IReadOnlyList<string>? documents = null, CancellationToken cancellationToken = default)
		=> UpdateAsync(new ChromaRecords(ids) { Embeddings = embeddings, Metadatas = metadatas, Documents = documents }, cancellationToken);

	/// <summary>
	/// Updates the records with the ids; by default, unless <c>WithBatchSplitting(false)</c>, they go in batches of the
	/// <c>max_batch_size</c> of the server. The client computes the sparse vectors of the <c>chroma_bm25</c> indexes of the schema
	/// that have a source key, as the Python client of Chroma does. A null value in the metadata, written <c>null!</c> as the type does
	/// not allow it, or an empty list deletes the key and the sparse vectors computed from its text. A null document keeps the stored one
	/// on Chroma 1.x and Chroma Cloud, and the 0.x servers delete it; with <c>ChromaRecords.NullDocumentsDelete</c> a null document
	/// deletes the document on every server, which needs <c>WithDocumentCopyKey</c>,
	/// <c>WithDocumentDeletionKey</c> or <c>WithEmptyDocumentDeletions</c>: without one of them, a null document throws an
	/// <c>ArgumentException</c> before any request. A null goes to the record whether it has the key or not, so
	/// two writes of the same record at the same time leave the record of one of them; the keys the metadata does not have stay. On Chroma
	/// Cloud, a write with nulls that would take the metadata of a record beyond the quota of 32 keys throws an <c>ArgumentException</c>
	/// before it goes, without a read; with <c>ChromaConfigurationOptions.ReadsBeyondKeyQuota</c> the client reads the record first and
	/// sends only the nulls of its keys, and a write that lands between that read and the write can keep what it wrote there. A record
	/// with more than 32 values and no null goes, and Chroma Cloud rejects it.
	/// With a generator from <c>WithEmbeddingGenerator</c>, records given with documents and without embeddings get the embeddings of
	/// their documents, and then every record needs a document.
	/// </summary>
	/// <param name="records">The records: ids, and embeddings, metadatas, documents and URIs when given.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	public virtual Task UpdateAsync(ChromaRecords records, CancellationToken cancellationToken = default)
	{
		records = WithoutEmptyMetadatas(records);
		// Before the collection of a client made by name is read: records that cannot go send no request.
		ChromaRequestChecks.SameLengths(records, nameof(records));
		ChromaRequestChecks.MetadataValues(records.Metadatas, nameof(records));
		CheckDocumentDeletions(records);
		CheckDocumentsToEmbed(records);
		// The embeddings are computed once: a client made by name that runs the operation again on another collection keeps them.
		Task<ChromaRecords>? withEmbeddings = null;
		return Operation("update", cancellationToken, async () =>
		{
			var embedded = await (withEmbeddings ??= WithEmbeddings(records, cancellationToken));
			// A local copy: a collection client made by name may run the operation again on another collection, from the records given.
			// The sparse vectors before the deletions, which count every key that goes against the quota of Chroma Cloud.
			var prepared = await WithDeletions(WithSparseVectors(WithDocumentMarks(WithDocumentCopies(embedded, update: true))), cancellationToken);
			await CheckListsInMetadata(prepared, cancellationToken);
			var base64 = prepared.Embeddings is not null && await _httpClient.SupportsBase64Embeddings(cancellationToken);
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", _tenant)
				.Insert("{database}", _database)
				.Insert("{collection_id}", _collection.Id);
			if (await WritesByStrategy(prepared, cancellationToken))
			{
				await WriteByStrategy(prepared, upsert: false, base64, requestParams, cancellationToken);
				return;
			}
			await InBatches(prepared, async batch =>
			{
				var request = new CollectionUpdateRequest()
				{
					Ids = batch.Ids,
					Embeddings = batch.Embeddings is { } embeddings ? new ChromaEmbeddings(embeddings, base64) : null,
					Metadatas = MetadatasOrNone(batch.Metadatas),
					Documents = batch.Documents,
					Uris = batch.Uris,
				};
				await _httpClient.Post(_httpClient.Routes.Collection + "/update", request, requestParams, cancellationToken);
			}, cancellationToken);
		});
	}

	/// <summary>
	/// Adds the records with the ids, or updates the ones that already exist. Since Chroma 1.0.16 the server requires
	/// the embeddings: the client computes them from the documents with a generator from <c>WithEmbeddingGenerator</c>.
	/// </summary>
	/// <param name="ids">The ids of the records.</param>
	/// <param name="embeddings">The embeddings, one for each id, or null for none.</param>
	/// <param name="metadatas">The metadata, one for each id, or null for none. A null value deletes the key on every tested Chroma:
	/// write it <c>null!</c>, as the type does not allow it.</param>
	/// <param name="documents">The documents, one for each id, or null for none.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	public virtual Task UpsertAsync(IReadOnlyList<string> ids, IReadOnlyList<ReadOnlyMemory<float>>? embeddings = null, IReadOnlyList<IReadOnlyDictionary<string, object>>? metadatas = null, IReadOnlyList<string>? documents = null, CancellationToken cancellationToken = default)
		=> UpsertAsync(new ChromaRecords(ids) { Embeddings = embeddings, Metadatas = metadatas, Documents = documents }, cancellationToken);

	/// <summary>
	/// Adds the records, or updates the ones that already exist; by default, unless <c>WithBatchSplitting(false)</c>, they go in
	/// batches of the <c>max_batch_size</c> of the server. Since Chroma 1.0.16 the server requires the embeddings: the client does
	/// not compute them without a generator. It computes the sparse vectors of the <c>chroma_bm25</c> indexes of the schema that have a source key, as
	/// the Python client of Chroma does. A null value in the metadata, written <c>null!</c> as the type does not allow it, or an empty
	/// list deletes the key of a record that exists and the sparse vectors computed from its text, and a new record is added without
	/// it. A null document keeps the stored one on Chroma 1.x and Chroma Cloud, and the 0.x servers delete it; with
	/// <c>ChromaRecords.NullDocumentsDelete</c> a null document deletes the document on every server, which needs
	/// <c>WithDocumentCopyKey</c>, <c>WithDocumentDeletionKey</c> or <c>WithEmptyDocumentDeletions</c>: without one of them, a null
	/// document throws an <c>ArgumentException</c> before any request. A null goes to the record whether it has the key
	/// or not, so two writes of the same record at the same time leave the record of one of them; the keys the metadata does not have
	/// stay. On Chroma Cloud, a write with nulls that would take the metadata of a record beyond the quota of 32 keys throws an
	/// <c>ArgumentException</c> before it goes, without a read; with <c>ChromaConfigurationOptions.ReadsBeyondKeyQuota</c> the client reads
	/// the record first and sends only the nulls of its keys, and a write that lands between that read and the write can keep what it
	/// wrote there. A record with more than 32 values and no null goes, and Chroma Cloud rejects it.
	/// With a generator from <c>WithEmbeddingGenerator</c>, records given with documents and without embeddings get the embeddings of
	/// their documents, and then every record needs a document.
	/// </summary>
	/// <param name="records">The records: ids, and embeddings, metadatas, documents and URIs when given.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	public virtual Task UpsertAsync(ChromaRecords records, CancellationToken cancellationToken = default)
	{
		records = WithoutEmptyMetadatas(records);
		// Before the collection of a client made by name is read: records that cannot go send no request.
		ChromaRequestChecks.SameLengths(records, nameof(records));
		ChromaRequestChecks.MetadataValues(records.Metadatas, nameof(records));
		CheckDocumentDeletions(records);
		CheckDocumentsToEmbed(records);
		// The embeddings are computed once: a client made by name that runs the operation again on another collection keeps them.
		Task<ChromaRecords>? withEmbeddings = null;
		return Operation("upsert", cancellationToken, async () =>
		{
			var embedded = await (withEmbeddings ??= WithEmbeddings(records, cancellationToken));
			// A local copy: a collection client made by name may run the operation again on another collection, from the records given.
			// The sparse vectors before the deletions, which count every key that goes against the quota of Chroma Cloud.
			var prepared = await WithDeletions(WithSparseVectors(WithDocumentMarks(WithDocumentCopies(embedded, update: true))), cancellationToken);
			await CheckListsInMetadata(prepared, cancellationToken);
			var base64 = prepared.Embeddings is not null && await _httpClient.SupportsBase64Embeddings(cancellationToken);
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", _tenant)
				.Insert("{database}", _database)
				.Insert("{collection_id}", _collection.Id);
			if (await WritesByStrategy(prepared, cancellationToken))
			{
				await WriteByStrategy(prepared, upsert: true, base64, requestParams, cancellationToken);
				return;
			}
			await InBatches(prepared, async batch =>
			{
				var request = new CollectionUpsertRequest()
				{
					Ids = batch.Ids,
					Embeddings = batch.Embeddings is { } embeddings ? new ChromaEmbeddings(embeddings, base64) : null,
					Metadatas = MetadatasOrNone(batch.Metadatas),
					Documents = batch.Documents,
					Uris = batch.Uris,
				};
				await _httpClient.Post(_httpClient.Routes.Collection + "/upsert", request, requestParams, cancellationToken);
			}, cancellationToken);
		});
	}

	// With a generator, records without embeddings get them from their documents, so each needs a document: a null one, which keeps the
	// stored document or deletes it, has no text. Before any request, as the other checks of the records.
	private void CheckDocumentsToEmbed(ChromaRecords records)
	{
#if !NETSTANDARD2_0
		if (_embeddingGenerator is not null && records.Embeddings is null && records.Documents is { } documents && documents.Any(document => document is null))
		{
			throw new ArgumentException("The client computes the embeddings from the documents, and a record has a null document: give every record a document, or give the embeddings.", nameof(records));
		}
#endif
	}

	// As an embedding function in the Python client of Chroma: records given without embeddings get the ones the generator computes from
	// their documents. Without a generator, or without documents, the records go as they are.
#if !NETSTANDARD2_0
	private async Task<ChromaRecords> WithEmbeddings(ChromaRecords records, CancellationToken cancellationToken)
	{
		if (_embeddingGenerator is not { } generator || records.Embeddings is not null || records.Documents is not { } documents)
		{
			return records;
		}
		return new ChromaRecords(records.Ids)
		{
			// CheckDocumentsToEmbed made sure that none is null.
			Embeddings = await Embed(generator, documents.Select(document => document!).ToList(), cancellationToken),
			Metadatas = records.Metadatas,
			Documents = records.Documents,
			Uris = records.Uris,
			NullDocumentsDelete = records.NullDocumentsDelete,
		};
	}

	// One embedding for each text, in the order of the texts.
	private static async Task<IReadOnlyList<ReadOnlyMemory<float>>> Embed(IEmbeddingGenerator<string, Embedding<float>> generator, IReadOnlyList<string> texts, CancellationToken cancellationToken)
	{
		if (texts.Count == 0)
		{
			return [];
		}
		var embeddings = await generator.GenerateAsync(texts, null, cancellationToken);
		if (embeddings.Count != texts.Count)
		{
			throw new InvalidOperationException($"The embedding generator returned {embeddings.Count} embeddings for {texts.Count} texts.");
		}
		return embeddings.Select(embedding => embedding.Vector).ToList();
	}
#else
	private static Task<ChromaRecords> WithEmbeddings(ChromaRecords records, CancellationToken cancellationToken)
		=> Task.FromResult(records);
#endif

	// The copy of each document under the document copy key, when the server takes it: Chroma Cloud takes a metadata value of at most
	// 8,182 bytes. In an update or an upsert, a document without its copy, as one too long or one deleted, deletes the stored copy with
	// a null, which WithDeletions sends along; a null document that stays keeps its copy.
	// An empty metadata goes as no metadata, null: in add, update and upsert, also in batches with other metadatas, every tested Chroma,
	// 0.4.10 to 1.5.9 and Chroma Cloud, treats the two the same. A new record has no metadata, and a stored one keeps its own.
	private static ChromaRecords WithoutEmptyMetadatas(ChromaRecords records)
		=> records.Metadatas is not { } metadatas || !metadatas.Any(metadata => metadata is { Count: 0 }) ? records : new ChromaRecords(records.Ids)
		{
			Embeddings = records.Embeddings,
			Metadatas = metadatas.Select(metadata => metadata is { Count: 0 } ? null : metadata).ToList(),
			Documents = records.Documents,
			Uris = records.Uris,
			NullDocumentsDelete = records.NullDocumentsDelete,
		};

	private ChromaRecords WithDocumentCopies(ChromaRecords records, bool update)
	{
		if (_documentCopyKey is not { } key || records.Documents is not { } documents)
		{
			return records;
		}
		var metadatas = new List<IReadOnlyDictionary<string, object>?>(records.Ids.Count);
		for (var i = 0; i < records.Ids.Count; i++)
		{
			var given = records.Metadatas?[i];
			var document = documents[i];
			var fits = document is not null && (!_httpClient.IsChromaCloud || System.Text.Encoding.UTF8.GetByteCount(document) <= ChromaCloudQuotas.MaxMetadataValueBytes);
			if (!fits && !(update && (document is not null || records.NullDocumentsDelete)))
			{
				metadatas.Add(given);
				continue;
			}
			var metadata = given?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? [];
			metadata[key] = fits ? document! : null!;
			metadatas.Add(metadata);
		}
		return new ChromaRecords(records.Ids)
		{
			Embeddings = records.Embeddings,
			Metadatas = metadatas.Any(metadata => metadata is not null) ? metadatas : null,
			Documents = records.Documents,
			Uris = records.Uris,
			NullDocumentsDelete = records.NullDocumentsDelete,
		};
	}

	// The mark under the document deletion key, in an update or an upsert: true for a document deleted with NullDocumentsDelete, which
	// goes empty, and the null that deletes it for any other document, so that no write reads the records first. A null document that
	// stays keeps its mark.
	private ChromaRecords WithDocumentMarks(ChromaRecords records)
	{
		if (_documentDeletionKey is not { } key || records.Documents is not { } documents)
		{
			return records;
		}
		var metadatas = new List<IReadOnlyDictionary<string, object>?>(records.Ids.Count);
		for (var i = 0; i < records.Ids.Count; i++)
		{
			var given = records.Metadatas?[i];
			if (documents[i] is null && !records.NullDocumentsDelete)
			{
				metadatas.Add(given);
				continue;
			}
			var metadata = given?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? [];
			metadata[key] = documents[i] is null ? true : null!;
			metadatas.Add(metadata);
		}
		return new ChromaRecords(records.Ids)
		{
			Embeddings = records.Embeddings,
			Metadatas = metadatas.Any(metadata => metadata is not null) ? metadatas : null,
			Documents = records.Documents,
			Uris = records.Uris,
			NullDocumentsDelete = records.NullDocumentsDelete,
		};
	}

	// Chroma has no deletion of a document: the client writes an empty one, which only the copy under a document copy key or the mark
	// under a document deletion key tells from an empty text, and which empty document deletions read back as empty. Without one of the
	// three ways, a null document with NullDocumentsDelete throws, before any request, with the three.
	private void CheckDocumentDeletions(ChromaRecords records)
	{
		if (DocumentReadKey is null && !_emptyDocumentDeletions && records.NullDocumentsDelete && records.Documents?.Any(document => document is null) == true)
		{
			throw new ArgumentException("NullDocumentsDelete needs a way to tell a deleted document from an empty one: Chroma cannot delete a document, so the client writes an empty one. Choose one on the collection client: WithDocumentCopyKey(key) copies each document into a metadata key and reads an empty document without its copy as null; WithDocumentDeletionKey(key) writes only a mark under a metadata key and reads an empty document with the mark as null; WithEmptyDocumentDeletions() writes an empty document that reads back as an empty string.", nameof(records));
		}
	}

	// In UpdateAsync and UpsertAsync: a null value or an empty list deletes the key, a null document deletes the document with
	// NullDocumentsDelete, and a text that goes takes the sparse vectors computed from it along. A null goes to the record whether it has
	// the key or not: every tested Chroma takes it, also in an upsert that creates the record, and leaves the record without the key. So
	// the write does not depend on a read that another writer can change before it goes. Chroma has no deletion of a document: the client
	// writes an empty one to every record whose document goes, without a read, as only a client with a document copy key, a document
	// deletion key or empty document deletions gets here (CheckDocumentDeletions); with a copy or a mark it reads that empty document as
	// none. Chroma Cloud counts a null against its quota of keys, and a sparse vector too: when the values, the sparse vectors that
	// WithSparseVectors computed and the nulls would take the metadata of a record beyond it, the write throws before it goes, or with
	// ReadsBeyondKeyQuota the client reads that record first and sends the nulls only to the keys it has.
	private async Task<ChromaRecords> WithDeletions(ChromaRecords records, CancellationToken cancellationToken)
	{
		var count = records.Ids.Count;
		var indexes = _collection.SparseVectorIndexes.Where(index => index.SourceKey is not null && index.EmbeddingFunction is not null).ToList();
		var deleted = new List<string>?[count];
		var documentDeleted = new bool[count];
		var keysRead = new bool[count];
		for (var i = 0; i < count; i++)
		{
			var metadata = records.Metadatas?[i];
			List<string>? keys = null;
			foreach (var pair in metadata ?? Enumerable.Empty<KeyValuePair<string, object>>())
			{
				if (pair.Value is null || ChromaRequestChecks.IsList(pair.Value) && ChromaRequestChecks.IsEmpty(pair.Value))
				{
					(keys ??= []).Add(pair.Key);
				}
			}
			documentDeleted[i] = records.NullDocumentsDelete && records.Documents is { } givenDocuments && givenDocuments[i] is null;
			foreach (var index in indexes)
			{
				var textDeleted = index.SourceKey == ChromaSearchKeys.Document ? documentDeleted[i] : keys?.Contains(index.SourceKey!) == true;
				// A vector the caller gives stays, and the null of one is in the keys already: each key counts once against the quota.
				if (textDeleted && metadata?.ContainsKey(index.Key) != true)
				{
					(keys ??= []).Add(index.Key);
				}
			}
			deleted[i] = keys;
			var keyCount = keys is null ? 0 : (metadata?.Count(pair => !keys.Contains(pair.Key)) ?? 0) + keys.Count;
			if (_httpClient.IsChromaCloud && keyCount > ChromaCloudQuotas.MaxMetadataKeys)
			{
				if (!_httpClient.ReadsBeyondKeyQuota)
				{
					throw new ArgumentException($"Record \"{records.Ids[i]}\" would take {keyCount} metadata keys to Chroma Cloud, beyond its quota of {ChromaCloudQuotas.MaxMetadataKeys}: its values and nulls, with the keys that the client adds, like the copy of the document, the mark of a deleted document and the BM25 sparse vectors. Send fewer keys or fewer nulls, or use ChromaConfigurationOptions.WithReadsBeyondKeyQuota() in the options of the ChromaClient: it reads the record first and sends only the nulls of the keys it has, but then two writes of the same record at the same time can mix.", nameof(records));
				}
				keysRead[i] = true;
			}
		}
		if (!deleted.Any(keys => keys is not null) && !documentDeleted.Contains(true))
		{
			return records;
		}
		var readIds = records.Ids.Where((_, i) => keysRead[i]).Distinct().ToList();
		var stored = readIds.Count == 0 ? new Dictionary<string, ChromaCollectionEntry>()
			: (await GetEntries(readIds, null, null, null, null, ChromaGetInclude.Metadatas, cancellationToken)).ToDictionary(entry => entry.Id);
		var metadatas = new List<IReadOnlyDictionary<string, object>?>(count);
		var documents = records.Documents?.ToList();
		for (var i = 0; i < count; i++)
		{
			var entry = stored.TryGetValue(records.Ids[i], out var found) ? found : null;
			var given = records.Metadatas?[i];
			if (deleted[i] is { } keys)
			{
				var metadata = given?.Where(pair => !keys.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value) ?? [];
				foreach (var key in keys.Where(key => !keysRead[i] || entry?.Metadata?.ContainsKey(key) == true))
				{
					metadata[key] = null!;
				}
				metadatas.Add(metadata.Count > 0 ? metadata : null);
			}
			else
			{
				metadatas.Add(given);
			}
			if (documentDeleted[i])
			{
				documents![i] = string.Empty;
			}
		}
		return new ChromaRecords(records.Ids)
		{
			Embeddings = records.Embeddings,
			Metadatas = metadatas.Any(metadata => metadata is not null) ? metadatas : null,
			Documents = documents,
			Uris = records.Uris,
			NullDocumentsDelete = records.NullDocumentsDelete,
		};
	}

	// As the Python client of Chroma: for each sparse vector index of the schema with a source key and an embedding function, the vector
	// of each record whose metadata does not have the key, from its document (#document) or from the text in its metadata. An empty text
	// gets its empty vector, which replaces the vector of the old text. The records and the metadata of the caller stay as they are: the
	// ones that change are copies.
	private ChromaRecords WithSparseVectors(ChromaRecords records)
	{
		if (records.Metadatas is null && records.Documents is null)
		{
			return records;
		}
		var indexes = _collection.SparseVectorIndexes.Where(index => index.SourceKey is not null && index.EmbeddingFunction is not null).ToList();
		if (indexes.Count == 0)
		{
			return records;
		}
		var metadatas = records.Metadatas?.ToList() ?? records.Ids.Select(_ => (IReadOnlyDictionary<string, object>?)null).ToList();
		var copied = new bool[metadatas.Count];
		foreach (var index in indexes)
		{
			for (var i = 0; i < metadatas.Count; i++)
			{
				var metadata = metadatas[i];
				if (metadata is not null && metadata.TryGetValue(index.Key, out var given))
				{
					// The key holds the vectors of the index: a record gives one, or deletes it with null or an empty list, and nothing else.
					if (given is not null && !IsSparseVector(given) && !(ChromaRequestChecks.IsList(given) && ChromaRequestChecks.IsEmpty(given)))
					{
						throw new ArgumentException($"The metadata key \"{index.Key}\" holds the vectors of the sparse vector index on \"{index.SourceKey}\": a record cannot give it another value.", nameof(records));
					}
					continue;
				}
				var text = index.SourceKey == ChromaSearchKeys.Document
					? records.Documents is { } documents && i < documents.Count ? documents[i] : null
					: metadata is not null && metadata.TryGetValue(index.SourceKey!, out var value) ? value as string : null;
				if (text is null)
				{
					continue;
				}
				var function = index.Bm25Function ?? throw CannotEmbed(index);
				var copy = copied[i] ? (Dictionary<string, object>)metadata! : metadata?.ToDictionary(x => x.Key, x => x.Value) ?? [];
				copy[index.Key] = function.Embed(text);
				metadatas[i] = copy;
				copied[i] = true;
			}
		}
		return !copied.Contains(true) ? records : new ChromaRecords(records.Ids)
		{
			Embeddings = records.Embeddings,
			Metadatas = metadatas,
			Documents = records.Documents,
			Uris = records.Uris,
			NullDocumentsDelete = records.NullDocumentsDelete,
		};
	}

	// Works around KD-49 (docs/COMPATIBILITY.md)
	// A strategy other than the upsert of the server, for writes with embeddings: on Chroma 0.x, which has not the defect, the writes go
	// as they are.
	private async Task<bool> WritesByStrategy(ChromaRecords records, CancellationToken cancellationToken)
		=> _httpClient.UpsertStrategy != ChromaUpsertStrategy.Server && records.Embeddings is not null && !await _httpClient.IsChroma0(cancellationToken);

	// The embeddings of the records are read first. A record whose embedding does not change is updated without it, with its other
	// fields as given; the others go by the update or the upsert of the server.
	private async Task WriteByStrategy(ChromaRecords records, bool upsert, bool base64, RequestQueryParams requestParams, CancellationToken cancellationToken)
	{
		var stored = await ReadStored(records.Ids.Distinct().ToList(), cancellationToken);
		var unchanged = new List<int>();
		var others = new List<int>();
		// An id given more than once goes with the others, in the order given: Chroma applies the writes in order, and the last one stays.
		var repeated = new HashSet<string>(records.Ids.GroupBy(id => id).Where(group => group.Count() > 1).Select(group => group.Key));
		for (var i = 0; i < records.Ids.Count; i++)
		{
			if (!repeated.Contains(records.Ids[i]) && stored.TryGetValue(records.Ids[i], out var entry) && entry.Embedding is { } embedding
				&& SameEmbedding(embedding.Span, records.Embeddings![i].Span, _collection.Space == ChromaSpace.Cosine))
			{
				unchanged.Add(i);
			}
			else
			{
				others.Add(i);
			}
		}
		// Once a write went, a failure is partly done: a client made by name does not run the operation again. The error says once how many
		// records went, of all those given.
		var went = 0;
		try
		{
			if (unchanged.Count > 0)
			{
				await SendWrite(Pick(records, unchanged, embeddings: false), "update", base64, requestParams, cancellationToken);
				went = unchanged.Count;
			}
			if (others.Count > 0)
			{
				await SendWrite(Pick(records, others), upsert ? "upsert" : "update", base64, requestParams, cancellationToken);
			}
		}
		catch (ChromaException ex) when (went + (ex.RecordsWent ?? 0) > 0)
		{
			var error = ex.RecordsWent is not null && ex.InnerException is ChromaException inner ? inner : ex;
			throw new ChromaException($"{went + (ex.RecordsWent ?? 0)} of the {records.Ids.Count} records went before the error, and stay: {error.Message}", error)
			{
				StatusCode = error.StatusCode,
				ErrorType = error.ErrorType,
				PartlyDone = true,
				RecordsWent = went + (ex.RecordsWent ?? 0),
			};
		}
	}

	// Works around KD-8 (docs/COMPATIBILITY.md)
	// Whether the embedding written is the one stored. In a cosine collection Chroma 1.x gives an embedding back 1 or 2 ulp off, so there
	// the values may differ by up to 4 ulp, far below any change of a vector that matters.
	private static bool SameEmbedding(ReadOnlySpan<float> stored, ReadOnlySpan<float> given, bool cosine)
	{
		if (!cosine || stored.Length != given.Length)
		{
			return stored.SequenceEqual(given);
		}
		var storedBits = System.Runtime.InteropServices.MemoryMarshal.Cast<float, int>(stored);
		var givenBits = System.Runtime.InteropServices.MemoryMarshal.Cast<float, int>(given);
		for (var i = 0; i < stored.Length; i++)
		{
			if (stored[i] != given[i] && ((storedBits[i] < 0) != (givenBits[i] < 0) || Math.Abs((long)storedBits[i] - givenBits[i]) > 4))
			{
				return false;
			}
		}
		return true;
	}

	// The embeddings of the records with the ids.
	private async Task<Dictionary<string, ChromaCollectionEntry>> ReadStored(IReadOnlyList<string> ids, CancellationToken cancellationToken)
	{
		var requestParams = new RequestQueryParams()
			.Insert("{tenant}", _tenant)
			.Insert("{database}", _database)
			.Insert("{collection_id}", _collection.Id);
		var size = Math.Max((_httpClient.BatchSplitting ? await BatchSize(cancellationToken) : null) ?? ids.Count, 1);
		var stored = new Dictionary<string, ChromaCollectionEntry>();
		for (var i = 0; i < ids.Count; i += size)
		{
			var request = new CollectionGetRequest()
			{
				Ids = ids.Skip(i).Take(size).ToList(),
				Include = ChromaGetInclude.Embeddings.ToInclude(),
			};
			var response = await Read(async () => (await _httpClient.Post<CollectionGetRequest, CollectionEntriesGetResponse>(_httpClient.Routes.Collection + "/get", request, requestParams, cancellationToken)).Checked("get"), cancellationToken);
			foreach (var entry in response.Map())
			{
				stored[entry.Id] = entry;
			}
		}
		return stored;
	}

	private static ChromaRecords Pick(ChromaRecords records, List<int> indexes, bool embeddings = true)
		=> new(indexes.Select(i => records.Ids[i]).ToList())
		{
			Embeddings = embeddings && records.Embeddings is { } given ? indexes.Select(i => given[i]).ToList() : null,
			Metadatas = records.Metadatas is { } metadatas ? indexes.Select(i => metadatas[i]).ToList() : null,
			Documents = records.Documents is { } documents ? indexes.Select(i => documents[i]).ToList() : null,
			Uris = records.Uris is { } uris ? indexes.Select(i => uris[i]).ToList() : null,
		};

	// The records in batches to the endpoint: update or upsert, which take the same fields.
	private Task SendWrite(ChromaRecords records, string endpoint, bool base64, RequestQueryParams requestParams, CancellationToken cancellationToken)
		=> InBatches(records, batch =>
		{
			var embeddings = batch.Embeddings is { } given ? new ChromaEmbeddings(given, base64) : null;
			var metadatas = MetadatasOrNone(batch.Metadatas);
			var path = _httpClient.Routes.Collection + "/" + endpoint;
			return endpoint switch
			{
				"upsert" => _httpClient.Post(path, new CollectionUpsertRequest() { Ids = batch.Ids, Embeddings = embeddings, Metadatas = metadatas, Documents = batch.Documents, Uris = batch.Uris }, requestParams, cancellationToken),
				_ => _httpClient.Post(path, new CollectionUpdateRequest() { Ids = batch.Ids, Embeddings = embeddings, Metadatas = metadatas, Documents = batch.Documents, Uris = batch.Uris }, requestParams, cancellationToken),
			};
		}, cancellationToken);

	// A list of metadata that are all null goes as no metadata.
	private static IReadOnlyList<IReadOnlyDictionary<string, object>?>? MetadatasOrNone(IReadOnlyList<IReadOnlyDictionary<string, object>?>? metadatas)
		=> metadatas?.Any(metadata => metadata is not null) == true ? metadatas : null;

	// The vector of a text query of SparseKnn, with the function of the sparse vector index of the key, as the Python client of Chroma does.
	private ChromaSparseVector EmbedText(string key, string text)
	{
		var index = _collection.SparseVectorIndexes.FirstOrDefault(index => index.Key == key)
			?? throw new ChromaException($"A text query on \"{key}\" needs a sparse vector index on that key in the schema of the collection, as GetCollection returns it; or give the sparse vector.");
		return (index.Bm25Function ?? throw CannotEmbed(index)).Embed(text);
	}

	private static ChromaException CannotEmbed(ChromaSparseVectorIndex index)
		=> new($"The sparse vectors of \"{index.Key}\" come from the embedding function \"{index.EmbeddingFunction}\" of the schema, which the client cannot compute: it computes chroma_bm25. Give the sparse vectors yourself.");

	// Works around KD-21 (docs/COMPATIBILITY.md)
	// The records in batches of the batch size, one request after the other, with batch splitting, on by default. Chroma Cloud
	// rejects a batch beyond its quota of records, "current usage of 301 exceeds limit of 300", before it writes any of it: the
	// client keeps that limit for the server, and sends that batch and the rest in batches of it.
	private async Task InBatches(ChromaRecords records, Func<ChromaRecords, Task> send, CancellationToken cancellationToken)
	{
		var total = records.Ids.Count;
		var offset = 0;
		do
		{
			var size = _httpClient.BatchSplitting ? await BatchSize(cancellationToken) : null;
			var count = size is { } limit ? Math.Min(limit, total - offset) : total - offset;
			var batch = offset == 0 && count == total ? records : Slice(records, offset, count);
			try
			{
				await send(batch);
				offset += count;
			}
			catch (ChromaException ex) when (_httpClient.BatchSplitting && RecordsQuota(ex) is { } quota && quota < count)
			{
				_httpClient.LearnRecordsLimit(quota);
			}
			// The batches sent stay: a client made by name does not run the operation again, on a collection created again either.
			catch (ChromaException ex) when (offset > 0)
			{
				throw new ChromaException($"{offset} of the {total} records went before the error, and stay: {ex.Message}", ex)
				{
					StatusCode = ex.StatusCode,
					ErrorType = ex.ErrorType,
					PartlyDone = true,
					RecordsWent = offset,
				};
			}
		}
		while (offset < total);
	}

	private static ChromaRecords Slice(ChromaRecords records, int offset, int count)
		=> new(records.Ids.Skip(offset).Take(count).ToList())
		{
			Embeddings = records.Embeddings?.Skip(offset).Take(count).ToList(),
			Metadatas = records.Metadatas?.Skip(offset).Take(count).ToList(),
			Documents = records.Documents?.Skip(offset).Take(count).ToList(),
			Uris = records.Uris?.Skip(offset).Take(count).ToList(),
		};

	// "Quota exceeded: 'Number of records' exceeded quota limit for action 'Add': current usage of 301 exceeds limit of 300".
	private static readonly System.Text.RegularExpressions.Regex RecordsQuotaMessage = new("'Number of records' exceeded quota limit .* exceeds limit of ([0-9]+)");

	private static int? RecordsQuota(ChromaException exception)
		=> RecordsQuotaMessage.Match(exception.Message) is { Success: true } match
			&& int.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var limit) && limit > 0
			? limit
			: null;

	// The quota of the records of a read, as Chroma Cloud rejects a get with a larger limit, before it reads anything:
	// "Quota exceeded: 'Limit value' exceeded quota limit for action 'Get': current usage of 1000 exceeds limit of 300". The client keeps
	// it as the quota of records, with the one of the writes, and reads that page again with it.
	private static readonly System.Text.RegularExpressions.Regex LimitQuotaMessage = new("'Limit value' exceeded quota limit .* exceeds limit of ([0-9]+)");

	private static int? LimitQuota(ChromaException exception)
		=> LimitQuotaMessage.Match(exception.Message) is { Success: true } match
			&& int.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var limit) && limit > 0
			? limit
			: null;

	// Works around KD-45 (docs/COMPATIBILITY.md)
	// The smallest of the limit of the caller, 300 on Chroma Cloud when not given, the max_batch_size the server declares, and the
	// quota of records the server enforced.
	private async Task<int?> BatchSize(CancellationToken cancellationToken)
		=> new[] { _httpClient.MaxBatchSize, await _httpClient.GetMaxBatchSize(cancellationToken), _httpClient.RecordsLimit }.Min();

	// Works around KD-18 (docs/COMPATIBILITY.md)
	// The 0.x servers accept lists in metadata but drop them without an error; Chroma 1.0 to 1.4 reject them, 1.5.0 stores them.
	// Sparse vectors too: Chroma 0.6.3 accepts them and stores the metadata as null.
	private async Task CheckListsInMetadata(ChromaRecords records, CancellationToken cancellationToken)
	{
		ChromaRequestChecks.NoEmptyLists(records.Metadatas, nameof(records));
		if (records.Metadatas?.Any(metadata => metadata?.Values.Any(ChromaRequestChecks.IsList) == true) == true
			&& await _httpClient.IsChroma0(cancellationToken))
		{
			throw new ChromaException("Chroma 0.x drops the lists in metadata without an error: they need Chroma 1.5.0 or later.");
		}
		if (records.Metadatas?.Any(metadata => metadata?.Values.Any(IsSparseVector) == true) == true
			&& await _httpClient.IsChroma0(cancellationToken))
		{
			throw new ChromaException("Chroma 0.x drops the sparse vectors in metadata without an error: only Chroma Cloud stores them.");
		}
	}

	// A tagged JsonElement object is what the client returns for a sparse vector read with ChromaMetadataValues.Inferred.
	private static bool IsSparseVector(object? value)
		=> value is ChromaSparseVector
			|| value is System.Text.Json.JsonElement element && ChromaSparseVectorConverter.IsTagged(element);

	/// <summary>
	/// Deletes the records with the ids, sending the filters with them when given; by default, unless
	/// <c>WithBatchSplitting(false)</c>, the ids go in batches of the <c>max_batch_size</c> of the server.
	/// </summary>
	/// <param name="ids">The ids of the records to delete.</param>
	/// <param name="where">The filter on the metadata, or null for none.</param>
	/// <param name="whereDocument">The filter on the documents, or null for none.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	public virtual Task DeleteAsync(IReadOnlyList<string> ids, ChromaWhereOperator? where = null, ChromaWhereDocumentOperator? whereDocument = null, CancellationToken cancellationToken = default)
	{
		// As in DeleteAsync(ChromaDelete): every tested Chroma rejects a delete without ids and filters. Before the collection of a
		// client made by name is read.
		if (ids is [] && where is null && whereDocument is null)
		{
			throw new ArgumentException("The ids of a delete without filters cannot be empty: there is nothing to delete.", nameof(ids));
		}
		CheckDocumentFilters(() => ChromaWhereOperator.Split(where, whereDocument, ids).WhereDocument?.ToRequestWhereDocument(), document: true, nameof(whereDocument));
		return WithRegexQuota(() => [(ChromaWhereOperator.Split(where, whereDocument, ids).WhereDocument?.ToRequestWhereDocument(), true)], Operation("delete", cancellationToken, async () =>
		{
			var split = ChromaWhereOperator.Split(where, whereDocument, ids);
			if (split.Where == ChromaWhereOperator.None)
			{
				return;
			}
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", _tenant)
				.Insert("{database}", _database)
				.Insert("{collection_id}", _collection.Id);
			await InBatches(new ChromaRecords(split.Ids!), async batch =>
			{
				var request = new CollectionDeleteRequest()
				{
					Ids = batch.Ids,
					Where = ChromaWhereOperator.ToRequestWhere(split.Where),
					WhereDocument = split.WhereDocument?.ToRequestWhereDocument(),
				};
				await _httpClient.Post(_httpClient.Routes.Collection + "/delete", request, requestParams, cancellationToken);
			}, cancellationToken);
		}));
	}

	/// <summary>
	/// Deletes the records with the ids, the ones the filters match, or both, at most <c>delete.Limit</c> of them.
	/// Returns the count the server answers, from Chroma 1.5.3: for a delete by ids, the number of ids given, also those
	/// without a record; null otherwise.
	/// </summary>
	/// <param name="delete">What to delete: ids, filters and a limit.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The count the server answers, from Chroma 1.5.3, which for a delete by ids is the number of ids given; null from the earlier servers.</returns>
	public virtual Task<int?> DeleteAsync(ChromaDelete delete, CancellationToken cancellationToken = default)
	{
		CheckDocumentFilters(() => ChromaWhereOperator.Split(delete.Where, delete.WhereDocument, delete.Ids).WhereDocument?.ToRequestWhereDocument(), document: true, nameof(delete));
		return WithRegexQuota(() => [(ChromaWhereOperator.Split(delete.Where, delete.WhereDocument, delete.Ids).WhereDocument?.ToRequestWhereDocument(), true)], Operation("delete", cancellationToken, async () =>
		{
			// The rules of Chroma and of its Python client, checked before any request. All is no filter.
			if (delete.Ids is [])
			{
				throw new ArgumentException("The ids of a delete cannot be empty: leave them null to delete by the filters only.", nameof(delete));
			}
			var (splitWhere, whereDocument, ids) = ChromaWhereOperator.Split(delete.Where, delete.WhereDocument, delete.Ids);
			var where = splitWhere == ChromaWhereOperator.All ? null : splitWhere;
			if (ids is null && where is null && whereDocument is null)
			{
				throw new ArgumentException("A delete needs ids, a where filter or a where document filter: without them it would select every record.", nameof(delete));
			}
			if (delete.Limit is < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(delete), "The limit of a delete cannot be negative.");
			}
			if (delete.Limit is not null && where is null && whereDocument is null)
			{
				throw new ArgumentException("The limit of a delete needs a where or where document filter: Chroma rejects it with the ids alone.", nameof(delete));
			}
			if (where == ChromaWhereOperator.None)
			{
				return 0;
			}
			if (delete.Limit is not null && !await _httpClient.SupportsDeleteLimit(cancellationToken))
			{
				throw new ChromaException("The server ignores the limit of a delete and would delete every matching record: Chroma 1.5.3 and later apply it.");
			}
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", _tenant)
				.Insert("{database}", _database)
				.Insert("{collection_id}", _collection.Id);
			int? deleted = null;
			var remaining = delete.Limit;
			var sent = false;
			async Task Send(IReadOnlyList<string>? ids)
			{
				// The limit is used up: the next batches would delete nothing. The first request goes anyway, also with a limit of 0.
				if (sent && remaining is <= 0)
				{
					return;
				}
				sent = true;
				var request = new CollectionDeleteRequest()
				{
					Ids = ids,
					Where = ChromaWhereOperator.ToRequestWhere(where),
					WhereDocument = whereDocument?.ToRequestWhereDocument(),
					Limit = remaining,
				};
				// {"deleted": n} from Chroma 1.5.3; {} or null before, and a list of ids from some 0.x servers.
				var response = await _httpClient.Post<CollectionDeleteRequest, System.Text.Json.JsonElement>(_httpClient.Routes.Collection + "/delete", request, requestParams, cancellationToken);
				if (response.ValueKind == System.Text.Json.JsonValueKind.Object
					&& response.TryGetProperty("deleted", out var count) && count.ValueKind == System.Text.Json.JsonValueKind.Number)
				{
					deleted = (deleted ?? 0) + count.GetInt32();
					remaining -= count.GetInt32();
				}
			}
			if (ids is null)
			{
				await Send(null);
			}
			else
			{
				await InBatches(new ChromaRecords(ids), batch => Send(batch.Ids), cancellationToken);
			}
			return deleted;
		}));
	}

	/// <summary>
	/// Counts the records of the collection.
	/// </summary>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The number of records.</returns>
	public virtual Task<int> CountAsync(CancellationToken cancellationToken = default)
		=> Operation("count", cancellationToken, async () =>
		{
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", _tenant)
				.Insert("{database}", _database)
				.Insert("{collection_id}", _collection.Id);
			return await Read(() => _httpClient.Get<int>(_httpClient.Routes.Collection + "/count", requestParams, cancellationToken), cancellationToken);
		});

	/// <summary>
	/// The count at a read level: <c>ChromaReadLevel.IndexOnly</c> leaves out the records not indexed yet. Chroma Cloud indexes them
	/// later, so right after a write the count can be lower; a single server indexes them at once.
	/// </summary>
	/// <param name="readLevel">Which records are read: <c>ChromaReadLevel.IndexOnly</c> leaves out the ones not indexed yet.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The number of records.</returns>
	public virtual Task<int> CountAsync(ChromaReadLevel readLevel, CancellationToken cancellationToken = default)
		=> Operation("count", cancellationToken, async () =>
		{
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", _tenant)
				.Insert("{database}", _database)
				.Insert("{collection_id}", _collection.Id)
				.Insert("{read_level}", ReadLevelName(readLevel));
			return await Read(() => _httpClient.Get<int>(_httpClient.Routes.Collection + "/count?read_level={read_level}", requestParams, cancellationToken), cancellationToken);
		});

	/// <summary>
	/// Runs one search with the Search API of Chroma, which only Chroma Cloud serves: a single server answers <c>501</c>.
	/// </summary>
	/// <param name="search">The search.</param>
	/// <param name="readLevel">Which records are read: <c>ChromaReadLevel.IndexOnly</c> leaves out the ones not indexed yet; null for the default of the server.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The records the search ranks, in order.</returns>
	public virtual async Task<IReadOnlyList<ChromaSearchEntry>> SearchAsync(ChromaSearch search, ChromaReadLevel? readLevel = null, CancellationToken cancellationToken = default)
		=> (await SearchAsync([search], readLevel, cancellationToken)).Single();

	/// <summary>
	/// Runs several searches in one request with the Search API of Chroma, which only Chroma Cloud serves: a single server answers
	/// <c>501</c>. The results come in the order of the searches. With <c>ChromaReadLevel.IndexOnly</c> the records not indexed yet are
	/// left out.
	/// </summary>
	/// <param name="searches">The searches, sent in one request.</param>
	/// <param name="readLevel">Which records are read: <c>ChromaReadLevel.IndexOnly</c> leaves out the ones not indexed yet; null for the default of the server.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>For each search, the records it ranks, in order.</returns>
	public virtual Task<IReadOnlyList<IReadOnlyList<ChromaSearchEntry>>> SearchAsync(IReadOnlyList<ChromaSearch> searches, ChromaReadLevel? readLevel = null, CancellationToken cancellationToken = default)
	{
		foreach (var search in searches ?? [])
		{
			CheckDocumentFilters(() => search.Where is { } where && where != ChromaWhereOperator.None && where != ChromaWhereOperator.All ? ChromaWhereOperator.ToRequestWhere(where) : null, document: false, nameof(searches));
			CheckDocumentFilters(() => search.WhereDocument?.ToRequestWhereDocument(), document: true, nameof(searches));
		}
		return WithRegexQuota(() => (searches ?? []).SelectMany(search => new (Dictionary<string, object>?, bool)[]
		{
			(search.Where is { } where && where != ChromaWhereOperator.None && where != ChromaWhereOperator.All ? ChromaWhereOperator.ToRequestWhere(where) : null, false),
			(search.WhereDocument?.ToRequestWhereDocument(), true),
		}), Operation<IReadOnlyList<IReadOnlyList<ChromaSearchEntry>>>("search", cancellationToken, async () =>
		{
			if (searches is not { Count: > 0 })
			{
				throw new ArgumentException("At least one search is needed.", nameof(searches));
			}
			foreach (var search in searches)
			{
				if (search.Offset < 0 || search.Limit is <= 0)
				{
					throw new ArgumentOutOfRangeException(nameof(searches), "The offset of a search cannot be negative, and its limit must be positive.");
				}
				if (search.Ids is [])
				{
					throw new ArgumentException("The ids of a search cannot be empty: leave them null to search all the records.", nameof(searches));
				}
			}
			// A search with None finds nothing and is not sent; without any other, no request goes.
			var sent = searches.Where(search => search.Where != ChromaWhereOperator.None).ToList();
			if (sent.Count == 0)
			{
				return searches.Select(_ => (IReadOnlyList<ChromaSearchEntry>)[]).ToList();
			}
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", _tenant)
				.Insert("{database}", _database)
				.Insert("{collection_id}", _collection.Id);
			var request = new CollectionSearchRequest()
			{
				Searches = sent.Select(search => new CollectionSearchPayload()
				{
					Filter = SearchFilter(search),
					Rank = search.Rank?.ToRank(EmbedText),
					GroupBy = search.GroupBy?.ToGroupBy() ?? [],
					Limit = new CollectionSearchLimit() { Offset = search.Offset, Limit = search.Limit },
					Select = new CollectionSearchSelect() { Keys = SearchSelect(search) },
				}).ToList(),
				ReadLevel = readLevel is { } level ? ReadLevelName(level) : null,
			};
			var response = await Read(async () => (await _httpClient.Post<CollectionSearchRequest, CollectionSearchResponse>(_httpClient.Routes.Collection + "/search", request, requestParams, cancellationToken)).Checked(sent.Count), cancellationToken);
			var results = response.Ids
				.Select((ids, i) =>
				{
					var copy = SearchReadsDocuments(sent[i]);
					var opposite = sent[i].Rank is ChromaOppositeScoreRank;
					return (IReadOnlyList<ChromaSearchEntry>)ids
						.Select((id, j) => new ChromaSearchEntry(id)
						{
							Document = copy.Document(response.Documents?[i]?[j], response.Metadatas?[i]?[j]),
							Embedding = response.Embeddings?[i]?[j],
							Metadata = copy.Metadata(response.Metadatas?[i] is { } metadatas ? metadatas[j] ?? DocumentReader.NoKeys : null),
							Score = opposite ? -response.Scores?[i]?[j] : response.Scores?[i]?[j],
						})
						.ToList();
				})
				.ToList();
			if (sent.Count == searches.Count)
			{
				return results;
			}
			var next = 0;
			return searches.Select(search => search.Where == ChromaWhereOperator.None ? [] : results[next++]).ToList();
		}));
	}

	// The keys a search selects, and with the documents the copy key or the deletion key, which tells a deleted document from an empty one.
	private List<string> SearchSelect(ChromaSearch search)
	{
		var keys = search.Select?.Distinct().ToList() ?? [];
		if (SearchReadsDocuments(search).ReadsMetadata && !keys.Contains(ChromaSearchKeys.Metadata) && !keys.Contains(DocumentReadKey!))
		{
			keys.Add(DocumentReadKey!);
		}
		return keys;
	}

	// The metadata stays in the results of a search that selects it, or the copy key or the deletion key itself.
	private DocumentReader SearchReadsDocuments(ChromaSearch search)
		=> search.Select is { } select
			? ReadsDocuments(select.Contains(ChromaSearchKeys.Document), select.Contains(ChromaSearchKeys.Metadata) || DocumentReadKey is { } key && select.Contains(key))
			: default;

	// Works around KD-56 and KD-57 (docs/COMPATIBILITY.md)
	// On Chroma Cloud a filter on the documents gives the wrong records, also in a delete: a $regex whose pattern takes an empty text finds
	// every record and its $not_regex none, and, once Chroma Cloud has indexed the records, a $contains of fewer than 3 characters finds
	// none and its $not_contains every record. Rather than give or delete the wrong records, such a filter throws there, before any
	// request, and so does a pattern that the client cannot read as Chroma does, which Chroma rejects. The filter is the one of the
	// request, built only on Chroma Cloud: where_document, or the where of a search, whose filters on the documents are under #document.
	// A filter that the operation rejects is left to it.
	private void CheckDocumentFilters(Func<Dictionary<string, object>?> filter, bool document, string paramName)
	{
		if (!_httpClient.IsChromaCloud)
		{
			return;
		}
		Dictionary<string, object>? request;
		try
		{
			request = filter();
		}
		catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or ArgumentException)
		{
			return;
		}
		foreach (var (@operator, value) in DocumentFilters(request, document))
		{
			if (@operator is "$regex" or "$not_regex")
			{
				var length = RegexMinimumLength.Of(value);
				if (length is null or 0)
				{
					var reason = length is 0 ? "it takes an empty text" : "the client cannot read it as Chroma does, with the syntax of the Rust crate regex, to tell that it needs a character";
					throw new ArgumentException($"On Chroma Cloud the pattern \"{value}\" of {@operator} gives the wrong records: {reason}. There a filter on the documents whose pattern takes an empty text finds every record with $regex, also in a delete, and none with $not_regex. A pattern that needs a character is right: Regex(\"(?s).\") finds the records with a document that is not empty, and NotRegex(\"(?s).\") the records with an empty document or none.", paramName);
				}
			}
			else if (Characters(value) < MinContainsCharacters)
			{
				throw new ArgumentException($"On Chroma Cloud the text \"{value}\" of {@operator} gives the wrong records: once Chroma Cloud has indexed the records, with fewer than {MinContainsCharacters} characters $contains finds no record and $not_contains every record, also in a delete. The text as the pattern of a $regex or $not_regex, its special characters escaped, is right, and Regex(\"(?s).\") finds the records with a document that is not empty.", paramName);
			}
		}
	}

	// Chroma Cloud rejects a pattern of $regex or $not_regex beyond the quota of the tenant, 256 bytes in UTF-8 by default, in every get,
	// query, delete and search, with 422 "Quota exceeded: 'Length of regex pattern' exceeded quota limit for action 'Get': current usage of
	// 257 exceeds limit of 256. https://trychroma.com/request-quota-increase?...". A tenant can have a higher quota, so the client sends the
	// pattern, and on that answer throws a ChromaException that names the patterns beyond the limit of the answer.
	private static readonly System.Text.RegularExpressions.Regex RegexQuotaMessage = new("'Length of regex pattern' exceeded quota limit for action '([^']*)': current usage of ([0-9]+) exceeds limit of ([0-9]+)\\.(?:\\s+(https?://\\S+))?");

	private static async Task<T> WithRegexQuota<T>(Func<IEnumerable<(Dictionary<string, object>? Filter, bool Document)>> filters, Task<T> operation)
	{
		try
		{
			return await operation;
		}
		catch (ChromaException ex) when (RegexQuotaMessage.Match(ex.Message) is { Success: true } match)
		{
			throw RegexQuotaError(ex, match, filters);
		}
	}

	private static async Task WithRegexQuota(Func<IEnumerable<(Dictionary<string, object>? Filter, bool Document)>> filters, Task operation)
	{
		try
		{
			await operation;
		}
		catch (ChromaException ex) when (RegexQuotaMessage.Match(ex.Message) is { Success: true } match)
		{
			throw RegexQuotaError(ex, match, filters);
		}
	}

	private static ChromaException RegexQuotaError(ChromaException ex, System.Text.RegularExpressions.Match match, Func<IEnumerable<(Dictionary<string, object>? Filter, bool Document)>> filters)
	{
		var limit = int.Parse(match.Groups[3].Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);
		List<(string Operator, string Pattern, int Bytes)> patterns;
		try
		{
			patterns = filters().SelectMany(x => DocumentFilters(x.Filter, x.Document))
				.Where(x => x.Operator is "$regex" or "$not_regex")
				.Select(x => (x.Operator, Pattern: x.Value, Bytes: System.Text.Encoding.UTF8.GetByteCount(x.Value)))
				.Where(x => x.Bytes > limit)
				.Distinct()
				.ToList();
		}
		catch (Exception e) when (e is NotSupportedException or InvalidOperationException or ArgumentException)
		{
			patterns = [];
		}
		var rejected = patterns.Count > 0
			? string.Join(" and ", patterns.Select(x => $"the pattern \"{x.Pattern}\" of {x.Operator}, of {x.Bytes} bytes in UTF-8"))
			: $"a pattern of {match.Groups[2].Value} bytes in UTF-8";
		var increase = match.Groups[4].Success ? $": {match.Groups[4].Value}" : "";
		return new ChromaException($"Chroma Cloud rejects {rejected}: the quota of the tenant for a regex pattern is {limit} bytes, in '{match.Groups[1].Value}'. A tenant can ask for a higher quota{increase}", ex)
		{
			StatusCode = ex.StatusCode,
			ErrorType = ex.ErrorType,
		};
	}

	// A text of a filter: a string, or a JSON string.
	private static string? Text(object? value) => value switch
	{
		string text => text,
		System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } json => json.GetString(),
		_ => null,
	};

	// The trigrams of the index of the documents of Chroma Cloud: a text of fewer characters has none.
	private const int MinContainsCharacters = 3;

	// The characters of a text as Chroma counts them, the Unicode code points: a character outside the BMP is one.
	private static int Characters(string text)
	{
		var count = 0;
		for (var i = 0; i < text.Length; i++)
		{
			if (!(char.IsLowSurrogate(text[i]) && i > 0 && char.IsHighSurrogate(text[i - 1])))
			{
				count++;
			}
		}
		return count;
	}

	// The $regex, $not_regex, $contains and $not_contains on the documents in a filter, also inside $and and $or.
	// The values of a filter given as a dictionary from JSON are JsonElement: objects, arrays and strings are read as the others.
	private static IEnumerable<(string Operator, string Value)> DocumentFilters(object? node, bool document)
	{
		if (node is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Object } json)
		{
			node = json.EnumerateObject().ToDictionary(property => property.Name, property => (object)property.Value);
		}
		else if (node is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Array } array)
		{
			node = array.EnumerateArray().Select(item => (object)item).ToList();
		}
		if (node is IDictionary<string, object> map)
		{
			foreach (var pair in map)
			{
				if (document && pair.Key is "$regex" or "$not_regex" or "$contains" or "$not_contains" && Text(pair.Value) is { } value)
				{
					yield return (pair.Key, value);
				}
				else
				{
					foreach (var found in DocumentFilters(pair.Value, document || pair.Key == ChromaSearchKeys.Document))
					{
						yield return found;
					}
				}
			}
		}
		else if (node is System.Collections.IEnumerable list and not string)
		{
			foreach (var item in list)
			{
				foreach (var found in DocumentFilters(item, document))
				{
					yield return found;
				}
			}
		}
	}

	// The where clause of the Search API holds the metadata, the documents (#document) and the ids (#id); several filters go in $and.
	private static Dictionary<string, object>? SearchFilter(ChromaSearch search)
	{
		var filters = new List<Dictionary<string, object>>();
		if (ChromaWhereOperator.ToRequestWhere(search.Where) is { } where)
		{
			filters.Add(where);
		}
		if (search.WhereDocument is { } whereDocument)
		{
			filters.Add(whereDocument.ToRequestSearchWhere());
		}
		if (search.Ids is { } ids)
		{
			filters.Add(ChromaWhereOperator.In(ChromaSearchKeys.Id, ids.ToArray<object>()).ToWhere());
		}
		return filters switch
		{
			[] => null,
			[var single] => single,
			_ => new() { ["$and"] = filters.Cast<object>().ToArray() },
		};
	}

	private static string ReadLevelName(ChromaReadLevel readLevel) => readLevel switch
	{
		ChromaReadLevel.IndexAndWal => "index_and_wal",
		ChromaReadLevel.IndexOnly => "index_only",
		ChromaReadLevel.IndexAndBoundedWal => "index_and_bounded_wal",
		_ => throw new ArgumentOutOfRangeException(nameof(readLevel)),
	};

	/// <summary>
	/// A copy of the collection under a new name, with the same records: Chroma Cloud only, a single server answers
	/// 501.
	/// </summary>
	/// <param name="newName">The name of the new collection.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The new collection.</returns>
	public virtual Task<ChromaCollection> ForkAsync(string newName, CancellationToken cancellationToken = default)
		=> Operation("fork", cancellationToken, async () =>
		{
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", _tenant)
				.Insert("{database}", _database)
				.Insert("{collection_id}", _collection.Id);
			var request = new ForkCollectionRequest()
			{
				NewName = newName,
			};
			return await _httpClient.Post<ForkCollectionRequest, ChromaCollection>(_httpClient.Routes.Collection + "/fork", request, requestParams, cancellationToken);
		});

	/// <summary>
	/// How many forks the collection has: Chroma Cloud only.
	/// </summary>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The number of forks.</returns>
	public virtual Task<int> ForkCountAsync(CancellationToken cancellationToken = default)
		=> Operation("fork_count", cancellationToken, async () =>
		{
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", _tenant)
				.Insert("{database}", _database)
				.Insert("{collection_id}", _collection.Id);
			return (await _httpClient.Get<ForkCountResponse>(_httpClient.Routes.Collection + "/fork_count", requestParams, cancellationToken)).Count;
		});

	/// <summary>
	/// How far the writes to the collection are indexed: Chroma Cloud only.
	/// </summary>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>How many writes are indexed.</returns>
	public virtual Task<ChromaIndexingStatus> GetIndexingStatusAsync(CancellationToken cancellationToken = default)
		=> Operation("get_indexing_status", cancellationToken, async () =>
		{
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", _tenant)
				.Insert("{database}", _database)
				.Insert("{collection_id}", _collection.Id);
			return await _httpClient.Get<ChromaIndexingStatus>(_httpClient.Routes.Collection + "/indexing_status", requestParams, cancellationToken);
		});

	/// <summary>
	/// Attaches a function of Chroma Cloud, like <c>ChromaFunctions.Statistics</c>, under a name of its own; its
	/// results go to the output collection. <c>Created</c> is false when a function with that name was already
	/// attached.
	/// </summary>
	/// <param name="function">The function to attach, like <c>ChromaFunctions.Statistics</c>.</param>
	/// <param name="name">The name of the attached function.</param>
	/// <param name="outputCollection">The collection the function writes to.</param>
	/// <param name="parameters">The parameters of the function, or null for none.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The attached function, and whether it was created now.</returns>
	public virtual Task<(ChromaAttachedFunction AttachedFunction, bool Created)> AttachFunctionAsync(string function, string name, string outputCollection, IReadOnlyDictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
		=> Operation("attach_function", cancellationToken, async () =>
		{
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", _tenant)
				.Insert("{database}", _database)
				.Insert("{collection_id}", _collection.Id);
			var request = new AttachFunctionRequest()
			{
				Name = name,
				FunctionId = function,
				OutputCollection = outputCollection,
				Params = parameters,
			};
			var response = await _httpClient.Post<AttachFunctionRequest, AttachFunctionResponse>(_httpClient.Routes.Collection + "/functions/attach", request, requestParams, cancellationToken);
			return (response.AttachedFunction, response.Created ?? true);
		});

	/// <summary>
	/// Gets the function attached to the collection under the name: Chroma Cloud only.
	/// </summary>
	/// <param name="name">The name of the attached function.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The attached function.</returns>
	public virtual Task<ChromaAttachedFunction> GetAttachedFunctionAsync(string name, CancellationToken cancellationToken = default)
		=> Operation("get_attached_function", cancellationToken, async () =>
		{
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", _tenant)
				.Insert("{database}", _database)
				.Insert("{collection_id}", _collection.Id)
				.Insert("{function_name}", name);
			return (await _httpClient.Get<GetAttachedFunctionResponse>(_httpClient.Routes.Collection + "/functions/{function_name}", requestParams, cancellationToken)).AttachedFunction;
		});

	/// <summary>
	/// Detaches the function attached under the name, and deletes its output collection when
	/// <c>deleteOutputCollection</c> is true: Chroma Cloud only. Returns the <c>success</c> of the answer of the
	/// server.
	/// </summary>
	/// <param name="name">The name of the attached function.</param>
	/// <param name="deleteOutputCollection">Whether the output collection of the function is deleted too.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>Whether the function was detached.</returns>
	public virtual Task<bool> DetachFunctionAsync(string name, bool deleteOutputCollection = false, CancellationToken cancellationToken = default)
		=> Operation("detach_function", cancellationToken, async () =>
		{
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", _tenant)
				.Insert("{database}", _database)
				.Insert("{collection_id}", _collection.Id)
				.Insert("{function_name}", name);
			var request = new DetachFunctionRequest()
			{
				DeleteOutput = deleteOutputCollection,
			};
			return (await _httpClient.Post<DetachFunctionRequest, DetachFunctionResponse>(_httpClient.Routes.Collection + "/attached_functions/{function_name}/detach", request, requestParams, cancellationToken)).Success;
		});

	/// <summary>
	/// Gets up to <c>limit</c> records of the collection, with a get that sets only the limit.
	/// </summary>
	/// <param name="limit">How many records to return.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	/// <returns>The first records.</returns>
	public virtual Task<IReadOnlyList<ChromaCollectionEntry>> PeekAsync(int limit = 10, CancellationToken cancellationToken = default)
		=> Operation<IReadOnlyList<ChromaCollectionEntry>>("peek", cancellationToken, async () =>
		{
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", _tenant)
				.Insert("{database}", _database)
				.Insert("{collection_id}", _collection.Id);
			var request = new CollectionPeekRequest()
			{
				Limit = limit,
			};
			var response = await Read(async () => (await _httpClient.Post<CollectionPeekRequest, CollectionEntriesGetResponse>(_httpClient.Routes.Collection + "/get", request, requestParams, cancellationToken)).Checked("peek"), cancellationToken);
			return response.Map() ?? [];
		});

	/// <summary>
	/// Changes the name or the metadata of the collection.
	/// </summary>
	/// <param name="name">The new name of the collection, or null to keep it.</param>
	/// <param name="metadata">The new metadata of the collection, or null to keep it.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	public virtual Task ModifyAsync(string? name = null, IReadOnlyDictionary<string, object>? metadata = null, CancellationToken cancellationToken = default)
		=> Operation("modify", cancellationToken, async () =>
		{
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", _tenant)
				.Insert("{database}", _database)
				.Insert("{collection_id}", _collection.Id);
			ChromaRequestChecks.NoLists(metadata, nameof(metadata));
			var request = new CollectionModifyRequest()
			{
				Name = name,
				Metadata = metadata,
			};
			await _httpClient.Put(_httpClient.Routes.Collection, request, requestParams, cancellationToken);
		});

	/// <summary>
	/// Changes the settings of the index, like <c>ef_search</c>. Chroma 1.0.6 and later apply them; the earlier
	/// versions answer without applying them, so on them the client throws a <c>ChromaException</c> before sending the
	/// request. The settings must be those of the index of the collection: HNSW on a single server, SPANN on Chroma
	/// Cloud. Chroma Cloud answers 500 to HNSW settings, and a single server answers without applying SPANN settings:
	/// the client throws before both.
	/// </summary>
	/// <param name="configuration">The settings of the index to change.</param>
	/// <param name="cancellationToken">The token that cancels the operation.</param>
	public virtual Task ModifyConfigurationAsync(ChromaCollectionConfigurationUpdate configuration, CancellationToken cancellationToken = default)
		=> Operation("modify", cancellationToken, async () =>
		{
			ChromaHnswConfiguration.CheckMaxNeighbors(configuration.Hnsw?.MaxNeighbors, nameof(configuration));
			var current = await CurrentConfiguration(cancellationToken);
			if (current is not { ValueKind: System.Text.Json.JsonValueKind.Object } value
				|| !value.TryGetProperty("hnsw", out var hnsw) && !value.TryGetProperty("spann", out _))
			{
				throw new ChromaException("The server answers without applying a new configuration: Chroma 1.0.6 and later apply it.");
			}
			var hasHnsw = hnsw.ValueKind == System.Text.Json.JsonValueKind.Object;
			var hasSpann = value.TryGetProperty("spann", out var spann) && spann.ValueKind == System.Text.Json.JsonValueKind.Object;
			// Works around KD-25 (docs/COMPATIBILITY.md)
			if (configuration.Hnsw is not null && !hasHnsw && hasSpann)
			{
				throw new ChromaException("The collection has a SPANN index, as on Chroma Cloud, which rejects HNSW settings: set Spann instead.");
			}
			if (configuration.Spann is not null && !hasSpann && hasHnsw)
			{
				throw new ChromaException("The collection has an HNSW index, as on a single Chroma server, which answers without applying SPANN settings: set Hnsw instead.");
			}
			var requestParams = new RequestQueryParams()
				.Insert("{tenant}", _tenant)
				.Insert("{database}", _database)
				.Insert("{collection_id}", _collection.Id);
			var request = new CollectionModifyRequest()
			{
				Configuration = new CollectionConfigurationUpdateRequest()
				{
					Hnsw = configuration.Hnsw,
					Spann = configuration.Spann,
					EmbeddingFunction = configuration.EmbeddingFunction?.ToJson(),
				},
			};
			await _httpClient.Put(_httpClient.Routes.Collection, request, requestParams, cancellationToken);
		});

	// Chroma 1.0.6 and later, Chroma Cloud too, send the configuration with "hnsw" and "spann", one of them null; 0.5.4 to 1.0.5
	// with "hnsw_configuration", and 0.4.10 to 0.5.3 send none. Without the configuration at hand, the collection is read by its name.
	private async Task<System.Text.Json.JsonElement?> CurrentConfiguration(CancellationToken cancellationToken)
	{
		var configuration = _collection.ConfigurationJson;
		if (configuration is null)
		{
			var requestParams = new RequestQueryParams()
				.Insert("{collectionName}", _collection.Name)
				.Insert("{tenant}", _tenant)
				.Insert("{database}", _database);
			configuration = (await _httpClient.Get<ChromaCollection>(_httpClient.Routes.CollectionByName, requestParams, cancellationToken)).ConfigurationJson;
		}
		return configuration;
	}
}
