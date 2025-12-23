using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SecretNest.FileWatcherForEmby;

internal sealed class EmbyClientService
{
    private readonly DebuggerService _debugger;
    private readonly bool _pathCaseSensitive;
    private readonly string _urlGetVirtualFolders;
    private readonly string _urlGetItemsFormat;
    private readonly string _urlRefreshFormat;
    
    public EmbyClientService(IOptions<EmbyClientOptions> options, DebuggerService debugger)
    {
        _debugger = debugger;
        _pathCaseSensitive = options.Value.EmbyEnvironmentPathCaseSensitive;
        var embyBaseUrl = options.Value.EmbyBaseUrl.EndsWith("/") 
            ? options.Value.EmbyBaseUrl
            : options.Value.EmbyBaseUrl + "/";
        
        _urlGetVirtualFolders = $"{embyBaseUrl}Library/VirtualFolders?api_key={options.Value.EmbyApiKey}";
        _urlGetItemsFormat = $"{embyBaseUrl}Items?ParentId={{0}}&Fields=Path&api_key={options.Value.EmbyApiKey}";
        _urlRefreshFormat = $"{embyBaseUrl}Items/{{0}}/Refresh?&api_key={options.Value.EmbyApiKey}";

        _debugger.WriteInfo("EmbyClient initialized.");
        if (_debugger.IsDebugMode)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("EmbyClient configuration:");
            sb.AppendLine($"  EmbyBaseUrl: {embyBaseUrl}");
            sb.AppendLine($"  EmbyApiKey: {options.Value.EmbyApiKey}");
            sb.AppendLine($"  EmbyEnvironmentPathCaseSensitive: {_pathCaseSensitive}");
            sb.AppendLine($"  URL GetVirtualFolders: {_urlGetVirtualFolders}");
            sb.AppendLine($"  URL GetItemsFormat: {_urlGetItemsFormat}");
            sb.Append($"  URL RefreshFormat: {_urlRefreshFormat}");
            _debugger.WriteDebugWithoutChecking(sb.ToString());
        }
    }
    
    public async Task<Dictionary<string, List<int>>?> GetLibrariesAsync(CancellationToken cancellationToken = default)
    {
        using var httpClient = new HttpClient();
        
        _debugger.WriteDebug($"EmbyClient: GET {_urlGetVirtualFolders}");
        
        //return null when error occurs
        var responseMessage = await httpClient.GetAsync(_urlGetVirtualFolders, cancellationToken);
        if (!responseMessage.IsSuccessStatusCode)
        {
            _debugger.WriteWarning($"EmbyClient: Failed to get libraries. Status code: {responseMessage.StatusCode}.");
            if (_debugger.IsDebugMode)
            {
                var errorContent = await responseMessage.Content.ReadAsStringAsync(cancellationToken);
                _debugger.WriteDebugWithoutChecking($"EmbyClient: Error content: {errorContent}");
            }
            return null;
        }
        
        var content = await responseMessage.Content.ReadAsStringAsync(cancellationToken);
        if (_debugger.IsDebugMode)
        {
            _debugger.WriteDebugWithoutChecking($"EmbyClient: Libraries content: {content}");
        }
        var json = JsonDocument.Parse(content).RootElement;
        _debugger.WriteInfo("EmbyClient: Successfully retrieved libraries.");
        
        var locationsAndIds = new Dictionary<string, List<int>>(_pathCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        
        foreach (var libraryElement in json.EnumerateArray())
        {
            if (!libraryElement.TryGetProperty("Id", out var idElement)) continue;
            if (!idElement.TryGetInt32(out var id)) continue; 

            if (!libraryElement.TryGetProperty("Locations", out var locationsElement)) continue;
            if (locationsElement.ValueKind != JsonValueKind.Array) continue;
            foreach (var locationElement in locationsElement.EnumerateArray())
            {
                var location = locationElement.GetString();
                if (string.IsNullOrEmpty(location)) continue;
                location = PathHelper.NormalizePath(Path.GetFullPath(location)); //Tail added
                if (!locationsAndIds.TryGetValue(location, out var idList))
                {
                    idList = [id];
                    locationsAndIds[location] = idList;
                }
                else
                {
                    idList.Add(id);
                }
            }
        }

        if (_debugger.IsDebugMode)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("EmbyClient: Libraries and their IDs:");
            sb.AppendJoin('\n',
                locationsAndIds.Select(kvp => $"  Location: {kvp.Key}, IDs: {string.Join(", ", kvp.Value)}"));
            _debugger.WriteDebugWithoutChecking(sb.ToString());
        }
        
        return locationsAndIds;
    }

    public class EmbyItem
    {
        public string? Path { get; init; }
        public bool IsFolder { get; init; }
    }

    public async Task<Dictionary<int, EmbyItem>?> GetItemsAsync(int parentId,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = new HttpClient();
        
        var url = string.Format(_urlGetItemsFormat, parentId);
        _debugger.WriteDebug($"EmbyClient: GET {url}");

        //return null when error occurs
        var responseMessage = await httpClient.GetAsync(url, cancellationToken);
        if (!responseMessage.IsSuccessStatusCode)
        {
            _debugger.WriteWarning($"EmbyClient: Failed to get items for ParentId {parentId}. Status code: {responseMessage.StatusCode}.");
            if (_debugger.IsDebugMode)
            {
                var errorContent = await responseMessage.Content.ReadAsStringAsync(cancellationToken);
                _debugger.WriteDebugWithoutChecking($"EmbyClient: Error content: {errorContent}");
            }
            return null;
        }
        
        var content = await responseMessage.Content.ReadAsStringAsync(cancellationToken);
        if (_debugger.IsDebugMode)
        {
            _debugger.WriteDebugWithoutChecking($"EmbyClient: Items content for ParentId {parentId}: {content}");
        }
        var json = JsonDocument.Parse(content).RootElement;
        _debugger.WriteInfo($"EmbyClient: Successfully retrieved items for ParentId {parentId}.");
        var items = new Dictionary<int, EmbyItem>();
        if (!json.TryGetProperty("TotalRecordCount", out var totalRecordCountElement)) return null;
        if (totalRecordCountElement.ValueKind != JsonValueKind.Array) return null;
        foreach (var itemElement in totalRecordCountElement.EnumerateArray())
        {
            if (!itemElement.TryGetProperty("Id", out var idElement)) continue;
            if (!idElement.TryGetInt32(out var id)) continue; 
            
            //Path may be missing
            string? path = null;
            if (itemElement.TryGetProperty("Path", out var pathElement))
            {
                path = PathHelper.NormalizePath(pathElement.GetString()!); //Tail added
            }
            
            //IsFolder may be missing
            var isFolder = false;
            if (itemElement.TryGetProperty("IsFolder", out var isFolderElement))
            {
                isFolder = isFolderElement.GetBoolean();
            }
            items[id] = new EmbyItem
            {
                Path = path,
                IsFolder = isFolder
            };
        }
        
        if (_debugger.IsDebugMode)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"EmbyClient: Items for ParentId {parentId}:");
            sb.AppendJoin('\n',
                items.Select(kvp => $"  ID: {kvp.Key}, Path: {kvp.Value.Path}, IsFolder: {kvp.Value.IsFolder}"));
            _debugger.WriteDebugWithoutChecking(sb.ToString());
        }
        
        return items;
    }
    
    public async Task<bool> RefreshItemAsync(int itemId, CancellationToken cancellationToken = default)
    {
        using var httpClient = new HttpClient();

        var url = string.Format(_urlRefreshFormat, itemId);
        _debugger.WriteDebug($"EmbyClient: GET {url}");
        
        var responseMessage = await httpClient.GetAsync(url, cancellationToken);
        if (!responseMessage.IsSuccessStatusCode)
        {
            _debugger.WriteWarning($"EmbyClient: Failed to refresh item {itemId}. Status code: {responseMessage.StatusCode}.");
            if (_debugger.IsDebugMode)
            {
                var errorContent = await responseMessage.Content.ReadAsStringAsync(cancellationToken);
                _debugger.WriteDebugWithoutChecking($"EmbyClient: Error content: {errorContent}");
            }
            return false;
        }
        
        _debugger.WriteInfo($"EmbyClient: Successfully refreshed item {itemId}.");
        return true;
    }
}