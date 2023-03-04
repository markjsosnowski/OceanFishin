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
using static System.Net.Mime.MediaTypeNames;
using System.ComponentModel;
using System.Globalization;

namespace OceanFishin.Windows;

public class MainWindow : Window, IDisposable
{
    private OceanFishin Plugin;
    private Configuration Configuration;
    
    private Lumina.Excel.ExcelSheet<Item>? itemSheet;
    private Lumina.Excel.ExcelSheet<Lumina.Excel.GeneratedSheets.Status>? statusSheet;
    private string fishersIntutionString = "string_fishers_intuition";
    
    private enum displayMode
    {
        standard,
        compact,
        full
    }

    /*private string[] donation_lines = new string[] {    "Rack up a good score on your last voyage?",
                                                            "Finally get that shark mount?",
                                                            "Do women want you and fish fear you now?",
                                                            "Land a big one on your last trip?",
                                                            "Get the highest score on the whole ship?",
                                                            "A bad day fishing is better than a good day programming.",
                                                            "Spare some krill?"};
    */

    //private int random_index;
    

    public MainWindow(OceanFishin plugin, Configuration configuration) : base(plugin.Name, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(255, 135),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.Plugin = plugin;
        this.Configuration = configuration;
        this.itemSheet = this.Plugin.itemSheet;
        this.statusSheet = this.Plugin.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Status>();
        #pragma warning disable CS8602 // Dereference of a possibly null reference.
        if (this.statusSheet != null) { this.fishersIntutionString = this.statusSheet.GetRow(568).Name.ToString(); }
        PluginLog.Debug("Buff name was set to " + this.fishersIntutionString);

        //Random random = new Random();
        //this.random_index = random.Next(0, donation_lines.Length);
    }

    public void Dispose(){}

    public override void Draw()
    {
        if (this.Plugin.InOceanFishingDuty())
        {
            
            OceanFishin.Location location = this.Plugin.GetFishingLocation();
            OceanFishin.Time time = this.Plugin.GetFishingTime();

            if (location == OceanFishin.Location.Unknown || time == OceanFishin.Time.Unknown) { LoadingWindow(); return; }

            if (this.Configuration.DisplayMode == (int)displayMode.compact) { CompactMode(location, time); }
            if (this.Configuration.DisplayMode == (int)displayMode.full) { FullMode(location, time); }
            if (this.Configuration.DisplayMode == (int)displayMode.standard) { DefaultMode(location, time); }
        }
        else { countdownWindow(); }
    }

    private void LoadingWindow()
    {
        ImGui.Text(Properties.Strings.Waiting_for_location___);
    }

    private void DefaultMode(OceanFishin.Location location, OceanFishin.Time time)
    {
        if (this.Plugin.IsSpectralCurrent())
        {
            ImGui.Text(Properties.Strings.Spectral_Current + " " + Properties.Strings.High_Points + ": " + Localize(this.Plugin.GetSpectralHighPointsBait(location, time)));
            var specIntuitionBait = (this.Plugin.GetSpectralIntuitionBait(location, time));
            if (specIntuitionBait != OceanFishin.Bait.None)
            {
                ImGui.Text(Properties.Strings.Spectral_Current + " " + this.fishersIntutionString + ": " + Localize(specIntuitionBait));
            }
        }
        else
        {
            ImGui.Text(Properties.Strings.Best_spectral_chance + ": " + Localize(this.Plugin.GetSpectralChanceBait(location)));
            ImGui.Text(this.fishersIntutionString + ": " + Localize(this.Plugin.GetFishersIntuitionBait(location, time)));
        }
        if (this.Configuration.IncludeAchievementFish)
        {
            ImGui.Separator();
            if (this.Plugin.IsSpectralCurrent() ) 
            {
                var specMissionFishHolder = this.Plugin.GetSpectralMissionFishBaits(location, time);
                if (specMissionFishHolder != null)
                {
                    ImGui.Text(Properties.Strings.Mission_Fish + " (" + Properties.Strings.Spectral_Current + ")");
                    ImGui.Text(MissionFishDictToString(specMissionFishHolder)); 
                }
            } 
            else
            {
                ImGui.Text(Properties.Strings.Mission_Fish);
                ImGui.Text(MissionFishDictToString(this.Plugin.GetMissionFishBaits(location)));
            }
        }
    }
    
    private void FullMode(OceanFishin.Location location, OceanFishin.Time time)
    {
        //TODO localize this stuff
        ImGui.Text(Properties.Strings.Best_spectral_chance + ": " + Localize(this.Plugin.GetSpectralChanceBait(location)));
        ImGui.Text(this.fishersIntutionString + ": " + Localize(this.Plugin.GetFishersIntuitionBait(location, time)));
        ImGui.Text(Properties.Strings.Spectral_Current + " " + Properties.Strings.High_Points + ": " + Localize(this.Plugin.GetSpectralHighPointsBait(location, time)));
        if (this.Plugin.GetSpectralIntuitionBait(location, time) != OceanFishin.Bait.None) ImGui.Text(Properties.Strings.Spectral_Current + " " + this.fishersIntutionString + ": " + Localize(this.Plugin.GetSpectralIntuitionBait(location, time)));
        
        if (this.Configuration.IncludeAchievementFish)
        {
            ImGui.Separator();
            ImGui.Text(Properties.Strings.Mission_Fish);
            ImGui.Text(MissionFishDictToString(this.Plugin.GetMissionFishBaits(location)));

            var specMissionFishHolder = this.Plugin.GetSpectralMissionFishBaits(location, time);
            if (specMissionFishHolder != null)
            {
                ImGui.Separator();
                ImGui.Text(Properties.Strings.Mission_Fish + " (" + Properties.Strings.Spectral_Current + ")");
                ImGui.Text(MissionFishDictToString(specMissionFishHolder));
            }
        }
    }

    private void CompactMode(OceanFishin.Location location, OceanFishin.Time time)
    {
        ImGui.Text(Properties.Strings.I_suggest + ": ");
        ImGui.SameLine();
        ImGui.Text(Localize(this.Plugin.GetSingleBestBait(location, time)));
    }
    
    private void countdownWindow()
    {
        // This window appears if the command is issued when not part of the duty.
        ImGui.Text(Properties.Strings.Suggestions_will_appear_here_during_Ocean_Fishing);
        ImGui.Text(GetTimeUntilNextVoyage());
        ImGui.Separator();
        //ImGui.Text(donation_lines[this.random_index]);
        ImGui.Text(Properties.Strings.Did_this_plugin_help_you_);
        ImGui.SameLine();
        if (ImGui.Button(Properties.Strings.Consider_Donating))
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = "https://ko-fi.com/sl0nderman",
                UseShellExecute = true
            });
        }
    }

    private string GetTimeUntilNextVoyage()
    {
        DateTime now = DateTime.UtcNow;
        int hour = now.Hour;
        int minutes = now.Minute;
        if (hour % 2 == 0)
        {
            switch (minutes)
            {
                case < 15:
                    return Properties.Strings.Boarding_is_open_for + " " + (15 - minutes) + " " + Properties.Strings.minutes;
                case 15:
                    return Properties.Strings.Boarding_is_open_for + " " + Properties.Strings.less_than_a_minute_;
                default:
                    return Properties.Strings.The_next_boat_leaves_in + " 1 " + Properties.Strings.hour + ", " + (60-minutes) +  " " + Properties.Strings.minutes;
            }
        }
        else
        {
            switch (minutes)
            {
                case 59:
                    return Properties.Strings.The_next_boat_leaves_in + " " + Properties.Strings.less_than_a_minute_;
                default:
                    return Properties.Strings.The_next_boat_leaves_in + " " + (60 - minutes) + " " + Properties.Strings.minutes;
            }
        }
    }

    public string MissionFishDictToString(Dictionary<OceanFishin.FishTypes ,OceanFishin.Bait> dict)
    {
        string ret = "";
        if (dict == null){ return ret; }
        
        foreach(var pair in dict)
        {
            switch (pair.Key)
            {

                case OceanFishin.FishTypes.Balloons: ret += Properties.Strings.Balloons; break;
                case OceanFishin.FishTypes.Crabs:ret += Properties.Strings.Crabs; break;
                case OceanFishin.FishTypes.Dragons: ret += Properties.Strings.Seahorses; break;
                case OceanFishin.FishTypes.Jellyfish: ret += Properties.Strings.Jellyfish; break;
                case OceanFishin.FishTypes.Mantas: ret += Properties.Strings.Mantas; break;
                case OceanFishin.FishTypes.Octopodes: ret += Properties.Strings.Octopodes; break;
                case OceanFishin.FishTypes.Sharks: ret += Properties.Strings.Sharks; break;
                default: PluginLog.Error("Unknown fish type: " + pair.Key); continue;
            }
            ret += ": " + Localize(pair.Value);
            ret += "\n";
        }

        return ret;
    }

    private string? Localize(OceanFishin.Bait bait)
    {
        #pragma warning disable CS8602 // Dereference of a possibly null reference.
        if (itemSheet != null) { return this.itemSheet.GetRow((uint)bait).Singular.ToString(); }
        return bait.ToString();
    }
}
