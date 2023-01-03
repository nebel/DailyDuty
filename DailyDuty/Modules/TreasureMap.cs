﻿using System;
using System.Linq;
using DailyDuty.DataModels;
using DailyDuty.Interfaces;
using DailyDuty.Localization;
using DailyDuty.UserInterface.Components;
using DailyDuty.Utilities;
using Dalamud.Game;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiLib.Caching;
using KamiLib.InfoBoxSystem;
using KamiLib.Interfaces;
using KamiLib.Utilities;
using Lumina.Excel.GeneratedSheets;
using Condition = KamiLib.Utilities.Condition;

namespace DailyDuty.Modules;

public class TreasureMapSettings : GenericSettings
{            
    public DateTime LastMapGathered;
}

internal class TreasureMap : IModule
{
    public ModuleName Name => ModuleName.TreasureMap;
    public IConfigurationComponent ConfigurationComponent { get; }
    public IStatusComponent StatusComponent { get; }
    public ILogicComponent LogicComponent { get; }
    public ITodoComponent TodoComponent { get; }
    public ITimerComponent TimerComponent { get; }

    private static TreasureMapSettings Settings => Service.ConfigurationManager.CharacterConfiguration.TreasureMap;
    public GenericSettings GenericSettings => Settings;

    public TreasureMap()
    {
        ConfigurationComponent = new ModuleConfigurationComponent(this);
        StatusComponent = new ModuleStatusComponent(this);
        LogicComponent = new ModuleLogicComponent(this);
        TodoComponent = new ModuleTodoComponent(this);
        TimerComponent = new ModuleTimerComponent(this);
    }

    public void Dispose()
    {
        LogicComponent.Dispose();
    }

    private class ModuleConfigurationComponent : IConfigurationComponent
    {
        public IModule ParentModule { get; }
        public ISelectable Selectable => new ConfigurationSelectable(ParentModule, this);

        public ModuleConfigurationComponent(IModule parentModule)
        {
            ParentModule = parentModule;
        }

        public void Draw()
        {
            InfoBox.Instance.DrawGenericSettings(this);

            InfoBox.Instance.DrawNotificationOptions(this);
        }
    }

    private class ModuleStatusComponent : IStatusComponent
    {
        public IModule ParentModule { get; }

        public ISelectable Selectable => new StatusSelectable(ParentModule, this, ParentModule.LogicComponent.Status);
        
        public ModuleStatusComponent(IModule parentModule)
        {
            ParentModule = parentModule;
        }

        public void Draw()
        {

            InfoBox.Instance.DrawGenericStatus(this);

            InfoBox.Instance
                .AddTitle(Strings.TreasureMap_NextMap)
                .BeginTable()
                .BeginRow()
                .AddString(Strings.TreasureMap_NextMap)
                .AddString(ModuleLogicComponent.GetNextTreasureMap())
                .EndRow()
                .EndTable()
                .Draw();
            
            InfoBox.Instance.DrawSuppressionOption(this);
        }
    }

    private unsafe class ModuleLogicComponent : ILogicComponent
    {
        public IModule ParentModule { get; }
        public DalamudLinkPayload? DalamudLinkPayload => null;
        public bool LinkPayloadActive => false;

        private delegate long GetNextMapAvailableTimeDelegate(UIState* uiState);

        [Signature("E8 ?? ?? ?? ?? 48 8B F8 E8 ?? ?? ?? ?? 49 8D 9F")]
        private readonly GetNextMapAvailableTimeDelegate getNextMapUnixTimestamp = null!;

        private static AtkUnitBase* ContentsTimerAgent => (AtkUnitBase*) Service.GameGui.GetAddonByName("ContentsInfo", 1);

        public ModuleLogicComponent(IModule parentModule)
        {
            ParentModule = parentModule;

            SignatureHelper.Initialise(this);

            Service.Chat.ChatMessage += OnChatMessage;
            Service.Framework.Update += OnFrameworkUpdate;
        }
        
        public void Dispose()
        {
            Service.Chat.ChatMessage -= OnChatMessage;
            Service.Framework.Update -= OnFrameworkUpdate;
        }

        public string GetStatusMessage() => Strings.TreasureMap_MapAvailable;

        public DateTime GetNextReset() => Time.NextDailyReset();

        public void DoReset()
        {
            // Do Nothing
        }

        public ModuleStatus GetModuleStatus() => TimeUntilNextMap() == TimeSpan.Zero ? ModuleStatus.Incomplete : ModuleStatus.Complete;

        private void OnChatMessage(XivChatType type, uint senderID, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (!Settings.Enabled) return;

            if ((int)type != 2115 || !Condition.IsGathering())
                return;

            if (message.Payloads.FirstOrDefault(p => p is ItemPayload) is not ItemPayload item)
                return;

            if (!IsMap(item.ItemId))
                return;

            Settings.LastMapGathered = DateTime.UtcNow;
            Service.ConfigurationManager.Save();
        }

        private static bool IsMap(uint itemID) => LuminaCache<TreasureHuntRank>.Instance
            .Any(item => item.ItemName.Row == itemID && item.ItemName.Row != 0);

        private static TimeSpan TimeUntilNextMap()
        {
            var lastMapTime = Settings.LastMapGathered;
            var nextAvailableTime = lastMapTime.AddHours(18);

            if (DateTime.UtcNow >= nextAvailableTime)
            {
                return TimeSpan.Zero;
            }
            else
            {
                return nextAvailableTime - DateTime.UtcNow;
            }
        }

        public static string GetNextTreasureMap()
        {
            var span = TimeUntilNextMap();

            if (span == TimeSpan.Zero)
            {
                return Strings.TreasureMap_MapAvailable;
            }

            return span.FormatTimespan(Settings.TimerSettings.TimerStyle.Value);
        }

        private DateTime GetNextMapAvailableTime()
        {
            var unixTimestamp = getNextMapUnixTimestamp(UIState.Instance());

            return unixTimestamp == -1 ? DateTime.MinValue : DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;
        }
        
        private void OnFrameworkUpdate(Framework framework)
        {
            if (ContentsTimerAgent == null) return;

            var nextAvailable = GetNextMapAvailableTime();

            if (nextAvailable != DateTime.MinValue)
            {
                var storedTime = Settings.LastMapGathered;
                storedTime = storedTime.AddSeconds(-storedTime.Second);

                var retrievedTime = nextAvailable;
                retrievedTime = retrievedTime.AddSeconds(-retrievedTime.Second).AddHours(-18);

                if (storedTime != retrievedTime)
                {
                    Settings.LastMapGathered = retrievedTime;
                    Service.ConfigurationManager.Save();
                }
            }
        }
    }

    private class ModuleTodoComponent : ITodoComponent
    {
        public IModule ParentModule { get; }
        public CompletionType CompletionType => CompletionType.Daily;
        public bool HasLongLabel => false;

        public ModuleTodoComponent(IModule parentModule)
        {
            ParentModule = parentModule;
        }

        public string GetShortTaskLabel() => Strings.TreasureMap_Label;

        public string GetLongTaskLabel() => Strings.TreasureMap_Label;
    }


    private class ModuleTimerComponent : ITimerComponent
    {
        public IModule ParentModule { get; }

        public ModuleTimerComponent(IModule parentModule)
        {
            ParentModule = parentModule;
        }

        public TimeSpan GetTimerPeriod() => TimeSpan.FromHours(18);

        public DateTime GetNextReset() => Settings.LastMapGathered + TimeSpan.FromHours(18);
    }
}