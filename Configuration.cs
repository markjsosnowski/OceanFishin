using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Diagnostics;

namespace OceanFishin
{
    public class Configuration : IPluginConfiguration
    {
        private DalamudPluginInterface? pluginInterface;
        public int Version { get; set; } = 0;


        public bool include_achievement_fish { get; set; } = true;
        public bool highlight_recommended_bait { get; set; } = true;
        public bool always_show_all { get; set; } = false;

        public string[] display_modes = new string[] { "default", "min", "full" };
        public string[] display_mode_desc = new string[] { "default-desc", "min-desc", "full-desc" };
        public int display_mode { get; set; } = 0;

        public bool DebugMode = false;
        public OceanFishin.Location DebugLocation = OceanFishin.Location.Unknown;
        public OceanFishin.Time DebugTime = OceanFishin.Time.Unknown;
        public bool DebugSpectral = false;
        public bool DebugIntution = false;

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