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
        private bool on_boat = false;

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
        private static IntPtr ocean_fishing_addon_ptr;
        private static IntPtr bait_window_addon_ptr;
        private static string last_highlighted_bait = null;
        private unsafe static AtkComponentNode* last_highlighted_bait_node = null;

        // Dictionaries
        public static Dictionary<string, Dictionary<string, Dictionary<string, dynamic>>> bait_dictionary;
        private string bait_file_url = "https://markjsosnowski.github.io/FFXIV/bait2.json";
        private static Dictionary<string, Int64> baitstring_to_iconid = new Dictionary<string, Int64>();
        private static Dictionary<Int64, string> iconid_to_baitstring = new Dictionary<Int64, string>()
        {
            [27023] = "Krill",
            [27015] ="Plump Worm",
            [27004] = "Ragworm"
            //TODO put in spectral int bait keys
        };

        private static GameGui StaticGameGui;



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
            StaticGameGui = this.GameGui;

            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);

            var assemblyLocation = Assembly.GetExecutingAssembly().Location;

            this.PluginUI = new PluginUI(this.Configuration);

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
                PluginLog.Error("There was a problem accessing the bait list. Is GitHub down?", e);
                bait_dictionary = null;
            }
            
            // So only one dictionary of iconids needs to be maintained.
            foreach(var pair in iconid_to_baitstring)
            {
                baitstring_to_iconid.Add(pair.Value, pair.Key);
            }
            
            this.PluginInterface.UiBuilder.Draw += DrawUI;
            this.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        }

        public void Dispose()
        {
            this.PluginUI.Dispose();
            this.CommandManager.RemoveHandler(commandName);
            this.CommandManager.RemoveHandler(altCommandName1);
            this.CommandManager.RemoveHandler(altCommandName2);
        }
        private void OnCommand(string command, string args)
        {
            this.PluginUI.Visible = true;
        }

        private void DrawUI()
        {
            string location = default_location;
            string time = default_time;
            on_boat = check_location();
            update_bait_window_addon_ptr(true); //change me back
            update_ocean_fishing_addon_ptr(on_boat);
            if (on_boat)
            {
                (location, time) = get_fishing_data();
            }
            //this.PluginUI.Draw(on_boat, location, time);
            this.PluginUI.Draw(true, "Galadion Bay", "Sunset");
        }

        private void DrawConfigUI()
        {
            this.PluginUI.SettingsVisible = true;
        }


        private bool check_location()
        {
            if ((int)ClientState.TerritoryType == endevor_territory_type)
                return true;
            else
                return false;
        }

        private unsafe (string, string) get_fishing_data()
        {
            if (OceanFishin.ocean_fishing_addon_ptr == IntPtr.Zero)
            {
                return (default_location, default_time);
            }
            AtkUnitBase* addon = (AtkUnitBase*)OceanFishin.ocean_fishing_addon_ptr;

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
        public static bool nested_key_exists(ref Dictionary<string, Dictionary<string, Dictionary<string, dynamic>>> dictionary, string key1, string key2, string key3)
        {
            if (dictionary.ContainsKey(key1))
                if (dictionary[key1].ContainsKey(key2))
                    if (dictionary[key1][key2].ContainsKey(key3))
                        return true;
            return false;
        }

        // When the spectral current occurs, a AtkResNode becomes visible behind the IKDFishingLog window.
        public static unsafe bool is_spectral_current()
        {
            AtkUnitBase* addon;
            if (OceanFishin.ocean_fishing_addon_ptr != IntPtr.Zero)
                 addon = (AtkUnitBase*)OceanFishin.ocean_fishing_addon_ptr;
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

        // I think GetAddonByName is linear search time? So by only looking for it when it isn't defined and needs to be, we can avoid redundant lookups. 
        private unsafe IntPtr update_ocean_fishing_addon_ptr(bool on_boat)
        {
            if (!on_boat)
                return OceanFishin.ocean_fishing_addon_ptr = IntPtr.Zero;
            if (OceanFishin.ocean_fishing_addon_ptr != IntPtr.Zero)
                return OceanFishin.ocean_fishing_addon_ptr;
            else
                // IKDFishingLog is the name of the blue window that appears during ocean fishing 
                // that displays location, time, and what you caught. This is known via Addon Inspector.
                return OceanFishin.ocean_fishing_addon_ptr = GameGui.GetAddonByName("IKDFishingLog", 1);
        }

        private unsafe IntPtr update_bait_window_addon_ptr(bool on_boat)
        {
            if (!on_boat)
                return bait_window_addon_ptr = IntPtr.Zero;
            if (bait_window_addon_ptr != IntPtr.Zero)
                return bait_window_addon_ptr;
            else
            {
                return bait_window_addon_ptr = GameGui.GetAddonByName("Bait", 1);
            }
        }


        /*
        // A dictionary of bait types and pointers to that bait's entry in the bait window
        // Updated only when the bait window changes (eg. runs out of Krill and then buys more).
        private unsafe static void update_bait_pointer_dictionary() 
        {
            // If the bait window isn't open, do nothing.
            if (OceanFishin.bait_window_addon_ptr == IntPtr.Zero)
                return;

            AtkUnitBase* addon = (AtkUnitBase*)bait_window_addon_ptr;
            if (addon->UldManager.NodeListCount < expected_bait_window_nodelist_count)
                return;
            // If the bait window hasn't changed, we don't need to update anything.
            AtkComponentNode* bait_list_componentnode = (AtkComponentNode*)addon->UldManager.NodeList[bait_list_componentnode_index];
                        
            if (bait_list_componentnode->Component->UldManager.NodeListCount == OceanFishin.last_known_bait_nodecount)
                return;

            // Reset the dictionary...
            OceanFishin.baitstring_to_iconnode_ptr = new Dictionary<string, IntPtr>();

            PluginLog.Debug("Trying to update the bait pointer map...");
            PluginLog.Debug("ComponentNode->Component Node list count is  " + bait_list_componentnode->Component->UldManager.NodeListCount);
            // ...then repopulate it.
            OceanFishin.last_known_bait_nodecount = bait_list_componentnode->Component->UldManager.NodeListCount;
            for(int i = 1; i< bait_list_componentnode->Component->UldManager.NodeListCount - 1; i++)
            {
                AtkComponentNode* list_item_node = (AtkComponentNode*)bait_list_componentnode->Component->UldManager.NodeList[i];
                AtkComponentNode* icon_component_node = (AtkComponentNode*)list_item_node->Component->UldManager.NodeList[iconid_index];
                AtkComponentIcon* icon_node = (AtkComponentIcon*)icon_component_node->Component;
                Int64 node_icon_id = icon_node->IconId;
                PluginLog.Debug("Node " + list_item_node->AtkResNode.NodeID + " at index " + i +" had an icon node " + icon_component_node->AtkResNode.NodeID + " with IconId " +node_icon_id);
                if (iconid_to_baitstring.ContainsKey(node_icon_id))
                {
                    PluginLog.Information("Item " + iconid_to_baitstring[node_icon_id] + " with icon ID " + node_icon_id + " was found and added to the map.");
                    baitstring_to_iconnode_ptr[iconid_to_baitstring[node_icon_id]] = (IntPtr)list_item_node;
                    continue;
                }
            }
        }

  
        
        //TODO Just do a linear, stop-short search of the bait list to get it working and worry about caching the pointer later
        public unsafe static void highlight_inventory_item(string bait) 
        {
            if (OceanFishin.bait_window_addon_ptr == IntPtr.Zero)
                return;
            if (bait == OceanFishin.last_highlighted_bait)
                return;
            if (OceanFishin.baitstring_to_iconnode_ptr == null)
                return;

            Int64 current_bait_iconid = baitstring_to_iconid[bait];
            AtkComponentNode* cached_item_component_ptr = (AtkComponentNode*)OceanFishin.baitstring_to_iconnode_ptr[bait];
            AtkComponentNode* cached_icon_component_ptr = (AtkComponentNode*)cached_item_component_ptr->Component->UldManager.NodeList[iconid_index];
            AtkComponentIcon* cached_icon_node_ptr = (AtkComponentIcon*)cached_icon_component_ptr->Component;
            Int64 cached_ptr_iconid = cached_icon_node_ptr->IconId;
            if (cached_ptr_iconid != current_bait_iconid)
                update_bait_pointer_dictionary();

            // if(if currently highlighted node's iconid != expected iconid)
            // scan the bait list until it is found
            // if it isn't there, return
            // if it is there, cache the single pointer

            // If last_highlighted_bait exists, unhighlight it before highlighting the new one.
            if (OceanFishin.last_highlighted_bait != null)
            {
                IntPtr prev_node_ptr = OceanFishin.baitstring_to_iconnode_ptr[OceanFishin.last_highlighted_bait];

                if (prev_node_ptr != IntPtr.Zero)
                {
                    AtkComponentNode* prev_item_node = (AtkComponentNode*)OceanFishin.baitstring_to_iconnode_ptr[OceanFishin.last_highlighted_bait];
                    prev_item_node->AtkResNode.MultiplyBlue = 100;
                    prev_item_node->AtkResNode.MultiplyGreen = 100;
                    prev_item_node->AtkResNode.MultiplyRed = 100;
                }

                OceanFishin.last_highlighted_bait = bait;
            }

            IntPtr item_node_ptr = OceanFishin.baitstring_to_iconnode_ptr[bait];
            if (item_node_ptr == IntPtr.Zero)
                return;
            
            AtkComponentNode* item_node = (AtkComponentNode*)item_node_ptr;
            item_node->AtkResNode.MultiplyBlue = 100;
            item_node->AtkResNode.MultiplyGreen = 0;
            item_node->AtkResNode.MultiplyRed = 0;
        }*/

        // Doesn't work if the bait window is closed and opened
        public unsafe static void highlight_inventory_item(string bait)
        {
            // If the bait window isn't open, do nothing.
            if (OceanFishin.bait_window_addon_ptr == IntPtr.Zero)
            {
                PluginLog.Debug("The bait window wasn't open.");
                return;
            }
            //If the bait hasn't changed, do nothing.
            if (bait == last_highlighted_bait)
            {
                PluginLog.Debug("The bait was the same as the previously highlighted bait.");
                return;
            }
            try
            {
                AtkComponentNode* bait_node;
                if (last_highlighted_bait_node == null)
                {
                    bait_node = find_bait_item_node(bait);
                    last_highlighted_bait_node = bait_node;
                }
                else
                {
                    AtkComponentNode* icon_component_node = (AtkComponentNode*)last_highlighted_bait_node->Component->UldManager.NodeList[iconid_index];
                    AtkComponentIcon* icon_node = (AtkComponentIcon*)icon_component_node->Component;
                    if (icon_node->IconId == baitstring_to_iconid[bait])
                        return;
                }
                bait_node = find_bait_item_node(bait);
                last_highlighted_bait_node = bait_node;
                if (bait_node != null)
                {
                    change_node_border(bait_node, true);
                }   
            }
            catch(NullReferenceException e)
            {
                PluginLog.Debug("The bait window became inaccessible.", e);
                return;
            }
        }

        // Works but fails when 2 baits have the same icon
        public unsafe static AtkComponentNode* find_bait_item_node(string bait)
        {
            PluginLog.Debug("Attempting to find the icon for " + bait);
            IntPtr bait_window_ptr = StaticGameGui.GetAddonByName("Bait", 1);
            if (bait_window_ptr == IntPtr.Zero)
                return null;
            AtkUnitBase* bait_window_addon = (AtkUnitBase*)bait_window_ptr;
            AtkComponentNode* bait_list_componentnode = (AtkComponentNode*)bait_window_addon->UldManager.NodeList[bait_list_componentnode_index];
            for (int i = 1; i < bait_list_componentnode->Component->UldManager.NodeListCount - 1; i++)
            {
                AtkComponentNode* list_item_node = (AtkComponentNode*)bait_list_componentnode->Component->UldManager.NodeList[i];
                AtkComponentNode* icon_component_node = (AtkComponentNode*)list_item_node->Component->UldManager.NodeList[iconid_index];
                AtkComponentIcon* icon_node = (AtkComponentIcon*)icon_component_node->Component;
                if(baitstring_to_iconid[bait] == icon_node->IconId)
                {
                    PluginLog.Debug("Found it at index " + i + " with IconID " + icon_node->IconId);
                    return list_item_node;
                }
            }
            PluginLog.Debug("Could not find it.");
            return null;
        }

        // Works
        public unsafe static void change_node_border(AtkComponentNode* node, bool higlight)
        {
            AtkComponentNode* icon_component_node = (AtkComponentNode*)node->Component->UldManager.NodeList[iconid_index];
            AtkImageNode* frame_node = (AtkImageNode*)icon_component_node->Component->UldManager.NodeList[item_border_image_index];
            if(higlight)
                frame_node->PartId = glowing_border_part_id;
            else
                frame_node->PartId = default_border_part_id;
        }
    }
}
