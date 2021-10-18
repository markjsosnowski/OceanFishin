using ImGuiNET;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;
using Dalamud.Logging;
using System.Net;
using System.Diagnostics;

namespace OceanFishin
{
    internal class PluginUI : IDisposable
    {
        private Configuration configuration;
        private string bait_file_url = "https://markjsosnowski.github.io/FFXIV/bait.json";

        // Dictionary keys
        private const string octopodes = "octopodes";
        private const string sharks = "sharks";
        private const string jellyfish = "jellyfish";
        private const string dragons = "dragons";
        private const string balloons = "balloons";
        private const string crabs = "crabs";
        private const string mantas = "mantas";
        private const string special = "special";

        //private string json_path;
        //private const string json_filename = "bait.json";

        Dictionary<string, Dictionary<string, Dictionary<string, string>>> bait_dictionary;

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

            try
            {
                using (WebClient wc = new WebClient())
                {
                    var json = wc.DownloadString(bait_file_url);
                    this.bait_dictionary = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(json);
                }
            }
            catch (WebException e)
            {
                PluginLog.Error("There was a problem accessing the bait list. Is GitHub down?", e);
                this.bait_dictionary = null;
            }

        }

        public void Draw(bool on_boat, string location, string time)
        {
            // This is our only draw handler attached to UIBuilder, so it needs to be
            // able to draw any windows we might have open.
            // Each method checks its own visibility/state to ensure it only draws when
            // it actually makes sense.
            // There are other ways to do this, but it is generally best to keep the number of
            // draw delegates as low as possible.

            //Dictionary<string, Dictionary<string, Dictionary<string, string>>> bait = null;
            //if (on_boat)
            //    bait = LoadJsonToDictionary();
            DrawMainWindow(on_boat, location, time, this.bait_dictionary);
            DrawSettingsWindow();
        }

        /*private Dictionary<string, Dictionary<string, Dictionary<string, string>>> LoadJsonToDictionary()
        {
            try
            {
                using (WebClient wc = new WebClient())
                {
                    var json = wc.DownloadString(bait_file_url);
                    Dictionary<string, Dictionary<string, Dictionary<string, string>>> dict = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(json);
                    return dict;
                }
            }
            catch (WebException e)
            {
                PluginLog.Error("There was a problem accessing the bait list. Is GitHub down?", e);
                return null;
            }
    }*/

        private bool nested_key_exists(Dictionary<string, Dictionary<string, Dictionary<string, string>>> dictionary, string key1, string key2, string key3)
        {
            if(dictionary.ContainsKey(key1))
                if (dictionary[key1].ContainsKey(key2))
                    if (dictionary[key1][key2].ContainsKey(key3))
                        return true;
            return false;
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
        
        public void DrawMainWindow(bool on_boat, string location, string time, Dictionary<string, Dictionary<string, Dictionary<string, string>>> bait)
        {
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

                        if (bait.ContainsKey(location))
                        {
                            if (time == "Day")
                                ImGui.Text("The suggested bait for " + location + " during the day is:");
                            else
                                ImGui.Text("The suggested bait for " + location + " at " + time + "is:");
                        
                            //ImGui.Text("The suggested bait for this area and time is: ");
                            ImGui.Text("Start with → " + bait[location]["normal"]["starting"]);
                            ImGui.Text("Fisher's Intuition → " + bait[location]["normal"]["intuition"]);
                        }
                        else
                        {
                            // This will show for a second when the window is open when loading into the duty
                            // and will automatically update once the location can actually be read.
                            ImGui.Text("Just a second, I'm still getting your location!");
                        }
                       
                        if(nested_key_exists(bait, location, "spectral", time))
                            ImGui.Text("Spectral Current → " + bait[location]["spectral"][time]);
                                                
                        if (this.configuration.include_achievement_fish)
                        {
                            ImGui.Separator();

                            // Achievement fish are not found in every area, so we don't show them unless it's relevant.
                            if (nested_key_exists(bait, location, octopodes, time))
                                ImGui.Text("Octopods → " + bait[location][octopodes][time]);

                            if (nested_key_exists(bait, location, sharks, time))
                                ImGui.Text("Sharks → " + bait[location][sharks][time]);

                            if (nested_key_exists(bait, location, jellyfish, time))
                                ImGui.Text("Jellyfish → " + bait[location][jellyfish][time]);

                            if (nested_key_exists(bait, location, dragons, time))
                                ImGui.Text("Sea Dragons → " + bait[location][dragons][time]);

                            if (nested_key_exists(bait, location, balloons, time))
                                ImGui.Text("Balloons (Fugu) → " + bait[location][balloons][time]);

                            if (nested_key_exists(bait, location, crabs, time))
                                ImGui.Text("Crabs → " + bait[location][crabs][time]);

                            if (nested_key_exists(bait, location, mantas, time))
                                ImGui.Text("Mantas → " + bait[location][mantas][time]);
                        }

                        // Super rare fish only found in specific locations and times that use abnormal bait.
                        if (nested_key_exists(bait, location, special, time))
                            ImGui.Text("Spectral Intuition → " + bait[location][special][time]);
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