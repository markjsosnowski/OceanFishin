using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using Dalamud.Data;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Lumina.Excel.GeneratedSheets;

namespace OceanFishin.Windows;

public class MainWindow : Window, IDisposable
{
    private OceanFishin Plugin;
    private Configuration Configuration;
    private Localizer Localizer;

    private const string octopodes = "octopodes";
    private const string sharks = "sharks";
    private const string jellyfish = "jellyfish";
    private const string dragons = "dragons";
    private const string balloons = "balloons";
    private const string crabs = "crabs";
    private const string mantas = "mantas";
    private const string special = "fabled";
    private const string always = "always";
    private const string starting = "start";
    private const string intuition = "intuition";
    private const string spectral = "spectral";

    private const int spectral_active = 1;
    private const int spectral_inactive = 0;

    private const int display_full = 2;
    private const int display_compact = 1;
    private const int display_standard = 0;

    private string[] donation_lines = new string[] {    "Rack up a good score on your last voyage?",
                                                            "Finally get that shark mount?",
                                                            "Do women want you and fish fear you now?",
                                                            "Land a big one on your last trip?",
                                                            "Get the highest score on the whole ship?",
                                                            "A bad day fishing is better than a good day programming.",
                                                            "Spare some krill?"};

    private int random_index;

    public MainWindow(OceanFishin plugin, Configuration configuration, Localizer localizer) : base("Ocean Fishin'", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(255, 135),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.Plugin = plugin;
        this.Configuration = configuration;
        this.Localizer = localizer; 
        Random random = new Random();
        this.random_index = random.Next(0, donation_lines.Length);
    }

    public void Dispose(){}

    public override void Draw()
    {
        if (this.Plugin.InOceanFishingDuty())
        {
            
            OceanFishin.Location location = this.Plugin.GetFishingLocation();
            OceanFishin.Time time = this.Plugin.GetFishingTime();

            if (location == OceanFishin.Location.Unknown || time == OceanFishin.Time.Unknown) { LoadingWindow(); return; }

            if (this.Configuration.display_mode == display_compact) { CompactMode(location, time); }
            if (this.Configuration.display_mode == display_full) { FullMode(location, time); }
            if (this.Configuration.display_mode == display_standard) { DefaultMode(location, time); }
        }
        else
        {
            { countdownWindow(); }
        }
    }

    private void LoadingWindow()
    {
        ImGui.Text("Waiting for location...");
    }

    private void DefaultMode(OceanFishin.Location location, OceanFishin.Time time)
    {
        if (this.Plugin.IsSpectralCurrent())
        {
            ImGui.Text("High Points: " + Localizer.Localize(this.Plugin.getSpectralHighPointsBait(location, time)));
            if (this.Plugin.getSpectralIntuitionBait(location, time) != OceanFishin.Bait.None) ImGui.Text("Fisher's Intuition Buff: " + this.Plugin.getSpectralIntuitionBait(location, time).ToString());
        }
        else
        {
            ImGui.Text("Best Spectral Chance: " + this.Plugin.getSpectralChanceBait(location).ToString());
            ImGui.Text("Fisher's Intuition Buff: " + this.Plugin.getFishersIntuitionBait(location, time).ToString());
        }
        if (this.Configuration.include_achievement_fish)
        {
            ImGui.Separator();
            ImGui.Text("Your mission fish:"); //TODO filter this to only show the actual fish being asked for
            if (this.Plugin.IsSpectralCurrent() ) 
            {
                var specMissionFishHolder = this.Plugin.getSpectralMissionFishBaits(location, time);
                if (specMissionFishHolder != null){ ImGui.Text(string.Join('\n', specMissionFishHolder)); }
            } 
            else
            {
                ImGui.Text(string.Join('\n', this.Plugin.getMissionFishBaits(location)));
            }
        }
    }
    
    private void FullMode(OceanFishin.Location location, OceanFishin.Time time)
    {
        ImGui.Text("Best Spectral Chance: " + Localizer.Localize(this.Plugin.getSpectralChanceBait(location)));
        ImGui.Text("Fisher's Intuition Buff: " + Localizer.Localize(this.Plugin.getFishersIntuitionBait(location, time)));
        ImGui.Text("Spectral Current High Points: " + Localizer.Localize(this.Plugin.getSpectralHighPointsBait(location, time)));
        if (this.Plugin.getSpectralIntuitionBait(location, time) != OceanFishin.Bait.None) ImGui.Text("Spectral Intuition: " + Localizer.Localize(this.Plugin.getSpectralIntuitionBait(location, time)));
        
        if (this.Configuration.include_achievement_fish)
        {
            ImGui.Separator();
            ImGui.Text("Mission Fish:");
            ImGui.Text(MissionFishDictToString(this.Plugin.getMissionFishBaits(location)));

            var specMissionFishHolder = this.Plugin.getSpectralMissionFishBaits(location, time);
            if (specMissionFishHolder != null)
            {
                ImGui.Separator();
                ImGui.Text("Spectral Current Mission Fish:");
                ImGui.Text(MissionFishDictToString(specMissionFishHolder));
            }
        }
    }

    private void CompactMode(OceanFishin.Location location, OceanFishin.Time time)
    {
        ImGui.Text(this.Plugin.getSingleBestBait(location, time).ToString());
    }
    
    private void countdownWindow()
    {
        // This window appears if the command is issued when not part of the duty.
        ImGui.Text(Localizer.Localize("notOnShip"));
        ImGui.Text(time_until_next_voyage());
        ImGui.Separator();
        //ImGui.Text(donation_lines[this.random_index]);
        ImGui.Text(Localizer.Localize("donateText"));
        ImGui.SameLine();
        if (ImGui.Button(Localizer.Localize("donateButton")))
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = "https://ko-fi.com/sl0nderman",
                UseShellExecute = true
            });
        }
    }

    private string time_until_next_voyage()
    {
        DateTime now = DateTime.UtcNow;
        int hour = now.Hour;
        int minutes = now.Minute;
        if (hour % 2 == 0)
        {
            switch (minutes)
            {
                case < 15:
                    return Localizer.Localize("currentShip") + (15 - minutes) + Localizer.Localize("minutes");
                default:
                    return Localizer.Localize("countdown") + "1 " + Localizer.Localize("hours") + ", " + (60 - minutes) + " " + Localizer.Localize("minutes");
            }
        }
        else
        {
            switch (minutes)
            {
                case 59:
                    return Localizer.Localize("countdown") + Localizer.Localize("lessThanOne");
                default:
                    return Localizer.Localize("countdown") + (60 - minutes) + " " + Localizer.Localize("minutes");
            }
        }
    }

    public string MissionFishDictToString(Dictionary<OceanFishin.FishTypes ,OceanFishin.Bait> dict)
    {
        string ret = "";
        if (dict == null){ return ret; }
        
        foreach(var pair in dict)
        {
            ret += (Localizer.Localize(pair.Key) + " -> " + Localizer.Localize(pair.Value));
            ret += "\n";
        }

        return ret;
    }
}
