namespace ChromaDB.Client.Tests.Common;

internal class EmbeddingsComparer : IEqualityComparer<ReadOnlyMemory<float>>
{
	public static EmbeddingsComparer Instance { get; } = new();

	private EmbeddingsComparer()
	{ }

	public bool Equals(ReadOnlyMemory<float> lhs, ReadOnlyMemory<float> rhs)
	{
		if (lhs.Length != rhs.Length)
			return false;

		for (var i = 0; i < lhs.Length; i++)
		{
			if (!lhs.Span[i].Equals(rhs.Span[i]))
				return false;
		}

		return true;
	}

	public int GetHashCode(ReadOnlyMemory<float> obj) => obj.GetHashCode();
}
