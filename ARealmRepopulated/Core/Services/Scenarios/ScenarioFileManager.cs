using ARealmRepopulated.Core.Files;
using ARealmRepopulated.Core.Json;
using ARealmRepopulated.Data.Location;
using ARealmRepopulated.Data.Scenarios;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;

namespace ARealmRepopulated.Core.Services.Scenarios;

public sealed record ScenarioFileData(string FileHash, string FileName, string FilePath, ScenarioFileMetaData MetaData);
public class ScenarioFileManager(IDalamudPluginInterface pluginInterface, IPluginLog log, IFramework dalamudFramework, ScenarioMigrator migrator) : IDisposable {
    private readonly FileSystemWatcherDebouncer _fileSystemWatcher = new();
    public static readonly JsonSerializerOptions ScenarioMetaSerializerOptions = new() { };
    public static readonly JsonSerializerOptions ScenarioLoadSerializerOptions = new() { Converters = { new Vector3Converter(), new Vector4Converter(), new JsonStringEnumConverter() }, TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { NullStringModifier.Instance, DefaultValueModifier.Instance } } };

    private readonly Lock _filesLock = new();
    private readonly List<ScenarioFileData> _currentFiles = [];

    public delegate void ScenarioFileChangedDelegate(ScenarioFileData metaData);
    public event ScenarioFileChangedDelegate? OnScenarioFileChanged;
    public event ScenarioFileChangedDelegate? OnScenarioFileRemoved;

    public string ScenarioPath => Path.Combine(pluginInterface.GetPluginConfigDirectory(), "Scenarios");
    public void StartMonitoring() {

        if (!Directory.Exists(ScenarioPath))
            Directory.CreateDirectory(ScenarioPath);

        _fileSystemWatcher.Path = ScenarioPath;
        _fileSystemWatcher.Filter = "*.json";
        _fileSystemWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.LastWrite;
        _fileSystemWatcher.IncludeSubdirectories = false;
        _fileSystemWatcher.EnableRaisingEvents = true;
        _fileSystemWatcher.OnModified += _fileSystemWatcherFired;

        ScanScenarioFiles();
    }

    public void StopMonitoring() {
        _fileSystemWatcher.EnableRaisingEvents = false;
        _fileSystemWatcher.OnModified -= _fileSystemWatcherFired;
    }

    public List<ScenarioFileData> GetScenarioFiles() {
        using var _ = _filesLock.EnterScope();
        return [.. _currentFiles];
    }

    public List<ScenarioFileData> GetScenarioFilesByTerritory(LocationData location) {
        using var _ = _filesLock.EnterScope();
        return [.. _currentFiles.Where(x => x.MetaData.Location.Territory == location.TerritoryType)];
    }

    public void ScanScenarioFiles() {
        using var _ = _filesLock.EnterScope();
        _fileSystemWatcher.EnableRaisingEvents = false;
        _currentFiles.Clear();
        Directory
            .GetFiles(ScenarioPath, "*.json", SearchOption.TopDirectoryOnly)
            .ToList().ForEach(TryReadScenarioFile);
        _fileSystemWatcher.EnableRaisingEvents = true;
    }

    private void _fileSystemWatcherFired(FileSystemEventArgs e)
        => TryReadScenarioFile(e.FullPath);

    private void TryReadScenarioFile(string pathToFile) {
        var fileInfo = new FileInfo(pathToFile);
        log.Info($"Scenario file changed: {fileInfo.Name}");

        try {
            ScanScenarioFile(fileInfo);
        } catch (JsonException jex) {
            log.Warning("Could not read meta data of scenario file {FileName}. Invalid json format: {JsonError}", [fileInfo.Name, jex.Message]);
        } catch (Exception ex) {
            log.Error(ex, "Could not read meta data of scenario file {FileName}", [fileInfo.Name]);
        }
    }

    private void ScanScenarioFile(FileInfo fileInfo) {
        using var _ = _filesLock.EnterScope();
        if (!fileInfo.Exists) {
            _currentFiles
                .Where(x => x.FilePath.Equals(fileInfo.FullName))
                .ToList().ForEach(f => {
                    _currentFiles.Remove(f);
                    dalamudFramework.RunOnTick(() => OnScenarioFileRemoved?.Invoke(f));
                });

            return;
        }

        fileInfo.WaitForAccessibility();
        if (migrator.Migrate(File.ReadAllText(fileInfo.FullName)) is not { } migration) {
            return;
        }

        if (migration.WasUpgraded) {
            File.WriteAllText(fileInfo.FullName, JsonSerializer.Serialize(migration.Scenario, ScenarioMetaSerializerOptions));
        }

        var fileMetaData = migration.Scenario.Deserialize<ScenarioFileMetaData>(ScenarioMetaSerializerOptions)
            ?? throw new InvalidDataException("File is not a valid scenario data format");

        var fileHash = fileInfo.GetFileHash();
        var fileData = new ScenarioFileData(fileHash, fileInfo.Name, fileInfo.FullName, fileMetaData);

        var loadedFileInfo = _currentFiles.FirstOrDefault(x => x.FileHash == fileHash || x.FilePath == fileInfo.FullName);
        if (loadedFileInfo != null) {
            _currentFiles.Remove(loadedFileInfo);
        }
        _currentFiles.Add(fileData);

        dalamudFramework.RunOnTick(() => {
            if (loadedFileInfo != null) {
                OnScenarioFileRemoved?.Invoke(loadedFileInfo);
            }
            OnScenarioFileChanged?.Invoke(fileData);
        });
    }

    public ScenarioData? LoadScenarioFile(ScenarioFileData file)
        => LoadScenarioFile(file.FilePath);
    public ScenarioData? LoadScenarioFile(string filePath)
        => LoadScenarioFile(new FileInfo(filePath));
    public ScenarioData? LoadScenarioFile(FileInfo fileInfo) {
        log.Debug($"Reading scenario file {fileInfo.Name}");
        try {
            var jsonString = File.ReadAllText(fileInfo.FullName);
            return JsonSerializer.Deserialize<ScenarioData>(jsonString, ScenarioLoadSerializerOptions);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            log.Warning("Could not read scenario file {FileName}: {IoError}", [fileInfo.Name, ex.Message]);
        } catch (JsonException jex) {
            log.Warning("Could not read full scenario data of {FileName}. Invalid json format: {JsonError}", [fileInfo.Name, jex.Message]);
        } catch (Exception ex) {
            log.Error(ex, "Could not read full scenario data of {FileName}", [fileInfo.Name]);
        }
        return null;
    }

    public ScenarioData? ImportBase64Scenario(string base64Data) {
        try {
            var jsonString = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64Data));
            return migrator.Migrate(jsonString)?.Scenario.Deserialize<ScenarioData>(ScenarioLoadSerializerOptions);
        } catch (FormatException fex) {
            log.Warning("Could not import scenario from base64 data. Invalid base64 format: {Base64Error}", [fex.Message]);
        } catch (JsonException jex) {
            log.Warning("Could not import scenario from base64 data. Invalid json format: {JsonError}", [jex.Message]);
        } catch (Exception ex) {
            log.Error(ex, "Could not import scenario from base64 data");
        }
        return null;
    }

    public static string ExportBase64Scenario(ScenarioData data) {
        var jsonString = JsonSerializer.Serialize(data, ScenarioLoadSerializerOptions);
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(jsonString));
    }

    public FileInfo StoreScenarioFile(ScenarioData data)
        => StoreScenarioFile(data, Path.Combine(ScenarioPath, $"{Guid.NewGuid()}.json"));

    public FileInfo StoreScenarioFile(ScenarioData scenarioData, string filePath) {
        var directory = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directory)) {
            log.Info($"Scenario directory does not exist. Creating '{directory}'");
            Directory.CreateDirectory(directory!);
        }
        log.Info($"Storing scenario file '{filePath}'");
        var jsonString = JsonSerializer.Serialize(scenarioData, ScenarioLoadSerializerOptions);
        File.WriteAllText(filePath, jsonString);

        var fileInfo = new FileInfo(filePath);
        using (_filesLock.EnterScope()) {
            // Update the meta data of the file so that the list does not flicker when enableding or disableding a scenario.
            var index = _currentFiles.FindIndex(f => f.FilePath == fileInfo.FullName);
            if (index >= 0 && JsonSerializer.Deserialize<ScenarioFileMetaData>(jsonString, ScenarioMetaSerializerOptions) is { } metaData)
                _currentFiles[index] = _currentFiles[index] with { MetaData = metaData };
        }

        return fileInfo;
    }

    public void RemoveScenarioFile(string filePath) {
        log.Info($"Removing scenario file: {filePath}");
        if (File.Exists(filePath)) {
            File.Delete(filePath);
        }
    }

    public void Dispose() {
        StopMonitoring();
        _fileSystemWatcher.Dispose();
        GC.SuppressFinalize(this);
    }

}
