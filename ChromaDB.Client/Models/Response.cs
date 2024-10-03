using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace ChromaDB.Client.Models;

public sealed class Response<T>
{
	public HttpStatusCode StatusCode { get; }
	public T? Data { get; }
	public string? ErrorMessage { get; }

	[MemberNotNullWhen(true, nameof(Data))]
	public bool Success => StatusCode < HttpStatusCode.BadRequest && StatusCode >= HttpStatusCode.OK;

	public Response(HttpStatusCode statusCode, T? data = default, string? errorMessage = null)
	{
		StatusCode = statusCode;
		Data = data;
		ErrorMessage = errorMessage;
		if (Success && Data is null)
			throw new InvalidOperationException();
	}
}

public static class Response
{
	public class Empty
	{
		public static readonly Empty Instance = new();

		private Empty()
		{ }
	}
}
