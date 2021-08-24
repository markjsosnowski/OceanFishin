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
        private Dictionary<int, string> bait_strings = new Dictionary<int, string>()
        {
            {-1, "Unknown Bait"},
            {1, "Ragworm"},
            {2, "Krill"},
            {0, "Plump Worm"},
        };
        private Dictionary<int, string> time_strings = new Dictionary<int, string>()
        {
            {-1, "Unknown Time" },
            {0, "during the Day" },
            {1, "at Sunset" },
            {2, "at Night"}
        };

        //TODO Achievement/Quest Fish (Crabs, Sharks, etc.) Helper

        enum bait_index : int
        {
            starter = 0,
            intuition = 1,
            spectral_day = 2,
            spectral_offset = 2,
            spectral_sunset = 3,
            spectral_night = 4
        }

        // This extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;
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

            Dictionary<string, Dictionary<string, string>> bait = null;
            if (on_boat)
                bait = LoadJsonToDictionary("bait.json", path);
            DrawMainWindow(on_boat, location, time, bait);
        }

        private Dictionary<string, Dictionary<string, string>> LoadJsonToDictionary(string filename, string path)
        {
            try
            {
                using (System.IO.StreamReader r = new System.IO.StreamReader(path+"\\"+filename))
                {
                    string json = r.ReadToEnd();
                    Dictionary<string, Dictionary<string, string>> dict = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string,string>>>(json);
                    return dict;
                }
            }
            catch(System.IO.FileNotFoundException e)
            {
                Dalamud.Plugin.PluginLog.Error("bait.json not found in " + path, e);
                return null;
            }
        }

        public void DrawMainWindow(bool on_boat, string location, string time, Dictionary<string, Dictionary<string, string>> bait)
        {
            if (!Visible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(250, 150), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(250, 150), new Vector2(float.MaxValue, float.MaxValue));


            if (ImGui.Begin("Ocean Fishin'", ref this.visible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                if (on_boat)
                {
                    ImGui.Text("You are aboard The Endeavor, sailing in " + location + " " + time + ".");
                    ImGui.Text("The suggested bait for this area and time is: ");
                    ImGui.Text("Starting Bait → " + bait[location]["starting"]);
                    ImGui.Text("Fisher's Intuition → " + bait[location]["intuition"]);
                    //This key in the json is formatted as "spectral time".
                    ImGui.Text("Spectral Current → " + bait[location]["spectral "+time]);
                    
                    // Achievement fish are not found in every area, so we don't show them unless it's relevant.
                    if (bait["crabs"].ContainsKey(location))
                        ImGui.Text("Crabs → " + bait["crabs"][location]);
                    if (bait["sharks"].ContainsKey(location))
                        ImGui.Text("Sharks → " + bait["sharks"][location]);
                    if (bait["mantas"].ContainsKey(location))
                        ImGui.Text("Mantas → " + bait["mantas"][location]);
                    if (bait["octopods"].ContainsKey(location))
                        ImGui.Text("Octopods → " + bait["octopods"][location]);
                    if (bait["jellyfish"].ContainsKey(location))
                        ImGui.Text("Jellyfish → " + bait["jellyfish"][location]);
                    if (bait["balloons"].ContainsKey(location))
                        ImGui.Text("Balloons → " + bait["balloons"][location]);
                    if (bait["dragons"].ContainsKey(location))
                        ImGui.Text("Sea Dragons → " + bait["dragons"][location]);

                    // Super rare fish only found in specific locations and times that use abnormal bait.
                    // This key in the json is formatted as "location time".
                    if (bait["special"].ContainsKey(location + " " + time))
                        ImGui.Text("Spectral Intuition → " + bait["special"][location + " " + time]);
                }
                else
                {
                    // This window appears if the command is issued when not part of the duty.
                    // Nothing regarding bait will be loaded and the location and time are set to their defaults and not used.
                    ImGui.Text("I can only help you when you're part of the Ocean Fishing duty.");
                    ImGui.Text("Board The Endeavor to update the bait list.");
                }

            }
        }
    }
}