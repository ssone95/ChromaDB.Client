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
