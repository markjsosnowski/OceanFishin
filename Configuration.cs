using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OceanFishin
{
    public class Configuration : IPluginConfiguration
    {
        private DalamudPluginInterface? pluginInterface;
        public int Version { get; set; } = 0;


        public bool IncludeAchievementFish { get; set; } = true;
        public bool HighlightRecommendedBait { get; set; } = true;

        public string[][] DisplayModes = new string[][]
        {
            new string[3] {Properties.Strings.Default, Properties.Strings.Minimal, Properties.Strings.Comprehensive },
            new string[3] {Properties.Strings.Suggestions_based_on_current_conditions_, Properties.Strings.Determines_the_single__best_choice_for_you_, Properties.Strings.All_possible_area_information_at_once_ }
        };

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