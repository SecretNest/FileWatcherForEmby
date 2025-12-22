using Microsoft.Extensions.Options;

namespace SecretNest.FileWatcherForEmby;

internal sealed class CachedEmbyLibrariesService
{
    private readonly CachedEmbyClientBase _cachedEmbyClient;
    
    public CachedEmbyLibrariesService(IOptions<CachedEmbyLibrariesOptions> options, EmbyClientService embyClientService, DebuggerService debugger)
    {
        options.Value.CacheDurationInSeconds ??= 3600; // Default to 1 hour

        _cachedEmbyClient = options.Value.CacheDurationInSeconds.Value switch
        {
            < 0 => new NoCachedEmbyClient(embyClientService, debugger),
            0 => new NoExpiredCachedEmbyClient(embyClientService, debugger),
            _ => new CachedEmbyClientWithExpiration(embyClientService, options.Value.CacheDurationInSeconds.Value, debugger)
        };
        
        debugger.WriteInfo("CachedEmbyLibraries initialized.");
        if (debugger.IsDebugMode)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("CachedEmbyLibraries configuration:");
            sb.AppendLine($"  CacheDurationInSeconds: {options.Value.CacheDurationInSeconds}");
            sb.AppendLine($"  ServicingClass: {_cachedEmbyClient.GetType().Name}");
            debugger.WriteDebugWithoutChecking(sb.ToString());
        }
    }

    public Task<Dictionary<string, List<int>>?> GetLibrariesAsync(CancellationToken cancellationToken = default)
    {
        return _cachedEmbyClient.GetLibrariesAsync(cancellationToken);
    }
}

internal abstract class CachedEmbyClientBase(EmbyClientService embyClientService, DebuggerService debugger)
{
    protected readonly EmbyClientService EmbyClientService = embyClientService;
    protected readonly DebuggerService Debugger = debugger;
    public abstract Task<Dictionary<string, List<int>>?> GetLibrariesAsync(CancellationToken cancellationToken = default);
}

internal sealed class NoExpiredCachedEmbyClient(EmbyClientService embyClientService, DebuggerService debugger) : CachedEmbyClientBase(embyClientService, debugger)
{
    private Dictionary<string, List<int>>? _cachedLibraries = null;

    public override async Task<Dictionary<string, List<int>>?> GetLibrariesAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedLibraries != null)
        {
            Debugger.WriteDebug("CachedEmbyLibraries: Returning cached libraries.");
            return _cachedLibraries;
        }
        
        _cachedLibraries ??= await EmbyClientService.GetLibrariesAsync(cancellationToken);

        return _cachedLibraries;
    }
}

internal sealed class NoCachedEmbyClient(EmbyClientService embyClientService, DebuggerService debugger) : CachedEmbyClientBase(embyClientService, debugger)
{
    public override async Task<Dictionary<string, List<int>>?> GetLibrariesAsync(CancellationToken cancellationToken = default)
    {
        return await EmbyClientService.GetLibrariesAsync(cancellationToken);
    }
}

internal sealed class CachedEmbyClientWithExpiration(EmbyClientService embyClientService, int cacheDurationInSeconds, DebuggerService debugger) : CachedEmbyClientBase(embyClientService, debugger)
{
    private Dictionary<string, List<int>>? _cachedLibraries = null;
    private DateTime _expiringTime = DateTime.MinValue;
    
    public override async Task<Dictionary<string, List<int>>?> GetLibrariesAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedLibraries != null && _expiringTime > DateTime.UtcNow)
        {
            Debugger.WriteDebug("CachedEmbyLibraries: Returning cached libraries.");
            return _cachedLibraries;
        }
        
        _cachedLibraries = await EmbyClientService.GetLibrariesAsync(cancellationToken);
        _expiringTime = DateTime.UtcNow.AddSeconds(cacheDurationInSeconds);
        Debugger.WriteDebug($"CachedEmbyLibraries: Cached libraries will expire at {_expiringTime:u}.");
        return _cachedLibraries;
    }
}