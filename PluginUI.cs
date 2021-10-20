using ImGuiNET;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;
using Dalamud.Logging;
using System.Net;
using System.Diagnostics;
using System.Text;

namespace OceanFishin
{
    internal class PluginUI : IDisposable
    {
        private Configuration configuration;

        // Dictionary keys
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

        private string[] donation_lines = new string[] {    "Rack up a good score on your last voyage?",
                                                            "Finally get that shark mount?",
                                                            "Do women want you and fish fear you now?",
                                                            "Land a big one on your last trip?",
                                                            "Get the highest score on the whole ship?",
                                                            "A bad day fishing is better than a good day programming.",
                                                            "Spare some krill?"};

        private int random_index;

        // This extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;
        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }

        private bool settingsVisible = false;
        
        public bool SettingsVisible
        {
            get { return this.settingsVisible; }
            set { this.settingsVisible = value; }
        }
        
        public void Dispose()
        {
        }

        public PluginUI(Configuration configuration)
        {
            Random random = new Random();
            this.configuration = configuration;
            
            // Since the window is constantly updated, we just pick one 
            // random line and stick with it until the plugin is reloaded.
            this.random_index = random.Next(0, donation_lines.Length);
        }

        public void Draw(bool on_boat, string location, string time)
        {
            // This is our only draw handler attached to UIBuilder, so it needs to be
            // able to draw any windows we might have open.
            // Each method checks its own visibility/state to ensure it only draws when
            // it actually makes sense.
            // There are other ways to do this, but it is generally best to keep the number of
            // draw delegates as low as possible.

            DrawMainWindow(on_boat, location, time, ref OceanFishin.bait_dictionary);
            DrawSettingsWindow();
        }

        private string time_until_next_voyage()
        {
            DateTime now = DateTime.UtcNow;
            int hour = now.Hour;
            int minutes = now.Minute;
            if(hour % 2 == 0)
            {
                if (minutes < 1)
                {
                    return "The current voyage set sail less than a minute ago.";
                }
                if(minutes == 1)
                {
                    return "The current voyage set sail 1 minute ago.";
                }
                if (minutes < 15) // The duty registration is open for 15 minutes.  
                {
                    return "The current voyage set sail " + minutes + " minutes ago.";
                }
                else
                {
                    return "The next voyage will begin in 1 hour, " + (60 - minutes) + " minute(s).";
                }
            }
            else
            {
                if (minutes == 59)
                {
                    return "The next voyage will begin in 1 minute!";
                }
                else 
                {
                    return "The next voyage will begin in " + (60 - minutes) + " minutes.";
                }
            }
        }

        public bool is_fish_available(ref Dictionary<string, Dictionary<string, Dictionary<string, dynamic>>> bait_dict, string location, string time, string fish_type, int state)
        {
            if(OceanFishin.nested_key_exists(ref bait_dict,location, time, fish_type))
            {
                if (bait_dict[location][time][fish_type][state] != null)
                    return true;
            }
            return false;
        }

        public void DrawMainWindow(bool on_boat, string location, string time, ref Dictionary<string, Dictionary<string, Dictionary<string, dynamic>>> bait_dict)
        {
            int spectral_state = 0;
            
            if (!Visible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(505, 150), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(255, 135), new Vector2(float.MaxValue, float.MaxValue));

            if (ImGui.Begin("Ocean Fishin'", ref this.visible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                if (on_boat)
                {
                    try
                    {
                        if (OceanFishin.is_spectral_current())
                            spectral_state = 1;
                        else
                            spectral_state = 0;

                        if (bait_dict.ContainsKey(location))
                        {
                            StringBuilder first_line = new StringBuilder();
                            first_line.Append("The suggested bait for ");
                            first_line.Append(location);
                            if (spectral_state == 1)
                                first_line.Append(" spectral current");
                            if (time == "Day")
                                first_line.Append(" during the Day is:");
                            else
                            {
                                first_line.Append(" at ");
                                first_line.Append(time);
                            }
                            first_line.Append(" is:");


                            ImGui.Text(first_line.ToString());

                            if(spectral_state == 0)
                            {
                                ImGui.Text("Start with → " + bait_dict[location]["always"]["start"]);
                                ImGui.Text("Fisher's Intuition → " + bait_dict[location]["always"]["intuition"]);
                            }

                            if (spectral_state == 1)
                            {
                                ImGui.Text("High points → " + bait_dict[location][time][spectral]);
                                // Super rare fish only found in specific locations and times that use abnormal bait.
                                if (OceanFishin.nested_key_exists(ref bait_dict, location, time, special))
                                    ImGui.Text("Fisher's Intuition → " + bait_dict[location][time][special]);
                            }

                            if (this.configuration.include_achievement_fish)
                            {
                                ImGui.Separator();

                                // Achievement fish are not found in every area, so we don't show them unless it's relevant.
                                if (is_fish_available(ref bait_dict, location, time, octopodes, spectral_state))
                                    ImGui.Text("Octopods → " + bait_dict[location][time][octopodes][spectral_state]);

                                if (is_fish_available(ref bait_dict, location, time, octopodes, spectral_state))
                                    ImGui.Text("Octopods → " + bait_dict[location][time][octopodes][spectral_state]);

                                if (is_fish_available(ref bait_dict, location, time, sharks, spectral_state))
                                    ImGui.Text("Sharks → " + bait_dict[location][time][sharks][spectral_state]);

                                if (is_fish_available(ref bait_dict, location, time, jellyfish, spectral_state))
                                    ImGui.Text("Jellyfish → " + bait_dict[location][time][jellyfish][spectral_state]);

                                if (is_fish_available(ref bait_dict, location, time, dragons, spectral_state))
                                    ImGui.Text("Sea Dragons → " + bait_dict[location][time][dragons][spectral_state]);

                                if (is_fish_available(ref bait_dict, location, time, balloons, spectral_state))
                                    ImGui.Text("Balloons (Fugu) → " + bait_dict[location][time][balloons][spectral_state]);

                                if (is_fish_available(ref bait_dict, location, time, crabs, spectral_state))
                                    ImGui.Text("Crabs → " + bait_dict[location][time][crabs][spectral_state]);

                                if (is_fish_available(ref bait_dict, location, time, mantas, spectral_state))
                                    ImGui.Text("Mantas → " + bait_dict[location][time][mantas][spectral_state]);
                            }
                        }
                        else
                        {
                            // This will show for a second when the window is open when loading into the duty
                            // and will automatically update once the location can actually be read.
                            ImGui.Text("Just a second, I'm still getting your location!");
                        }
                    }
                    catch(KeyNotFoundException e)
                    {
                        // Now that we check if location is a key immediately, this shouldn't pop unless the json got messed up.
                        PluginLog.Warning("A dictionary key was not found. Location was " + location + " and time was " + time, e);
                        ImGui.Text("If this window does not update in a few seconds, something is broken.");
                        ImGui.Text("Please contact the developer with your dalamund.log file.");
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
        }
    
        public void DrawSettingsWindow()
        {
            if (!SettingsVisible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(400, 100), ImGuiCond.Always);
            if (ImGui.Begin("Ocean Fishin' Configuration", ref this.settingsVisible,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                var include_achievement_fish = this.configuration.include_achievement_fish;
                if (ImGui.Checkbox("Recommend bait for mission/achievement fish.", ref include_achievement_fish))
                {
                    this.configuration.include_achievement_fish = include_achievement_fish;
                    this.configuration.Save();
                }

                // TODO upcoming features
               //var highlight_recommended_bait = this.configuration.highlight_recommended_bait;
               /*if (ImGui.Checkbox("Highlight recommended bait in your tackle box.", ref highlight_recommended_bait))
                {
                    this.configuration.highlight_recommended_bait = highlight_recommended_bait;
                    this.configuration.Save();
                }*/
            }
            ImGui.End();

        }
    }
}