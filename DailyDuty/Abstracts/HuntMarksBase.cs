﻿using System.Collections.Generic;
using System.Linq;
using DailyDuty.Models;
using DailyDuty.Models.Attributes;
using DailyDuty.Models.Enums;
using DailyDuty.System.Localization;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using KamiLib.Caching;
using Lumina.Excel.GeneratedSheets;

namespace DailyDuty.Abstracts;

public class HuntMarksConfig : ModuleConfigBase
{
    [SelectableTasks]
    public List<LuminaTaskConfig<MobHuntOrderType>> Tasks = new();
}

public class HuntMarksData : ModuleDataBase
{
    [SelectableTasks] 
    public List<LuminaTaskData<MobHuntOrderType>> Tasks = new();
}

public abstract unsafe class HuntMarksBase : Module.SpecialModule
{
    public override ModuleDataBase ModuleData { get; protected set; } = new HuntMarksData();
    public override ModuleConfigBase ModuleConfig { get; protected set; } = new HuntMarksConfig();
    protected HuntMarksConfig Config => ModuleConfig as HuntMarksConfig ?? new HuntMarksConfig();
    protected HuntMarksData Data => ModuleData as HuntMarksData ?? new HuntMarksData();

    private static MobHunt* HuntData => MobHunt.Instance();
    
    public override void Update()
    {
        foreach (var task in Data.Tasks)
        {
            // If we have the active mark bill
            if (HuntData->unkArray[task.RowId] == HuntData->MarkID[task.RowId] && !task.Complete)
            {
                var orderData = LuminaCache<MobHuntOrderType>.Instance.GetRow(task.RowId)!;
                var targetRow = orderData.OrderStart.Row + HuntData->MarkID[task.RowId] - 1;
                
                // Elite
                if (orderData.Type is 2 && IsEliteMarkComplete(targetRow, task.RowId))
                {
                    task.Complete = true;
                    DataChanged = true;
                }
                // Regular Hunt
                else if (orderData.Type is 1 && IsNormalMarkComplete(targetRow, task.RowId))
                {
                    task.Complete = true;
                    DataChanged = true;
                }
            }
        }
        
        base.Update();
    }

    private bool IsEliteMarkComplete(uint targetRow, uint markId)
    {
        var eliteTargetInfo = LuminaCache<MobHuntOrder>.Instance.GetRow(targetRow, 0)!;

        return HuntData->CurrentKillsSpan[(int) markId][0] == eliteTargetInfo.NeededKills;
    }

    private bool IsNormalMarkComplete(uint targetRow, uint markId)
    {
        var allTargetsKilled = Enumerable.Range(0, 5).All(index => 
        {
            var huntData = LuminaCache<MobHuntOrder>.Instance.GetRow(targetRow, (uint) index)!;

            return HuntData->CurrentKillsSpan[(int) markId][index] == huntData.NeededKills;
        });

        return allTargetsKilled;
    }

    public override void Reset()
    {
        foreach (var data in Data.Tasks)
        {
            data.Complete = false;
        }
        
        base.Reset();
    }
    
    protected override ModuleStatus GetModuleStatus() => GetIncompleteCount(Config.Tasks, Data.Tasks) == 0 ? ModuleStatus.Complete : ModuleStatus.Incomplete;

    protected override StatusMessage GetStatusMessage() => new()
    {
        Message = $"{GetIncompleteCount(Config.Tasks, Data.Tasks)} {Strings.HuntsRemaining}",
    };
}