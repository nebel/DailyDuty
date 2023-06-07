﻿using System;
using System.Drawing;
using System.Numerics;
using DailyDuty.Abstracts;
using DailyDuty.Models;
using DailyDuty.Models.Enums;
using DailyDuty.System.Commands;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using KamiLib;
using KamiLib.Atk;
using KamiLib.AutomaticUserInterface;
using KamiLib.GameState;
using KamiLib.Utilities;

namespace DailyDuty.System;

public class TodoController : IDisposable
{
    public TodoConfig Config = new();
    private bool configChanged;
    private readonly TodoCommands todoCommands = new();
    private TodoUiController? uiController;
    private Vector2? holdOffset;

    public void Dispose() => Unload();
    
    public void Load()
    {
        PluginLog.Debug($"[TodoConfig] Loading Todo System");
        
        KamiCommon.CommandManager.RemoveCommand(todoCommands);
        KamiCommon.CommandManager.AddCommand(todoCommands);
        Config = LoadConfig();
        
        uiController ??= new TodoUiController();

        foreach (var module in DailyDutySystem.ModuleController.GetModules())
        {
            module.ModuleConfig.TodoOptions.StyleChanged = true;
        }
    }
    
    public void Unload()
    {
        PluginLog.Debug("[TodoConfig] Unloading Todo System");
        
        KamiCommon.CommandManager.RemoveCommand(todoCommands);

        uiController?.Dispose();
        uiController = null;
    }
    
    public void DrawConfig()
    {
        DrawableAttribute.DrawAttributes(Config, () =>
        {
            SaveConfig();
            foreach (var module in DailyDutySystem.ModuleController.GetModules())
            {
                module.ModuleConfig.TodoOptions.StyleChanged = true;
            }
        });
    }

    public void DrawExtras()
    {
        if (Config.CanDrag && uiController != null)
        {
            var size = uiController.GetSize() + Vector2.One * TodoUiController.EdgeSize * 2.0f;

            var positionOffsetX = Config.Anchor.HasFlag(WindowAnchor.TopRight) ? size.X - TodoUiController.EdgeSize : TodoUiController.EdgeSize;
            var positionOffsetY = Config.Anchor.HasFlag(WindowAnchor.BottomLeft) ? size.Y - TodoUiController.EdgeSize : TodoUiController.EdgeSize;
            
            var position = Config.Position - new Vector2(positionOffsetX, positionOffsetY);

            ImGui.SetNextWindowPos(position);
            ImGui.SetNextWindowSize(size);
            if (ImGui.Begin("##todoDrag", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoBackground))
            {
                ImGui.GetBackgroundDrawList().AddRect(position, position + size, ImGui.GetColorU32(new Vector4(1.0f, 0.0f, 0.0f, 1.0f)), 0.0f, ImDrawFlags.RoundCornersNone, 2.0f);
                
                var pos = ImGui.GetMousePos();
                if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && ImGui.IsWindowFocused()) 
                {
                    holdOffset ??= Config.Position - pos;
                
                    var old = Config.Position;
                    Config.Position = (Vector2)(pos + holdOffset)!;
                
                    if (old != Config.Position)
                        configChanged = true;
                } 
                else 
                {
                    holdOffset = null;
                }
            }
            
            ImGui.End();
        }
    }

    public void Update()
    {
        uiController?.Show(Config.Enable);
        
        if (Config.Enable)
        {
            UpdateCategory(ModuleType.Daily, Config.DailyTasks);
            UpdateCategory(ModuleType.Weekly, Config.WeeklyTasks);
            UpdateCategory(ModuleType.Special, Config.SpecialTasks);
        
            if(Config.HideDuringQuests && Condition.IsInQuestEvent()) uiController?.Show(false);
            if(Config.HideInDuties && Condition.IsBoundByDuty()) uiController?.Show(false);
        
            uiController?.Update(Config);
        }
        
        if(configChanged) SaveConfig();
        configChanged = false;
    }

    private void UpdateCategory(ModuleType type, bool enabled)
    {
        foreach (var module in DailyDutySystem.ModuleController.GetModules(type))
        {
            if (enabled && module.ModuleConfig.TodoOptions.StyleChanged)
            {
                uiController?.UpdateModuleStyle(type, module.ModuleName, GetModuleTextStyleOptions(module));
                uiController?.UpdateHeaderStyle(type, HeaderOptions);
                uiController?.UpdateCategoryStyle(type, BackgroundImageOptions);
                    
                module.ModuleConfig.TodoOptions.StyleChanged = false;
            }
            
            uiController?.UpdateModule(type, module.ModuleName, GetModuleTodoLabel(module), GetModuleActiveState(module) && enabled || Config.PreviewMode);
            uiController?.UpdateCategoryHeader(type, GetCategoryLabel(type), Config.ShowHeaders);
            uiController?.UpdateCategory(type, enabled);
        }
    }
    
    private string GetModuleTodoLabel(BaseModule module)
    {
        var todoOptions = module.ModuleConfig.TodoOptions;

        if (todoOptions.UseCustomTodoLabel && todoOptions.CustomTodoLabel != string.Empty)
        {
            return todoOptions.CustomTodoLabel;
        }

        return module.ModuleName.GetLabel();
    }

    private bool GetModuleActiveState(BaseModule module)
    {
        if (!module.ModuleConfig.ModuleEnabled) return false;
        if (!module.ModuleConfig.TodoOptions.Enabled) return false;
        if (module.ModuleStatus is not ModuleStatus.Incomplete) return false;

        return true;
    }

    private string GetCategoryLabel(ModuleType type) => type switch
    {
        ModuleType.Daily => Config.DailyLabel,
        ModuleType.Weekly => Config.WeeklyLabel,
        ModuleType.Special => Config.SpecialLabel,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };
    
    private TextNodeOptions HeaderOptions => new()
    {
        Alignment = AlignmentType.Left,
        TextColor = Config.HeaderTextColor,
        EdgeColor = Config.HeaderTextOutline,
        BackgroundColor = KnownColor.White.AsVector4(),
        FontSize = (byte) Config.HeaderFontSize,
        Flags = GetHeaderFlags(),
        Type = NodeType.Text,
    };

    private TextNodeOptions GetModuleTextStyleOptions(BaseModule module) => new()
    {
        Alignment = AlignmentType.Left,
        TextColor = module.ModuleConfig.TodoOptions.OverrideTextColor ? module.ModuleConfig.TodoOptions.TextColor : Config.ModuleTextColor,
        EdgeColor = module.ModuleConfig.TodoOptions.OverrideTextColor ? module.ModuleConfig.TodoOptions.TextOutline : Config.ModuleOutlineColor,
        BackgroundColor = KnownColor.White.AsVector4(),
        FontSize = (byte) Config.FontSize,
        Flags = GetModuleFlags(),
        Type = NodeType.Text,
    };

    private ImageNodeOptions BackgroundImageOptions => new()
    {
        Color = Config.CategoryBackgroundColor
    };

    private TextFlags GetHeaderFlags()
    {
        var flags = TextFlags.AutoAdjustNodeSize;

        if (Config.HeaderItalic) flags |= TextFlags.Italic;
        if (Config.Edge) flags |= TextFlags.Edge;
        if (Config.Glare) flags |= TextFlags.Glare;

        return flags;
    }
    
    private TextFlags GetModuleFlags()
    {
        var flags = TextFlags.AutoAdjustNodeSize;

        if (Config.ModuleItalic) flags |= TextFlags.Italic;
        if (Config.Edge) flags |= TextFlags.Edge;
        if (Config.Glare) flags |= TextFlags.Glare;

        return flags;
    }
    
    public void Show() => uiController?.Show(Config.Enable);
    public void Hide() => uiController?.Hide();
    private TodoConfig LoadConfig() => (TodoConfig) FileController.LoadFile("Todo.config.json", Config);
    public void SaveConfig() => FileController.SaveFile("Todo.config.json", Config.GetType(), Config);
}