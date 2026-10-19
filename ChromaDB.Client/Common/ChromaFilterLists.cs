namespace ChromaDB.Client.Common;

// The lists of $and and $or as they go to the server. A single Chroma server turns a filter into an SQLite expression where a
// list of n filters is n deep, plus its deepest filter, and SQLite stops at 1000: Chroma 1.5.9 takes 987 to 994 filters in
// one list, whatever the operator, and from about 4,400 it crashes on a stack overflow; where and where_document have a limit each.
// The client counts that depth: up to 900, with room for the rest of the query, a filter goes as it is, one list for a chain, as
// the Python client writes it. Beyond, each list of n filters goes as ⌈√n⌉ lists of about √n, with the same meaning, about 2√n deep.
internal static class ChromaFilterLists
{
	public const int MaxDepth = 900;

	// Works around KD-4 (docs/COMPATIBILITY.md)
	public static Dictionary<string, object> Shape(Dictionary<string, object> filter)
		=> Depth(filter) <= MaxDepth ? filter : (Dictionary<string, object>)Split(filter);

	// At most the depth of the SQLite expression: the deepest filter of a list counts as if it were the first of the chain.
	private static int Depth(object filter)
		=> List(filter) is var (_, filters) ? filters.Length - 1 + filters.Max(Depth) : 1;

	private static object Split(object filter)
	{
		if (List(filter) is not var (@operator, filters))
		{
			return filter;
		}
		var operands = filters.Select(Split).ToList();
		var count = (int)Math.Ceiling(Math.Sqrt(operands.Count));
		// Fewer than 5 filters are as deep in one list as in groups.
		if (operands.Count < 5)
		{
			return new Dictionary<string, object> { [@operator] = operands.ToArray() };
		}
		var groups = new List<object>();
		for (int i = 0, start = 0; i < count; i++)
		{
			// Sizes that differ by one at most; Chroma takes $and and $or with two filters or more, so a group of one is the filter.
			var size = operands.Count / count + (i < operands.Count % count ? 1 : 0);
			groups.Add(size == 1 ? operands[start] : new Dictionary<string, object> { [@operator] = operands.GetRange(start, size).ToArray() });
			start += size;
		}
		return new Dictionary<string, object> { [@operator] = groups.ToArray() };
	}

	// A list of $and or $or: an array in a dictionary, as the operators write it, or a JSON array in a dictionary or a JSON object, as a
	// filter given as a dictionary has it.
	private static (string Operator, object[] Filters)? List(object filter)
		=> filter switch
		{
			IReadOnlyDictionary<string, object> { Count: 1 } dictionary when dictionary.First() is { Key: "$and" or "$or" } pair && Filters(pair.Value) is { } filters
				=> (pair.Key, filters),
			System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Object } json
				when json.EnumerateObject().Take(2).ToList() is [{ Name: "$and" or "$or" } property] && Filters(property.Value) is { } filters
				=> (property.Name, filters),
			_ => null,
		};

	// A filter given as a dictionary, written to JSON once when the client gets it, with the serializer, the options and the converters
	// of the requests, and kept as that JSON: the JSON that goes, which the checks of Chroma Cloud, the split of the long lists and the
	// Search API read whatever the types of the values. A value the client cannot write throws here.
	public static Dictionary<string, object> ToJson(IReadOnlyDictionary<string, object> filter)
		=> System.Text.Json.JsonSerializer.SerializeToElement(filter.ToDictionary(pair => pair.Key, pair => pair.Value), HttpClientHelpers.PostJsonSerializerOptions.TypeInfo<Dictionary<string, object>>())
			.EnumerateObject().ToDictionary(property => property.Name, property => (object)property.Value);

	private static object[]? Filters(object value) => value switch
	{
		object[] filters => filters,
		System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Array } json => json.EnumerateArray().Select(item => (object)item).ToArray(),
		_ => null,
	};
}
