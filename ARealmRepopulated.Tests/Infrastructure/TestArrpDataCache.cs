using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Infrastructure;
using System.Collections.Generic;

namespace ARealmRepopulated.Tests.Infrastructure;

/// <summary>
/// Test implementation of <see cref="ArrpDataCache"/>.
/// </summary>
internal class TestArrpDataCache(byte height, float scaleFactor) : ArrpDataCache(null!, null!) {

    public List<(int ModelCharaId, NpcTribe Tribe, NpcSex Sex, NpcBodyType BodyType, float ModelScale)> Lookups { get; } = [];

    public override ModelScaleComponents? ReverseModelScale(int modelCharaId, NpcTribe tribe, NpcSex sex, NpcBodyType bodyType, float modelScale) {
        Lookups.Add((modelCharaId, tribe, sex, bodyType, modelScale));
        return modelCharaId == 0 ? new ModelScaleComponents(height, scaleFactor) : null;
    }
}
