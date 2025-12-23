namespace SecretNest.FileWatcherForEmby;

internal sealed class PathMatcherOptions
{
    public required List<PathMapping> PathMappings { get; set; }
    
    public bool CaseSensitive { get; set; }

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
                    .Select(Path.GetFullPath)
                    .Distinct()
                    .ToList()
            }).DistinctBy(i => i.Source).ToList();
        }
    }
}