using KamiLib.Extensions;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;

namespace DailyDuty.Classes;

public class Cache
{
    public DateTime UtcNow = DateTime.Now;
    public bool IsBoundByDuty;
    public bool IsInQuestEvent;
    public byte DataCenterRegion;

    public void OnLogin()
    {
        DataCenterRegion = LookupDatacenterRegion(Service.ClientState.LocalPlayer?.HomeWorld.GameData?.DataCenter.Row);
    }

    public void OnUpdate()
    {
        UtcNow = DateTime.UtcNow;
        IsBoundByDuty = Service.Condition.IsBoundByDuty();
        IsInQuestEvent = Service.Condition.IsInQuestEvent();
    }

    private static byte LookupDatacenterRegion(uint? playerDatacenterId) {
        if (playerDatacenterId == null) return 0;

        return Service.DataManager.GetExcelSheet<WorldDCGroupType>()!
            .Where(world => world.RowId == playerDatacenterId.Value)
            .Select(dc => dc.Region)
            .FirstOrDefault();
    }
}