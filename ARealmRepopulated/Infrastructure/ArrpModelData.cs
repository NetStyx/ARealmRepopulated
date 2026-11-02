using ARealmRepopulated.Data.Appearance;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace ARealmRepopulated.Infrastructure;

/// <summary>
/// Data and calculations related to a character model
/// </summary>
public class ArrpModelData(ArrpHumanCmpFile humanCmp, IDataManager dataManager) {
    private ExcelSheet<ModelChara> _modelCharaSheet = null!;

    public void Populate() {
        _modelCharaSheet = dataManager.GetExcelSheet<ModelChara>();
    }

    public bool IsHumanModel(int modelCharaId)
        => modelCharaId == 0 || _modelCharaSheet.GetRowOrDefault((uint)modelCharaId)?.Type == 1;

    public virtual ModelScaleComponents? ReverseModelScale(int modelCharaId, NpcTribe tribe, NpcSex sex, NpcBodyType bodyType, float modelScale) {
        if (!IsHumanModel(modelCharaId))
            return null;

        return humanCmp.Heights?.ReverseModelScale(tribe, sex, bodyType, modelScale);
    }

    public ushort GetModelSoundPack(int modelCharaId)
        => _modelCharaSheet.GetRowOrDefault((uint)modelCharaId)?.SEPack ?? 0;

    // Humans get theirs from the race instead, which is up to the customize data.
    public int GetModelSkeletonId(int modelCharaId) {
        var model = _modelCharaSheet.GetRowOrDefault((uint)modelCharaId);
        return model?.Type switch {
            2 => model.Value.Model + 10000,
            3 => model.Value.Model,
            _ => 0
        };
    }

    // Unknown6 contains the height per unit of scale for certain models (see /doc/research/nameplates.md#anchor).
    public float GetNamePlateOffsetScale(int modelCharaId)
        => (_modelCharaSheet.GetRowOrDefault((uint)modelCharaId)?.Unknown6 ?? 0) * 0.1f;
}
