namespace SecretNest.FileWatcherForEmby;

internal static class WindowsServiceHelper
{
#pragma warning disable CA1416
    private const string ServiceName = "File Watcher for Emby";
    private const string ServiceDisplayName = "Notify file changes to Emby Server";
    
    public static bool IsAdministrator()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
    
    private static string GetExecutablePath()
    {
        return Environment.ProcessPath!;
    }
    
    private static Tuple<int, string>? RunSc(string arguments)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = System.Diagnostics.Process.Start(psi);
        process!.WaitForExit();
        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            return Tuple.Create(process.ExitCode, error);
        }
        return null;
    }
    
    public static Tuple<int, string>? InstallService()
    {
        var result = RunSc($"create \"{ServiceName}\" binPath= \"{GetExecutablePath()} --service\" start= auto");
        if (result != null)
        {
            return result;
        }
        
        result = RunSc($"failure \"{ServiceName}\" reset= 0 actions= restart/5000");
        if (result != null)
        {
            return result;
        }

        result = RunSc($"description \"{ServiceName}\" \"{ServiceDisplayName}\"");
        return result;
    }
    
    public static Tuple<int, string>? UninstallService()
    {
        return RunSc($"delete \"{ServiceName}\"");
    }
    
    public static Tuple<int, string>? StartService()
    {
        return RunSc($"start \"{ServiceName}\"");
    }
    
    public static Tuple<int, string>? StopService()
    {
        return RunSc($"stop \"{ServiceName}\"");
    }
#pragma warning restore CA1416
}