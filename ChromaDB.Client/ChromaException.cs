using System.Net;

namespace ChromaDB.Client;

/// <summary>
/// The error of a failed request, with the status code and the kind of error from the answer of the server, when there is one.
/// </summary>
public class ChromaException : Exception
{
	/// <summary>
	/// An exception without a message.
	/// </summary>
	public ChromaException() { }
	/// <summary>
	/// An exception with the given message.
	/// </summary>
	/// <param name="message">The message.</param>
	public ChromaException(string? message) : base(message) { }
	/// <summary>
	/// An exception with the given message and the exception that caused it.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="inner">The exception that caused this one.</param>
	public ChromaException(string? message, Exception? inner) : base(message, inner) { }

	/// <summary>
	/// The status code of the answer of the server; null when there was no answer, like on a timeout, or when the client found the error.
	/// </summary>
	public HttpStatusCode? StatusCode { get; init; }

	/// <summary>
	/// The kind of error that the server names, like <c>NotFoundError</c> or <c>InvalidArgumentError</c> from Chroma 1.x,
	/// <c>InvalidCollection</c> from Chroma 0.5 and 0.6, <c>ValueError</c> from the v1 API of Chroma 0.4; null when it names none.
	/// </summary>
	public string? ErrorType { get; init; }

	// Works around KD-42 and KD-43 (docs/COMPATIBILITY.md)
	// A missing collection: 404 from Chroma 1.x, 400 or 500 from the 0.x servers, always with "does not exist" in the message, also when
	// the tenant or the database is missing. A bare 404, like the one of a wrong address, is not one.
	// Part of the operation went before the error, as the first batches of a write: it is not run again.
	internal bool PartlyDone { get; init; }

	// How many records of a write in batches went before the error, which is the inner exception.
	internal int? RecordsWent { get; init; }

	// An answer to a read whose lists do not have one item for every record (AnswerChecks): the read goes again.
	internal bool UnevenAnswer { get; init; }

	internal bool IsMissingCollection
		=> StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError
			&& (Message.Contains("does not exist") || ErrorType == "NotFoundError" && Message.StartsWith("Collection", StringComparison.Ordinal));
}