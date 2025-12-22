namespace SecretNest.FileWatcherForEmby;

internal sealed class DelayOptions
{
    public TimeSpan? RefreshDelay { get; set; }
    public bool ResetDelayOnRequest { get; set; }
}