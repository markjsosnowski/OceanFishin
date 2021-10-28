using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.IoC;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Reflection;
using Dalamud.Game.ClientState;
using Dalamud.Game;
using Dalamud.Game.Gui;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using Dalamud.Logging;

namespace OceanFishin
{  
    public sealed class OceanFishin : IDalamudPlugin
    {
        private const bool DEBUG = true;
        
        public string Name => "Ocean Fishin'";

        public const string commandName = "/oceanfishin";
        public const string altCommandName1 = "/oceanfishing";
        public const string altCommandName2 = "/bait";

        private DalamudPluginInterface PluginInterface { get; init; }
        private CommandManager CommandManager { get; init; }
        private Configuration Configuration { get; init; }
        private PluginUI PluginUI { get; init; }
        private ClientState ClientState { get; init; }
        private Framework Framework { get; init; }
        private GameGui GameGui { get; init; }

        private const string default_location = "location unknown";
        private const string default_time = "time unknown";

        // This is the TerritoryType for the entire instance and does not
        // provide any information on fishing spots, routes, etc.
        private const int endevor_territory_type = 900;

        // NodeList indexes, known via addon inspector.
        private const int location_textnode_index = 20;
        private const int night_imagenode_index = 22;
        private const int sunset_imagenode_index = 23;
        private const int day_imagenode_index = 24;
        private const int expected_nodelist_count = 24;
        private const int cruising_resnode_index = 2;
        private const int expected_bait_window_nodelist_count = 14;
        private const int bait_list_componentnode_index = 3;
        private const int iconid_index = 2;
        private const int item_border_image_index = 4;

        // Three image nodes make up the time of day indicator.
        // They all use the same texture, so the part_id determines
        // which part of the texture is used. Those part_ids are:
        // Day      Active = 9  Inactive = 4
        // Sunset   Active = 10 Inactive = 5
        // Night    Active = 11 Inactive = 6
        private const int day_icon_lit = 9;
        private const int sunset_icon_lit = 10;
        private const int night_icon_lit = 11;

        // Inventory icon texture part ids
        private const int glowing_border_part_id = 5;
        private const int default_border_part_id = 0;

        // Cached Values
        private IntPtr ocean_fishing_addon_ptr;
        private IntPtr bait_window_addon_ptr;
        private string last_highlighted_bait = "";
        private unsafe AtkComponentNode* last_highlighted_bait_node = null;

        // Dictionaries
        public Dictionary<string, Dictionary<string, Dictionary<string, dynamic>>> bait_dictionary;
        private string bait_file_url = "https://markjsosnowski.github.io/FFXIV/bait2.json";
        private Dictionary<string, Int64> baitstring_to_iconid = new Dictionary<string, Int64>(); // Generated on initalization based on iconid_to_baitstring
        private Dictionary<Int64, string> iconid_to_baitstring = new Dictionary<Int64, string>()
        {
            [27023] = "Krill",
            [27015] ="Plump Worm",
            [27004] = "Ragworm"
            //TODO put in spectral int bait keys
        };

        public OceanFishin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager,            
            [RequiredVersion("1.0")] ClientState clientState,
            [RequiredVersion("1.0")] Framework framework,
            [RequiredVersion("1.0")] GameGui gameGui)
        {
            this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;
            this.ClientState = clientState;
            this.Framework = framework;
            this.GameGui = gameGui;

            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);

            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            this.PluginUI = new PluginUI(this, this.Configuration);

            this.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens a window with bait suggestions for the ocean fishing duty."
            });

            this.CommandManager.AddHandler(altCommandName1, new CommandInfo(OnCommand)
            {
                HelpMessage = "Alias for /oceanfishin"
            });

            this.CommandManager.AddHandler(altCommandName2, new CommandInfo(OnCommand)
            {
                HelpMessage = "Alias for /oceanfishin"
            });

            try
            {
                using (WebClient wc = new WebClient())
                {
                    var json = wc.DownloadString(bait_file_url);
                    bait_dictionary = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, dynamic>>>>(json);
                }
            }
            catch (WebException e)
            {
                PluginLog.Error("There was a problem retriving the bait list. Is GitHub down?", e);
                bait_dictionary = null;
            }
            
            // So only one dictionary of iconids needs to be maintained.
            foreach(var pair in iconid_to_baitstring)
            {
                baitstring_to_iconid.Add(pair.Value, pair.Key);
            }

            Framework.Update += update_addon_pointers;
            this.bait_window_addon_ptr = IntPtr.Zero;
            this.ocean_fishing_addon_ptr = IntPtr.Zero;

            this.PluginInterface.UiBuilder.Draw += DrawUI;
            this.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        }

        public unsafe void Dispose()
        {
            Framework.Update -= update_addon_pointers;
            this.PluginUI.Dispose();
            this.CommandManager.RemoveHandler(commandName);
            this.CommandManager.RemoveHandler(altCommandName1);
            this.CommandManager.RemoveHandler(altCommandName2);
            if (this.last_highlighted_bait_node != null)
                change_node_border(this.last_highlighted_bait_node, false);
        }
        private void OnCommand(string command, string args)
        {
            this.PluginUI.Visible = true;
        }

        private void DrawUI()
        {
            string location = default_location;
            string time = default_time;
            if (in_ocean_fishing_duty())
            {
                (location, time) = get_fishing_data();
            }
            if (DEBUG)
            {
                location = "Galadion Bay";
                time = "Sunset";
            }
            this.PluginUI.Draw(in_ocean_fishing_duty(), location, time);
        }

        private void DrawConfigUI()
        {
            this.PluginUI.SettingsVisible = true;
        }


        private bool in_ocean_fishing_duty()
        {
            if (DEBUG || (int)ClientState.TerritoryType == endevor_territory_type)
                return true;
            else
                return false;
        }

        private unsafe (string, string) get_fishing_data()
        {
            if (this.ocean_fishing_addon_ptr == IntPtr.Zero)
            {
                return (default_location, default_time);
            }
            AtkUnitBase* addon = (AtkUnitBase*)this.ocean_fishing_addon_ptr;

            // Without this check, the plugin might try to get a child node before the list was 
            // populated and cause a null pointer exception. 
            if (addon->UldManager.NodeListCount < expected_nodelist_count)
            {
                return (default_location, default_time);
            }
            return (get_location(addon), get_time(addon));
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
        public bool nested_key_exists(ref Dictionary<string, Dictionary<string, Dictionary<string, dynamic>>> dictionary, string key1, string key2, string key3)
        {
            if (dictionary.ContainsKey(key1))
                if (dictionary[key1].ContainsKey(key2))
                    if (dictionary[key1][key2].ContainsKey(key3))
                        return true;
            return false;
        }

        // When the spectral current occurs, this AtkResNode becomes visible behind the IKDFishingLog window.
        public unsafe bool is_spectral_current()
        {
            AtkUnitBase* addon;
            if (this.ocean_fishing_addon_ptr != IntPtr.Zero)
                 addon = (AtkUnitBase*)this.ocean_fishing_addon_ptr;
            else
                return false;
            if(addon->UldManager.NodeListCount < expected_nodelist_count)
                return false;
            AtkResNode* crusing_resnode = (AtkResNode*)addon->UldManager.NodeList[cruising_resnode_index];
            if (crusing_resnode->IsVisible)
                return true;
            else
                return false;
        }

        private unsafe void update_addon_pointers(Framework framework)
        {
            if (!in_ocean_fishing_duty())
            {
                bait_window_addon_ptr = IntPtr.Zero;
                ocean_fishing_addon_ptr = IntPtr.Zero;
                return;
            }
            try
            {
                // "IKDFishingLog" is the name of the blue window that appears during ocean fishing 
                // that displays location, time, and what you caught. This is known via Addon Inspector.
                this.ocean_fishing_addon_ptr = this.GameGui.GetAddonByName("IKDFishingLog", 1);
                // "Bait" is the Bait & Tackle window that fishers use to select their bait.
                this.bait_window_addon_ptr = this.GameGui.GetAddonByName("Bait", 1);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                PluginLog.Verbose("Ocean Fishin' caught an exception: " + e);
            }
        }

        public unsafe void highlight_inventory_item(string bait)
        {
            // If the bait window isn't open, do nothing.
            if (!addon_is_open(this.bait_window_addon_ptr))
            {
                return;
            }
            try
            {
                if(bait != this.last_highlighted_bait)
                {
                    if(this.last_highlighted_bait_node != null)
                        change_node_border(this.last_highlighted_bait_node, false);
                    this.last_highlighted_bait = bait;
                }
                AtkComponentNode* bait_node;
                bait_node = find_bait_item_node(bait);
                if (bait_node != null)
                {
                    this.last_highlighted_bait_node = bait_node;
                    change_node_border(bait_node, true);
                }
                    
            }
            catch(NullReferenceException e)
            {
                PluginLog.Debug("[Ocean Fishin'] The bait window was probably closed while it was being used. " + e, e);
                return;
            }
        }

        /* Problem: Searching by icon doesn't work because multiple baits use the same icon.
         * Ideas:
         * Search from the back: ocean baits are always at the end of the list, but will have the same problemfor special intuition baits.
         * The bait list is based on ItemID numbers, not inventory order or alphabetically. Figure out how the bait list is populated, but that's a lot of work.
         * Find some sort of static, unique identifier in each node. There's plenty of different pointers in each node, but nothing yet found that's static.
         * Skip the first search result and use the second. Won't work if the player only has one of the other bait.
         * The bait has to know it's name somehow when it is hovered over for the tooltip, so figure out how the tooltip is being generated without having to hover.
         */
        public unsafe AtkComponentNode* find_bait_item_node(string bait)
        {
            if (!addon_is_open(this.bait_window_addon_ptr))
                return null;
            PluginLog.Debug("Attempting to find the icon for " + bait);
            AtkUnitBase* bait_window_addon = (AtkUnitBase*)this.bait_window_addon_ptr;
            AtkComponentNode* bait_list_componentnode = (AtkComponentNode*)bait_window_addon->UldManager.NodeList[bait_list_componentnode_index];
            for (int i = 1; i < bait_list_componentnode->Component->UldManager.NodeListCount - 1; i++)
            {
                AtkComponentNode* list_item_node = (AtkComponentNode*)bait_list_componentnode->Component->UldManager.NodeList[i];
                AtkComponentNode* icon_component_node = (AtkComponentNode*)list_item_node->Component->UldManager.NodeList[iconid_index];
                AtkComponentIcon* icon_node = (AtkComponentIcon*)icon_component_node->Component;
                if(baitstring_to_iconid[bait] == icon_node->IconId)
                {
                    PluginLog.Debug("Found " + bait + " at index " + i + " with IconID " + icon_node->IconId);
                    return list_item_node;
                }
            }
            PluginLog.Debug("Could not find the node for " + bait);
            return null;
        }

        public unsafe  void change_node_border(AtkComponentNode* node, bool higlight)
        {
            AtkComponentNode* icon_component_node = (AtkComponentNode*)node->Component->UldManager.NodeList[iconid_index];
            AtkImageNode* frame_node = (AtkImageNode*)icon_component_node->Component->UldManager.NodeList[item_border_image_index];
            if(higlight)
                frame_node->PartId = glowing_border_part_id;
            else
                frame_node->PartId = default_border_part_id;
        }

        public unsafe bool addon_is_open(IntPtr addon)
        {
            return (addon != IntPtr.Zero);
        }
    }
}
