using System.IO;

namespace SecretNest.FileWatcherForEmby;

internal sealed class FolderWatcher(string path, TimeSpan? retryDelay = null)
    : IDisposable
{
    private readonly string _path = path;
    private readonly TimeSpan _retryDelay = retryDelay ?? TimeSpan.FromSeconds(2);
    private readonly Lock _lock = new();
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _retryCts;
    private Task? _retryTask;
    private bool _disposed;
    
    public event EventHandler<FileSystemChangedEventArgs>? FileSystemChanged;
    public event EventHandler<FileSystemWatcherExceptionEventArgs>? WatcherExceptionOccurred;
    public event EventHandler<FileSystemWatcherStartedEventArgs>? WatcherStarted;
    
    
    public void Start()
    {
        lock (_lock)
        {
            if (_disposed || _watcher != null || _retryTask != null)
                return;
            TryCreateWatcherOrRetry();
        }
    }
    
    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            _retryCts?.Cancel();
            _retryCts = null;
            TryDisposeWatcher();
        }
    }
    
    private void TryCreateWatcherOrRetry()
    {
        if (_disposed)
            return;
        if (Directory.Exists(_path))
        {
            TryCreateWatcher();
        }
        else
        {
            StartRetryLoop();
        }
    }
    
    private void TryCreateWatcher()
    {
        lock (_lock)
        {
            if (_disposed || _watcher != null)
                return;
            try
            {
                var fsw = new FileSystemWatcher(_path)
                {
                    NotifyFilter =
                        NotifyFilters.FileName |
                        NotifyFilters.DirectoryName |
                        NotifyFilters.LastWrite,
                    InternalBufferSize = 64 * 1024
                };
                fsw.Created += (s, e) =>
                {
                    var parent = Path.GetDirectoryName(e.FullPath);
                    if (!string.IsNullOrEmpty(parent))
                        FileSystemChanged?.Invoke(this, new FileSystemChangedEventArgs(parent, _path));
                };
                fsw.Changed += (s, e) =>
                {
                    FileSystemChanged?.Invoke(this, new FileSystemChangedEventArgs(e.FullPath, _path));
                };
                fsw.Deleted += (s, e) =>
                {
                    var parent = Path.GetDirectoryName(e.FullPath);
                    if (!string.IsNullOrEmpty(parent))
                        FileSystemChanged?.Invoke(this, new FileSystemChangedEventArgs(parent, _path));
                };
                fsw.Renamed += (s, e) =>
                {
                    var oldParent = Path.GetDirectoryName(e.OldFullPath);
                    var newParent = Path.GetDirectoryName(e.FullPath);
                    if (!string.IsNullOrEmpty(oldParent))
                        FileSystemChanged?.Invoke(this, new FileSystemChangedEventArgs(oldParent, _path));
                    if (oldParent != newParent)
                    {
                        if (!string.IsNullOrEmpty(newParent))
                            FileSystemChanged?.Invoke(this, new FileSystemChangedEventArgs(newParent, _path));
                    }
                };
                fsw.Error += (s, e) =>
                {
                    // Root/UNC removed or fatal watcher issue
                    WatcherExceptionOccurred?.Invoke(this, new FileSystemWatcherExceptionEventArgs(_path, e.ToString(), e.GetException()));
                    HandleWatcherFailure();
                };
                fsw.EnableRaisingEvents = true;
                _watcher = fsw;
                WatcherStarted?.Invoke(this, new FileSystemWatcherStartedEventArgs(_path));
            }
            catch
            {
                // Creation failed → probably path vanished mid‑creation
                StartRetryLoop();
            }
        }
    }
    
    private void HandleWatcherFailure()
    {
        TryDisposeWatcher();
        StartRetryLoop();
    }
    
    private void StartRetryLoop()
    {
        lock (_lock)
        {
            if (_disposed || _retryTask != null)
                return;
            _retryCts = new CancellationTokenSource();
            var token = _retryCts.Token;
            _retryTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && !_disposed)
                {
                    try
                    {
                        if (Directory.Exists(_path))
                        {
                            lock (_lock)
                            {
                                _retryTask = null;
                                _retryCts = null;
                            }
                            TryCreateWatcher();
                            return;
                        }
                        await Task.Delay(_retryDelay, token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }, token);
        }
    }
    
    private void TryDisposeWatcher()
    {
        if (_watcher == null) return;
        try
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }
        catch
        {
            // ignored
        }
        finally
        {
            _watcher = null;
        }
    }
}

internal sealed class FileSystemChangedEventArgs(string path, string watchingPath) : EventArgs
{
    public readonly string Path = path;
    public readonly string WatchingPath = watchingPath;
}

internal sealed class FileSystemWatcherExceptionEventArgs(string watchingPath, string? message, Exception? innerException = null) : EventArgs
{
    public readonly string WatchingPath = watchingPath;
    public readonly string? Message = message;
    public readonly Exception? InnerException = innerException;
}

internal sealed class FileSystemWatcherStartedEventArgs(string watchingPath) : EventArgs
{
    public readonly string WatchingPath = watchingPath;
}