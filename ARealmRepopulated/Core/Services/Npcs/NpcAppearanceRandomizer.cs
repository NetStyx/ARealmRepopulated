using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Infrastructure;
using Lumina.Excel.Sheets;

namespace ARealmRepopulated.Core.Services.Npcs;

public readonly record struct AppearanceRandomizationOptions(AppearanceMixLocks Locks, uint? ResidentsOfTerritory);

/// <summary>
/// Decides who goes into the mix of the <see cref="NpcAppearanceMixer"/> and finishes what comes out of it.
/// </summary>
public class NpcAppearanceRandomizer(NpcAppearanceService appearanceService, ArrpNpcData npcData, ArrpCharacterCreationData creationData) {

    private List<NpcAppearanceData>? _everyone;

    public NpcAppearanceData? Randomize(NpcAppearanceData current, AppearanceRandomizationOptions options) {
        var isCustomizable = creationData.IsCustomizable(current);
        var locks = isCustomizable ? options.Locks : AppearanceMixLocks.None;

        var everyone = _everyone ??= ReadPeople(npcData.GetHumanENpcs());
        var residents = options.ResidentsOfTerritory is { } territoryId ? ReadPeople(npcData.GetResidents(territoryId)) : null;

        // too few people and random does not feel random anymore
        var people = residents is { Count: >= 10 } ? residents : everyone;

        var mixed = NpcAppearanceMixer.Mix(current, people, everyone, locks);
        if (mixed == null)
            return null;

        // the ability to select the young bodytype is creating more trouble then its worth, but anyway! lets make sure we dont fuck it up
        // if the clan lock exists on a young body type
        if (current.BodyType != NpcBodyType.Normal) {
            creationData.GetClan(mixed.Tribe, mixed.Sex)?.ClampOptions(mixed, npcData.GetBodyTypeLooks(mixed));
        }

        if (mixed.Voice == null && creationData.GetVoices(mixed.Race, mixed.Tribe, mixed.Sex) is { Length: > 0 } voices) {
            mixed.Voice = voices[Random.Shared.Next(voices.Length)];
        }

        if (!isCustomizable) {
            mixed.Scale = NpcAppearanceData.ScaleDefault;
        }

        return mixed;
    }

    private List<NpcAppearanceData> ReadPeople(IEnumerable<ENpcBase> npcs)
        => [.. npcs.Where(n => n.BodyType == (byte)NpcBodyType.Normal).Select(n => {
            var person = new NpcAppearanceData();
            appearanceService.Read(n, person);
            return person;
        })];
}
