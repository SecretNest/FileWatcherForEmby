using Microsoft.Extensions.Hosting;

namespace SecretNest.FileWatcherForEmby;

internal sealed class ServiceHostService(
    FolderWatcherService folderWatcherService, 
    CachedPathMatcherService cachedPathMatcherService,
    DelayService delayService,
    CachedEmbyItemsService cachedEmbyItemsService,
    DebuggerService debugger)
    : BackgroundService
{
    private CancellationToken _stoppingToken;
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        
        debugger.WriteDebug("ServiceHostService: starting...");
        folderWatcherService.FileSystemChanged += OnFileSystemChanged;
        delayService.DelayElapsed += OnDelayElapsed;
        folderWatcherService.Start();
        debugger.WriteDebug("ServiceHostService: started.");
        
        var tcs = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        
        if (stoppingToken.IsCancellationRequested)
        {
            debugger.WriteDebug("ServiceHostService: stopping...");
            folderWatcherService.Dispose();
            debugger.WriteDebug("ServiceHostService: stopped.");
            tcs.SetResult(null);
        }
        else
        {
            stoppingToken.Register(() =>
            {
                debugger.WriteDebug("ServiceHostService: stopping...");
                folderWatcherService.Dispose();
                debugger.WriteDebug("ServiceHostService: stopped.");
                tcs.SetResult(null);
            });
        }
        return tcs.Task;
    }

    private void OnFileSystemChanged(object? sender, FileSystemChangedEventArgs e)
    {
        ProcessFileSystemChange(e.Path);
    }

    private void ProcessFileSystemChange(string path)
    {
        try
        {
            var matchResult = cachedPathMatcherService.GetMappedItemsAsync(path, _stoppingToken).GetAwaiter().GetResult();
            
            if (matchResult == null || matchResult.Item2.Count == 0)
            {
                //no matching item found
                return;
            }
            
            foreach (var key in matchResult.Item2)
            {
                debugger.WriteDebug(
                    $"ServiceHostService: Scheduling refresh for Emby item with ID {key.Id} (parent ID {key.ParentId}) due to file system change at path: {path}");
                delayService.Trigger(key, matchResult.Item1 ? path : null); //only add path when found in cache
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            debugger.WriteDebug($"ServiceHostService: ProcessFileSystemChange Exception: {exception}");
        }
    }

    private void OnDelayElapsed(object? sender, DelayElapsedEventArgs e)
    {
        try
        {
            if (!cachedEmbyItemsService.RefreshItemAsync(e.Id, e.ParentId, _stoppingToken).GetAwaiter().GetResult())
            {
                //refresh failed.
                debugger.WriteWarning(
                    $"ServiceHostService: Failed to refresh Emby item with ID {e.Id} with parent ID {e.ParentId}.");
                if (!e.AdditionalInfo.IsEmpty)
                {
                    //some path is cached. Drop cache and retry.
                    foreach (var path in e.AdditionalInfo)
                    {
                        cachedPathMatcherService.DropCache(path);
                        debugger.WriteDebug(
                            $"ServiceHostService: Retrying processing file system change for path: {path}");
                        ProcessFileSystemChange(path);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            debugger.WriteDebug($"ServiceHostService: OnDelayElapsed Exception: {exception}");
        }
    }
}