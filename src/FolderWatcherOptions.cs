namespace SecretNest.FileWatcherForEmby;

internal sealed class FolderWatcherOptions
{
    public TimeSpan? RetryDelay { get; set; }
    public List<string>? IgnoredExtensions { get; set; }
}