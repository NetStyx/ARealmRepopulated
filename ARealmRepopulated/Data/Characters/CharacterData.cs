using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Data.Scenarios;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ARealmRepopulated.Data.Characters;

public interface ICharacterDefinition {
    string Name { get; set; }
    NpcAppearanceData Appearance { get; set; }
    Dictionary<string, string> AdditionalData { get; set; }
}

public class CharacterData : ICharacterDefinition {
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Revision { get; set; }
    public string Name { get; set; } = "";
    public NpcAppearanceData Appearance { get; set; } = NpcAppearanceData.Default;
    public Dictionary<string, string> AdditionalData { get; set; } = [];

    [DefaultValue("")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Group { get; set; } = "";

    public string[] GroupSegments()
        => Group.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public CharacterData Copy() {
        var copy = new CharacterData { Id = Id, Revision = Revision, Group = Group };
        copy.CopyCharacterFrom(this);
        return copy;
    }
}

public static class CharacterDefinitionExtensions {

    public static void CopyCharacterFrom(this ICharacterDefinition target, ICharacterDefinition source) {
        target.Name = source.Name;
        target.Appearance = source.Appearance.Copy();
        target.AdditionalData = new(source.AdditionalData);
    }

    public static bool HasSameCharacterAs(this ICharacterDefinition definition, ICharacterDefinition other)
        => definition.Name == other.Name
            && definition.Appearance == other.Appearance
            && definition.AdditionalData.Count == other.AdditionalData.Count
            && definition.AdditionalData.All(e => other.AdditionalData.TryGetValue(e.Key, out var value) && value == e.Value);

    public static List<Guid> ApplyCharacters(this ScenarioData scenario, IReadOnlyDictionary<Guid, CharacterData> characters) {
        var missing = new List<Guid>();
        foreach (var npc in scenario.Npcs) {
            if (npc.CharacterId is not { } id)
                continue;

            if (characters.TryGetValue(id, out var character)) {
                npc.CopyCharacterFrom(character);
            } else {
                missing.Add(id);
            }
        }
        return missing;
    }

    /// <summary>
    /// Whether an imported character replaces the local one: unknown, newer, or the same revision with other content.
    /// </summary>
    public static bool ReplacesLocal(this CharacterData imported, CharacterData? local)
        => local == null
            || imported.Revision > local.Revision
            || (imported.Revision == local.Revision && !imported.HasSameCharacterAs(local));
}
