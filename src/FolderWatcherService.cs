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
        
        if (pathMatcherOptions.Value.PathMappings.Count == 0)
        {
            _debugger.WriteInfo("FolderWatcherService initialized with no paths to watch.");
            _watcherInstances = new List<FolderWatcher>();
        }
        else
        {
            var ignoredExtensions = new HashSet<string>(
                pathMatcherOptions.Value.SourcePathCaseSensitive
                    ? StringComparer.Ordinal
                    : StringComparer.OrdinalIgnoreCase);
            if (options.Value.IgnoredExtensions != null)
            {
                foreach (var extension in options.Value.IgnoredExtensions)
                {
                    if (!extension.StartsWith("."))
                    {
                        ignoredExtensions.Add("." + extension);
                    }
                    else
                    {
                        ignoredExtensions.Add(extension);
                    }
                }
            }

            _watcherInstances = pathMatcherOptions.Value.PathMappings
                .Select(path => new FolderWatcher(path.Source, ignoredExtensions, pathMatcherOptions.Value.SourcePathCaseSensitive, options.Value.RetryDelay))
                .ToList();
            _watcherInstances.ForEach(watcher =>
            {
                watcher.FileSystemChanged += OnFileSystemChanged;
                watcher.WatcherExceptionOccurred += OnWatcherExceptionOccurred;
                watcher.WatcherStarted += OnWatcherStarted;
            });
            _debugger.WriteInfo($"FolderWatcherService initialized.");
            if (_debugger.IsDebugMode)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("FolderWatcherService configuration:");
                sb.AppendLine($"  RetryDelay: {options.Value.RetryDelay}");
                sb.AppendLine("  Watched Paths:");
                sb.AppendJoin('\n', pathMatcherOptions.Value.PathMappings.Select(i => $"    {i.Source}"));
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
        _debugger.WriteInfo($"FolderWatcherService: File system change detected under \"{e.WatchingPath}\". Need refresh on \"{e.Path}\".");
        
        FileSystemChanged?.Invoke(this, e);
    }
    
    private void OnWatcherExceptionOccurred(object? sender, FileSystemWatcherExceptionEventArgs e)
    {
        _debugger.WriteError($"FolderWatcherService: File system watcher on \"{e.WatchingPath}\" exception occurred. Message: {e.Message}. Exception: {e.InnerException}");
    }

    private void OnWatcherStarted(object? sender, FileSystemWatcherStartedEventArgs e)
    {
        _debugger.WriteDebug($"FolderWatcherService: File system watcher started on \"{e.WatchingPath}\".");
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