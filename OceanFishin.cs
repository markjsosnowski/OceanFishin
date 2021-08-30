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
        // When true, the location will always be set to whatever is defined in the DrawUI() block.
        private bool debug_mode = false;
        public string Name => "Ocean Fishin'";

        private const string command_name = "/oceanfishin";
        private const string alt_command_1 = "/oceanfishing";
        private const string alt_command_2 = "/bait";

        private DalamudPluginInterface pi;
        private OceanFishinUI ui;

        // This is the TerritoyType for the entire instance and does not
        // provide any information on fishing spots, routes, etc.
        private const int endevor_territory_type = 900;
        private bool on_boat = false;
        
        private const string default_location = "location unknown";
        public const string default_time = "time unknown";        

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

        static string codebase = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
        static UriBuilder uri = new UriBuilder(codebase);
        static string path = Uri.UnescapeDataString(uri.Path);
        string plugin_dir = System.IO.Path.GetDirectoryName(path);


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
            // that displays location, time, and what you caught. This is known via Addon Inspector.
            var addon_ptr = pi.Framework.Gui.GetUiObjectByName("IKDFishingLog", 1);
            if(addon_ptr == null || addon_ptr == IntPtr.Zero)
            {
                return (current_location, current_time);
            }
            AtkUnitBase* addon = (AtkUnitBase*)addon_ptr;
            
            // Without this check, the plugin might try to get a child node before the list was 
            // populated and cause a null pointer exception. 
            if(addon->UldManager.NodeListCount < expected_nodelist_count)
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
                return "Day";
            res_node = ptr->UldManager.NodeList[sunset_imagenode_index];
            image_node = (AtkImageNode*)res_node;
            if (image_node->PartId == sunset_icon_lit)
                return "Sunset";
            res_node = ptr->UldManager.NodeList[night_imagenode_index];
            image_node = (AtkImageNode*)res_node;
            if (image_node->PartId == night_icon_lit)
                return "Night";
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
            else if (debug_mode)
            {
                on_boat = true;
                // These can be changed to make sure the json is being read correctly.
                location = "Galadion Bay";
                time = default_time;
            }
            this.ui.Draw(on_boat, location, time, plugin_dir);
        }
    }
}
