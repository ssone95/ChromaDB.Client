using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChromaDB.Client.Models.Requests
{
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

		public bool HasKey(string key, StringComparison stringComparison = StringComparison.CurrentCulture) => _queryParams.Keys.Count > 0 
			&& _queryParams.Keys.Any(x => string.Equals(x, key, stringComparison));
		public bool HasKeyIgnoreCase(string key) => HasKey(key, StringComparison.InvariantCultureIgnoreCase);

		public IDictionary<string, string> Build()
		{
			return _queryParams;
		}
	}
}
