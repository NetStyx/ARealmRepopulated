using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace ARealmRepopulated.Core.Files;

public class FileSystemWatcherDebouncer : FileSystemWatcher {
    public delegate void FileSystemEventDelegate(FileSystemEventArgs args);
    public event FileSystemEventDelegate? OnModified;

    private readonly ConcurrentDictionary<string, FileSystemEventArgs> _eventDictionary = new();

    public FileSystemWatcherDebouncer() {
        this.Created += FileSystemEntryChanged;
        this.Changed += FileSystemEntryChanged;
        this.Deleted += FileSystemEntryChanged;
        this.Renamed += FileSystemEntryChanged;
    }

    private void FileSystemEntryChanged(object source, FileSystemEventArgs e) {
        if (!_eventDictionary.TryAdd(e.FullPath, e))
            return;

        Task.Run(async () => {
            await Task.Delay(100);
            if (_eventDictionary.TryRemove(e.FullPath, out var args)) {
                OnModified?.Invoke(args);
            }
        });

    }
}
