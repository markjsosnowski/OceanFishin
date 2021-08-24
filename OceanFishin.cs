using Dalamud.Game.Command;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace OceanFishin
{
    public class OceanFishin : IDalamudPlugin
    {
        
        enum time : int
        {
            Unknown  = -1,
            Day = 0,
            Sunset = 1,
            Night = 2
        }
        enum bait : int
        {
            Unknown = -1,
            PlumpWorm = 0,
            Ragworm = 1,
            Krill = 2
        }
        public string Name => "Ocean Fishin'";
       
        private const string command_name = "/oceanfishin";
        private const string alt_command_1 = "/oceanfishing";
        private const string alt_command_2 = "/bait";

        private bool on_boat = false;

        private DalamudPluginInterface pi;
        private OceanFishinUI ui;

        // This is the TerritoyType for the entire instance and does not
        // provide any information on fishing spots, routes, etc.
        private const int endevor_territory_type = 900;
        private const string default_location = "Unknown Location";
        private const string default_time = "Unknown Time";

        // These are known via addon inspector.
        private const int location_textnode_index = 20;
        private const int night_imagenode_index = 22;
        private const int sunset_imagenode_index = 23;
        private const int day_imagenode_index = 24;
        private const int expected_nodelist_count = 24;

        // Three image nodes make up the time of day indicator.
        // They all use the same texture, so the part_id determines
        // which part of the texture is used. Those part_ids are:
        // Day      Active = 9  Inactive = 4
        // Sunset   Active = 10 Inactive = 5
        // Night    Active = 11 Inactive = 6
        private const int day_icon_lit = 9;
        private const int sunset_icon_lit = 10;
        private const int night_icon_lit = 11;

        // Format = Location : [Start, Intuition, Spectral Day, Spectral Sunset, Spectral Night]
        // If time is known, that's time spectral can be accessed by index (time+2).
        // This information is based on Zeke's Fishing Guidebook found here:
        // https://docs.google.com/spreadsheets/d/17A_IIlSO0wWmn8I3-mrH6JRok0ZIxiNFaDH2MhN63cI/
        private Dictionary<string, int[]> bait_dict = new Dictionary<string, int[]>()
        {
            {"Galadion Bay",                    new int[] {(int)bait.PlumpWorm, (int)bait.Krill, (int)bait.Ragworm, (int)bait.PlumpWorm, (int)bait .Krill} },
            {"The Southern Strait of Merlthor", new int[] {(int)bait.Krill, (int)bait.PlumpWorm, (int)bait.Krill, (int)bait.Ragworm, (int)bait.PlumpWorm} },
            {"The Northern Strait of Merlthor", new int[] {(int)bait.Ragworm, (int)bait.Ragworm, (int)bait.PlumpWorm, (int)bait.Ragworm, (int)bait.Krill} },
            {"Rhotano Sea",                     new int[] {(int)bait.PlumpWorm, (int)bait.Krill, (int)bait.PlumpWorm, (int)bait.Ragworm, (int)bait.Krill} },
            {"The Cieldalaes",                  new int[] {(int)bait.Ragworm, (int)bait.Krill, (int)bait.Krill, (int)bait.PlumpWorm, (int)bait.Krill} },
            {"The Bloodbrine Sea",              new int[] {(int)bait.Krill, (int)bait.Krill, (int)bait.Ragworm, (int)bait.PlumpWorm, (int)bait.Krill} },
            {"The Rothlyt Sound",               new int[] {(int)bait.PlumpWorm, (int)bait.Ragworm, (int)bait.Krill, (int)bait.Krill, (int)bait.Krill} },
            {default_location,                  new int[] {(int)bait.Unknown, (int)bait.Unknown, (int)bait.Unknown, (int)bait.Unknown, (int)bait.Unknown} },
        };

        public string AssemblyLocation { get => assemblyLocation; set => assemblyLocation = value; }
        private string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;

        public void Dispose()
        {
            this.ui.Dispose();
            this.pi.CommandManager.RemoveHandler(command_name);
            this.pi.CommandManager.RemoveHandler(alt_command_1);
            this.pi.CommandManager.RemoveHandler(alt_command_2);
            this.pi.Dispose();
        }

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pi = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface), "DalamudPluginInterface cannot be null");
            this.ui = new OceanFishinUI();

            this.pi.CommandManager.AddHandler(command_name, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the ocean fishing bait chart."
            }) ;
            this.pi.CommandManager.AddHandler(alt_command_1, new CommandInfo(OnCommand)
            {
                HelpMessage = "Alias for /oceanfishin"
            });
            this.pi.CommandManager.AddHandler(alt_command_2, new CommandInfo(OnCommand)
            {
                HelpMessage = "Alias for /oceanfishin"
            });

            this.pi.UiBuilder.OnBuildUi += DrawUI;
        }

        private void OnCommand(string command, string args)
        {
            // In response to the slash command, just display our main ui.
            this.ui.Visible = true;
        }

        private bool check_location()
        {
            if ((int)pi.ClientState.TerritoryType == endevor_territory_type)
                return true;
            else
                return false;
        }

        private unsafe (string, string) get_data()
        {
            string current_location = default_location;
            string current_time = default_time;
            
            // IKDFishingLog is the name of the blue window that appears during ocean fishing 
            // that displays location, time, and what you caught.
            var addon_ptr = pi.Framework.Gui.GetUiObjectByName("IKDFishingLog", 1);
            if(addon_ptr == null || addon_ptr == IntPtr.Zero)
            {
                return (current_location, current_time);
            }
            AtkUnitBase* addon = (AtkUnitBase*)addon_ptr;
            
            // Without this check, the plugin might try to get a child node before the list was 
            // populated and cause a null pointer exception. 
            if(addon->UldManager.NodeListCount  < expected_nodelist_count)
            {
                return (current_location, current_time);
            }
            current_location = get_location(addon);
            current_time = get_time(addon);
            return (current_location, current_time);
        }
        
        private unsafe string get_location(AtkUnitBase* ptr)
        {
            if (ptr == null)
                return default_location;
            AtkResNode* res_node = ptr->UldManager.NodeList[location_textnode_index];
            AtkTextNode* text_node = (AtkTextNode*)res_node;
            return Marshal.PtrToStringAnsi(new IntPtr(text_node->NodeText.StringPtr));
        }

        private unsafe string get_time(AtkUnitBase* ptr)
        {
            if (ptr == null)
                return default_time;
            AtkResNode* res_node = ptr->UldManager.NodeList[day_imagenode_index];
            AtkImageNode* image_node = (AtkImageNode*)res_node;
            if (image_node->PartId == day_icon_lit)
                return "day";
            res_node = ptr->UldManager.NodeList[sunset_imagenode_index];
            image_node = (AtkImageNode*)res_node;
            if (image_node->PartId == sunset_icon_lit)
                return "sunset";
            res_node = ptr->UldManager.NodeList[night_imagenode_index];
            image_node = (AtkImageNode*)res_node;
            if (image_node->PartId == night_icon_lit)
                return "night";
            return default_time;
        }

        private void DrawUI()
        {
            string location = default_location;
            string time = default_time;
            on_boat = check_location();
            if (on_boat)
            {
                (location, time) = get_data();
            }
            // This usually isn't a problem but just here for safety.
            if(bait_dict.ContainsKey(location))
            {
                this.ui.Draw(on_boat, location, time);
            }
        }
    }
}
