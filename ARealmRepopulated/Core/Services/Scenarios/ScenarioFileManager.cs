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

namespace ARealmRepopulated.Core.Services.Scenarios;

public sealed record ScenarioFileData(string FileHash, string FileName, string FilePath, ScenarioFileMetaData MetaData);
public class ScenarioFileManager(IDalamudPluginInterface pluginInterface, IPluginLog log, IFramework dalamudFramework, ScenarioMigrator migrator) : IDisposable {
    private readonly FileSystemWatcherDebouncer _fileSystemWatcher = new();
    public static readonly JsonSerializerOptions ScenarioMetaSerializerOptions = new() { };
    public static readonly JsonSerializerOptions ScenarioLoadSerializerOptions = new() { Converters = { new Vector3Converter(), new JsonStringEnumConverter() }, TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { NullStringModifier.Instance } } };


    private readonly List<ScenarioFileData> _currentFiles = [];

    public delegate void ScenarioFileChangedDelegate(ScenarioFileData metaData);
    public event ScenarioFileChangedDelegate? OnScenarioFileChanged;
    public event ScenarioFileChangedDelegate? OnScenarioFileRemoved;

    public string ScenarioPath => Path.Combine(pluginInterface.GetPluginConfigDirectory(), "Scenarios");
    public void StartMonitoring() {

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
        return [.. _currentFiles];
    }

    public List<ScenarioFileData> GetScenarioFilesByTerritory(LocationData location) {
        return [.. _currentFiles.Where(x => x.MetaData.Location.Territory == location.TerritoryType)];
    }

    public void ScanScenarioFiles() {

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
        if (!migrator.Migrate(fileInfo, out var fileMetaData)) {
            return;
        }

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

    public ScenarioData? LoadScenarioFile(string filePath) {
        var fileName = Path.GetFileName(filePath);
        var fileData = File.ReadAllText(filePath);

        return DeserializeScenarioData(fileData);
    }

    public ScenarioData? ImportBase64Scenario(string base64Data) {
        try {
            var jsonString = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64Data));
            return DeserializeScenarioData(jsonString);
        } catch (FormatException fex) {
            log.Warning("Could not import scenario from base64 data. Invalid base64 format: {Base64Error}", [fex.Message]);
        } catch (Exception ex) {
            log.Error(ex, "Could not import scenario from base64 data");
        }
        return null;
    }

    public string ExportBase64Scenario(ScenarioData data) {
        var jsonString = JsonSerializer.Serialize(data, ScenarioLoadSerializerOptions);
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(jsonString));
    }

    private ScenarioData? DeserializeScenarioData(string jsonString) {
        try {
            return JsonSerializer.Deserialize<ScenarioData>(jsonString, ScenarioLoadSerializerOptions);
        } catch (JsonException jex) {
            log.Warning("Could not read full scenario data. Invalid json format: {JsonError}", [jex.Message]);
        } catch (Exception ex) {
            log.Error(ex, "Could not read full scenario data");
        }
        return null;
    }

    public FileInfo StoreScenarioFile(ScenarioData data)
        => StoreScenarioFile(data, Path.Combine(ScenarioPath, $"{Guid.NewGuid()}.json"));

    public FileInfo StoreScenarioFile(ScenarioData scenarioData, string filePath) {
        var directory = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directory)) {
            Directory.CreateDirectory(directory!);
        }
        var jsonString = JsonSerializer.Serialize(scenarioData, ScenarioLoadSerializerOptions);
        File.WriteAllText(filePath, jsonString);

        return new FileInfo(filePath);
    }

    public void RemoveScenarioFile(string filePath) {
        if (File.Exists(filePath)) {
            File.Delete(filePath);
        }
    }

    public void Dispose() {
        StopMonitoring();
        _fileSystemWatcher.Dispose();
    }

}
