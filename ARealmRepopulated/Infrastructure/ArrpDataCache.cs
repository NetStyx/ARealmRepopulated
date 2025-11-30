using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARealmRepopulated.Infrastructure;

public class ArrpDataCache(IDataManager dataManager)
{
    private ExcelSheet<TerritoryType> _territoryTypeSheet = null!;
    private ExcelSheet<Emote> _emoteTypeSheet = null!;

    public void Populate()
    {
        _territoryTypeSheet = dataManager.GetExcelSheet<TerritoryType>();
        _emoteTypeSheet = dataManager.GetExcelSheet<Emote>();
    }

    public Emote GetEmote(ushort emoteId)
        => _emoteTypeSheet.GetRow(emoteId);

    public List<Emote> GetEmotes()
        => [.. _emoteTypeSheet];

    public TerritoryType GetTerritoryType(ushort territoryTypeId)
        => _territoryTypeSheet.GetRow(territoryTypeId);

}
