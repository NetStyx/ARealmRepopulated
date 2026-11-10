using ARealmRepopulated.Core.IPC;
using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Data.Characters;
using ARealmRepopulated.Data.Scenarios;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace ARealmRepopulated.Core.Services.Scenarios;

public static class ScenarioNpcDataExtensions {
    
    public static unsafe NpcSpawnOptions ToSpawnOptions(this ScenarioNpcData npc, ICharacterDefinition definition, Character* localPlayer) {
        var options = new NpcSpawnOptions {
            Position = npc.Position,
            Rotation = npc.Rotation,
            DrawOffset = npc.DrawOffset,
            SnapToSurface = npc.SnapToSurface,
            Appearance = definition.Appearance,
            DisplayName = definition.Name,
            DisplayNameSetting = npc.DisplayNameSetting
        };

        if (definition.TryGetIntegrationProperty(IntegrationProvider.ActorNameConfigKey, out var actorName)) {
            options.IsPublic = true;
            options.Kind = ObjectKind.Pc;
            options.Name = actorName;
            options.HomeWorld = localPlayer->HomeWorld;
            options.CurrentWorld = localPlayer->CurrentWorld;

            if (definition.TryGetIntegrationProperty<bool>(IntegrationProvider.ExternalAppearanceConfigKey, out var isExternal) && isExternal) {
                options.AppearanceManagement = AppearanceManagement.External;
            }
        }

        return options;
    }
}
