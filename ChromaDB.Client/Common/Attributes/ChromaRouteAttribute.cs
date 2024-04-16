using System.Text.RegularExpressions;

namespace ChromaDB.Client.Common.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public abstract partial class ChromaRouteAttribute : Attribute
{
	private string _endpoint;
	private IReadOnlyList<string> _queryParams;

	public HttpMethod Method { get; }
	public required Type Source { get; init; }
	public Type? RequestType { get; init; }
	public required Type ResponseType { get; init; }

	public string Endpoint
	{
		get => _endpoint;
		init
		{
			ArgumentException.ThrowIfNullOrEmpty(value, nameof(Endpoint));

			_endpoint = value;
			_queryParams = PrepareQueryParams(_endpoint);
		}
	}

	public IReadOnlyList<string> QueryParams => _queryParams;

	public ChromaRouteAttribute(string method)
	{
		_endpoint = string.Empty;
		_queryParams = [];
		Method = HttpMethod.Parse(method);
	}

	private static List<string> PrepareQueryParams(string input)
		=> PrepareQueryParamsRegex().Matches(input)
			.Select(x => x.Value)
			.ToList();

	[GeneratedRegex(@"{[a-zA-Z0-9\-_]+}", RegexOptions.CultureInvariant)]
	private static partial Regex PrepareQueryParamsRegex();
}

public sealed class ChromaGetRouteAttribute() : ChromaRouteAttribute("GET");
public sealed class ChromaPostRouteAttribute() : ChromaRouteAttribute("POST");
