using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
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

        public ConfigWindow(OceanFishin plugin, Configuration configuration) : base("Ocean Fishin' Configuration", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(255, 135),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };

            this.Plugin = plugin;
            this.Configuration = configuration;
        }

        public void Dispose(){}

        public override void Draw()
        {
            var display_mode = this.Configuration.display_mode;
            if (ImGui.Combo("Display Mode", ref display_mode, this.Configuration.display_modes, 3))
            {
                this.Configuration.display_mode = display_mode;
                this.Configuration.Save();
            }

            ImGui.TextWrapped(this.Configuration.display_mode_desc[display_mode]);

            var include_achievement_fish = this.Configuration.include_achievement_fish;
            if (ImGui.Checkbox("Recommend bait for mission/achievement fish.", ref include_achievement_fish))
            {
                this.Configuration.include_achievement_fish = include_achievement_fish;
                this.Configuration.Save();
            }
        }
    }
}
