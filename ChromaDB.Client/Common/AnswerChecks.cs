using ChromaDB.Client.Models.Responses;

namespace ChromaDB.Client.Common;

// An answer to a get, a query or a search whose lists do not have one item for every record: Chroma 1.5.9 answers a get with embeddings
// without the embedding of a record while other clients write and delete the same records, and the same get right after is whole. The
// read goes again, as after an error that can pass (ChromaException.UnevenAnswer); after the last attempt the ChromaException stands,
// instead of an ArgumentOutOfRangeException from the lists.
internal static class AnswerChecks
{
	public static CollectionEntriesGetResponse Checked(this CollectionEntriesGetResponse answer, string operation)
	{
		var ids = answer.Ids.Count;
		Check(operation, ids, "embeddings", answer.Embeddings?.Count);
		Check(operation, ids, "metadatas", answer.Metadatas?.Count);
		Check(operation, ids, "documents", answer.Documents?.Count);
		Check(operation, ids, "uris", answer.Uris?.Count);
		return answer;
	}

	// A query has one list of each for every query embedding, the ids too.
	public static CollectionEntriesQueryResponse Checked(this CollectionEntriesQueryResponse answer, int queryEmbeddings)
	{
		CheckIds("query", answer.Ids, queryEmbeddings);
		CheckEach("query", answer.Ids, "distances", answer.Distances, list => list?.Count, optional: false);
		CheckEach("query", answer.Ids, "metadatas", answer.Metadatas, list => list?.Count, optional: false);
		CheckEach("query", answer.Ids, "embeddings", answer.Embeddings, list => list?.Count, optional: false);
		CheckEach("query", answer.Ids, "documents", answer.Documents, list => list?.Count, optional: false);
		CheckEach("query", answer.Ids, "uris", answer.Uris, list => list?.Count, optional: false);
		return answer;
	}

	// A search has one list of each for every search, the ids too; another list is null when the search does not select it.
	public static CollectionSearchResponse Checked(this CollectionSearchResponse answer, int searches)
	{
		CheckIds("search", answer.Ids, searches);
		CheckEach("search", answer.Ids, "documents", answer.Documents, list => list?.Count, optional: true);
		CheckEach("search", answer.Ids, "embeddings", answer.Embeddings, list => list?.Count, optional: true);
		CheckEach("search", answer.Ids, "metadatas", answer.Metadatas, list => list?.Count, optional: true);
		CheckEach("search", answer.Ids, "scores", answer.Scores, list => list?.Count, optional: true);
		return answer;
	}

	private static void CheckIds(string operation, List<List<string>> ids, int expected)
	{
		if (ids.Count != expected)
		{
			var each = operation == "query" ? "query embedding" : "search";
			throw Uneven($"The answer of the server to a {operation} has {ids.Count} {(ids.Count == 1 ? "list" : "lists")} of ids for {expected} {(expected == 1 ? each : operation == "query" ? "query embeddings" : "searches")}, while it has one list of each for every {each}.");
		}
	}

	private static void CheckEach<T>(string operation, List<List<string>> ids, string name, List<T>? lists, Func<T, int?> count, bool optional)
	{
		if (lists is null)
		{
			return;
		}
		if (lists.Count != ids.Count)
		{
			throw Uneven($"The answer of the server to a {operation} has {ids.Count} lists of ids and {lists.Count} lists of {name}, while it has one list of each for every {(operation == "query" ? "query embedding" : "search")}.");
		}
		for (var i = 0; i < ids.Count; i++)
		{
			if (count(lists[i]) is { } items)
			{
				Check(operation, ids[i].Count, name, items);
			}
			else if (!optional)
			{
				Check(operation, ids[i].Count, name, 0);
			}
		}
	}

	private static void Check(string operation, int ids, string name, int? count)
	{
		if (count is { } items && items != ids)
		{
			throw Uneven($"The answer of the server to a {operation} has {ids} {(ids == 1 ? "id" : "ids")} and {items} {name}, while it has one item for every record in each list.");
		}
	}

	private static ChromaException Uneven(string message) => new(message) { UnevenAnswer = true };
}
