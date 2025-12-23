namespace SecretNest.FileWatcherForEmby;

internal sealed class PathMatcherOptions
{
    public required List<PathMapping> PathMappings { get; set; }
    
    public bool SourcePathCaseSensitive { get; set; }

    internal sealed class PathMapping
    {
        public required string Source { get; set; }
        public required List<string> Targets { get; set; }
    }

    internal sealed class PathMatcherOptionsPostConfigure : Microsoft.Extensions.Options.IPostConfigureOptions<PathMatcherOptions>
    {
        public void PostConfigure(string? name, PathMatcherOptions options)
        {
            options.PathMappings ??= new List<PathMapping>();

            options.PathMappings = options.PathMappings.Select(mapping => new PathMapping()
            {
                Source = Path.GetFullPath(mapping.Source),
                Targets = mapping.Targets
                    .Distinct()
                    .ToList()
            }).DistinctBy(i => options.SourcePathCaseSensitive ? i.Source : i.Source.ToLowerInvariant()).ToList();
        }
    }
}