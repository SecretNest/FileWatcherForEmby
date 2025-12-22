using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace SecretNest.FileWatcherForEmby;

internal sealed class DelayService : IDisposable
{
    private readonly DebuggerService _debugger;
    private readonly TimeSpan _delay;
    private readonly bool _resetDelayOnRequest;

    private readonly ConcurrentDictionary<NodeKey, Tuple<Timer, ConcurrentBag<string>>> _timers = new(); //id+parentId, (timer, additionalInfoBag)

    public DelayService(IOptions<DelayOptions> options, DebuggerService debugger)
    {
        _debugger = debugger;
        _delay = options.Value.RefreshDelay ?? TimeSpan.FromSeconds(5);
        _resetDelayOnRequest = options.Value.ResetDelayOnRequest;
        
        _debugger.WriteInfo("DelayService initialized.");
        if (_debugger.IsDebugMode)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("DelayService configuration:");
            sb.AppendLine($"  RefreshDelay: {_delay}");
            _debugger.WriteDebugWithoutChecking(sb.ToString());
        }
    }

    public event EventHandler<DelayElapsedEventArgs>? DelayElapsed;
    
    public void Trigger(NodeKey key, string? additionalInfo = null)
    {
        _timers.AddOrUpdate(
            key,
            _ => //first time
            {
                _debugger.WriteDebug($"Delay timer created for ID {key.Id} with parent ID {key.ParentId}.");
                var bag = new ConcurrentBag<string>();
                if (additionalInfo != null)
                {
                    bag.Add(additionalInfo);
                }

                return new Tuple<Timer, ConcurrentBag<string>>(
                    new Timer(OnTimerElapsed, key, _delay, Timeout.InfiniteTimeSpan), bag);
            },
            (_, existingTimer) => //reset timer
            {
                if (_resetDelayOnRequest)
                {
                    existingTimer.Item1.Change(_delay, Timeout.InfiniteTimeSpan);
                    _debugger.WriteDebug($"Delay timer reset for ID {key.Id} with parent ID {key.ParentId}.");
                }
                else
                {
                    _debugger.WriteDebug($"Delay timer hit for ID {key.Id} with parent ID {key.ParentId}.");
                }
                if (additionalInfo != null)
                {
                    existingTimer.Item2.Add(additionalInfo);
                }
                
                return existingTimer;
            });
    }
    
    private void OnTimerElapsed(object? state)
    {
        if (state is not NodeKey key)
            return;
        _debugger.WriteDebug($"Delay timer elapsed for ID {key.Id} with parent ID {key.ParentId}.");
        //remove timer
        if (_timers.TryRemove(key, out var timer))
        {
            timer.Item1.Dispose();
            
            // trigger event
            DelayElapsed?.Invoke(this, new DelayElapsedEventArgs(key.Id, key.ParentId, timer.Item2));
        }
    }
    
    public void Dispose()
    {
        foreach (var timer in _timers.Values)
        {
            timer.Item1.Dispose();
        }
        _timers.Clear();
    }
}

internal sealed class DelayElapsedEventArgs(int id, int parentId, ConcurrentBag<string> additionalInfo) : EventArgs
{
    public int Id { get; } = id;
    public int ParentId { get; } = parentId;
    public ConcurrentBag<string> AdditionalInfo { get; } = additionalInfo;
}