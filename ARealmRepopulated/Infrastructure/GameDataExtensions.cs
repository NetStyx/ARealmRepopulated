namespace ARealmRepopulated.Infrastructure;

public static class GameDataExtensions {

    public static IServiceCollection AddGameData(this IServiceCollection services) {
        services
            .AddSingleton<ArrpAnimationData>()
            .AddSingleton<ArrpCharacterCreationData>()
            .AddSingleton<ArrpHumanCmpFile>()
            .AddSingleton<ArrpItemData>()
            .AddSingleton<ArrpModelData>()
            .AddSingleton<ArrpNpcData>()
            .AddSingleton<ArrpTerritoryData>();
        return services;
    }

}
