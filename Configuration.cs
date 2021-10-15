﻿using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace OceanFishin
{
    public class Configuration : IPluginConfiguration
    {
        private DalamudPluginInterface? pluginInterface;
        public int Version { get; set; } = 0;

        public bool include_achievement_fish { get; set; } = true;
        //public bool highlight_recommended_bait {get;set;} = true;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface!.SavePluginConfig(this);
        }
    }
}