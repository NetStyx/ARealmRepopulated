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
        var integrations = sp.GetServices<IIntegrationSetup>();

        foreach (var integration in integrations) {
            integration.Setup();
        }
        return sp;
    }

}
