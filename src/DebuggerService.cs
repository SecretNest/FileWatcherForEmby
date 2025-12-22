using Microsoft.Extensions.Options;

namespace SecretNest.FileWatcherForEmby;

internal class DebuggerService
{
    private ConsoleColor _infoColor;
    private ConsoleColor _debugColor;
    private ConsoleColor _warningColor;
    private ConsoleColor _errorColor;
    private readonly IOptions<ConsoleOptions> _options;
    
    public bool IsConsoleMode => _options.Value.IsConsoleMode;
    public bool IsDebugMode => _options.Value is { IsDebugMode: true, IsConsoleMode: true };

    public DebuggerService(IOptions<ConsoleOptions> options)
    {
        _options = options;
        _infoColor = Console.ForegroundColor;
        var isDark = _infoColor is ConsoleColor.White or ConsoleColor.Gray;
        _debugColor = isDark ? ConsoleColor.DarkGray : ConsoleColor.Gray;
        _warningColor = isDark ? ConsoleColor.Yellow : ConsoleColor.DarkYellow;
        _errorColor = ConsoleColor.Red;
    }
    
    public void WriteError(string message)
    {
        if (IsConsoleMode)
        {
            Console.ForegroundColor = _errorColor;
            Console.WriteLine("ERROR: " + message);
        }
    }
    
    public void WriteWarning(string message)
    {
        if (IsConsoleMode)
        {
            Console.ForegroundColor = _warningColor;
            Console.WriteLine("WARNING: " + message);
        }
    }

    public void WriteInfo(string message)
    {
        if (IsConsoleMode)
        {
            Console.ForegroundColor = _infoColor;
            Console.WriteLine("INFO: " + message);
        }
    }

    public void WriteDebug(string message)
    {
        if (IsDebugMode)
        {
            WriteDebugWithoutChecking(message);
        }
    }
    
    public void WriteDebugWithoutChecking(string message)
    {
        Console.ForegroundColor = _debugColor;
        Console.WriteLine("DEBUG: " + message);
    }
}