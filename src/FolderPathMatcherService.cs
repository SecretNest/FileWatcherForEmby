using Microsoft.Extensions.Options;

namespace SecretNest.FileWatcherForEmby;

internal sealed class FolderPathMatcherService
{
    private readonly StringComparison _comparison;
    private readonly CachedEmbyItemsService _cachedEmbyItemsService;
    private readonly DebuggerService _debugger;

    public FolderPathMatcherService(IOptions<PathMatcherOptions> options, CachedEmbyItemsService cachedEmbyItemsService, DebuggerService debugger)
    {
        _cachedEmbyItemsService = cachedEmbyItemsService;
        _debugger = debugger;
        _comparison = options.Value.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        
        _debugger.WriteInfo("FolderPathMatcher initialized.");
        if (_debugger.IsDebugMode)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("FolderPathMatcher configuration:");
            sb.AppendLine($"  CaseSensitive: {options.Value.CaseSensitive}");
            _debugger.WriteDebugWithoutChecking(sb.ToString());
        }
    }

    public class MappedItem
    {
        public required int Id { get; init; }
        public required int ParentId { get; init; }
        public required string MappedPath { get; init; }
    }
    
    public async Task<MappedItem?> GetMappedItemAsync(int libraryId, string mappedFullPath, CancellationToken cancellationToken = default)
    {
        _debugger.WriteInfo($"FolderPathMatcher: Start matching path '{mappedFullPath}' under library ID {libraryId}.");
        var searchingIds = new Queue<int>(libraryId);
        
        int? proximalMatchedId = null;
        string? proximalMatchedPath = null;
        int? parentOfProximalMatchedId = null;
        while (searchingIds.Count > 0)
        {
            var searchingId = searchingIds.Dequeue();
            _debugger.WriteDebug($"FolderPathMatcher: Searching path '{mappedFullPath}' under item ID {searchingId}.");
            
            var subNodes = await _cachedEmbyItemsService.GetItemsAsync(searchingId, cancellationToken);
            if (subNodes is null) continue;

            foreach (var subNode in subNodes)
            {
                if (string.IsNullOrEmpty(subNode.Value.Path))
                {
                    _debugger.WriteDebug($"FolderPathMatcher: Item ID {subNode.Key} has no path, checking its children is required.");
                    //node without path, need to check its children
                    searchingIds.Enqueue(subNode.Key);
                }
                else if (string.Equals(mappedFullPath, subNode.Value.Path, _comparison))
                {
                    _debugger.WriteInfo($"FolderPathMatcher: Exact match found for path '{mappedFullPath}' at item ID {subNode.Key}, parent ID {searchingId}.");
                    //exact item found
                    return new MappedItem
                    {
                        Id = subNode.Key,
                        ParentId = searchingId,
                        MappedPath = subNode.Value.Path
                    };
                }
                else if (string.Equals(subNode.Value.Path, mappedFullPath, _comparison))
                {
                    _debugger.WriteDebug($"FolderPathMatcher: Proximal match found for path '{mappedFullPath}' at item ID {subNode.Key}.");
                    //proximal item found
                    proximalMatchedId = subNode.Key;
                    proximalMatchedPath = subNode.Value.Path;
                    parentOfProximalMatchedId = searchingId;
                    searchingIds.Clear();
                    searchingIds.TrimExcess();
                    searchingIds.Enqueue(subNode.Key);
                }
            }
        }

        if (proximalMatchedId == null)
        {
            _debugger.WriteWarning($"FolderPathMatcher: No match found for path '{mappedFullPath}' under library ID {libraryId}.");
            return null;
        }
        else
        {
            _debugger.WriteInfo($"FolderPathMatcher: Proximal match returned for path '{mappedFullPath}' at item ID {proximalMatchedId.Value}, parent ID {parentOfProximalMatchedId!.Value}.");
            return new MappedItem
            {
                Id = proximalMatchedId.Value,
                ParentId = parentOfProximalMatchedId!.Value,
                MappedPath = proximalMatchedPath!
            };
        }
    }

}