using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiLib.Extensions;
using Lumina.Excel.Sheets;
using System;

namespace DailyDuty.Classes;

public class Cache
{
    public DateTime UtcNow = DateTime.Now;
    public bool IsBoundByDuty;
    public bool IsInQuestEvent;
    public byte DataCenterRegion;

    public void OnLogin()
    {
        DataCenterRegion = LookupDatacenterRegion();
    }

    public void OnUpdate()
    {
        UtcNow = DateTime.UtcNow;
        IsBoundByDuty = Service.Condition.IsBoundByDuty();
        IsInQuestEvent = Service.Condition.IsInQuestEvent();
    }

    private static unsafe byte LookupDatacenterRegion()
    {
        var worldId = AgentLobby.Instance()->LobbyData.HomeWorldId;
        var world = Service.DataManager.GetExcelSheet<World>().GetRow(worldId);
        return Service.DataManager.GetExcelSheet<WorldDCGroupType>().GetRow(world.DataCenter.RowId).Region;
    }
}