using System.IO;

namespace SecretNest.FileWatcherForEmby;

internal sealed class FolderWatcher(string path, HashSet<string> ignoredExtensions, bool caseSensitive, TimeSpan? retryDelay = null)
    : IDisposable
{
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
        if (Directory.Exists(path))
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
                var fsw = new FileSystemWatcher(path)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter =
                        NotifyFilters.FileName |
                        NotifyFilters.DirectoryName |
                        NotifyFilters.LastWrite,
                    InternalBufferSize = 64 * 1024
                };
                fsw.Created += (s, e) =>
                {
                    if (ignoredExtensions.Count > 0)
                    {
                        var extension = Path.GetExtension(e.FullPath) ?? ".";
                        if (ignoredExtensions.Contains(extension)) return;
                    }
                    var parent = Path.GetDirectoryName(e.FullPath);
                    if (!string.IsNullOrEmpty(parent))
                        FileSystemChanged?.Invoke(this, new FileSystemChangedEventArgs(parent, path));
                };
                fsw.Changed += (s, e) =>
                {
                    if (ignoredExtensions.Count > 0)
                    {
                        var extension = Path.GetExtension(e.FullPath) ?? ".";
                        if (ignoredExtensions.Contains(extension)) return;
                    }
                    FileSystemChanged?.Invoke(this, new FileSystemChangedEventArgs(e.FullPath, path));
                };
                fsw.Deleted += (s, e) =>
                {
                    if (ignoredExtensions.Count > 0)
                    {
                        var extension = Path.GetExtension(e.FullPath) ?? ".";
                        if (ignoredExtensions.Contains(extension)) return;
                    }
                    var parent = Path.GetDirectoryName(e.FullPath);
                    if (!string.IsNullOrEmpty(parent))
                        FileSystemChanged?.Invoke(this, new FileSystemChangedEventArgs(parent, path));
                };
                fsw.Renamed += (s, e) =>
                {
                    var oldParent = Path.GetDirectoryName(e.OldFullPath);
                    var newParent = Path.GetDirectoryName(e.FullPath);

                    if (oldParent != newParent)
                    {
                        //notify old name
                        if (!string.IsNullOrEmpty(oldParent) &&
                            ignoredExtensions.Count > 0 &&
                            e.OldFullPath.StartsWith(path, !caseSensitive, null))
                        {
                            var extension = Path.GetExtension(e.OldFullPath);
                            if (!ignoredExtensions.Contains(extension))
                            {
                                FileSystemChanged?.Invoke(this, new FileSystemChangedEventArgs(oldParent, path));
                            }
                        }
                    }

                    //notify new name
                    if (!string.IsNullOrEmpty(newParent) &&
                        ignoredExtensions.Count > 0 &&
                        e.FullPath.StartsWith(path, !caseSensitive, null))
                    {
                        var extension = Path.GetExtension(e.FullPath);
                        if (!ignoredExtensions.Contains(extension))
                        {
                            FileSystemChanged?.Invoke(this, new FileSystemChangedEventArgs(newParent, path));
                        }
                    }
                };
                fsw.Error += (s, e) =>
                {
                    // Root/UNC removed or fatal watcher issue
                    WatcherExceptionOccurred?.Invoke(this, new FileSystemWatcherExceptionEventArgs(path, e.ToString(), e.GetException()));
                    HandleWatcherFailure();
                };
                fsw.EnableRaisingEvents = true;
                _watcher = fsw;
                WatcherStarted?.Invoke(this, new FileSystemWatcherStartedEventArgs(path));
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
                        if (Directory.Exists(path))
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