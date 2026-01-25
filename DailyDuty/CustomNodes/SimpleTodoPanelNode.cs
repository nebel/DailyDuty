using DailyDuty.Classes;
using DailyDuty.Enums;
using DailyDuty.Features.TodoOverlay;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Enums;
using KamiToolKit.Extensions;
using KamiToolKit.Nodes;
using KamiToolKit.Overlay.UiOverlay;
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

    public SimpleTodoPanelNode() {
        warningList = new VerticalListNode {
            FitContents = true,
        };
        warningList.AttachNode(this);

        OnMoveComplete = _ => {
            Config?.Position = Position;
            ModuleTodoOverlayConfig?.MarkDirty();
        };
    }

    protected override void OnSizeChanged() {
        base.OnSizeChanged();

        warningList.Width = Width - 32.0f;
        warningList.Position = new Vector2(16.0f, 0);
        warningList.RecalculateLayout();
    }

    protected override void Dispose(bool disposing, bool isNativeDestructor) {
        base.Dispose(disposing, isNativeDestructor);

        configWindow?.Dispose();
        configWindow = null;
    }

    protected override void OnUpdate() {
        if (Config.AttachToQuestList) {
            var todoAddon = RaptureAtkUnitManager.Instance()->GetAddonByName("_ToDoList");
            if (todoAddon is not null) {
                Position = todoAddon->Position + todoAddon->RootSize - new Vector2(Width, 0.0f);
            }
        }

        EnableMoving = Config.EnableMoving;

        if (Config.Alignment != warningList.Alignment) {
            warningList.Alignment = Config.Alignment;
            warningList.RecalculateLayout();
        }

        warningList.IsVisible = !Config.IsCollapsed;
        warningList.ItemSpacing = Config.ItemSpacing;

        var warningModules = Config.Modules.Select(moduleName => System.ModuleManager.GetModule(moduleName))
            .OfType<ModuleBase>()
            .Where(module => module.ModuleStatus is CompletionStatus.Incomplete)
            .OrderBy(module => module, ModuleComparer.Instance)
            .ToList();

        var shouldHideInDuties = ModuleTodoOverlayConfig.HideInDuties && Services.Condition.IsBoundByDuty;
        var shouldHideInQuestEvent = ModuleTodoOverlayConfig.HideDuringQuests && Services.Condition.IsInCutsceneOrQuestEvent;
        var shouldHideNoWarnings = !(warningModules.Count is not 0 || Config.Modules.Count is 0);

        IsVisible = !shouldHideNoWarnings && !shouldHideInQuestEvent && !shouldHideInDuties;


        if (warningList.SyncWithListData(warningModules, node => node.Module, BuildTodoEntry)) {
            warningList.Width = MathF.Max(50.0f, warningList.Nodes.Sum(node => node.IsVisible ? node.Width : 0.0f));
            warningList.ReorderNodes((a, b) => ModuleComparer.Instance.CompareRawNodes(a, b));
            warningList.RecalculateLayout();

            Height = warningList.Bounds.Bottom + 18.0f;
        }

        foreach (var node in warningList.GetNodes<SimpleTodoListEntryNode>()) {
            node.Update();
        }
    }

    private SimpleTodoListEntryNode BuildTodoEntry(ModuleBase data) {
        var newNode = new SimpleTodoListEntryNode {
            Height = 24.0f,
            TextFlags = TextFlags.AutoAdjustNodeSize | TextFlags.Edge,
            AlignmentType = AlignmentType.Left,
            Module = data,
            String = data.Name,
            Config = Config,
        };

        if (data.Tooltip is { TooltipText.IsEmpty: false }) {
            newNode.TextTooltip = data.Tooltip.TooltipText;

            if (data.Tooltip is { ClickAction: not PayloadId.Unset }) { }
        }

        return newNode;
    }
}

public class ModuleComparer : IComparer<ModuleBase> {
    public static readonly ModuleComparer Instance = new();

    public int CompareRawNodes(NodeBase a, NodeBase b) {
        return CompareNodes(a as SimpleTodoListEntryNode, b as SimpleTodoListEntryNode);
    }

    public int CompareNodes(SimpleTodoListEntryNode? a, SimpleTodoListEntryNode? b) {
        return Compare(a?.Module, b?.Module);
    }

    public int Compare(ModuleBase? a, ModuleBase? b) {
        if (a is null && b is null) return 0;
        if (a is null) return -1;
        if (b is null) return 1;

        var diff = a.ModuleInfo.Type.CompareTo(b.ModuleInfo.Type);
        if (diff == 0)
            diff = string.Compare(a.ModuleInfo.DisplayName, b.ModuleInfo.DisplayName, StringComparison.Ordinal);
        return diff;
    }
}
