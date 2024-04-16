using System.Net;

namespace ChromaDB.Client;

public class BaseResponse<T>
{
	public T? Data { get; init; }
	public bool Success => StatusCode < HttpStatusCode.BadRequest && StatusCode >= HttpStatusCode.OK;
	public HttpStatusCode StatusCode { get; init; }
	public string? StatusReason { get; init; }

	public BaseResponse(T? data, HttpStatusCode statusCode = HttpStatusCode.OK, string? reasonPhrase = null)
	{
		Data = data;
		StatusCode = statusCode;
		StatusReason = reasonPhrase;
	}
}
