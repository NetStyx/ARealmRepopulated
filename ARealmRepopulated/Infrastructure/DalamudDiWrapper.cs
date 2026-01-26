using ARealmRepopulated.Configuration;
using ARealmRepopulated.Core.l10n;
using ARealmRepopulated.Core.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;

namespace ARealmRepopulated.Infrastructure;

public class DalamudDiWrapper(IDalamudPluginInterface pluginInterface, IPluginLog pluginLog) {
    public IServiceCollection CreateServiceCollection()
        => new ServiceCollection()
            .AddLogging(c => {
                c.ClearProviders();
                c.SetMinimumLevel(LogLevel.Trace);
                c.Services.AddSingleton<ILoggerProvider>(new ArrpLogProvider(pluginLog));
            })
            .AddSingleton(pluginLog)
            .AddSingleton(pluginInterface)
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
            .AddSingleton<ArrpCharacterCreationData>()
            .AddSingleton<ArrpDtrControl>()
            .AddSingleton<ArrpTranslation>()
            .AddSingleton(s => s.GetRequiredService<IDalamudPluginInterface>().GetPluginConfig() as PluginConfig ?? new PluginConfig());
}
