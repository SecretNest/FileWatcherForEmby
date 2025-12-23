using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;

namespace SecretNest.FileWatcherForEmby;

internal class Program
{
    public static async Task<int> Main(string[] args)
    {
        var isConsole = !WindowsServiceHelpers.IsWindowsService();
        
        #if DEBUG
        var isDebug = true;
        #else
        var isDebug = false;
        #endif

        if (isConsole)
        {
            Console.WriteLine("File Watcher for Emby");
            Console.WriteLine("Version: " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            Console.WriteLine();

            #region Handle command line arguments for service management
            if (args.Length > 0)
            {
                var command = args[0].ToLowerInvariant();
                switch (command)
                {
                    case "debug":
                        isDebug = !isDebug;
                        break;
                    case "install":
                    {
                        if (!WindowsServiceHelper.IsAdministrator())
                        {
                            Console.WriteLine("Error: Administrator privileges are required to install the service.");
                            return -1;
                        }

                        var result = WindowsServiceHelper.InstallService();
                        if (result != null)
                        {
                            Console.WriteLine($"Error installing service. Code: {result.Item1}, Message: {result.Item2}");
                            return result.Item1;
                        }
                        else
                        {
                            Console.WriteLine("Service installed successfully.");
                            return 0;
                        }
                    }
                    case "uninstall":
                    {
                        if (!WindowsServiceHelper.IsAdministrator())
                        {
                            Console.WriteLine("Error: Administrator privileges are required to uninstall the service.");
                            return -1;
                        }

                        var result = WindowsServiceHelper.UninstallService();
                        if (result != null)
                        {
                            Console.WriteLine($"Error uninstalling service. Code: {result.Item1}, Message: {result.Item2}");
                            return result.Item1;
                        }
                        else
                        {
                            Console.WriteLine("Service uninstalled successfully.");
                            return 0;
                        }
                    }
                    case "start":
                    {
                        if (!WindowsServiceHelper.IsAdministrator())
                        {
                            Console.WriteLine("Error: Administrator privileges are required to start the service.");
                            return -1;
                        }

                        var result = WindowsServiceHelper.StartService();
                        if (result != null)
                        {
                            Console.WriteLine($"Error starting service. Code: {result.Item1}, Message: {result.Item2}");
                            return result.Item1;
                        }
                        else
                        {
                            Console.WriteLine("Service started successfully.");
                            return 0;
                        }
                    }
                    case "stop":
                    {
                        if (!WindowsServiceHelper.IsAdministrator())
                        {
                            Console.WriteLine("Error: Administrator privileges are required to stop the service.");
                            return -1;
                        }

                        var result = WindowsServiceHelper.StopService();
                        if (result != null)
                        {
                            Console.WriteLine($"Error stopping service. Code: {result.Item1}, Message: {result.Item2}");
                            return result.Item1;
                        }
                        else
                        {
                            Console.WriteLine("Service stopped successfully.");
                            return 0;
                        }
                    }
                }
            }
            #endregion
    
            //Tell user how to run as service
            Console.WriteLine("To run this application as a Windows Service, use the following command:");
            var fileName = Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName);
            Console.WriteLine($"  To install service:\t{fileName} install");
            Console.WriteLine($"  To uninstall service:\t{fileName} uninstall");
            Console.WriteLine($"  To start service:\t{fileName} start");
            Console.WriteLine($"  To stop service:\t{fileName} stop");
            Console.WriteLine();
    
            Console.WriteLine("This application is running in console mode.");
            Console.WriteLine("Press Ctrl+C to shut down.");
            Console.WriteLine("----------------------------------------");
        }

        var host = Host.CreateDefaultBuilder(args)
            .UseWindowsService()  // ✅ auto-detects console vs service
            .ConfigureServices((context, services) =>
            {
                services.Configure<EmbyClientOptions>(context.Configuration.GetSection("embyClient"));
                services.Configure<CachedEmbyLibrariesOptions>(context.Configuration.GetSection("cachedEmbyLibraries"));
                services.Configure<CachedEmbyItemsCacheOptions>(context.Configuration.GetSection("cachedEmbyItemsCache"));
                services.Configure<CachedEmbyItemsEntryOptions>(context.Configuration.GetSection("cachedEmbyItemsEntry"));
                services.Configure<PathMatcherOptions>(context.Configuration.GetSection("pathMatcher"));
                services.Configure<FolderWatcherOptions>(context.Configuration.GetSection("folderWatcher"));
                services.Configure<CachedPathMatcherCacheOptions>(context.Configuration.GetSection("cachedPatchMatcherCache"));
                services.Configure<CachedPathMatcherEntryOptions>(context.Configuration.GetSection("cachedPatchMatcherEntry"));
                services.Configure<DelayOptions>(context.Configuration.GetSection("refreshDelay"));
                services.Configure<ConsoleOptions>(options => { options.IsConsoleMode = isConsole; options.IsDebugMode = isDebug; });

                services.AddSingleton<EmbyClientService>();
                services.AddSingleton<CachedEmbyLibrariesService>();
                services.AddSingleton<CachedEmbyItemsService>();
                services.AddSingleton<LibraryPathMatcherService>();
                services.AddSingleton<FolderPathMatcherService>();
                services.AddSingleton<FolderWatcherService>();
                services.AddSingleton<CachedPathMatcherService>();
                services.AddTransient<DelayService>();
                services.AddSingleton<DebuggerService>();
                
                services.AddHostedService<ServiceHostService>();
            })
            .Build();
        await host.RunAsync();
        return 0;
    }
}
