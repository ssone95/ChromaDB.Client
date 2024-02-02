using ChromaDB.Client.Common.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChromaDB.Client.Common.Attributes
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
	public class ChromaRouteAttribute : Attribute
	{
		private HttpMethod _method;
		public required Type Source { get; init; }
		public required Type ResponseType { get; init; }
		private string _endpoint { get; init; }
		public string TargetEndpoint
		{
			get { return _endpoint; }
			init
			{
				_endpoint = value;
				if (string.IsNullOrEmpty(_endpoint))
				{
					throw new ChromaDBException($"Property {nameof(TargetEndpoint)} cannot be null!");
				}
				_queryParams = PrepareQueryParams(_endpoint);
			}
		}

		private static List<string> PrepareQueryParams(string input)
		{
			List<string> queryParams = new();

			MatchCollection matches = Regex.Matches(input, "{[a-zA-Z0-9\\-_]+}", RegexOptions.IgnoreCase|RegexOptions.CultureInvariant|RegexOptions.Singleline);
			if (matches.Count > 0)
			{
				foreach (Match match in matches)
				{
					queryParams.Add(match.Value);
				}
			}
			return queryParams;
		}

		private IReadOnlyList<string> _queryParams = new List<string>();
		public IReadOnlyList<string> QueryParams() => _queryParams;

		public ChromaRouteAttribute(string method)
		{
			_endpoint = string.Empty;
			_method = HttpMethod.Parse(method);
		}

		public HttpMethod Method() => _method;
	}
}
