using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace SecretNest.FileWatcherForEmby;

internal sealed class CachedEmbyItemsService : IDisposable
{
    private readonly MemoryCache _cache;
    private readonly EmbyClientService _embyClientService;
    private readonly DebuggerService _debugger;
    private readonly MemoryCacheEntryOptions _entryOptions;

    public CachedEmbyItemsService(EmbyClientService embyClientService,
        IOptions<CachedEmbyItemsCacheOptions> cacheOptions,
        IOptions<CachedEmbyItemsEntryOptions> entryOptions,
        DebuggerService debugger)
    {
        _embyClientService = embyClientService;
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
        
        if (cacheOptions.Value.SizeLimit != null)
        {
            _entryOptions.Size = 1;
        }

        _cache = new MemoryCache(cacheOptions);
        
        _debugger.WriteInfo("CachedEmbyItems initialized.");
        if (_debugger.IsDebugMode)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("CachedEmbyItems configuration:");
            sb.AppendLine($"  Cache Clock: {cacheOptions.Value.Clock}");
            sb.AppendLine($"  Cache CompactionPercentage: {cacheOptions.Value.CompactionPercentage}");
            sb.AppendLine($"  Cache ExpirationScanFrequency: {cacheOptions.Value.ExpirationScanFrequency}");
            sb.AppendLine($"  Cache SizeLimit: {cacheOptions.Value.SizeLimit}");
            sb.AppendLine($"  Cache TrackLinkedCacheEntries: {cacheOptions.Value.TrackLinkedCacheEntries}");
            sb.AppendLine($"  Cache TrackStatistics: {cacheOptions.Value.TrackStatistics}");
            sb.AppendLine($"  Entry AbsoluteExpirationRelativeToNow: {_entryOptions.AbsoluteExpirationRelativeToNow}");
            sb.AppendLine($"  Entry SlidingExpiration: {_entryOptions.SlidingExpiration}");
            _debugger.WriteDebugWithoutChecking(sb.ToString());
        }
    }

    public async Task<Dictionary<int, EmbyClientService.EmbyItem>?> GetItemsAsync(int parentId, CancellationToken cancellationToken)
    {
        if (!_cache.TryGetValue(parentId, out Dictionary<int, EmbyClientService.EmbyItem>? items))
        {
            items = await _embyClientService.GetItemsAsync(parentId, cancellationToken).ConfigureAwait(false);
            _cache.Set(parentId, items, _entryOptions);
        }
        else
        {
           _debugger.WriteDebug($"CachedEmbyItems: Returning cached items for ParentId {parentId}.");
        }
        return items;
    }
    
    //refreshed need to remove from cache
    public async Task<bool> RefreshItemAsync(int itemId, int parentId, CancellationToken cancellationToken)
    {
        var result = await _embyClientService.RefreshItemAsync(itemId, cancellationToken).ConfigureAwait(false);
        
        //if cached, read it to remove all sub-items, recursively
        var toProcess = new Stack<int>();
        toProcess.Push(itemId);
        while (toProcess.Count > 0)
        {
            var currentId = toProcess.Pop();
            if (_cache.TryGetValue(currentId, out Dictionary<int, EmbyClientService.EmbyItem>? items))
            {
                _cache.Remove(currentId);
                _debugger.WriteDebug($"CachedEmbyItems: Removed cached items for {currentId} due to refresh of ItemId {itemId}.");
                foreach (var item in items!.Keys)
                {
                    toProcess.Push(item);
                }
            }
        }
        
        //if failed, remove the item contains this item from cache.
        if (!result)
        {
            _cache.Remove(parentId);
            _debugger.WriteDebug($"CachedEmbyItems: Removed cached items for ParentId {parentId} due to refresh failure of ItemId {itemId}.");
        }
        
        return result;
    }
    
    public void Dispose()
    {
        _cache.Dispose();
    }
}