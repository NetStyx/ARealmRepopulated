using ARealmRepopulated.Core.IPC;
using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Data.Scenarios;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace ARealmRepopulated.Core.Services.Scenarios;

public static class ScenarioNpcDataExtensions {

    /// <summary>
    /// The definition a scenario actor is spawned with. See <see cref="NpcSpawnOptions"/> for details.
    /// </summary>
    public static unsafe NpcSpawnOptions ToSpawnOptions(this ScenarioNpcData npc, Character* localPlayer) {
        var options = new NpcSpawnOptions {
            Position = npc.Position,
            Rotation = npc.Rotation,
            DrawOffset = npc.DrawOffset,
            SnapToSurface = npc.SnapToSurface,
            Appearance = npc.Appearance
        };
        
        if (npc.TryGetIntegrationProperty(IntegrationProvider.ActorNameConfigKey, out var actorName)) {
            options.IsPublic = true;
            options.Kind = ObjectKind.Pc;
            options.Name = actorName;
            options.HomeWorld = localPlayer->HomeWorld;
            options.CurrentWorld = localPlayer->CurrentWorld;

            if (npc.TryGetIntegrationProperty<bool>(IntegrationProvider.ExternalAppearanceConfigKey, out var isExternal) && isExternal) {
                options.AppearanceManagement = AppearanceManagement.External;
            }
        }

        return options;
    }
}
