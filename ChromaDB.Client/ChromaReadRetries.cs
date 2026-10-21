namespace ChromaDB.Client;

/// <summary>
/// How a read goes again after an error that can pass, with an exponential backoff with jitter: before each new attempt it waits a
/// random time between <c>MinDelay</c> and the smaller of <c>MaxDelay</c> and <c>BaseDelay</c> times 2 to the number of the attempts
/// that went again before. <c>MinDelay</c> keeps a read from going again at once; without it, it is 0.8 times <c>BaseDelay</c>, the
/// lower bound of the jitter of Azure.Core. The reads are each request of <c>GetAsync</c>, <c>PeekAsync</c>,
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
	/// The attempts and the delays, with a smallest delay of 0.8 times <c>baseDelay</c>.
	/// </summary>
	/// <param name="maxAttempts">How many times a read goes at most, the first included: 1 never goes again.</param>
	/// <param name="baseDelay">The largest delay before the second attempt; each following one can double it.</param>
	/// <param name="maxDelay">The largest delay before any attempt, at least <c>baseDelay</c> and at most <c>Int32.MaxValue</c>
	/// milliseconds, the longest delay of <c>Task.Delay</c>.</param>
	public ChromaReadRetries(int maxAttempts, TimeSpan baseDelay, TimeSpan maxDelay)
		: this(maxAttempts, baseDelay < TimeSpan.Zero ? TimeSpan.Zero : TimeSpan.FromTicks(baseDelay.Ticks / 5 * 4), baseDelay, maxDelay)
	{
	}

	/// <summary>
	/// The attempts and the delays.
	/// </summary>
	/// <param name="maxAttempts">How many times a read goes at most, the first included: 1 never goes again.</param>
	/// <param name="minDelay">The smallest delay before any attempt, from zero to <c>baseDelay</c>.</param>
	/// <param name="baseDelay">The largest delay before the second attempt; each following one can double it.</param>
	/// <param name="maxDelay">The largest delay before any attempt, at least <c>baseDelay</c> and at most <c>Int32.MaxValue</c>
	/// milliseconds, the longest delay of <c>Task.Delay</c>.</param>
	public ChromaReadRetries(int maxAttempts, TimeSpan minDelay, TimeSpan baseDelay, TimeSpan maxDelay)
	{
		if (maxAttempts < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(maxAttempts), "A read goes at least once.");
		}
		// Beyond Int32.MaxValue milliseconds, the longest delay of Task.Delay on every platform of the client, Task.Delay would throw in
		// the middle of a read.
		if (baseDelay < TimeSpan.Zero || maxDelay < baseDelay || maxDelay.TotalMilliseconds > int.MaxValue)
		{
			throw new ArgumentOutOfRangeException(baseDelay < TimeSpan.Zero ? nameof(baseDelay) : nameof(maxDelay), "The delays cannot be negative, the largest one is at least the base, and it is at most Int32.MaxValue milliseconds, the longest delay of Task.Delay.");
		}
		if (minDelay < TimeSpan.Zero || minDelay > baseDelay)
		{
			throw new ArgumentOutOfRangeException(nameof(minDelay), "The smallest delay is from zero to the base.");
		}
		MaxAttempts = maxAttempts;
		MinDelay = minDelay;
		BaseDelay = baseDelay;
		MaxDelay = maxDelay;
	}

	/// <summary>
	/// How many times a read goes at most, the first included.
	/// </summary>
	public int MaxAttempts { get; }

	/// <summary>
	/// The smallest delay before any attempt.
	/// </summary>
	public TimeSpan MinDelay { get; }

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

	// The random number from 0 to 1 of each delay; null takes the shared Random of the client. Only the tests set it, to make a delay
	// certain.
	internal Func<double>? Jitter { get; init; }

	// The delay before the attempt after the given number of attempts that went again, for a random number from 0 to 1.
	internal TimeSpan Delay(int retry, double random)
	{
		// The base times 2 to the retry, up to the largest delay, without going beyond a long; never under the smallest delay, which is
		// at most the base.
		var bound = retry >= 62 || BaseDelay.Ticks > MaxDelay.Ticks >> retry ? MaxDelay.Ticks : BaseDelay.Ticks << retry;
		return TimeSpan.FromTicks(MinDelay.Ticks + (long)((bound - MinDelay.Ticks) * random));
	}
}
