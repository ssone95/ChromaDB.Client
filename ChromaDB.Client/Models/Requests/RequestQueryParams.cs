using System.Globalization;

namespace ChromaDB.Client.Models.Requests;

public class RequestQueryParams
{
	private Dictionary<string, string> _queryParams;

	public RequestQueryParams()
	{
		_queryParams = new Dictionary<string, string>(StringComparer.Ordinal);
	}

	public RequestQueryParams Insert(string key, string value)
	{
		_queryParams[key] = value;
		return this;
	}
	public RequestQueryParams Insert(string key, IFormattable value) => Insert(key, value.ToString(null, CultureInfo.InvariantCulture));

	public IDictionary<string, string> Build() => _queryParams;
}
