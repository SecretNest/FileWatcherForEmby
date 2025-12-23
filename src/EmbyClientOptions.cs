namespace SecretNest.FileWatcherForEmby;

internal sealed class EmbyClientOptions
{
    public required string EmbyBaseUrl { get; set; }
    public required string EmbyApiKey { get; set; }
    public required bool EmbyEnvironmentPathCaseSensitive { get; set; }
}