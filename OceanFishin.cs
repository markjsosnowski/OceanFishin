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

        public static Dictionary<string, Dictionary<string, Dictionary<string, dynamic>>> bait_dictionary;
        private string bait_file_url = "https://markjsosnowski.github.io/FFXIV/bait2.json";

        private DalamudPluginInterface PluginInterface { get; init; }
        private CommandManager CommandManager { get; init; }
        private Configuration Configuration { get; init; }
        private PluginUI PluginUI { get; init; }
        private ClientState ClientState { get; init; }
        private Framework Framework { get; init; }
        private GameGui GameGui { get; init; }

        // This is the TerritoryType for the entire instance and does not
        // provide any information on fishing spots, routes, etc.
        private const int endevor_territory_type = 900;
        private bool on_boat = false;

        private const string default_location = "location unknown";
        private const string default_time = "time unknown";

        // These are known via addon inspector.
        private const int location_textnode_index = 20;
        private const int night_imagenode_index = 22;
        private const int sunset_imagenode_index = 23;
        private const int day_imagenode_index = 24;
        private const int expected_nodelist_count = 24;
        private const int cruising_texture_index = 3;

        // Three image nodes make up the time of day indicator.
        // They all use the same texture, so the part_id determines
        // which part of the texture is used. Those part_ids are:
        // Day      Active = 9  Inactive = 4
        // Sunset   Active = 10 Inactive = 5
        // Night    Active = 11 Inactive = 6
        private const int day_icon_lit = 9;
        private const int sunset_icon_lit = 10;
        private const int night_icon_lit = 11;

        private static IntPtr ocean_fishing_addon_ptr;

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
            update_ocean_fishing_addon_ptr(on_boat);
            if (on_boat)
            {
                (location, time) = get_fishing_data();
            }
            this.PluginUI.Draw(on_boat, location, time);
            //this.PluginUI.Draw(true, "The Southern Strait of Merlthor", "Sunset");
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

        // When the spectral current occurs, two image nodes become visible behind the IKDFishingLog addon for a graphical effect.
        // They are both layered to increase the effect, so just one is chosen and checked for visiblility. 
        // If it is visible, the spectral current must be active.
        public static unsafe bool is_spectral_current()
        {
            AtkUnitBase* addon;
            if (OceanFishin.ocean_fishing_addon_ptr != IntPtr.Zero)
                 addon = (AtkUnitBase*)OceanFishin.ocean_fishing_addon_ptr;
            else
                return false;
            if(addon->UldManager.NodeListCount < expected_nodelist_count)
                return false;
            AtkImageNode* crusing_imagenode = (AtkImageNode*)addon->UldManager.NodeList[cruising_texture_index];
            if (crusing_imagenode->AtkResNode.IsVisible)
                return true;
            else
                return false;
        }

        // I think GetAddonByName is linear search time? So by only looking for it when it isn't defined and needs to be, we can avoid redundant lookups. 
        private unsafe IntPtr  update_ocean_fishing_addon_ptr(bool on_boat)
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
    }
}
