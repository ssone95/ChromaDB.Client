namespace ChromaDB.Client;

/// <summary>
/// A filter on the documents of the records, sent as <c>where_document</c>.
/// The <c>&amp;</c> and <c>|</c> operators combine filters with <c>$and</c> and <c>$or</c>.
/// </summary>
public abstract class ChromaWhereDocumentOperator
{
	/// <summary>
	/// The Chroma operator of the filter, like <c>$contains</c> or <c>$and</c>.
	/// </summary>
	protected string Operator { get; }

	/// <summary>
	/// Creates a filter with the given Chroma operator.
	/// </summary>
	/// <param name="operator">The Chroma operator, like <c>$eq</c> or <c>$and</c>.</param>
	protected ChromaWhereDocumentOperator(string @operator)
	{
		Operator = @operator;
	}

	internal abstract Dictionary<string, object> ToWhereDocument();

	internal abstract ChromaWhereDocumentOperator Negate();

	/// <summary>
	/// A filter written as the dictionary of the <c>where_document</c> of the Chroma API, as the Python client takes it, like
	/// <c>{"$contains": "text"}</c> or <c>{"$and": [...]}</c>: nested filters as <c>Dictionary&lt;string, object&gt;</c>, lists as arrays.
	/// Get, query and delete send it as it is, with very long <c>$and</c> and <c>$or</c> lists split as for the other filters, and the
	/// server checks it. The Search API takes the filters on the documents in its where
	/// clause, on <c>#document</c>: there the client writes it as it writes the other filters, and for what it cannot write, like an
	/// operator other than <c>$contains</c>, <c>$not_contains</c>, <c>$regex</c>, <c>$not_regex</c>, <c>$and</c> and <c>$or</c>, throws a
	/// <c>NotSupportedException</c> without sending the search. <c>&amp;</c> and <c>|</c> join it to other filters; <c>Not</c> throws a
	/// <c>NotSupportedException</c>. The client writes the dictionary to JSON when it gets it: a value it cannot write throws there; later
	/// changes to it do not change the filter.
	/// </summary>
	/// <param name="whereDocument">The filter, as a dictionary.</param>
	/// <returns>The filter.</returns>
	public static ChromaWhereDocumentOperator FromDictionary(IReadOnlyDictionary<string, object> whereDocument)
		=> new ChromaWhereDocumentDictionaryOperator(Common.ChromaFilterLists.ToJson(whereDocument ?? throw new ArgumentNullException(nameof(whereDocument))));

	/// <summary>
	/// The documents that the filter does not match: Chroma has no <c>$not</c>, so the negation goes into the operators,
	/// <c>$contains</c> to <c>$not_contains</c>, <c>$regex</c> to <c>$not_regex</c>, and <c>$and</c> to <c>$or</c> of the negations.
	/// </summary>
	/// <param name="filter">The filter to negate.</param>
	/// <returns>The negated filter.</returns>
	public static ChromaWhereDocumentOperator Not(ChromaWhereDocumentOperator filter)
		=> filter.Negate();

	// The filter as it goes to the server, with the long lists split as the server takes them.
	internal Dictionary<string, object> ToRequestWhereDocument() => Common.ChromaFilterLists.Shape(ToWhereDocument());

	// The same filter in the where clause of the Search API, on the #document key: {"#document": {"$contains": "..."}}.
	internal abstract Dictionary<string, object> ToSearchWhere();

	internal Dictionary<string, object> ToRequestSearchWhere() => Common.ChromaFilterLists.Shape(ToSearchWhere());

	/// <summary>
	/// The JSON of the filter, as the client sends it in <c>where_document</c>.
	/// </summary>
	/// <returns>The JSON.</returns>
	public override string ToString()
		=> System.Text.Json.JsonSerializer.Serialize(ToRequestWhereDocument(), Common.HttpClientHelpers.TypeInfo<Dictionary<string, object>>(Common.HttpClientHelpers.PostJsonSerializerOptions));

	/// <summary>
	/// The documents that contain the character, with <c>$contains</c>. On Chroma Cloud the client throws an <c>ArgumentException</c>,
	/// as a text of one character gives the wrong records there (KD-57 in docs/COMPATIBILITY.md).
	/// </summary>
	/// <param name="value">The text the documents contain.</param>
	/// <returns>The filter.</returns>
	public static ChromaWhereDocumentOperator Contains(char value)
		=> Contains(value.ToString());
	/// <summary>
	/// The documents that contain the text, with <c>$contains</c>. On Chroma Cloud a text of fewer than 3 characters gives the wrong
	/// records once it has indexed them (KD-57 in docs/COMPATIBILITY.md), so the client throws an <c>ArgumentException</c> before the
	/// request: a <c>Regex</c> with the text, its special characters escaped, is right.
	/// </summary>
	/// <param name="value">The text the documents contain.</param>
	/// <returns>The filter.</returns>
	public static ChromaWhereDocumentOperator Contains(string value)
		=> new ChromaWhereDocumentStringOperator("$contains", "$not_contains", value);

	/// <summary>
	/// The documents that do not contain the character, with <c>$not_contains</c>. On Chroma Cloud the client throws an
	/// <c>ArgumentException</c>, as a text of one character gives the wrong records there (KD-57 in docs/COMPATIBILITY.md).
	/// </summary>
	/// <param name="value">The text the documents do not contain.</param>
	/// <returns>The filter.</returns>
	public static ChromaWhereDocumentOperator NotContains(char value)
		=> NotContains(value.ToString());
	/// <summary>
	/// The documents that do not contain the text, with <c>$not_contains</c>. On Chroma Cloud a text of fewer than 3 characters gives the
	/// wrong records once it has indexed them (KD-57 in docs/COMPATIBILITY.md), so the client throws an <c>ArgumentException</c> before
	/// the request: a <c>NotRegex</c> with the text, its special characters escaped, is right.
	/// </summary>
	/// <param name="value">The text the documents do not contain.</param>
	/// <returns>The filter.</returns>
	public static ChromaWhereDocumentOperator NotContains(string value)
		=> new ChromaWhereDocumentStringOperator("$not_contains", "$contains", value);

	/// <summary>
	/// The documents that match the regular expression, with <c>$regex</c>, from Chroma 1.0.12; the earlier versions reject it. On Chroma
	/// Cloud a pattern that takes an empty text, like <c>^$</c>, finds every record (KD-56 in docs/COMPATIBILITY.md), so the client throws
	/// an <c>ArgumentException</c> before the request, also for a pattern it cannot read as Chroma does: <c>Regex("(?s).")</c> finds the
	/// records with a document that is not empty.
	/// </summary>
	/// <param name="pattern">The regular expression.</param>
	/// <returns>The filter.</returns>
	public static ChromaWhereDocumentOperator Regex(string pattern)
		=> new ChromaWhereDocumentStringOperator("$regex", "$not_regex", pattern);
	/// <summary>
	/// The documents that do not match the regular expression, with <c>$not_regex</c>, from Chroma 1.0.12; the earlier versions reject it.
	/// On Chroma Cloud a pattern that takes an empty text finds no record (KD-56 in docs/COMPATIBILITY.md), so the client throws an
	/// <c>ArgumentException</c> before the request, also for a pattern it cannot read as Chroma does: <c>NotRegex("(?s).")</c> finds the
	/// records with an empty document or none.
	/// </summary>
	/// <param name="pattern">The regular expression.</param>
	/// <returns>The filter.</returns>
	public static ChromaWhereDocumentOperator NotRegex(string pattern)
		=> new ChromaWhereDocumentStringOperator("$not_regex", "$regex", pattern);

	/// <summary>
	/// Always <c>false</c>, so that <c>||</c> combines two filters with <c>$or</c>, like <c>|</c>.
	/// </summary>
	/// <param name="_">The filter.</param>
	/// <returns>Always false, so that <c>&amp;&amp;</c> and <c>||</c> combine both filters.</returns>
	public static bool operator true(ChromaWhereDocumentOperator _)
		=> false;
	/// <summary>
	/// Always <c>false</c>, so that <c>&amp;&amp;</c> combines two filters with <c>$and</c>, like <c>&amp;</c>.
	/// </summary>
	/// <param name="_">The filter.</param>
	/// <returns>Always false, so that <c>&amp;&amp;</c> and <c>||</c> combine both filters.</returns>
	public static bool operator false(ChromaWhereDocumentOperator _)
		=> false;

	/// <summary>
	/// The documents that match both filters, with <c>$and</c>.
	/// </summary>
	/// <param name="lhs">The first filter.</param>
	/// <param name="rhs">The second filter.</param>
	/// <returns>The filter that both filters pass, with <c>$and</c>.</returns>
	public static ChromaWhereDocumentOperator operator &(ChromaWhereDocumentOperator lhs, ChromaWhereDocumentOperator rhs)
		=> new ChromaWhereDocumentLogicalOperator("$and", lhs, rhs);

	/// <summary>
	/// The documents that match either filter, with <c>$or</c>.
	/// </summary>
	/// <param name="lhs">The first filter.</param>
	/// <param name="rhs">The second filter.</param>
	/// <returns>The filter that either filter passes, with <c>$or</c>.</returns>
	public static ChromaWhereDocumentOperator operator |(ChromaWhereDocumentOperator lhs, ChromaWhereDocumentOperator rhs)
		=> new ChromaWhereDocumentLogicalOperator("$or", lhs, rhs);
}

internal class ChromaWhereDocumentLogicalOperator : ChromaWhereDocumentOperator
{
	protected ChromaWhereDocumentOperator Lhs { get; }
	protected ChromaWhereDocumentOperator Rhs { get; }

	internal ChromaWhereDocumentLogicalOperator(string @operator, ChromaWhereDocumentOperator lhs, ChromaWhereDocumentOperator rhs)
		: base(@operator)
	{
		Lhs = lhs;
		Rhs = rhs;
	}

	// a & b & c goes as {"$and":[a,b,c]}, as for the filters on the metadata.
	internal override Dictionary<string, object> ToWhereDocument()
		=> new()
		{
			{ Operator, Operands().Select(x => (object)x.ToWhereDocument()).ToArray() }
		};

	internal override Dictionary<string, object> ToSearchWhere()
		=> new()
		{
			{ Operator, Operands().Select(x => (object)x.ToSearchWhere()).ToArray() }
		};

	// !(a & b) is !a | !b, and !(a | b) is !a & !b.
	internal override ChromaWhereDocumentOperator Negate()
		=> Operands().Select(x => x.Negate()).Aggregate((left, right) => Operator == "$and" ? left | right : left & right);

	// The filters of a chain of the same operator, in their order, without recursion for a long chain.
	private List<ChromaWhereDocumentOperator> Operands()
	{
		var operands = new List<ChromaWhereDocumentOperator>();
		var stack = new Stack<ChromaWhereDocumentOperator>([Rhs, Lhs]);
		while (stack.Count > 0)
		{
			var filter = stack.Pop();
			if (filter is ChromaWhereDocumentLogicalOperator logical && logical.Operator == Operator)
			{
				stack.Push(logical.Rhs);
				stack.Push(logical.Lhs);
			}
			else
			{
				operands.Add(filter);
			}
		}
		return operands;
	}
}

internal class ChromaWhereDocumentStringOperator : ChromaWhereDocumentOperator
{
	protected string String { get; }
	// The operator of the negation, like $not_contains for $contains: Chroma has no $not.
	protected string NegatedOperator { get; }

	internal ChromaWhereDocumentStringOperator(string @operator, string negatedOperator, string @string)
		: base(@operator)
	{
		NegatedOperator = negatedOperator;
		String = @string;
	}

	internal override ChromaWhereDocumentOperator Negate()
		=> new ChromaWhereDocumentStringOperator(NegatedOperator, Operator, String);

	internal override Dictionary<string, object> ToWhereDocument()
		=> new()
		{
			{ Operator, String }
		};

	internal override Dictionary<string, object> ToSearchWhere()
		=> new()
		{
			{ ChromaSearchKeys.Document, new Dictionary<string, object> { { Operator, String } } }
		};
}

// A filter on the documents given as the dictionary of the where_document of the Chroma API: sent as it is, and written for the
// Search API on #document, as the other filters are.
internal sealed class ChromaWhereDocumentDictionaryOperator(IReadOnlyDictionary<string, object> whereDocument) : ChromaWhereDocumentOperator("dictionary")
{
	internal override Dictionary<string, object> ToWhereDocument() => whereDocument.ToDictionary(x => x.Key, x => x.Value);

	internal override Dictionary<string, object> ToSearchWhere() => ToSearchWhere(whereDocument);

	internal override ChromaWhereDocumentOperator Negate()
		=> throw new NotSupportedException("Not cannot negate a filter on the documents given as a dictionary: Chroma has no $not.");

	// {"$and"|"$or": [filters]} keeps its operator, and {"$contains"|"$not_contains"|"$regex"|"$not_regex": text} goes on #document. The
	// dictionary at the top has the JSON of the values, which FromDictionary wrote, and below it is all JSON.
	private static Dictionary<string, object> ToSearchWhere(object node)
	{
		var pairs = (node switch
		{
			System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Object } json => json.EnumerateObject().Select(property => new KeyValuePair<string, object>(property.Name, property.Value)),
			IEnumerable<KeyValuePair<string, object>> filter => filter,
			_ => throw Unwritable("has a filter that is not a dictionary"),
		}).Take(2).ToList();
		if (pairs.Count != 1)
		{
			throw Unwritable($"has a filter with {(pairs.Count == 0 ? "no operator" : "more than one operator")}: Chroma takes one in each");
		}
		var pair = pairs[0];
		return pair.Key switch
		{
			"$and" or "$or" when pair.Value is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Array } list
				=> new() { { pair.Key, list.EnumerateArray().Select(x => (object)ToSearchWhere(x)).ToArray() } },
			"$contains" or "$not_contains" or "$regex" or "$not_regex" when pair.Value is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } text
				=> new() { { ChromaSearchKeys.Document, new Dictionary<string, object> { { pair.Key, text.GetString()! } } } },
			"$and" or "$or" => throw Unwritable($"has {pair.Key} with a value that is not a list of filters"),
			"$contains" or "$not_contains" or "$regex" or "$not_regex" => throw Unwritable($"has {pair.Key} with a value that is not a text"),
			_ => throw Unwritable($"has {pair.Key}, which the client cannot write for the Search API: it writes $contains, $not_contains, $regex, $not_regex, $and and $or. ChromaWhereOperator.FromDictionary sends a where clause of the Search API as it is, with #document"),
		};
	}

	private static NotSupportedException Unwritable(string what)
		=> new($"The filter on the documents given as a dictionary {what}.");
}
