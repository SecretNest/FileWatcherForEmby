using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace SecretNest.FileWatcherForEmby;

internal sealed class CachedPathMatcherService : IDisposable
{
    private readonly MemoryCache _cache;
    private readonly LibraryPathMatcherService _libraryPathMatcherService;
    private readonly FolderPathMatcherService _folderPathMatcherService;
    private readonly DebuggerService _debugger;
    private readonly MemoryCacheEntryOptions _entryOptions;

    public CachedPathMatcherService(LibraryPathMatcherService libraryPathMatcherService,
        FolderPathMatcherService folderPathMatcherService,
        IOptions<CachedPathMatcherCacheOptions> cacheOptions,
        IOptions<CachedPathMatcherEntryOptions> entryOptions,
        DebuggerService debugger)
    {
        _libraryPathMatcherService = libraryPathMatcherService;
        _folderPathMatcherService = folderPathMatcherService;
        _debugger = debugger;
        
        if (entryOptions.Value.AbsoluteExpirationRelativeToNow == null &&
            entryOptions.Value.SlidingExpiration == null)
        {
            _entryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(1)
            };
        }
        else
        {
            _entryOptions = new MemoryCacheEntryOptions()
            {
                AbsoluteExpirationRelativeToNow = entryOptions.Value.AbsoluteExpirationRelativeToNow,
                SlidingExpiration = entryOptions.Value.SlidingExpiration
            };
        }

        _cache = new MemoryCache(cacheOptions);

        _debugger.WriteInfo("CachedPathMatcher initialized.");
        if (_debugger.IsDebugMode)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("CachedPathMatcher configuration:");
            sb.AppendLine($"  Cache Clock: {cacheOptions.Value.Clock}");
            sb.AppendLine($"  Cache CompactionPercentage: {cacheOptions.Value.CompactionPercentage}");
            sb.AppendLine($"  Cache ExpirationScanFrequency: {cacheOptions.Value.ExpirationScanFrequency}");
            sb.AppendLine($"  Cache SizeLimit: {cacheOptions.Value.SizeLimit}");
            sb.AppendLine($"  Cache TrackLinkedCacheEntries: {cacheOptions.Value.TrackLinkedCacheEntries}");
            sb.AppendLine($"  Cache TrackStatistics: {cacheOptions.Value.TrackStatistics}");
            sb.AppendLine($"  Entry AbsoluteExpirationRelativeToNow: {_entryOptions.AbsoluteExpirationRelativeToNow}");
            sb.Append($"  Entry SlidingExpiration: {_entryOptions.SlidingExpiration}");
            _debugger.WriteDebugWithoutChecking(sb.ToString());
        }
    }

    //result: Item found in cache, List of IDs
    public async Task<Tuple<bool, List<NodeKey>>?> GetMappedItemsAsync(string path, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(path, out List<NodeKey>? ids))
        {
            _debugger.WriteInfo($"CachedPathMatcher: Retrieved mapped items from cache for path: {path}");
            if (_debugger.IsDebugMode)
            {
                _debugger.WriteDebugWithoutChecking($"CachedPathMatcher: Mapped IDs for {path}: {string.Join("; ", ids!)}");
            }
            return new Tuple<bool, List<NodeKey>>(true, ids!);
        }
        
        _debugger.WriteInfo($"CachedPathMatcher: Getting mapped items for path: {path}");
        
        //get libraries
        var libraries = await _libraryPathMatcherService.GetMappedLibrariesAsync(path, cancellationToken);
        if (libraries == null) return null;
        if (libraries.Count == 0)
        {
            _cache.Set(path, new List<NodeKey>(), _entryOptions);
            _debugger.WriteInfo($"CachedPathMatcher: No libraries matched for path: {path}");
            return new Tuple<bool, List<NodeKey>>(false, new List<NodeKey>());
        }
        
        //get item for each library
        var results = new List<FolderPathMatcherService.MappedItem>();
        foreach(var library in libraries)
        {
            var item = await _folderPathMatcherService.GetMappedItemAsync(library.LibraryId, path, cancellationToken);
            if (item != null)
            {
                _debugger.WriteDebug($"CachedPathMatcher: Mapped item found in library {library.LibraryId} for path {path}: Item ID: {item.Id}, Path: {item.MappedPath}");
                results.Add(item);
            }
            else
            {
                _debugger.WriteWarning($"CachedPathMatcher: No mapped item found in library {library.LibraryId} for path {path}.");
            }
        }
        if (results.Count == 0)
        {
            _cache.Set(path, new List<NodeKey>(), _entryOptions);
            _debugger.WriteInfo($"CachedPathMatcher: No items matched for path: {path}");
            return new Tuple<bool, List<NodeKey>>(false, new List<NodeKey>());
        }

        if (_debugger.IsDebugMode)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"CachedPathMatcher: Mapped IDs for {path}:");
            sb.AppendJoin('\n',
                results.Select(i => $"  ID: {i.Id}, Parent ID: {i.ParentId}, Mapped Path: {i.MappedPath}"));
            _debugger.WriteDebugWithoutChecking(sb.ToString());
        }
        
        ids = results.Select(r => new NodeKey(r.Id, r.ParentId)).ToList();
        _cache.Set(path, ids, _entryOptions);
        
        return new Tuple<bool, List<NodeKey>>(false, ids);
    }

    public void DropCache(string path)
    {
        _cache.Remove(path);
        _debugger.WriteDebug($"CachedPathMatcher: Dropped cache for path: {path}");
    }

    public void Dispose()
    {
        _cache.Dispose();
    }
}