using System.Collections;
using System.Text.Json;

namespace ChromaDB.Client.Common;

// What a server would drop, change or fail on, stopped before the request with a message that says why.
internal static class ChromaRequestChecks
{
	// Works around KD-22 (docs/COMPATIBILITY.md)
	// An empty list in the metadata of a record: Chroma 0.6.3 and 1.5.9 drop the key without an error, 1.0 to 1.4
	// reject it, Chroma Cloud stores it, and the Python client rejects it. Rejected here too, so that the same code does
	// the same on every server.
	public static void NoEmptyLists(IReadOnlyList<IReadOnlyDictionary<string, object>?>? metadatas, string paramName)
	{
		if (metadatas is null)
		{
			return;
		}
		foreach (var metadata in metadatas)
		{
			if (metadata is null)
			{
				continue;
			}
			foreach (var pair in metadata)
			{
				if (IsList(pair.Value) && IsEmpty(pair.Value))
				{
					throw new ArgumentException($"The list of the metadata key \"{pair.Key}\" is empty: Chroma takes lists with at least one value, as the Python client checks, and a single server drops an empty one without an error.", paramName);
				}
			}
		}
	}

	// A null value in the metadata of a new record: Chroma 0.x drops the key without an error, and 1.x rejects the request with
	// 422. In an update or an upsert a null deletes the key on every tested Chroma, so only AddAsync rejects it.
	public static void NoNullValues(IReadOnlyList<IReadOnlyDictionary<string, object>?>? metadatas, string paramName)
	{
		if (metadatas is null)
		{
			return;
		}
		foreach (var metadata in metadatas)
		{
			if (metadata is null)
			{
				continue;
			}
			foreach (var pair in metadata)
			{
				if (pair.Value is null)
				{
					throw new ArgumentException($"The metadata key \"{pair.Key}\" is null: a null deletes a key in UpdateAsync and UpsertAsync, and a new record has none to delete; Chroma 0.x would drop the key, and 1.x rejects the request.", paramName);
				}
			}
		}
	}

	// Works around KD-20 (docs/COMPATIBILITY.md)
	// No Chroma stores a list in the metadata of a collection: 0.x and 1.0 to 1.4 reject it, Chroma Cloud answers 500
	// and 1.5.9 closes the connection.
	public static void NoLists(IReadOnlyDictionary<string, object>? metadata, string paramName)
	{
		if (metadata is null)
		{
			return;
		}
		foreach (var pair in metadata)
		{
			if (IsList(pair.Value))
			{
				throw new ArgumentException($"The metadata key \"{pair.Key}\" of the collection has a list: Chroma stores lists only in the metadata of records.", paramName);
			}
		}
	}

	// A JsonElement array is what the client returns for a list read with ChromaMetadataValues.Inferred. A byte[] goes
	// as a base64 string.
	public static bool IsList(object? value)
		=> value is JsonElement { ValueKind: JsonValueKind.Array }
			or IEnumerable and not string and not IDictionary and not byte[];

	// A JsonElement number or string, what the client returns with ChromaMetadataValues.Inferred and sends as its raw
	// value, checked as the double or string it holds.
	public static object? Scalar(object? value)
		=> value switch
		{
			JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetDouble(out var number) => number,
			JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
			_ => value,
		};

	// Only from a count: enumerating the list would consume one that can be read once, before it is sent.
	public static bool IsEmpty(object? value)
		=> value is JsonElement element ? element.GetArrayLength() == 0 : value is ICollection { Count: 0 };

	// Lists of another length than the ids would be cut differently in each batch, so that part of a write would go before the error;
	// and the numbers of an embedding must be finite, which JSON numbers and the base64 embeddings of the server need.
	public static void SameLengths(ChromaDB.Client.Models.ChromaRecords records, string paramName)
	{
		var count = records.Ids.Count;
		Check(records.Embeddings?.Count, "embeddings");
		Check(records.Metadatas?.Count, "metadatas");
		Check(records.Documents?.Count, "documents");
		Check(records.Uris?.Count, "uris");
		foreach (var embedding in records.Embeddings ?? [])
		{
			foreach (var number in embedding.Span)
			{
				if (float.IsNaN(number) || float.IsInfinity(number))
				{
					throw new ArgumentException($"An embedding has {number.ToString(System.Globalization.CultureInfo.InvariantCulture)}: Chroma takes finite numbers only.", paramName);
				}
			}
		}

		void Check(int? length, string name)
		{
			if (length is { } given && given != count)
			{
				throw new ArgumentException($"The {name} are {given} and the ids {count}: each record needs one, null for none.", paramName);
			}
		}
	}

	// Works around KD-58 (docs/COMPATIBILITY.md)
	// Chroma keeps the document of a record in the metadata key chroma:document: a text there sets the document, and another value
	// makes Chroma 1.x fail every write and read of the server until it restarts. The Python client rejects the key with any value;
	// here only the values that are not texts, so that what works today keeps working.
	public const string DocumentKey = "chroma:document";

	public static bool IsText(object? value) => value is string or JsonElement { ValueKind: JsonValueKind.String };

	// The metadata a document copy key or a document deletion key adds after the given one is checked: under chroma:document, a
	// deleted document or a mark would go as a value that is not a text.
	public static void GeneratedDocumentKey(IReadOnlyList<IReadOnlyDictionary<string, object>?>? metadatas, string paramName)
	{
		foreach (var metadata in metadatas ?? [])
		{
			if (metadata is not null && metadata.TryGetValue(DocumentKey, out var value) && !IsText(value))
			{
				throw new ArgumentException($"The document copy key or the document deletion key of the collection client is \"{DocumentKey}\", which holds the document of the record in Chroma: a deleted document or a mark would write a value that is not a text there, and that makes Chroma 1.x fail every write and read of the server until it restarts. Choose another key.", paramName);
			}
		}
	}

	// What the Python client checks too: a list holds values of one type, strings, integers, floating-point numbers or Booleans. The
	// server answers with an error that does not say what is wrong. A list that can be read once is not read here.
	public static void MetadataValues(IReadOnlyList<IReadOnlyDictionary<string, object>?>? metadatas, string paramName)
	{
		foreach (var metadata in metadatas ?? [])
		{
			foreach (var pair in metadata ?? Enumerable.Empty<KeyValuePair<string, object>>())
			{
				if (pair.Key == DocumentKey && !IsText(pair.Value))
				{
					throw new ArgumentException($"The metadata key \"{DocumentKey}\" holds the document of the record in Chroma and takes a text only: another value makes Chroma 1.x fail every write and read of the server until it restarts.", paramName);
				}
				if (IsList(pair.Value) && pair.Value is JsonElement or ICollection && Kinds(pair.Value).Distinct().Skip(1).Any())
				{
					throw new ArgumentException($"The list of the metadata key \"{pair.Key}\" holds values of more than one type: Chroma takes strings, integers, floating-point numbers or Booleans, one type in a list.", paramName);
				}
			}
		}
	}

	// The type of each value of a list, as Chroma tells them: null for one it does not take.
	private static IEnumerable<string?> Kinds(object value)
	{
		if (value is JsonElement element)
		{
			foreach (var item in element.EnumerateArray())
			{
				yield return item.ValueKind switch
				{
					JsonValueKind.String => "string",
					JsonValueKind.True or JsonValueKind.False => "bool",
					JsonValueKind.Number => item.TryGetInt64(out _) ? "int" : "float",
					_ => null,
				};
			}
			yield break;
		}
		foreach (var item in (IEnumerable)value)
		{
			yield return item switch
			{
				string => "string",
				bool => "bool",
				sbyte or byte or short or ushort or int or uint or long or ulong => "int",
				float or double or decimal => "float",
				JsonElement json => json.ValueKind switch
				{
					JsonValueKind.String => "string",
					JsonValueKind.True or JsonValueKind.False => "bool",
					JsonValueKind.Number => json.TryGetInt64(out _) ? "int" : "float",
					_ => null,
				},
				_ => null,
			};
		}
	}

	// A lone half of a surrogate pair: UTF-8 has no form for it, and System.Text.Json would send U+FFFD in its place, so
	// an id or a document would come back changed.
	public static void NoLoneSurrogates(string value, string what)
	{
		for (var i = 0; i < value.Length; i++)
		{
			var c = value[i];
			if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
			{
				i++;
				continue;
			}
			if (char.IsSurrogate(c))
			{
				throw new ArgumentException($"{what} has a lone surrogate, U+{(int)c:X4} at {i}: UTF-8 cannot carry it, and it would reach the server as U+FFFD.");
			}
		}
	}
}
