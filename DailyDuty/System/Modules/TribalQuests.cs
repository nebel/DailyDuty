﻿using DailyDuty.Abstracts;
using DailyDuty.Models;
using DailyDuty.Models.Enums;
using DailyDuty.System.Localization;
using FFXIVClientStructs.FFXIV.Client.Game;
using KamiLib.AutomaticUserInterface;

namespace DailyDuty.System;

public class TribalQuestsConfig : ModuleConfigBase
{
    [IntConfigOption("NotificationThreshold", "ModuleConfiguration", 1, 0, 12)]
    public int NotificationThreshold = 12;

    [EnumConfigOption("ComparisonMode", "ModuleConfiguration", 1, "ComparisonHelp")]
    public ComparisonMode ComparisonMode = ComparisonMode.LessThan;
}

public class TribalQuestsData : ModuleDataBase
{
    [UintDisplay("AllowancesRemaining", "ModuleData", 1)]
    public uint RemainingAllowances;
}

public unsafe class TribalQuests : Module.DailyModule
{
    public override ModuleName ModuleName => ModuleName.TribalQuests;

    public override ModuleConfigBase ModuleConfig { get; protected set; } = new TribalQuestsConfig();
    public override ModuleDataBase ModuleData { get; protected set; } = new TribalQuestsData();
    private TribalQuestsConfig Config => ModuleConfig as TribalQuestsConfig ?? new TribalQuestsConfig();
    private TribalQuestsData Data => ModuleData as TribalQuestsData ?? new TribalQuestsData();

    public override void Update()
    {
        TryUpdateData(ref Data.RemainingAllowances, QuestManager.Instance()->GetBeastTribeAllowance());
        
        base.Update();
    }

    public override void Reset()
    {
        Data.RemainingAllowances = 12;
        
        base.Reset();
    }

    protected override ModuleStatus GetModuleStatus() => Config.ComparisonMode switch
    {
        ComparisonMode.LessThan when Config.NotificationThreshold > Data.RemainingAllowances => ModuleStatus.Complete,
        ComparisonMode.EqualTo when Config.NotificationThreshold == Data.RemainingAllowances => ModuleStatus.Complete,
        ComparisonMode.LessThanOrEqual when Config.NotificationThreshold >= Data.RemainingAllowances => ModuleStatus.Complete,
        _ => ModuleStatus.Incomplete
    };
    
    protected override StatusMessage GetStatusMessage() => new()
    {
        Message = $"{Data.RemainingAllowances} {Strings.AllowancesRemaining}",
    };
}