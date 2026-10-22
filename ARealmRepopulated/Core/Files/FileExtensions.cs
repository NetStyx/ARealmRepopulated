using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ARealmRepopulated.Core.Files;

public static class FileExtensions {    
    public static bool IsSamePath(this string path, string otherPath)
        => string.Equals(Path.GetFullPath(path), Path.GetFullPath(otherPath), StringComparison.OrdinalIgnoreCase);

    public static bool IsAccessible(this FileInfo fileInfo) {
        try {
            using var _ = fileInfo.OpenRead();
        } catch (Exception) {
            return false;
        }

        return true;
    }

    public static bool WaitForAccessibility(this FileInfo fileInfo, int timeoutMilliseconds = 5000, int pollIntervalMilliseconds = 50) {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMilliseconds) {
            if (fileInfo.IsAccessible()) {
                return true;
            }
            Thread.Sleep(pollIntervalMilliseconds);
        }
        return false;
    }

}
