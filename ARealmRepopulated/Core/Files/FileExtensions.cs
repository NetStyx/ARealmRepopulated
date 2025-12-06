using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace ARealmRepopulated.Core.Files;

public static class FileExtensions {

    public static string GetFileHash(this FileInfo fileInfo) {
        ArgumentNullException.ThrowIfNull(fileInfo);

        if (!fileInfo.Exists)
            throw new FileNotFoundException("File not found.", fileInfo.FullName);

        using var stream = fileInfo.OpenRead();
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToBase64String(hash);
    }

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
