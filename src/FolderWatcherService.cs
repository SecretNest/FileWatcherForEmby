using Microsoft.Extensions.Options;

namespace SecretNest.FileWatcherForEmby;

internal sealed class FolderWatcherService : IDisposable
{
    private readonly DebuggerService _debugger;
    private readonly List<FolderWatcher> _watcherInstances;
    
    public event EventHandler<FileSystemChangedEventArgs>? FileSystemChanged;

    public FolderWatcherService(IOptions<FolderWatcherOptions> options,
        IOptions<PathMatcherOptions> pathMatcherOptions,
        DebuggerService debugger)
    {
        _debugger = debugger;
        
        if (pathMatcherOptions.Value.PathMappings == null || pathMatcherOptions.Value.PathMappings.Count == 0)
        {
            _debugger.WriteInfo("FolderWatcherService initialized with no paths to watch.");
            _watcherInstances = new List<FolderWatcher>();
        }
        else
        {
            _watcherInstances = pathMatcherOptions.Value.PathMappings.Keys
                .Select(path => new FolderWatcher(path, options.Value.RetryDelay))
                .ToList();
            _watcherInstances.ForEach(watcher =>
            {
                watcher.FileSystemChanged += OnFileSystemChanged;
                watcher.WatcherExceptionOccurred += OnWatcherExceptionOccurred;
            });
            _debugger.WriteInfo($"FolderWatcherService initialized.");
            if (_debugger.IsDebugMode)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("FolderWatcherService configuration:");
                sb.AppendLine($"  RetryDelay: {options.Value.RetryDelay}");
                sb.AppendLine("  Watched Paths:");
                foreach (var path in pathMatcherOptions.Value.PathMappings.Keys)
                {
                    sb.AppendLine($"    {path}");
                }
                _debugger.WriteDebugWithoutChecking(sb.ToString());
            }
        }
    }

    public void Start()
    {
        foreach (var watcher in _watcherInstances)
        {
            watcher.Start();
        }
    }

    private void OnFileSystemChanged(object? sender, FileSystemChangedEventArgs e)
    {
        _debugger.WriteInfo($"FolderWatcherService: File system change detected. Need refresh on \"{e.Path}\".");
        
        FileSystemChanged?.Invoke(this, e);
    }
    
    private void OnWatcherExceptionOccurred(object? sender, FileSystemWatcherExceptionEventArgs e)
    {
        _debugger.WriteError($"FolderWatcherService: File system watcher exception occurred. Message: {e.Message}. Exception: {e.InnerException}");
    }

    public void Dispose()
    {
        _watcherInstances.ForEach(watcher =>
        {
            watcher.FileSystemChanged -= OnFileSystemChanged;
            watcher.Dispose();
        });
    }
}