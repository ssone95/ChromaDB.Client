namespace ChromaDB.Client;

/// <summary>
/// How a read goes again after an error that can pass, with the exponential backoff with full jitter of AWS ("Exponential Backoff And
/// Jitter"): before each new attempt it waits a random time between zero and the smaller of <c>MaxDelay</c> and <c>BaseDelay</c> times 2
/// to the number of the attempts that went again before. The reads are each request of <c>GetAsync</c>, <c>PeekAsync</c>,
/// <c>QueryAsync</c>, <c>SearchAsync</c> and <c>CountAsync</c>, and the reads that a write makes first, as the get of
/// <c>ChromaUpsertStrategy.SkipUnchangedEmbeddings</c>, the get of <c>ChromaConfigurationOptions.ReadsBeyondKeyQuota</c> and the
/// pages of ids of <c>DeleteCollectionAsync</c> with <c>deleteRecordsFirst</c>. The errors that can pass are an answer <c>429</c>,
/// <c>500</c>, <c>502</c>, <c>503</c> or <c>504</c>, other than a missing collection, a request that fails on the network, and an
/// answer whose lists do not have one item for every record. After the last attempt the error stands: for such an answer, a
/// <c>ChromaException</c> that says it. A timeout of the <c>HttpClient</c> does not go again. A write never goes again: one that
/// failed may have gone.
/// </summary>
public sealed class ChromaReadRetries
{
	/// <summary>
	/// The attempts and the delays.
	/// </summary>
	/// <param name="maxAttempts">How many times a read goes at most, the first included: 1 never goes again.</param>
	/// <param name="baseDelay">The largest delay before the second attempt; each following one can double it.</param>
	/// <param name="maxDelay">The largest delay before any attempt, at least <c>baseDelay</c>.</param>
	public ChromaReadRetries(int maxAttempts, TimeSpan baseDelay, TimeSpan maxDelay)
	{
		if (maxAttempts < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(maxAttempts), "A read goes at least once.");
		}
		if (baseDelay < TimeSpan.Zero || maxDelay < baseDelay)
		{
			throw new ArgumentOutOfRangeException(baseDelay < TimeSpan.Zero ? nameof(baseDelay) : nameof(maxDelay), "The delays cannot be negative, and the largest one is at least the base.");
		}
		MaxAttempts = maxAttempts;
		BaseDelay = baseDelay;
		MaxDelay = maxDelay;
	}

	/// <summary>
	/// How many times a read goes at most, the first included.
	/// </summary>
	public int MaxAttempts { get; }

	/// <summary>
	/// The largest delay before the second attempt; each following one can double it.
	/// </summary>
	public TimeSpan BaseDelay { get; }

	/// <summary>
	/// The largest delay before any attempt.
	/// </summary>
	public TimeSpan MaxDelay { get; }

	/// <summary>
	/// The default of <c>ChromaConfigurationOptions.ReadRetries</c>.
	/// </summary>
	public static ChromaReadRetries Default { get; } = new(4, TimeSpan.FromMilliseconds(50), TimeSpan.FromSeconds(1));

	/// <summary>
	/// A read goes once, and its error stands.
	/// </summary>
	public static ChromaReadRetries None { get; } = new(1, TimeSpan.Zero, TimeSpan.Zero);

	// The delay before the attempt after the given number of attempts that went again, for a random number from 0 to 1.
	internal TimeSpan Delay(int retry, double random)
	{
		// The base times 2 to the retry, up to the largest delay, without going beyond a long.
		var bound = retry >= 62 || BaseDelay.Ticks > MaxDelay.Ticks >> retry ? MaxDelay.Ticks : BaseDelay.Ticks << retry;
		return TimeSpan.FromTicks((long)(bound * random));
	}
}
