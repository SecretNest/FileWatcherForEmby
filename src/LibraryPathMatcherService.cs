using Microsoft.Extensions.Options;

namespace SecretNest.FileWatcherForEmby;

internal sealed class LibraryPathMatcherService
{
    private readonly Dictionary<string, List<string>> _pathMappings;
    private readonly StringComparison _comparison;
    private readonly CachedEmbyLibrariesService _cachedEmbyLibrariesService;
    private readonly DebuggerService _debugger;

    public LibraryPathMatcherService(IOptions<PathMatcherOptions> options, CachedEmbyLibrariesService cachedEmbyLibrariesService, DebuggerService debugger)
    {
        _cachedEmbyLibrariesService = cachedEmbyLibrariesService;
        _debugger = debugger;
        if (options.Value.CaseSensitive)
        {
            _comparison = StringComparison.Ordinal;
            if (options.Value.PathMappings != null)
                _pathMappings = new Dictionary<string, List<string>>(
                    options.Value.PathMappings.Select(kv =>
                        new KeyValuePair<string, List<string>>(
                            PathHelper.NormalizePath(Path.GetFullPath(kv.Key)),
                            kv.Value.Select(Path.GetFullPath).ToList())
                    ));
            else
                _pathMappings = new Dictionary<string, List<string>>();
        }
        else
        {
            _comparison = StringComparison.OrdinalIgnoreCase;
            if (options.Value.PathMappings != null)
                _pathMappings = new Dictionary<string, List<string>>(
                    options.Value.PathMappings.Select(kv =>
                        new KeyValuePair<string, List<string>>(
                            PathHelper.NormalizePath(Path.GetFullPath(kv.Key)),
                            kv.Value.Select(Path.GetFullPath).ToList())
                    ), StringComparer.OrdinalIgnoreCase);
            else
                _pathMappings = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }
        
        _debugger.WriteInfo($"LibraryPathMatcher initialized.");
        if (_debugger.IsDebugMode)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("LibraryPathMatcher configuration:");
            sb.AppendLine("  Path Mappings:");
            foreach (var mapping in _pathMappings)
            {
                sb.AppendLine($"    \"{mapping.Key}\" => [{string.Join(", ", "\"" + mapping.Value) + "\""}]");
            }
            sb.AppendLine($"  CaseSensitive: {options.Value.CaseSensitive}");
            _debugger.WriteDebugWithoutChecking(sb.ToString());
        }
    }
    
    public class MappedLibrary
    {
        public required int LibraryId { get; init; }
        public required string FullPath { get; init; }
    }

    public async Task<List<MappedLibrary>?> GetMappedLibrariesAsync(string path, CancellationToken cancellationToken)
    {
        _debugger.WriteDebug($"LibraryPathMatcher: Getting mapped libraries for path: {path}");
        
        var mappedPaths = new List<string>();
        path = Path.GetFullPath(path);
        foreach(var mapping in _pathMappings)
        {
            var subPath = PathHelper.GetSubPath(mapping.Key, path, _comparison);
            if (subPath == null) continue;
            mappedPaths.AddRange(mapping.Value.Select(mappedPath => Path.Combine(mappedPath, 
                subPath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))));
        }

        var pathAndParentIds = await _cachedEmbyLibrariesService.GetLibrariesAsync(cancellationToken);
        if (pathAndParentIds == null)
            return null;

        var result = new List<MappedLibrary>();
        foreach (var pathToCheck in mappedPaths)
        {
            foreach(var libraryPath in pathAndParentIds)
            {
                //if pathToCheck is under libraryPath.Key
                if (!PathHelper.IsSubPath(libraryPath.Key, pathToCheck, _comparison)) continue;
                foreach (var id in libraryPath.Value)
                {
                    result.Add(new MappedLibrary
                    {
                        LibraryId = id,
                        FullPath = pathToCheck
                    });
                }
            }
        }
        
        if (_debugger.IsDebugMode)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"LibraryPathMatcher: Mapped libraries for path: {path}");
            foreach (var mappedLibrary in result)
            {
                sb.AppendLine($"  LibraryId: {mappedLibrary.LibraryId}, FullPath: \"{mappedLibrary.FullPath}\"");
            }
            _debugger.WriteDebugWithoutChecking(sb.ToString());
        }

        return result;
    }
    

}