namespace SecretNest.FileWatcherForEmby;

internal sealed class CachedPathMatcherEntryOptions
{
    public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
    public TimeSpan? SlidingExpiration { get; set; }
}