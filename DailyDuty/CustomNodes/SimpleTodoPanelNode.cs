using DailyDuty.Classes;
using DailyDuty.Enums;
using DailyDuty.Features.TodoOverlay;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Enums;
using KamiToolKit.Extensions;
using KamiToolKit.Nodes;
using KamiToolKit.UiOverlay;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DailyDuty.CustomNodes;

public unsafe class SimpleTodoPanelNode : OverlayNode {
    public override OverlayLayer OverlayLayer => OverlayLayer.BehindUserInterface;
    private readonly VerticalListNode warningList;

    private TodoOverlayPanelConfigWindow? configWindow;

    public required TodoPanelConfig Config { get; init; }
    public required TodoOverlayConfig ModuleTodoOverlayConfig { get; init; }

    private List<ModuleBase> lastModules = [];
    private bool lastVisible;

    public SimpleTodoPanelNode() {
        warningList = new VerticalListNode {
            FitContents = true,
        };
        warningList.AttachNode(this);

        OnMoveComplete = _ => {
            Config?.Position = Position;
            ModuleTodoOverlayConfig?.MarkDirty();
        };

        if (warningList.Nodes.Count is 0 && System.ModuleManager.LoadedModules is { } modules) {
            foreach (var loadedModule in modules.OrderBy(module => module, ModuleComparer.Instance)) {
                if (loadedModule.FeatureBase is ModuleBase module) {
                    var entry = BuildTodoEntry(loadedModule, module);

                    warningList.AddNode(entry);
                }
            }
        }
    }

    protected override void OnSizeChanged() {
        base.OnSizeChanged();

        warningList.Width = Width - 32.0f;
        warningList.Position = new Vector2(16.0f, 0);
        warningList.RecalculateLayout();
    }

    protected override void Dispose(bool isNativeDestructor) {
        if (IsDisposed) return;

        base.Dispose(isNativeDestructor);

        configWindow?.Dispose();
        configWindow = null;
    }

    protected override void OnUpdate() {
        if (Config.AttachToQuestList) {
            var todoAddon = RaptureAtkUnitManager.Instance()->GetAddonByName("_ToDoList");
            if (todoAddon is not null) {
                var halfX = AtkStage.Instance()->ScreenSize.Width / 2.0f;

                if (todoAddon->Position.X < halfX) {
                    Position = todoAddon->Position + new Vector2(0.0f, todoAddon->RootSize.Y * todoAddon->Scale);
                }
                else {
                    Position = todoAddon->Position + todoAddon->RootSize * todoAddon->Scale - new Vector2(Width, 0.0f) * Config.Scale;
                }
            }
        }

        var warningModules = Config.Modules.Select(moduleName => System.ModuleManager.GetModule(moduleName))
            .OfType<ModuleBase>()
            .Where(module => module is { ModuleStatus: CompletionStatus.Incomplete or CompletionStatus.ResultsAvailable, IsEnabled: true })
            .OrderBy(module => module, ModuleComparer.Instance)
            .ToList();

        var shouldHideInDuties = ModuleTodoOverlayConfig.HideInDuties && ICondition.Get().IsBoundByDuty;
        var shouldHideInQuestEvent = ModuleTodoOverlayConfig.HideDuringQuests && ICondition.Get().IsInCutsceneOrQuestEvent;
        var shouldHideNoWarnings = !(warningModules.Count is not 0 || Config.Modules.Count is 0);

        IsVisible = !shouldHideNoWarnings && !shouldHideInQuestEvent && !shouldHideInDuties;
        EnableMoving = Config.EnableMoving;

        if (lastVisible == IsVisible && lastModules.SequenceEqual(warningModules))
            return;

        IPluginLog.Get().Debug($"Updating {nameof(SimpleTodoPanelNode)}");
        lastVisible = IsVisible;
        lastModules = [.. warningModules];

        foreach (var entry in warningList.GetNodes<TodoListEntryNode>()) {
            entry.IsVisible = warningModules.Contains(entry.Module);
            entry.TextColor = Config.TextColor;
            entry.TextOutlineColor = entry.Module.ModuleInfo.Type switch {
                ModuleType.Special => new Vector4(0.30241936f, 0.30241936f, 0.30241936f, 1f),
                ModuleType.Daily => new Vector4(0.5568628f, 0.41568628f, 0.047058824f, 1f),
                ModuleType.Weekly => new Vector4(0.5282258f, 0.13312142f, 0.06602822f, 1f),
                _ => Config.OutlineColor
            };

            if (entry is { IsVisible: true, Module.Tooltip: { TooltipText: { IsEmpty: false } tooltipText } tooltipEntry }) {
                entry.TextTooltip = tooltipText;
                entry.ShowClickableCursor = tooltipEntry.ClickAction is not PayloadId.Unset;
            }
            else {
                entry.ShowClickableCursor = false;
            }

            entry.AlignmentType = Config.Alignment switch {
                VerticalListAlignment.Left => AlignmentType.Left,
                VerticalListAlignment.Right => AlignmentType.Right,
                _ => AlignmentType.Left,
            };
        }

        warningList.Alignment = Config.Alignment;
        warningList.IsVisible = !Config.IsCollapsed;
        warningList.ItemSpacing = -8f;
        warningList.RecalculateLayout();

        var newHeight = warningList.Bounds.Bottom + 18.0f;
        if (Math.Abs(Height - newHeight) > 0.1f) {
            Height = newHeight;
        }
    }

    private TodoListEntryNode BuildTodoEntry(LoadedModule loadedModule, ModuleBase module) => new() {
        Height = 24.0f,
        TextFlags = TextFlags.AutoAdjustNodeSize | TextFlags.Edge,
        AlignmentType = AlignmentType.Left,
        Module = module,
        LoadedModule = loadedModule,
        String = module.Name,
        Config = Config,
    };
}

public class ModuleComparer : IComparer<ModuleBase>, IComparer<LoadedModule> {
    public static readonly ModuleComparer Instance = new();

    public int Compare(ModuleBase? a, ModuleBase? b) {
        return Compare(a?.ModuleInfo, b?.ModuleInfo);
    }

    public int Compare(LoadedModule? a, LoadedModule? b) {
        return Compare(a?.FeatureBase.ModuleInfo, b?.FeatureBase.ModuleInfo);
    }

    private static int Compare(ModuleInfo? a, ModuleInfo? b) {
        if (a is null && b is null) return 0;
        if (a is null) return -1;
        if (b is null) return 1;

        var diff = a.Type.CompareTo(b.Type);
        if (diff == 0)
            diff = string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
        return diff;
    }
}
