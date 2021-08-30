using ImGuiNET;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;

namespace OceanFishin
{
    internal class OceanFishinUI : IDisposable
    {

        // This extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;

        // Dictionary keys
        private const string octopodes = "octopodes";
        private const string sharks = "sharks";
        private const string jellyfish = "jellyfish";
        private const string dragons = "dragons";
        private const string balloons = "balloons";
        private const string crabs = "crabs";
        private const string mantas = "mantas";
        private const string special = "special";

        private const string json_filename = "bait.json";

        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }

        public void Dispose()
        {
        }

        public void Draw(bool on_boat, string location, string time, string path)
        {
            // This is our only draw handler attached to UIBuilder, so it needs to be
            // able to draw any windows we might have open.
            // Each method checks its own visibility/state to ensure it only draws when
            // it actually makes sense.
            // There are other ways to do this, but it is generally best to keep the number of
            // draw delegates as low as possible.

            Dictionary<string, Dictionary<string, Dictionary<string, string>>> bait = null;
            if (on_boat)
                bait = LoadJsonToDictionary(json_filename, path);
            DrawMainWindow(on_boat, location, time, bait);
        }

        private Dictionary<string, Dictionary<string, Dictionary<string, string>>> LoadJsonToDictionary(string filename, string path)
        {
            try
            {
                using (System.IO.StreamReader r = new System.IO.StreamReader(path+"\\"+filename))
                {
                    string json = r.ReadToEnd();
                    Dictionary<string, Dictionary<string, Dictionary<string, string>>> dict = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(json);
                    return dict;
                }
            }
            catch(System.IO.FileNotFoundException e)
            {
                Dalamud.Plugin.PluginLog.Error("Required file " + json_filename +" not found in " + path, e);
                throw e;
            }
        }

        private bool nested_key_exists(Dictionary<string, Dictionary<string, Dictionary<string, string>>> dictionary, string key1, string key2, string key3)
        {
            if(dictionary.ContainsKey(key1))
                if (dictionary[key1].ContainsKey(key2))
                    if (dictionary[key1][key2].ContainsKey(key3))
                        return true;
            return false;
        }
        
        public void DrawMainWindow(bool on_boat, string location, string time, Dictionary<string, Dictionary<string, Dictionary<string, string>>> bait)
        {
            if (!Visible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(495, 145), ImGuiCond.FirstUseEver);
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
                                ImGui.Text("You are aboard The Endeavor, sailing in " + location + " during the " + time + ".");
                            else
                                ImGui.Text("You are aboard The Endeavor, sailing in " + location + " at " + time + ".");
                        
                            ImGui.Text("The suggested bait for this area and time is: ");
                            ImGui.Text("Starting Bait → " + bait[location]["normal"]["starting"]);
                            ImGui.Text("Fisher's Intuition → " + bait[location]["normal"]["intuition"]);
                        }
                        else
                        {
                            // This will show for a second when the window is open when loading into the duty
                            // and will automatically update once the location is set.
                            ImGui.Text("Just a second, I'm still getting your location!");
                        }
                       
                        if(nested_key_exists(bait, location, "spectral", time))
                            ImGui.Text("Spectral Current → " + bait[location]["spectral"][time]);
                        
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
                        
                        // Super rare fish only found in specific locations and times that use abnormal bait.
                        if (nested_key_exists(bait, location, special, time))
                            ImGui.Text("Spectral Intuition → " + bait[location][special][time]);
                    }
                    catch(KeyNotFoundException e)
                    {
                        // Now that we check if location is a key immediately, this shouldn't pop unless the json is messed up.
                        Dalamud.Plugin.PluginLog.Warning("A dictionary key was not found. Location was "+location+" and time was "+time, e);
                        ImGui.Text("I'm having trouble getting your information.");
                        ImGui.Text("If this window does not update in a few seconds, something is broken, and you should");
                        ImGui.Text("reinstall the plugin.");
                    }  
                }
                else
                {
                    // This window appears if the command is issued when not part of the duty.
                    // Nothing regarding bait will be loaded and the location and time are set to their defaults and not used.
                    ImGui.Text("I can only help you when you're part of the Ocean Fishing duty.");
                    ImGui.Text("Board The Endeavor to update the bait list.");
                    // ImGui.Text("Did this plugin help you get a great score? Consider saying thank you with a donation:");
                }

            }
        }
    }
}