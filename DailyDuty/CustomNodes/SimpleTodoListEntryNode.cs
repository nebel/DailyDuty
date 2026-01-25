using DailyDuty.Classes;
using DailyDuty.Enums;
using DailyDuty.Features.TodoOverlay;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using System.Numerics;

namespace DailyDuty.CustomNodes;

public class SimpleTodoListEntryNode : TextNode {
    public required ModuleBase Module { get; init; }
    public required TodoPanelConfig Config { get; init; }

    public SimpleTodoListEntryNode() {
        AddEvent(AtkEventType.MouseClick, OnMouseClick);
    }

    private void OnMouseClick()
        => PayloadController.InvokePayload(Module.Tooltip?.ClickAction ?? PayloadId.Unset);

    public void Update() {
        TextColor = Config.TextColor;
        if (Module.ModuleInfo.Type == ModuleType.Special) {
            TextOutlineColor = new Vector4(0.30241936f, 0.30241936f, 0.30241936f, 1f);
        }
        else if (Module.ModuleInfo.Type == ModuleType.Daily) {
            TextOutlineColor = new Vector4(0.5568628f, 0.41568628f, 0.047058824f, 1f);
        }
        else if (Module.ModuleInfo.Type == ModuleType.Weekly) {
            TextOutlineColor = new Vector4(0.5282258f, 0.13312142f, 0.06602822f, 1f);
        }
        else {
            TextOutlineColor = Config.OutlineColor;
        }

        if (Module.Tooltip is not null) {

            // The tooltip has been changed
            if (TextTooltip != string.Empty && TextTooltip != Module.Tooltip.TooltipText) {
                TextTooltip = Module.Tooltip.TooltipText;
            }

            ShowClickableCursor = Module.Tooltip.ClickAction is not PayloadId.Unset;
        }
        else {
            ShowClickableCursor = false;
        }
    }
}
