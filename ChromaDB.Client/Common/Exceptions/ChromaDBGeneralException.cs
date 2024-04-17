namespace ChromaDB.Client.Common.Exceptions;

public sealed class ChromaDBGeneralException : Exception
{
	public ChromaDBGeneralException()
	{ }

	public ChromaDBGeneralException(string? message) : base(message)
	{ }

	public ChromaDBGeneralException(string? message, Exception? innerException) : base(message, innerException)
	{ }
}