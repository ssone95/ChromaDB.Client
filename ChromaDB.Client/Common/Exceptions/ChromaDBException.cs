using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ChromaDB.Client.Common.Exceptions
{
	public class ChromaDBException : HttpRequestException
	{
		public string? ErrorMessageBody { get; init; }
		public ChromaDBException()
		{
		}

		public ChromaDBException(string? message) : base(message)
		{
		}

		public ChromaDBException(string? message, Exception? inner) : base(message, inner)
		{
		}

		public ChromaDBException(string? message, Exception? inner, HttpStatusCode? statusCode) : base(message, inner, statusCode)
		{
		}

		public ChromaDBException(HttpRequestError httpRequestError, string? message = null, Exception? inner = null, HttpStatusCode? statusCode = null) : base(httpRequestError, message, inner, statusCode)
		{
		}
	}
}
