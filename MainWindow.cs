using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;

namespace OceanFishin.Windows;

public class MainWindow : Window, IDisposable
{
    private OceanFishin Plugin;
    private Configuration Configuration;

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

    public MainWindow(OceanFishin plugin, Configuration configuration) : base("Ocean Fishin'", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(255, 135),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.Plugin = plugin;
        this.Configuration = configuration;
        Random random = new Random();
        this.random_index = random.Next(0, donation_lines.Length);
    }

    public void Dispose(){}

    public override void Draw()
    {

        if (Plugin.in_ocean_fishing_duty())
        {
            Dictionary<string, Dictionary<string, Dictionary<string, dynamic>>> bait_dict = Plugin.bait_dictionary;
            (string location, string time) = Plugin.get_fishing_data();
            int spectral_key;
            bool intuition_state;
            try
            {

                // This is stored as an int so it can be used to index the bait array of mission fish.
                spectral_key = this.Plugin.is_spectral_current() ? spectral_active : spectral_inactive;
                intuition_state = this.Plugin.has_intuition_buff();

                if (bait_dict.ContainsKey(location))
                {

                    if (this.Configuration.display_mode == display_compact)
                    {
                        if (spectral_key == spectral_inactive)
                        {
                            if (intuition_state)
                                ImGui.Text("Recommended → " + bait_dict[location]["always"]["intuition"]);
                            else
                                ImGui.Text("Recommended → " + bait_dict[location]["always"]["start"]);
                        }
                        else
                        {
                            if (intuition_state && this.Plugin.nested_key_exists(ref bait_dict, location, time, special))
                                ImGui.Text("Recommended → " + bait_dict[location][time][special]);
                            else
                                ImGui.Text("Recommended → " + bait_dict[location][time][spectral]);
                        }
                    }
                    else
                    {
                        StringBuilder first_line = new StringBuilder();
                        first_line.Append("The suggested bait for ");
                        first_line.Append(location);
                        if (spectral_key == 1)
                            first_line.Append(" spectral current");
                        if (time == "Day")
                            first_line.Append(" during the Day");
                        else
                        {
                            first_line.Append(" at ");
                            first_line.Append(time);
                        }
                        first_line.Append(" is:");

                        ImGui.TextWrapped(first_line.ToString());


                        if (spectral_key == spectral_inactive || this.Configuration.display_mode == display_full)
                        {
                            ImGui.Text("Start with → " + bait_dict[location]["always"]["start"]);
                            ImGui.Text("Fisher's Intuition → " + bait_dict[location]["always"]["intuition"]);
                        }

                        if (spectral_key == spectral_active || this.Configuration.display_mode == display_full)
                        {
                            ImGui.Text("Spectral high points → " + bait_dict[location][time]["spectral"]);
                            // Super rare fish only found in specific locations and times that use abnormal bait.
                            if (this.Plugin.nested_key_exists(ref bait_dict, location, time, special))
                                ImGui.Text("Spectral Fisher's Intuition → " + bait_dict[location][time][special]);
                        }
                    }
                    if (this.Configuration.include_achievement_fish)
                    {
                        ImGui.Separator();

                        if (this.Configuration.display_mode == display_full)
                        {
                            // Achievement fish are not found in every area, so we don't show them unless it's relevant.
                            if (is_fish_available(ref bait_dict, location, time, octopodes, spectral_inactive))
                                ImGui.Text("Octopods → " + bait_dict[location][time][octopodes][spectral_inactive]);

                            if (is_fish_available(ref bait_dict, location, time, octopodes, spectral_active))
                                ImGui.Text("Spectral Current Octopods → " + bait_dict[location][time][octopodes][spectral_active]);

                            if (is_fish_available(ref bait_dict, location, time, sharks, spectral_inactive))
                                ImGui.Text("Sharks → " + bait_dict[location][time][sharks][spectral_inactive]);

                            if (is_fish_available(ref bait_dict, location, time, sharks, spectral_active))
                                ImGui.Text("Spectral Current Sharks → " + bait_dict[location][time][sharks][spectral_active]);

                            if (is_fish_available(ref bait_dict, location, time, jellyfish, spectral_inactive))
                                ImGui.Text("Jellyfish → " + bait_dict[location][time][jellyfish][spectral_inactive]);

                            if (is_fish_available(ref bait_dict, location, time, jellyfish, spectral_active))
                                ImGui.Text("Spectral Current Jellyfish → " + bait_dict[location][time][jellyfish][spectral_active]);

                            if (is_fish_available(ref bait_dict, location, time, dragons, spectral_inactive))
                                ImGui.Text("Sea Dragons → " + bait_dict[location][time][dragons][spectral_inactive]);

                            if (is_fish_available(ref bait_dict, location, time, dragons, spectral_active))
                                ImGui.Text("Spectral Current Dragons → " + bait_dict[location][time][dragons][spectral_active]);

                            if (is_fish_available(ref bait_dict, location, time, balloons, spectral_inactive))
                                ImGui.Text("Balloons (Fugu) → " + bait_dict[location][time][balloons][spectral_inactive]);

                            if (is_fish_available(ref bait_dict, location, time, balloons, spectral_active))
                                ImGui.Text("Spectral Current Fugu → " + bait_dict[location][time][balloons][spectral_active]);

                            if (is_fish_available(ref bait_dict, location, time, crabs, spectral_inactive))
                                ImGui.Text("Crabs → " + bait_dict[location][time][crabs][spectral_inactive]);

                            if (is_fish_available(ref bait_dict, location, time, crabs, spectral_active))
                                ImGui.Text("Spectral Current Crabs → " + bait_dict[location][time][crabs][spectral_active]);

                            if (is_fish_available(ref bait_dict, location, time, mantas, spectral_inactive))
                                ImGui.Text("Mantas → " + bait_dict[location][time][mantas][spectral_inactive]);

                            if (is_fish_available(ref bait_dict, location, time, mantas, spectral_active))
                                ImGui.Text("Spectral Current Mantas → " + bait_dict[location][time][mantas][spectral_active]);
                        }
                        else // Standard display mode...
                        {
                            if (is_fish_available(ref bait_dict, location, time, octopodes, spectral_key))
                                ImGui.Text("Octopods → " + bait_dict[location][time][octopodes][spectral_key]);
                            if (is_fish_available(ref bait_dict, location, time, sharks, spectral_key))
                                ImGui.Text("Sharks → " + bait_dict[location][time][sharks][spectral_key]);
                            if (is_fish_available(ref bait_dict, location, time, jellyfish, spectral_key))
                                ImGui.Text("Jellyfish → " + bait_dict[location][time][jellyfish][spectral_key]);
                            if (is_fish_available(ref bait_dict, location, time, dragons, spectral_key))
                                ImGui.Text("Sea Dragons → " + bait_dict[location][time][dragons][spectral_key]);
                            if (is_fish_available(ref bait_dict, location, time, balloons, spectral_key))
                                ImGui.Text("Balloons (Fugu) → " + bait_dict[location][time][balloons][spectral_key]);
                            if (is_fish_available(ref bait_dict, location, time, crabs, spectral_key))
                                ImGui.Text("Crabs → " + bait_dict[location][time][crabs][spectral_key]);
                            if (is_fish_available(ref bait_dict, location, time, mantas, spectral_key))
                                ImGui.Text("Mantas → " + bait_dict[location][time][mantas][spectral_key]);
                        }
                    }

                }
                else
                {
                    // This will show for a second when the window is open when loading into/out of the duty
                    // and will automatically update once the location can actually be read.
                    ImGui.Text("Just a second, I'm still getting your location!");
                }
            }
            catch (KeyNotFoundException e)
            {
                // Now that we check if location is a key immediately, this shouldn't pop unless the json got messed up.
                PluginLog.Error("A dictionary key was not found. Location was " + location + " and time was " + time + ":" + e.ToString(), e);
                ImGui.Text("The plugin ran into a problem.");
                ImGui.Text("Please contact the developer with your dalamund.log file.");
                throw new KeyNotFoundException();
            }
        }
        else
        {
            // This window appears if the command is issued when not part of the duty.
            ImGui.Text("This plugin is meant to be used during the Ocean Fishing duty.");
            ImGui.Text("Once you're aboard The Endeavor, the bait list will automatically update.");
            ImGui.Text(time_until_next_voyage());
            ImGui.Separator();
            ImGui.Text(donation_lines[this.random_index]);
            ImGui.SameLine();
            if (ImGui.Button("Donate"))
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo
                {
                    FileName = "https://ko-fi.com/sl0nderman",
                    UseShellExecute = true
                });
            }
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
                case < 1:
                    return "The current voyage set sail less than a minute ago.";
                case 1:
                    return "The current voyage set sail 1 minute ago.";
                case < 15:
                    return "The current voyage set sail " + minutes + " minutes ago.";
                default:
                    return "The next voyage will begin in 1 hour, " + (60 - minutes) + " minute(s).";
            }
        }
        else
        {
            switch (minutes)
            {
                case 59:
                    return "The next voyage will begin in 1 minute!";
                default:
                    return "The next voyage will begin in " + (60 - minutes) + " minutes.";
            }
        }
    }

    public bool is_fish_available(ref Dictionary<string, Dictionary<string, Dictionary<string, dynamic>>> bait_dict, string location, string time, string fish_type, int state)
    {
        if (this.Plugin.nested_key_exists(ref bait_dict, location, time, fish_type))
        {
            if (bait_dict[location][time][fish_type][state] != null)
                return true;
        }
        return false;
    }

}
