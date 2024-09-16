using System.Net;

namespace ChromaDB.Client;

public class BaseResponse<T>
{
	public T? Data { get; }
	public HttpStatusCode StatusCode { get; }
	public string? ReasonPhrase { get; }

	public bool Success => StatusCode < HttpStatusCode.BadRequest && StatusCode >= HttpStatusCode.OK;

	public BaseResponse(T? data, HttpStatusCode statusCode = HttpStatusCode.OK, string? reasonPhrase = null)
	{
		Data = data;
		StatusCode = statusCode;
		ReasonPhrase = reasonPhrase;
	}
}

public static class BaseResponse
{
	public class None
	{
		public static readonly None Instance = new None();

		private None()
		{ }
	}
}
