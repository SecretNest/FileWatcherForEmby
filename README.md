# File Watcher for Emby
Monitor Files on Windows and notify Emby through API.

## Why
Emby DOES have a built-in file watcher to monitor folders for changes. However, the docker version of Emby relies on inotify, which is not supported on Windows hosts. This means that if you are running Emby in a Docker container on a Windows machine, the built-in file watcher will not work. This project aims to fill that gap by providing a way to monitor files on Windows and notify Emby of any changes.

## Use

1. Download the latest release from the [Releases](releases/latest). Or, build it yourself with source code.
   1. The ``framework-dependent`` version need ``.net 10 runtime``. You can download it [here](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). ``Desktop`` support is NOT necessary for this application.
   2. The ``self-contained-singlefile`` version can be run without runtime installed.
2. Edit appsettings.json to configure:
   1. your Emby server URL ``embyBaseUrl`` and API key ``embyApiKey`` under ``embyClient`` section. ``EmbyEnvironmentPathCaseSensitive`` must be set to true for Linux based docker containers like Emby official docker container.
   2. the paths you want to monitor from the Windows which runs this application as key, and the paths mounted as target in Emby docker container as values under ``pathMappings`` of ``pathMatcher`` section. Multiple paths can be monitored by adding more ``Source``-``Targets`` pairs. Each source must be unique. Each source can have multiple target paths. ``sourcePathCaseSensitive`` must be set to false for local paths on Windows hosts.
   3. (optional) other settings:
      1. ``cachedEmbyLibraries`` section: ``cacheDurationInSeconds`` controls how long to cache Emby library info to reduce API calls. Default is 3600 seconds (1 hour). Set to ``0`` to let cache never expire. Set to negative value to disable caching.
      2. ``cachedEmbyItemsCache`` section: controls how many Emby items to cache in memory to reduce API calls. See [MemoryCacheOptions](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.memory.memorycacheoptions) for details.
      3. ``cachedEmbyItemsEntry`` section: ``slidingExpirationInSeconds`` controls how long each cached Emby item will stay in cache since last accessed. Default is ``00:05:00``. ``slidingExpiration`` controls whether to use sliding expiration or absolute expiration. Default is ``00:01:00``. See [MemoryCacheEntryOptions](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.memory.memorycacheentryoptions) for details.
      4. ``folderWatcher`` sections: ``retryDelay`` controls how long to wait before restarting FileSystemWatcher after an error occurs. Default is ``00:00:02``. ``ignoredExtensions`` controls which file extensions to ignore when monitoring file changes.
      5. ``cachedPatchMatcherCache`` section: controls how many path mappings to cache in memory to reduce path matching time. See [MemoryCacheOptions](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.memory.memorycacheoptions) for details.
      6. ``cachedPatchMatcherEntry`` section: ``slidingExpirationInSeconds`` controls how long each cached path mapping will stay in cache since last accessed. Default is ``00:05:00``. ``slidingExpiration`` controls whether to use sliding expiration or absolute expiration. Default is ``00:01:00``. See [MemoryCacheEntryOptions](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.memory.memorycacheentryoptions) for details.
      7. ``refreshDelay`` section: ``refreshDelay`` controls how long to wait before sending refresh command to Emby after a file change is detected. Default is ``00:00:05``. This is to avoid sending too many refresh commands in a short period of time when multiple file changes occur. ``resetDelayOnRequest`` controls whether to reset the delay timer when a new file change is detected for the same path. Default is ``false``.
3. Run the application in the same Windows host in ``console``. The application will monitor the specified paths for file changes and notify Emby through API when changes are detected. You can enable debug logging by provide ``debug`` as argument.
4. After testing, you can set up the application as a Windows Service by providing argument to this application with administrative privilege:
   1. ``install``: to install the application as a Windows Service. Service name is ``File Watcher for Emby``. It will start automatically with Windows.
   2. ``uninstall``: to uninstall the Windows Service.
   3. ``start``: to start the Windows Service.
   4. ``stop``: to stop the Windows Service.

### NOTE

- You need a Windows to run this application. This application is designed for Windows only.
- Local and Windows shared folders (UNC as path) are both supported.
- For Windows Service mode, make sure the service account has access to the folders being monitored.
