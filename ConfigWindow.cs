using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static Dalamud.Interface.Windowing.Window;

namespace OceanFishin
{

    public class ConfigWindow : Window, IDisposable
    {
        private OceanFishin Plugin;
        private Configuration Configuration;
        private Localizer Localizer;

        public ConfigWindow(OceanFishin plugin, Configuration configuration, Localizer localizer) : base("Ocean Fishin' Configuration", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(255, 135),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };

            this.Plugin = plugin;
            this.Configuration = configuration;
            this.Localizer = localizer;
        }

        public void Dispose(){}

        public override void Draw()
        {
            var display_mode = this.Configuration.display_mode;
            if (ImGui.Combo(Properties.Strings.Display_Mode, ref display_mode, this.Configuration.DisplayModes[0], 3))
            {
                this.Configuration.display_mode = display_mode;
                this.Configuration.Save();
            }

            ImGui.TextWrapped(Localizer.Localize(this.Configuration.DisplayModes[1][this.Configuration.display_mode]));

            var include_achievement_fish = this.Configuration.IncludeAchievementFish;
            if (ImGui.Checkbox(Properties.Strings.Include_suggestions_for_mission_and_achievement_fish_, ref include_achievement_fish))
            {
                this.Configuration.IncludeAchievementFish = include_achievement_fish;
                this.Configuration.Save();
            }

            var debugMode = this.Configuration.DebugMode;
            if (ImGui.Checkbox("Debug Tools", ref debugMode))
            {
                this.Configuration.DebugMode = debugMode;
                this.Configuration.Save();
            }

            if(this.Configuration.DebugMode)
            {
                int debugLocation = (int)this.Configuration.DebugLocation;
                if(ImGui.Combo("Force Location", ref debugLocation, System.Enum.GetNames(typeof(OceanFishin.Location)), 8))
                {
                    this.Configuration.DebugLocation = (OceanFishin.Location)debugLocation;
                    this.Configuration.Save();
                }

                int debugTime = (int)this.Configuration.DebugTime;
                if (ImGui.Combo("Force Time", ref debugTime, System.Enum.GetNames(typeof(OceanFishin.Time)), 4))
                {
                    this.Configuration.DebugTime = (OceanFishin.Time)debugTime;
                    this.Configuration.Save();
                }
                
                bool debugSpectral = this.Configuration.DebugSpectral; 
                if (ImGui.Checkbox("Force Spectral", ref debugSpectral))
                {
                    this.Configuration.DebugSpectral = debugSpectral; 
                    this.Configuration.Save();
                }

                bool debugIntution = this.Configuration.DebugIntution;   
                if (ImGui.Checkbox("Force Intuition", ref debugIntution))
                {
                    this.Configuration.DebugIntution = debugIntution;
                    this.Configuration.Save();
                }
            }
        }
    }
}
