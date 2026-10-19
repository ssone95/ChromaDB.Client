namespace ChromaDB.Client;

/// <summary>
/// A filter on the metadata of the records, sent as <c>where</c>.
/// The <c>&amp;</c> and <c>|</c> operators combine filters with <c>$and</c> and <c>$or</c>.
/// It can also filter the ids, with <c>Equal</c> and <c>In</c> on <c>ChromaSearchKeys.Id</c>, and the documents, with <c>Document</c>,
/// as the where clause of the Search API does. Get, query and delete take neither in their where clause: the client sends them as
/// the ids and the <c>where_document</c> of the request, which takes them only joined to the other conditions with <c>&amp;</c>.
/// </summary>
public abstract class ChromaWhereOperator
{
	/// <summary>
	/// The Chroma operator of the filter, like <c>$eq</c> or <c>$and</c>.
	/// </summary>
	protected string Operator { get; }

	/// <summary>
	/// Creates a filter with the given Chroma operator.
	/// </summary>
	/// <param name="operator">The Chroma operator, like <c>$eq</c> or <c>$and</c>.</param>
	protected ChromaWhereOperator(string @operator)
	{
		Operator = @operator;
	}

	internal abstract Dictionary<string, object> ToWhere();

	// The filter as it goes to the server, with the long lists split as the server takes them.
	internal Dictionary<string, object> ToRequestWhere() => Common.ChromaFilterLists.Shape(ToWhere());

	// The where clause of a request: none for All, which matches every record. A request with None is not sent.
	internal static Dictionary<string, object>? ToRequestWhere(ChromaWhereOperator? where)
		=> where is null || where == All ? null : where.ToRequestWhere();

	// The conditions joined with $and at the top of the filter: the filter itself for any other.
	internal virtual IEnumerable<ChromaWhereOperator> Conjuncts() => [this];

	// Whether the filter has a condition on the ids or on the documents, which only the Search API takes in its where clause.
	internal virtual bool HasSearchOnlyCondition => false;

	// The filter as a filter on the documents, when it has only conditions on the documents; null otherwise.
	internal virtual ChromaWhereDocumentOperator? AsDocumentFilter() => null;

	// For get, query and delete, whose where clause takes neither ids nor documents: the conditions on the ids, with Equal and In, and
	// the ones on the documents, joined to the others with $and, go to the ids and to where_document of the request. The ids of the
	// conditions and the given ones are the ones in all of them; with none left, the filter is None.
	internal static (ChromaWhereOperator? Where, ChromaWhereDocumentOperator? WhereDocument, IReadOnlyList<string>? Ids) Split(
		ChromaWhereOperator? where, ChromaWhereDocumentOperator? whereDocument, IReadOnlyList<string>? ids)
	{
		if (where is null || !where.HasSearchOnlyCondition)
		{
			return (where, whereDocument, ids);
		}
		var rest = All;
		var keptIds = ids;
		foreach (var condition in where.Conjuncts())
		{
			if (condition is ChromaWhereValueOperator value && value.Ids() is { } conditionIds)
			{
				keptIds = keptIds is null ? conditionIds.Distinct().ToList() : keptIds.Intersect(conditionIds).ToList();
			}
			else if (condition.AsDocumentFilter() is { } document)
			{
				whereDocument = whereDocument is null ? document : whereDocument & document;
			}
			else if (condition.HasSearchOnlyCondition)
			{
				throw new NotSupportedException(
					"Get, query and delete take a condition on the ids, with Equal or In, or on the documents only joined to the other conditions with &: the Search API takes them anywhere.");
			}
			else
			{
				rest &= condition;
			}
		}
		return keptIds is [] ? (None, null, null) : (rest, whereDocument, keptIds);
	}

	/// <summary>
	/// The records whose document the filter matches, as a condition of the where clause on <c>ChromaSearchKeys.Document</c>, which
	/// the Search API takes. For get, query and delete the client sends it as <c>where_document</c>.
	/// </summary>
	/// <param name="filter">The filter on the documents.</param>
	/// <returns>The filter.</returns>
	public static ChromaWhereOperator Document(ChromaWhereDocumentOperator filter)
		=> new ChromaWhereDocumentFilter(filter ?? throw new ArgumentNullException(nameof(filter)));

	/// <summary>
	/// A filter written as the dictionary of the <c>where</c> of the Chroma API, as the Python client takes it, like
	/// <c>{"year": {"$gte": 2020}}</c> or <c>{"$and": [...]}</c>: nested filters as <c>Dictionary&lt;string, object&gt;</c>, lists as
	/// arrays. The client sends it as it is, with very long <c>$and</c> and <c>$or</c> lists split as for the other filters, and does not
	/// check it: the server does. <c>&amp;</c> and <c>|</c> join it to other filters; <c>Not</c> throws a <c>NotSupportedException</c>.
	/// The client copies the dictionary, so later changes to it do not change the filter; it reads each list once.
	/// </summary>
	/// <param name="where">The filter, as a dictionary.</param>
	/// <returns>The filter.</returns>
	public static ChromaWhereOperator FromDictionary(IReadOnlyDictionary<string, object> where)
		=> new ChromaWhereDictionaryOperator(Common.ChromaFilterLists.Copy(where ?? throw new ArgumentNullException(nameof(where))));

	/// <summary>
	/// The filter that matches every record: the client sends no <c>where</c>, which Chroma has no value for. It is a single instance,
	/// which <c>&amp;</c>, <c>|</c> and <c>Not</c> return when the filter they make matches every record.
	/// </summary>
	public static ChromaWhereOperator All { get; } = new ChromaWhereConstantOperator(matchesAll: true);

	/// <summary>
	/// The filter that matches no record: a read with it returns nothing and a delete deletes nothing, without a request, as Chroma has
	/// no value for it. It is a single instance, which <c>&amp;</c>, <c>|</c>, <c>Not</c> and <c>In</c> without values return when the
	/// filter they make matches no record.
	/// </summary>
	public static ChromaWhereOperator None { get; } = new ChromaWhereConstantOperator(matchesAll: false);

	/// <summary>
	/// The records that the filter does not match, as Chroma can tell them: Chroma has no <c>$not</c>, so the negation goes into the
	/// operators, <c>$eq</c> to <c>$ne</c>, <c>$gt</c> to <c>$lte</c>, <c>$in</c> to <c>$nin</c>, <c>$contains</c> to <c>$not_contains</c>,
	/// and <c>$and</c> to <c>$or</c> of the negations. <c>$ne</c>, <c>$nin</c> and <c>$not_contains</c> match the records without the key,
	/// <c>$ne</c> and <c>$nin</c> from Chroma 0.5.15, but a comparison like <c>$lte</c> does not: <c>Not(GreaterThan(key, 5))</c> leaves out
	/// the records without the key, as <c>GreaterThan(key, 5)</c> does.
	/// </summary>
	/// <param name="filter">The filter to negate.</param>
	/// <returns>The negated filter.</returns>
	public static ChromaWhereOperator Not(ChromaWhereOperator filter)
		=> filter.Negate();

	internal abstract ChromaWhereOperator Negate();

	// Works around KD-46 (docs/COMPATIBILITY.md)
	/// <summary>
	/// The records whose value for the key is one of the values, with <c>$in</c>. Without values it is <c>None</c>: every tested Chroma
	/// rejects <c>$in</c> without values.
	/// </summary>
	/// <param name="key">The metadata key.</param>
	/// <param name="values">The values, one of which the key has.</param>
	/// <returns>The filter.</returns>
	public static ChromaWhereOperator In(string key, params object[] values)
		=> values is { Length: > 0 } ? new ChromaWhereValueOperator(key, "$in", "$nin", values) : None;

	// Works around KD-46 (docs/COMPATIBILITY.md)
	/// <summary>
	/// The records whose value for the key is not one of the values, with <c>$nin</c>, which matches the records without the key too from
	/// Chroma 0.5.15.
	/// Without values it is <c>All</c>: every tested Chroma rejects <c>$nin</c> without values.
	/// </summary>
	/// <param name="key">The metadata key.</param>
	/// <param name="values">The values, none of which the key has.</param>
	/// <returns>The filter.</returns>
	public static ChromaWhereOperator NotIn(string key, params object[] values)
		=> values is { Length: > 0 } ? new ChromaWhereValueOperator(key, "$nin", "$in", values) : All;

	/// <summary>
	/// The JSON of the filter, as the client sends it in <c>where</c>; <c>true</c> for <c>All</c> and <c>false</c> for <c>None</c>,
	/// which the client sends no <c>where</c> for.
	/// </summary>
	/// <returns>The JSON.</returns>
	public override string ToString()
		=> System.Text.Json.JsonSerializer.Serialize(ToRequestWhere(), Common.HttpClientHelpers.TypeInfo<Dictionary<string, object>>(Common.HttpClientHelpers.PostJsonSerializerOptions));

	/// <summary>
	/// The records whose value for the key is greater than the value, with <c>$gt</c>.
	/// </summary>
	/// <param name="key">The metadata key.</param>
	/// <param name="value">The value to compare with.</param>
	/// <returns>The filter.</returns>
	public static ChromaWhereOperator GreaterThan(string key, object value)
		=> new ChromaWhereValueOperator(key, "$gt", "$lte", value);

	/// <summary>
	/// The records whose value for the key is greater than or equal to the value, with <c>$gte</c>.
	/// </summary>
	/// <param name="key">The metadata key.</param>
	/// <param name="value">The value to compare with.</param>
	/// <returns>The filter.</returns>
	public static ChromaWhereOperator GreaterThanOrEqual(string key, object value)
		=> new ChromaWhereValueOperator(key, "$gte", "$lt", value);

	/// <summary>
	/// The records whose value for the key is less than the value, with <c>$lt</c>.
	/// </summary>
	/// <param name="key">The metadata key.</param>
	/// <param name="value">The value to compare with.</param>
	/// <returns>The filter.</returns>
	public static ChromaWhereOperator LessThan(string key, object value)
		=> new ChromaWhereValueOperator(key, "$lt", "$gte", value);

	/// <summary>
	/// The records whose value for the key is less than or equal to the value, with <c>$lte</c>.
	/// </summary>
	/// <param name="key">The metadata key.</param>
	/// <param name="value">The value to compare with.</param>
	/// <returns>The filter.</returns>
	public static ChromaWhereOperator LessThanOrEqual(string key, object value)
		=> new ChromaWhereValueOperator(key, "$lte", "$gt", value);

	/// <summary>
	/// The records whose value for the key equals the value, with <c>$eq</c>.
	/// </summary>
	/// <param name="key">The metadata key.</param>
	/// <param name="value">The value to compare with.</param>
	/// <returns>The filter.</returns>
	public static ChromaWhereOperator Equal(string key, object value)
		=> new ChromaWhereValueOperator(key, "$eq", "$ne", value);

	/// <summary>
	/// The records whose value for the key does not equal the value, with <c>$ne</c>.
	/// </summary>
	/// <param name="key">The metadata key.</param>
	/// <param name="value">The value to compare with.</param>
	/// <returns>The filter.</returns>
	public static ChromaWhereOperator NotEqual(string key, object value)
		=> new ChromaWhereValueOperator(key, "$ne", "$eq", value);

	/// <summary>
	/// The records whose list in the metadata contains the value, with <c>$contains</c>: Chroma 1.5.0 and later.
	/// </summary>
	/// <param name="key">The metadata key.</param>
	/// <param name="value">The value the list of the key holds.</param>
	/// <returns>The filter.</returns>
	public static ChromaWhereOperator Contains(string key, object value)
		=> new ChromaWhereValueOperator(key, "$contains", "$not_contains", value);

	/// <summary>
	/// The records whose list in the metadata does not contain the value, with <c>$not_contains</c>: Chroma 1.5.0 and later.
	/// </summary>
	/// <param name="key">The metadata key.</param>
	/// <param name="value">The value the list of the key does not hold.</param>
	/// <returns>The filter.</returns>
	public static ChromaWhereOperator NotContains(string key, object value)
		=> new ChromaWhereValueOperator(key, "$not_contains", "$contains", value);

	/// <summary>
	/// Always <c>false</c>, so that <c>||</c> combines two filters with <c>$or</c>, like <c>|</c>.
	/// </summary>
	/// <param name="_">The filter.</param>
	/// <returns>Always false, so that <c>&amp;&amp;</c> and <c>||</c> combine both filters.</returns>
	public static bool operator true(ChromaWhereOperator _)
		=> false;
	/// <summary>
	/// Always <c>false</c>, so that <c>&amp;&amp;</c> combines two filters with <c>$and</c>, like <c>&amp;</c>.
	/// </summary>
	/// <param name="_">The filter.</param>
	/// <returns>Always false, so that <c>&amp;&amp;</c> and <c>||</c> combine both filters.</returns>
	public static bool operator false(ChromaWhereOperator _)
		=> false;

	/// <summary>
	/// The records that match both filters, with <c>$and</c>. With <c>None</c> it is <c>None</c>, and with <c>All</c> the other filter.
	/// </summary>
	/// <param name="lhs">The first filter.</param>
	/// <param name="rhs">The second filter.</param>
	/// <returns>The filter that both filters pass, with <c>$and</c>.</returns>
	public static ChromaWhereOperator operator &(ChromaWhereOperator lhs, ChromaWhereOperator rhs)
		=> lhs == None || rhs == None ? None
			: lhs == All ? rhs
			: rhs == All ? lhs
			: new ChromaWhereLogicalOperator("$and", lhs, rhs);

	/// <summary>
	/// The records that match either filter, with <c>$or</c>. With <c>All</c> it is <c>All</c>, and with <c>None</c> the other filter.
	/// </summary>
	/// <param name="lhs">The first filter.</param>
	/// <param name="rhs">The second filter.</param>
	/// <returns>The filter that either filter passes, with <c>$or</c>.</returns>
	public static ChromaWhereOperator operator |(ChromaWhereOperator lhs, ChromaWhereOperator rhs)
		=> lhs == All || rhs == All ? All
			: lhs == None ? rhs
			: rhs == None ? lhs
			: new ChromaWhereLogicalOperator("$or", lhs, rhs);
}

// All and None: Chroma has no where clause for either, so the client sends none for All and no request for None.
internal sealed class ChromaWhereConstantOperator(bool matchesAll) : ChromaWhereOperator(matchesAll ? "true" : "false")
{
	internal override Dictionary<string, object> ToWhere()
		=> throw new InvalidOperationException($"The client sends no where clause for {(matchesAll ? "All" : "None")}.");

	internal override ChromaWhereOperator Negate() => matchesAll ? None : All;

	public override string ToString() => Operator;
}

internal class ChromaWhereLogicalOperator : ChromaWhereOperator
{
	protected ChromaWhereOperator Lhs { get; }
	protected ChromaWhereOperator Rhs { get; }

	internal ChromaWhereLogicalOperator(string @operator, ChromaWhereOperator lhs, ChromaWhereOperator rhs)
		: base(@operator)
	{
		Lhs = lhs;
		Rhs = rhs;
	}

	// a & b & c goes as {"$and":[a,b,c]}, as the Python client writes it: each & inside the next would add a level for each filter,
	// and System.Text.Json stops at 64 levels, so a chain of 32 filters failed.
	internal override Dictionary<string, object> ToWhere()
		=> new()
		{
			{ Operator, Operands().Select(x => (object)x.ToWhere()).ToArray() }
		};

	// !(a & b) is !a | !b, and !(a | b) is !a & !b.
	internal override ChromaWhereOperator Negate()
		=> Operands().Select(x => x.Negate()).Aggregate((left, right) => Operator == "$and" ? left | right : left & right);

	internal override IEnumerable<ChromaWhereOperator> Conjuncts()
		=> Operator == "$and" ? Operands() : [this];

	internal override bool HasSearchOnlyCondition
		=> Operands().Any(x => x.HasSearchOnlyCondition);

	internal override ChromaWhereDocumentOperator? AsDocumentFilter()
	{
		var filters = Operands().Select(x => x.AsDocumentFilter()).ToList();
		return filters.Contains(null) ? null : filters.Aggregate((left, right) => Operator == "$and" ? left! & right! : left! | right!);
	}

	// The filters of a chain of the same operator, in their order, without recursion for a long chain.
	private List<ChromaWhereOperator> Operands()
	{
		var operands = new List<ChromaWhereOperator>();
		var stack = new Stack<ChromaWhereOperator>([Rhs, Lhs]);
		while (stack.Count > 0)
		{
			var filter = stack.Pop();
			if (filter is ChromaWhereLogicalOperator logical && logical.Operator == Operator)
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

internal class ChromaWhereValueOperator : ChromaWhereOperator
{
	protected string Key { get; }
	protected object Value { get; }
	// The operator of the negation, like $ne for $eq: Chroma has no $not.
	protected string NegatedOperator { get; }

	internal ChromaWhereValueOperator(string key, string @operator, string negatedOperator, object value)
		: base(@operator)
	{
		Key = key;
		NegatedOperator = negatedOperator;
		Value = value;
	}

	internal override Dictionary<string, object> ToWhere()
		=> new()
		{
			{ Key, new Dictionary<string, object> { { Operator, Value } } }
		};

	internal override ChromaWhereOperator Negate()
		=> new ChromaWhereValueOperator(Key, NegatedOperator, Operator, Value);

	internal override bool HasSearchOnlyCondition => Key == ChromaSearchKeys.Id;

	// The ids of a condition on the ids with $eq or $in, which get, query and delete send as their ids; null for any other.
	internal IReadOnlyList<string>? Ids()
		=> Key != ChromaSearchKeys.Id ? null
			: Operator == "$eq" ? [Id(Value)]
			: Operator == "$in" ? ((IEnumerable<object>)Value).Select(Id).ToList()
			: null;

	private static string Id(object id)
		=> id as string ?? throw new ArgumentException("The ids of the records are strings.", nameof(id));
}

// A filter on the documents in the where clause, which the Search API takes on #document.
internal sealed class ChromaWhereDocumentFilter(ChromaWhereDocumentOperator filter) : ChromaWhereOperator(ChromaSearchKeys.Document)
{
	internal override Dictionary<string, object> ToWhere() => filter.ToSearchWhere();

	internal override ChromaWhereOperator Negate() => new ChromaWhereDocumentFilter(ChromaWhereDocumentOperator.Not(filter));

	internal override bool HasSearchOnlyCondition => true;

	internal override ChromaWhereDocumentOperator? AsDocumentFilter() => filter;
}

// A filter given as the dictionary of the where of the Chroma API, sent as it is.
internal sealed class ChromaWhereDictionaryOperator(IReadOnlyDictionary<string, object> where) : ChromaWhereOperator("dictionary")
{
	internal override Dictionary<string, object> ToWhere() => where.ToDictionary(x => x.Key, x => x.Value);

	internal override ChromaWhereOperator Negate()
		=> throw new NotSupportedException("Not cannot negate a filter given as a dictionary: Chroma has no $not.");
}
