using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

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


        enum bait_index : int
        {
            starter = 0,
            intuition = 1,
            spectral_day = 2,
            spectral_offset = 2,
            spectral_sunset = 3,
            spectral_night = 4
        }

        // this extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;
        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }

        public void Dispose()
        {
        }

        public void Draw(bool on_boat, string location, int time, int[] bait_list)
        {
            // This is our only draw handler attached to UIBuilder, so it needs to be
            // able to draw any windows we might have open.
            // Each method checks its own visibility/state to ensure it only draws when
            // it actually makes sense.
            // There are other ways to do this, but it is generally best to keep the number of
            // draw delegates as low as possible.

            DrawMainWindow(on_boat, location, time, bait_list);
        }

        public void DrawMainWindow(bool on_boat, string location, int time, int[] bait_list)
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
                    ImGui.Text("You are aboard The Endeavor, sailing in " + location + " " + time_strings[time] + ".");
                    ImGui.Text("The ideal bait for this area and time is: ");
                    ImGui.Text("Starting Bait → " + bait_strings[bait_list[(int)bait_index.starter]]);
                    ImGui.Text("Fisher's Intution → " + bait_strings[bait_list[(int)bait_index.intuition]]);
                    ImGui.Text("Spectral Current → " + bait_strings[bait_list[(int)bait_index.spectral_offset + time]]);
                }
                else
                {
                    ImGui.Text("Board The Endeavor to update the bait list.");
                }

            }
        }
    }
}