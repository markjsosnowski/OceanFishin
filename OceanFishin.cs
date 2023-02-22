using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.IoC;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.Game;
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
using Dalamud.Game.ClientState.Objects.SubKinds;
using OceanFishin.Windows;
using Dalamud.Interface.Windowing;
using System.Runtime.CompilerServices;

namespace OceanFishin
{  
    public sealed class OceanFishin : IDalamudPlugin
    {
        private const bool DEBUG = false;
        
        public string Name => "Ocean Fishin'";

        public const string commandName = "/oceanfishin";
        public const string altCommandName1 = "/oceanfishing";
        public const string altCommandName2 = "/bait";

        private DalamudPluginInterface PluginInterface { get; init; }
        private CommandManager CommandManager { get; init; }
        private Configuration Configuration { get; init; }
        private MainWindow MainWindow { get; init; }
        private ClientState ClientState { get; init; }
        private Framework Framework { get; init; }
        private GameGui GameGui { get; init; }
        private ConfigWindow ConfigWindow { get; init; }

        private WindowSystem WindowSystem = new("Ocean Fishin'");

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

        private const int intuition_buff_id = 568;

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
        /*private Dictionary<Int64, string> iconid_to_baitstring = new Dictionary<Int64, string>()
        {
            [27023] = "Krill",
            [27015] ="Plump Worm",
            [27004] = "Ragworm"
            //TODO put in spectral int bait keys
        };*/

        private Dictionary<Int64, string> iconid_to_baitstring = new Dictionary<Int64, string>()
        {
            [29715] = "Krill",
            [29716] ="Plump Worm",
            [29714] = "Ragworm"
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
            this.MainWindow = new MainWindow(this, this.Configuration);
            this.WindowSystem.AddWindow(this.MainWindow);

            this.ConfigWindow = new ConfigWindow(this, this.Configuration);
            this.WindowSystem.AddWindow(this.ConfigWindow);


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
            this.MainWindow.Dispose();
            this.CommandManager.RemoveHandler(commandName);
            this.CommandManager.RemoveHandler(altCommandName1);
            this.CommandManager.RemoveHandler(altCommandName2);
            if (this.last_highlighted_bait_node != null)
                change_node_border(this.last_highlighted_bait_node, false);
            WindowSystem.RemoveAllWindows();
        }
        private void OnCommand(string command, string args)
        {
            MainWindow.IsOpen = true;
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
            WindowSystem.Draw();
        }

        private void DrawConfigUI()
        {
            this.ConfigWindow.IsOpen = true;
        }


        // Since you have to be a fisher to get into the Duty, checking player job is probably unnecessary. 
        public bool in_ocean_fishing_duty()
        {
            if (DEBUG || (int)ClientState.TerritoryType == endevor_territory_type)
                return true;
            else
                return false;
        }

        public unsafe (string, string) get_fishing_data()
        {

            if (DEBUG)
            {
                return ("Galadion Bay", "Sunset");
            }
            
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

        // crashes on load in to the duty, should check player_character for null and return until it's actually defined
        public unsafe bool has_intuition_buff()
        {
            Dalamud.Game.ClientState.Statuses.StatusList buff_list;
            PlayerCharacter player_character = ClientState.LocalPlayer;
            
            if(player_character != null && player_character.StatusList != null)
            {
                buff_list = player_character.StatusList;
                for (int i = 0; i < buff_list.Length; i++)
                {
                    if (DEBUG && buff_list[i].StatusId != 0) PluginLog.Debug("Status id " + i + " : " + buff_list[i].StatusId);
                    if (buff_list[i].StatusId == intuition_buff_id)
                    {
                        if (DEBUG) PluginLog.Debug("Intuition was detected!");
                        return true;
                    }
                }
                return false;
            }
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
            //TODO a crash still happens right around here. maybe check expected count?
            AtkUnitBase* bait_window_addon = (AtkUnitBase*)this.bait_window_addon_ptr;
            AtkComponentNode* bait_list_componentnode = (AtkComponentNode*)bait_window_addon->UldManager.NodeList[bait_list_componentnode_index];
            //TODO this is only a temporary solution, searching for the back. have to find a solution that is more consistent
            for (int i = bait_list_componentnode->Component->UldManager.NodeListCount - 1; i > 0 ; i--)
            {
                AtkComponentNode* list_item_node = (AtkComponentNode*)bait_list_componentnode->Component->UldManager.NodeList[i];
                AtkComponentNode* icon_component_node = (AtkComponentNode*)list_item_node->Component->UldManager.NodeList[iconid_index];
                AtkComponentIcon* icon_node = (AtkComponentIcon*)icon_component_node->Component;
                if(baitstring_to_iconid[bait] == icon_node->IconId)
                {
                    //PluginLog.Debug("Found " + bait + " at index " + i + " with IconID " + icon_node->IconId);
                    return list_item_node;
                }
            }
            PluginLog.Debug("Could not find the node for " + bait);
            return null;
        }

        
        /*
        // Treated the node as an InventoryItem does not work 
        // Treating the node as a ListItemTrenderer doesn't work
        public unsafe AtkComponentNode* find_bait_item_node(string bait_name)
        {
            try
            {
                
                

                AtkUnitBase* bait_window_addon = (AtkUnitBase*)this.bait_window_addon_ptr;
                AtkComponentNode* list_componentnode = (AtkComponentNode*)bait_window_addon->UldManager.NodeList[bait_list_componentnode_index]->GetComponent();
                for(int i = 0; i < list_componentnode->Component->UldManager.NodeListCount; i++)
                {
                    var text_node = (InventoryItem*)list_componentnode->Component->UldManager.NodeList[i];
                    PluginLog.Debug("["+i+"] " +text_node->ToString());
                    PluginLog.Debug("Item ID: " + text_node->ItemID);
                    PluginLog.Debug("Quantity: " + text_node->Quantity);
                }



                /*AtkComponentNode* lst_comp_node = (AtkComponentNode*)bait_window_addon->UldManager.NodeList[bait_list_componentnode_index];
                AtkComponentNode* item_comp_node = (AtkComponentNode*)lst_comp_node->Component->UldManager.NodeList[3];
                AtkCollisionNode* coll_node = (AtkCollisionNode*)item_comp_node->Component->UldManager.NodeList[0];
                long* vtbl = (long*)coll_node->AtkResNode.AtkEventTarget.vtbl;
                
                PluginLog.Debug(vtbl->ToString());
                var linked_comp = coll_node->LinkedComponent;
                PluginLog.Debug("Linked component: " + linked_comp->ToString());*/


                /*AtkComponentList* list_component = (AtkComponentList*)bait_window_addon->UldManager.NodeList[bait_list_componentnode_index]->GetComponent();
                var list_length = list_component->ListLength;
                PluginLog.Debug("List length is " + list_length);
                AtkComponentList.ListItem* item_list = list_component->ItemRendererList;
                for(int i = 0; i<list_component->ListLength; i++)
                {
                    var list_item_ptr = (IntPtr*)&item_list[i].AtkComponentListItemRenderer;
                    PluginLog.Debug("[" + i + "] Item Renderer Ptr: " + list_item_ptr->ToString("X"));
                    PluginLog.Debug("Type of [" + i + "] is " + item_list[i].ToString());
                    var button_node_ptr = (IntPtr*)&item_list[i].AtkComponentListItemRenderer->AtkComponentButton;
                    var button_node = item_list[i].AtkComponentListItemRenderer->AtkComponentButton;
                    PluginLog.Debug("[" + i + "] Button Ptr: " + button_node_ptr->ToString("X"));
                    PluginLog.Debug("[" + i + "] Button IsEnabled: " + button_node.IsEnabled);
                    var text_node_ptr = (IntPtr*)&item_list[i].AtkComponentListItemRenderer->AtkComponentButton.ButtonTextNode;
                    PluginLog.Debug("[" + i + "] ButtonTextNodePtr: " + text_node_ptr->ToString("X"));
                    PluginLog.Debug("[" + i + "] ButtonNode NodeListCount: " + button_node.AtkComponentBase.UldManager.NodeListCount);
                    for (int j =0; j< button_node.AtkComponentBase.UldManager.NodeListCount; j++)
                    {
                        PluginLog.Debug("\t["+j+"] is " + button_node.AtkComponentBase.UldManager.NodeList[j]->ToString() +" at " + ((IntPtr*)button_node.AtkComponentBase.UldManager.NodeList[j])->ToString("X"));
                    }


                }

                AtkComponentListItemRenderer* item_renderer = item_list[0].AtkComponentListItemRenderer;
                IntPtr* item_renderer_ptr = (IntPtr*)item_renderer;
                PluginLog.Debug("Breakpoint 2" + item_renderer_ptr->ToString("X"));
                AtkComponentButton item_renderer_button = item_renderer->AtkComponentButton;
                PluginLog.Debug("Breakpoint 3");
                AtkTextNode* button_text_node = item_renderer_button.ButtonTextNode;
                PluginLog.Debug("Breakpoint 4");
                PluginLog.Debug(text_node_to_string(button_text_node));

            }
            catch (Exception e)
            {
                PluginLog.Debug(e.ToString());
                return null;
            }

            //AtkComponentNode* bait_list_componentnode = (AtkComponentNode*)bait_window_addon->UldManager.NodeList[bait_list_componentnode_index];
            try
            {
                for (int i = 1; i < bait_list_componentnode->Component->UldManager.NodeListCount - 1; i++)
                {
                    PluginLog.Debug("Seaching index " + i);
                    AtkComponentListItemRenderer* list_item_node = (AtkComponentListItemRenderer*)bait_list_componentnode->Component->UldManager.NodeList[i];
                    

                    PluginLog.Debug("Break Point 1");
                    AtkComponentButton item_button_node = list_item_node->AtkComponentButton;
                    PluginLog.Debug("Break Point 2");
                    AtkTextNode* button_text_node = item_button_node.ButtonTextNode;
                    PluginLog.Debug("Break Point 3");
                    PluginLog.Debug("The ListItemRenderer's ComponentButton's TextNode's Text was " + Marshal.PtrToStringAnsi(new IntPtr(button_text_node->NodeText.StringPtr)));
                    //if (baitstring_to_iconid[bait_name] == inventory_item_node->ItemID)
                    //    return list_item_node;
                }
                return null;
            }
            catch(Exception e)
            {
                PluginLog.Debug(e.ToString(), e);
                return null;
            }
            return null;

        }*/

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

        public unsafe string text_node_to_string(AtkTextNode* text_node)
        {
            try
            {
                return Marshal.PtrToStringAnsi(new IntPtr(text_node->NodeText.StringPtr));
            }
            catch(Exception e)
            {
                PluginLog.Debug(e.ToString());
                return "Text node was null.";
            }
            
        }
    }
}
