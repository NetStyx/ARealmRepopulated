using ARealmRepopulated.Data.Appearance;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace ARealmRepopulated.Infrastructure;

/// <summary>
/// Data related to a character model
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

    // Humans (type 1) fall through on purpose. Their skeleton comes from tribe, sex and body type in the customize data, 
    // which the game turns into 20000 + model code when it builds the character. The game checks if the skeleton is 0 and takes over for us. How nice of it.
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
