using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Infrastructure;
using System.Collections.Generic;

namespace ARealmRepopulated.Tests.Infrastructure;

/// <summary>
/// Returns a fixed model scale for human models.
/// </summary>
internal class FixedModelScaleCache(float modelScale) : ArrpDataCache(null!, null!) {

    public List<(int ModelCharaId, NpcTribe Tribe, NpcSex Sex, NpcBodyType BodyType, byte Height)> Lookups { get; } = [];

    public override float? GetModelScale(int modelCharaId, NpcTribe tribe, NpcSex sex, NpcBodyType bodyType, byte height) {
        Lookups.Add((modelCharaId, tribe, sex, bodyType, height));
        return modelCharaId == 0 ? modelScale : null;
    }
}
