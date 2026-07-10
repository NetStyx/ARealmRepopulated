using Dalamud.Plugin.Services;

namespace ARealmRepopulated.Core.IPC;

public static class IntegrationExtensions {

    public static IServiceCollection AddIntegrations(this IServiceCollection services) {
        services.AddIntegration<Glamourer>();
        services.AddIntegration<Penumbra>();
        return services;
    }

    private static void AddIntegration<T>(this IServiceCollection sp) where T : class, IIntegrationSetup {
        sp.AddSingleton<T>();
        sp.AddSingleton<IIntegrationSetup, T>((sp) => sp.GetRequiredService<T>());
    }

    public static IServiceProvider EnableIntegrations(this IServiceProvider sp) {
        var logger = sp.GetRequiredService<IPluginLog>();
        var integrations = sp.GetServices<IIntegrationSetup>();

        foreach (var integration in integrations) {
            try {
                integration.Setup();
            } catch (Exception e) {
                logger.Error(e, "Error occurred while setting up integration: {0}", integration.GetType().Name);
            }
        }
        return sp;
    }

}
