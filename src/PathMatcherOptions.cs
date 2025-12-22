namespace SecretNest.FileWatcherForEmby;

internal sealed class PathMatcherOptions
{
    public Dictionary<string, List<string>>? PathMappings { get; set; } //Real Path -> Emby Docker Mapped Path
    
    public bool CaseSensitive { get; set; } 
}