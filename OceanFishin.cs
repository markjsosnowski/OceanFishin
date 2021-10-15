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

        // This is the TerritoryType for the entire instance and does not
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

        //static string codebase = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
        //static UriBuilder uri = new UriBuilder(codebase);
        //static string path = Uri.UnescapeDataString(uri.Path);
        //string plugin_dir = System.IO.Path.GetDirectoryName(path);

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
            var json_path = Path.Combine(Path.GetDirectoryName(assemblyLocation)!, "bait.json");

            this.PluginUI = new PluginUI(this.Configuration, json_path);

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
            if (on_boat)
            {
                (location, time) = get_data();
            }
            this.PluginUI.Draw(on_boat, location, time);
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

        private unsafe (string, string) get_data()
        {
            string current_location = default_location;
            string current_time = default_time;

            // IKDFishingLog is the name of the blue window that appears during ocean fishing 
            // that displays location, time, and what you caught. This is known via Addon Inspector.
            var addon_ptr = GameGui.GetAddonByName("IKDFishingLog", 1);
            if (addon_ptr == null || addon_ptr == IntPtr.Zero)
            {
                return (current_location, current_time);
            }
            AtkUnitBase* addon = (AtkUnitBase*)addon_ptr;

            // Without this check, the plugin might try to get a child node before the list was 
            // populated and cause a null pointer exception. 
            if (addon->UldManager.NodeListCount < expected_nodelist_count)
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
    }
}
