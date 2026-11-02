using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Infrastructure;
using Lumina.Excel.Sheets;

namespace ARealmRepopulated.Core.Services.Npcs;

public readonly record struct AppearanceRandomizationOptions(AppearanceRollLocks Locks, uint? ResidentsOfTerritory);

/// <summary>
/// Decides who goes into the mix of the <see cref="AppearanceRoller"/> and finishes what comes out of it.
/// </summary>
public class NpcAppearanceRandomizer(NpcAppearanceService appearanceService, ArrpNpcData npcData, ArrpCharacterCreationData creationData) {

    private List<NpcAppearanceData>? _everyone;

    /// <summary>
    /// Randomize an appearance.
    /// </summary>
    public NpcAppearanceData? Randomize(NpcAppearanceData current, AppearanceRandomizationOptions options) {
        var isCustomizable = creationData.IsCustomizable(current);
        var locks = isCustomizable ? options.Locks : AppearanceRollLocks.None;

        var everyone = _everyone ??= ReadPeople(npcData.GetHumanENpcs());
        var residents = options.ResidentsOfTerritory is { } territoryId ? ReadPeople(npcData.GetResidents(territoryId)) : null;

        // too few people and random does not feel random anymore
        var people = residents is { Count: >= 10 } ? residents : everyone;

        var rolled = AppearanceRoller.Roll(current, people, everyone, locks);
        if (rolled == null)
            return null;

        // the ability to select the young bodytype is creating more trouble then its worth, but anyway! lets make sure we dont fuck it up
        // if the clan lock exists on a young body type
        if (current.BodyType != NpcBodyType.Normal) {
            creationData.GetClan(rolled.Tribe, rolled.Sex)?.FitOptions(rolled, npcData.GetBodyTypeLooks(rolled));
        }

        if (rolled.Voice == null && creationData.GetVoices(rolled.Race, rolled.Tribe, rolled.Sex) is { Length: > 0 } voices) {
            rolled.Voice = voices[Random.Shared.Next(voices.Length)];
        }

        if (!isCustomizable) {
            rolled.Scale = NpcAppearanceData.ScaleDefault;
        }

        return rolled;
    }

    private List<NpcAppearanceData> ReadPeople(IEnumerable<ENpcBase> npcs)
        => [.. npcs.Where(n => n.BodyType == (byte)NpcBodyType.Normal).Select(n => {
            var person = new NpcAppearanceData();
            appearanceService.Read(n, person);
            return person;
        })];
}
