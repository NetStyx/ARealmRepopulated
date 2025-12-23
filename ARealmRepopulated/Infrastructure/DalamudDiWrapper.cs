using ARealmRepopulated.Configuration;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace ARealmRepopulated.Infrastructure;

public class DalamudDiWrapper(IDalamudPluginInterface pluginInterface) {
    public IServiceCollection CreateServiceCollection()
        => new ServiceCollection()
            .AddSingleton(pluginInterface)
            .AddSingleton(pluginInterface.GetRequiredService<IPluginLog>())
            .AddSingleton(pluginInterface.GetRequiredService<IDataManager>())
            .AddSingleton(pluginInterface.GetRequiredService<ICommandManager>())
            .AddSingleton(pluginInterface.GetRequiredService<IObjectTable>())
            .AddSingleton(pluginInterface.GetRequiredService<IClientState>())
            .AddSingleton(pluginInterface.GetRequiredService<IPlayerState>())
            .AddSingleton(pluginInterface.GetRequiredService<IFramework>())
            .AddSingleton(pluginInterface.GetRequiredService<IGameInteropProvider>())
            .AddSingleton(pluginInterface.GetRequiredService<ICondition>())
            .AddSingleton(pluginInterface.GetRequiredService<ITargetManager>())
            .AddSingleton(pluginInterface.GetRequiredService<IDtrBar>())
            .AddSingleton(pluginInterface.GetRequiredService<IGameGui>())
            .AddSingleton(pluginInterface.GetRequiredService<ITextureProvider>())
            .AddSingleton<ArrpGameHooks>()
            .AddSingleton<ArrpEventService>()
            .AddSingleton<ArrpDataCache>()
            .AddSingleton<ArrpDtrControl>()
            .AddSingleton(s => s.GetRequiredService<IDalamudPluginInterface>().GetPluginConfig() as PluginConfig ?? new PluginConfig());
}
