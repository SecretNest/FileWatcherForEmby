namespace SecretNest.FileWatcherForEmby;

internal sealed class CachedEmbyItemsEntryOptions
{
    public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
    public TimeSpan? SlidingExpiration { get; set; }
}