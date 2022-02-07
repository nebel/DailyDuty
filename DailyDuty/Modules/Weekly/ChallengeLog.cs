﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DailyDuty.Interfaces;
using ImGuiNET;

namespace DailyDuty.Modules.Weekly;

internal class ChallengeLog : ICollapsibleHeader, IUpdateable
{
    public void Dispose()
    {
    }

    public string HeaderText => "Challenge Log";

    void ICollapsibleHeader.DrawContents()
    {
        ImGui.Text("Not Implemented Yet");
    }

    public void Update()
    {
            
    }
}