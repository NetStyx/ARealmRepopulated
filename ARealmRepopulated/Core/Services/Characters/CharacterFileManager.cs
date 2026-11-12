using ARealmRepopulated.Core.Files;
using ARealmRepopulated.Core.Services.Scenarios;
using ARealmRepopulated.Data.Characters;
using ARealmRepopulated.Data.Scenarios;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace ARealmRepopulated.Core.Services.Characters;

/// <summary>
/// Handles our character files.
/// </summary>
public class CharacterFileManager(IDalamudPluginInterface pluginInterface, IPluginLog log, IFramework framework) : IDisposable {
    private readonly FileSystemWatcherDebouncer _fileSystemWatcher = new();
    private readonly Dictionary<Guid, CharacterFile> _characters = [];
    private readonly Lock _charactersLock = new();
    private readonly CharacterData _missingCharacter = new();

    public delegate void CharacterChangedDelegate(Guid characterId);
    public event CharacterChangedDelegate? OnCharacterChanged;
    public event CharacterChangedDelegate? OnCharacterRemoved;

    public string CharacterPath => Path.Combine(pluginInterface.GetPluginConfigDirectory(), "Characters");

    public void StartMonitoring() {
        if (!Directory.Exists(CharacterPath))
            Directory.CreateDirectory(CharacterPath);

        _fileSystemWatcher.Path = CharacterPath;
        _fileSystemWatcher.Filter = "*.json";
        _fileSystemWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.LastWrite;
        _fileSystemWatcher.IncludeSubdirectories = false;
        _fileSystemWatcher.OnModified += FileSystemWatcher_Modified;

        using (_charactersLock.EnterScope()) {
            _characters.Clear();
            foreach (var filePath in Directory.GetFiles(CharacterPath, "*.json", SearchOption.TopDirectoryOnly)) {
                if (TryReadCharacterFile(filePath) is { } character)
                    _characters[character.Id] = new CharacterFile(filePath, character);
            }
        }

        _fileSystemWatcher.EnableRaisingEvents = true;
    }

    public List<CharacterData> GetCharacters() {
        using var _ = _charactersLock.EnterScope();
        return [.. _characters.Values.Select(c => c.Character)];
    }

    public bool Contains(Guid characterId) {
        using var _ = _charactersLock.EnterScope();
        return _characters.ContainsKey(characterId);
    }

    public CharacterData? GetCharacter(Guid characterId) {
        using var _ = _charactersLock.EnterScope();
        return _characters.TryGetValue(characterId, out var file) ? file.Character : null;
    }

    public ICharacterDefinition ResolveDefinition(ScenarioNpcData npc)
        => npc.CharacterId is { } id && !Contains(id) ? _missingCharacter : npc;

    public void ApplyCharacters(ScenarioData scenario, string scenarioName) {
        Dictionary<Guid, CharacterData> characters;
        using (_charactersLock.EnterScope()) {
            characters = _characters.ToDictionary(e => e.Key, e => e.Value.Character);
        }

        foreach (var missingId in scenario.ApplyCharacters(characters).Distinct()) {
            log.Warning("Scenario {ScenarioName} links to character {CharacterId}, which does not exist", [scenarioName, missingId]);
        }
    }

    public void Save(CharacterData character) {
        character.Revision++;
        Write(character.Copy());
    }

    /// <summary>
    /// Stores an imported character as it is, keeping the local group of the character it replaces.
    /// </summary>
    public void Adopt(CharacterData character) {
        var adopted = character.Copy();
        adopted.Group = GetCharacter(character.Id)?.Group ?? "";
        Write(adopted);
    }

    public void Remove(Guid characterId) {
        string? filePath;
        using (_charactersLock.EnterScope()) {
            filePath = _characters.TryGetValue(characterId, out var file) ? file.FilePath : null;
            _characters.Remove(characterId);
        }

        log.Info($"Removing character {characterId}");
        if (filePath != null && File.Exists(filePath))
            File.Delete(filePath);

        OnCharacterRemoved?.Invoke(characterId);
    }

    private void Write(CharacterData character) {
        string filePath;
        using (_charactersLock.EnterScope()) {
            filePath = _characters.TryGetValue(character.Id, out var existing) ? existing.FilePath : Path.Combine(CharacterPath, $"{character.Id}.json");
            _characters[character.Id] = new CharacterFile(filePath, character);
        }

        log.Info($"Storing character file '{filePath}'");
        File.WriteAllText(filePath, JsonSerializer.Serialize(character, ScenarioFileManager.ScenarioLoadSerializerOptions));
    }

    private CharacterData? TryReadCharacterFile(string filePath) {
        try {
            var fileInfo = new FileInfo(filePath);
            fileInfo.WaitForAccessibility();
            return JsonSerializer.Deserialize<CharacterData>(File.ReadAllText(filePath), ScenarioFileManager.ScenarioLoadSerializerOptions);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) {
            log.Warning("Could not read character file {FileName}: {Error}", [Path.GetFileName(filePath), ex.Message]);
        }
        return null;
    }

    private void FileSystemWatcher_Modified(FileSystemEventArgs e) {
        if (!Path.GetExtension(e.FullPath).Equals(".json", StringComparison.OrdinalIgnoreCase))
            return;

        var character = File.Exists(e.FullPath) ? TryReadCharacterFile(e.FullPath) : null;

        List<Guid> removedIds;
        using (_charactersLock.EnterScope()) {
            removedIds = [.. _characters.Where(c => c.Value.FilePath.IsSamePath(e.FullPath) && c.Key != character?.Id).Select(c => c.Key)];
            removedIds.ForEach(id => _characters.Remove(id));

            if (character != null)
                _characters[character.Id] = new CharacterFile(e.FullPath, character);
        }

        framework.RunOnTick(() => {
            removedIds.ForEach(id => OnCharacterRemoved?.Invoke(id));
            if (character != null)
                OnCharacterChanged?.Invoke(character.Id);
        });
    }

    public void Dispose() {
        _fileSystemWatcher.EnableRaisingEvents = false;
        _fileSystemWatcher.OnModified -= FileSystemWatcher_Modified;
        _fileSystemWatcher.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record CharacterFile(string FilePath, CharacterData Character);
}
