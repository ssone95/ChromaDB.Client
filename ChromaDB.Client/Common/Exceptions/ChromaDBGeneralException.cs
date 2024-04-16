using System.Runtime.Serialization;

namespace ChromaDB.Client.Common.Exceptions;

public class ChromaDBGeneralException : Exception
{
	public ChromaDBGeneralException()
	{
	}

	public ChromaDBGeneralException(string? message) : base(message)
	{
	}

	public ChromaDBGeneralException(string? message, Exception? innerException) : base(message, innerException)
	{
	}

	[Obsolete("This exception constructor is obsolete, and shouldn't be used!")]
	protected ChromaDBGeneralException(SerializationInfo info, StreamingContext context) : base(info, context)
	{
	}
}
