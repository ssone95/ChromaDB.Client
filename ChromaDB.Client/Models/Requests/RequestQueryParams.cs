using System.Globalization;

namespace ChromaDB.Client.Models.Requests;

public class RequestQueryParams
{
	private IDictionary<string, string> _queryParams;

	public RequestQueryParams(IDictionary<string, string> queryParams)
	{
		_queryParams = queryParams;
	}

	public RequestQueryParams()
	{
		_queryParams = new Dictionary<string, string>();
	}

	public RequestQueryParams Add(string key, string value)
	{
		string urlEncodedQueryParam = Uri.EscapeDataString(value);
		_queryParams[key] = urlEncodedQueryParam;
		return this;
	}
	public RequestQueryParams Add(string key, IFormattable value) => Add(key, value.ToString(null, CultureInfo.InvariantCulture));

	public bool HasKey(string key, StringComparison stringComparison = StringComparison.InvariantCulture)
		=> _queryParams.Keys.Any(x => string.Equals(x, key, stringComparison));
	public bool HasKeyIgnoreCase(string key) => HasKey(key, StringComparison.InvariantCultureIgnoreCase);

	public IDictionary<string, string> Build() => _queryParams;
}
