namespace SecretNest.FileWatcherForEmby;

internal static class PathHelper
{
    internal static string NormalizePath(string path) //always ensure ends with directory separator, no matter it's a file or directory
    {
        path = path.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        );
        return path + Path.DirectorySeparatorChar;
    }
    
    internal static bool IsSubPath(string shortPath, string longPath, StringComparison stringComparison)
    {
        try
        {
            return longPath.StartsWith(shortPath, stringComparison);
        }
        catch
        {
            return false;
        }
    }
    
    internal static string? GetSubPath(string shortPath, string longPath, StringComparison stringComparison)
    {
        try
        {
            if (!longPath.StartsWith(shortPath, stringComparison))
                return null;
            var subPath = longPath.Substring(shortPath.Length);
            subPath = subPath.TrimStart(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            );
            return subPath;
        }
        catch
        {
            return null;
        }
    }
}