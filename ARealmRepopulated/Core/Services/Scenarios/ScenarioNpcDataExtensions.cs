using ARealmRepopulated.Core.IPC;
using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Data.Scenarios;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace ARealmRepopulated.Core.Services.Scenarios;

public static class ScenarioNpcDataExtensions {

    /// <summary>
    /// The options a scenario actor is spawned with. The local player is only read for integration actors, which take over its worlds;
    /// every other actor keeps the npc world.
    /// </summary>
    public static unsafe NpcSpawnOptions ToSpawnOptions(this ScenarioNpcData npc, Character* localPlayer) {
        var options = new NpcSpawnOptions {
            Position = npc.Position,
            Rotation = npc.Rotation,
            DrawOffset = npc.DrawOffset,
            SnapToSurface = npc.SnapToSurface,
            Appearance = npc.Appearance
        };

        // an integration actor name makes the actor a Pc, so other plugins can find it by that name
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
