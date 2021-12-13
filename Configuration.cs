using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace OceanFishin
{
    public class Configuration : IPluginConfiguration
    {
        private DalamudPluginInterface? pluginInterface;
        public int Version { get; set; } = 0;


        public bool include_achievement_fish { get; set; } = true;
        public bool highlight_recommended_bait { get; set; } = true;
        public bool always_show_all { get; set; } = false;

        public string[] display_modes = new string[] { "Standard", "Compact", "Full" };
        public string[] display_mode_desc = new string[] {  "Default; recommends bait based on current conditions.",
                                                            "Only recommends one bait at a time based on current conditions.",
                                                            "Shows a list of all bait recommendations for the area at all times."};
        public int display_mode { get; set; } = 0;

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