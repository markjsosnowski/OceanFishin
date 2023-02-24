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
using FFXIVClientStructs.Attributes;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Dalamud.Data;
using Lumina.Excel.GeneratedSheets;
using Lumina.Excel;
using FFXIVClientStructs.FFXIV.Client.System.String;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Collections;

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
        private MainWindow MainWindow { get; init; }
        private ClientState ClientState { get; init; }
        private Framework Framework { get; init; }
        private GameGui GameGui { get; init; }
        private ConfigWindow ConfigWindow { get; init; }
        public DataManager DataManager { get; init; }
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
        private string last_location_string = "";
        private unsafe AtkComponentNode* last_highlighted_bait_node = null;

        public Dalamud.ClientLanguage UserLanguage;

        public ExcelSheet<IKDSpot>? LocationSheet { get; private set; }

        public enum Location
        {
            Unknown = 0,
            GaladionBay = 1,
            SouthernStraight = 2,
            NorthernStrait = 3,
            RhotanoSea = 4,
            Cieldales = 5,
            BloodbrineSea = 6,
            RothlytSound = 7
        }

        public enum Time
        {
            Unknown,
            Day,
            Sunset,
            Night
        }

        public enum FishTypes
        {
            None,
            Balloons,
            Crabs,
            Dragons,
            Jellyfish,
            Mantas,
            Octopodes,
            Sharks
        }

        public enum Bait : uint
        {
            Glowworm = 2603,
            HeavySteelJig = 2619,
            Krill = 29715,
            PillBug = 2587,
            PlumpWorm = 29716,
            Ragworm = 29714,
            RatTail = 2591,
            ShrimpCageFeeder = 2613,
            SquidStrip = 27590,
            None = 0
        }

        //private Dictionary<string, Int64> baitstring_to_iconid = new Dictionary<string, Int64>();
        //Bait Icon ids
        //Krill 27023
        //PlumpWorm 27015
        //Ragworm 27004

        //Bait Dictionaries

        private Dictionary<Location, Bait> SpectralChanceBaitDictionary = new Dictionary<Location, Bait>
        {
            [Location.BloodbrineSea] = Bait.Krill,
            [Location.Cieldales] = Bait.Ragworm,
            [Location.GaladionBay] = Bait.Krill,
            [Location.NorthernStrait] = Bait.Ragworm,
            [Location.RhotanoSea] = Bait.PlumpWorm,
            [Location.RothlytSound] = Bait.PlumpWorm,
            [Location.SouthernStraight] = Bait.Krill
        };

        private Dictionary<Location, Bait> FishersIntutionBaitDictionary = new Dictionary<Location, Bait>
        {
            [Location.BloodbrineSea] = Bait.Krill,
            [Location.Cieldales] = Bait.Krill,
            [Location.GaladionBay] = Bait.Krill,
            [Location.NorthernStrait] = Bait.Ragworm,
            [Location.RhotanoSea] = Bait.Krill,
            [Location.RothlytSound] = Bait.Ragworm,
            [Location.SouthernStraight] = Bait.PlumpWorm
        };

        private Dictionary<(Location,Time), Bait> SpectralIntuitionBaitDictionary = new Dictionary<(Location, Time), Bait>
        {
            [(Location.BloodbrineSea, Time.Day)] =  Bait.PillBug,
            [(Location.Cieldales, Time.Night)] =  Bait.SquidStrip,
            [(Location.GaladionBay, Time.Night)] = Bait.Glowworm,
            [(Location.NorthernStrait, Time.Day)] = Bait.HeavySteelJig,
            [(Location.RhotanoSea, Time.Sunset)] = Bait.RatTail,
            [(Location.RothlytSound, Time.Sunset)] = Bait.Ragworm,
            [(Location.SouthernStraight, Time.Night)] = Bait.ShrimpCageFeeder
        };

        private Dictionary<Location, Dictionary<Time, Bait>> SpectralHighPointsBaitDictionary = new Dictionary<Location, Dictionary<Time, Bait>>
        {
            [Location.BloodbrineSea] = new Dictionary<Time, Bait>{ [Time.Day] = Bait.Ragworm, [Time.Sunset] = Bait.PlumpWorm, [Time.Night] = Bait.Krill },
            [Location.Cieldales] = new Dictionary<Time, Bait>{ [Time.Day] = Bait.Krill, [Time.Sunset] = Bait.PlumpWorm, [Time.Night] = Bait.Krill },
            [Location.GaladionBay] = new Dictionary<Time, Bait> { [Time.Day] = Bait.Ragworm, [Time.Sunset]= Bait.PlumpWorm, [Time.Night]=Bait.Krill },
            [Location.NorthernStrait] = new Dictionary<Time, Bait> { [Time.Day] = Bait.PlumpWorm, [Time.Sunset] = Bait.Krill, [Time.Night] = Bait.Krill },
            [Location.RhotanoSea] = new Dictionary<Time, Bait> { [Time.Day] = Bait.PlumpWorm, [Time.Sunset] = Bait.Ragworm, [Time.Night] = Bait.Krill },
            [Location.RothlytSound] = new Dictionary<Time, Bait> { [Time.Day] = Bait.Krill, [Time.Sunset] = Bait.Krill, [Time.Night] = Bait.Krill },
            [Location.SouthernStraight] = new Dictionary<Time, Bait> { [Time.Day] = Bait.Krill, [Time.Sunset] = Bait.Ragworm, [Time.Night] = Bait.Ragworm }
        };

        private Dictionary<Location, Dictionary<FishTypes, Bait>> MissionFishBaitDictionary = new Dictionary<Location, Dictionary<FishTypes, Bait>>
        {
            [Location.BloodbrineSea] = new Dictionary<FishTypes, Bait> { [FishTypes.Crabs] = Bait.Ragworm },
            [Location.Cieldales] = new Dictionary<FishTypes, Bait> { [FishTypes.Balloons] = Bait.Ragworm, [FishTypes.Crabs] = Bait.Krill, [FishTypes.Mantas] = Bait.PlumpWorm },
            [Location.GaladionBay] = new Dictionary<FishTypes, Bait> { [FishTypes.Octopodes] = Bait.Krill , [FishTypes.Sharks] = Bait.PlumpWorm },
            [Location.NorthernStrait] = new Dictionary<FishTypes, Bait> { [FishTypes.Crabs] = Bait.Krill, [FishTypes.Balloons] = Bait.Krill},
            [Location.RhotanoSea] = new Dictionary<FishTypes, Bait> { [FishTypes.Sharks] = Bait.PlumpWorm, [FishTypes.Balloons] = Bait.Ragworm},
            [Location.RothlytSound] = new Dictionary<FishTypes, Bait> { [FishTypes.Balloons] = Bait.Ragworm, [FishTypes.Jellyfish] = Bait.Krill, [FishTypes.Sharks] = Bait.Krill},
            [Location.SouthernStraight] = new Dictionary<FishTypes, Bait> { [FishTypes.Jellyfish] = Bait.Ragworm, [FishTypes.Dragons] = Bait.Ragworm , [FishTypes.Balloons] = Bait.Krill }
        };

        private Dictionary<(Location, Time), Dictionary<FishTypes, Bait>> SpectralFishBaitDictionary = new Dictionary<(Location, Time), Dictionary<FishTypes, Bait>>
        {
            [(Location.BloodbrineSea, Time.Day)] = new Dictionary<FishTypes, Bait> { [FishTypes.Crabs] = Bait.Ragworm, [FishTypes.Sharks] = Bait.PlumpWorm },
            [(Location.BloodbrineSea, Time.Sunset)] = new Dictionary<FishTypes, Bait> { [FishTypes.Sharks] = Bait.PlumpWorm },
            [(Location.BloodbrineSea, Time.Night)] = new Dictionary<FishTypes, Bait> { [FishTypes.Sharks] = Bait.PlumpWorm, [FishTypes.Mantas] = Bait.Krill },
            [(Location.NorthernStrait, Time.Sunset)] = new Dictionary<FishTypes, Bait> { [FishTypes.Dragons] = Bait.Ragworm },
            [(Location.NorthernStrait, Time.Night)] = new Dictionary<FishTypes, Bait> { [FishTypes.Octopodes] = Bait.Krill, [FishTypes.Crabs] = Bait.Ragworm },
            [(Location.RhotanoSea, Time.Day)] = new Dictionary<FishTypes, Bait> { [FishTypes.Sharks] = Bait.PlumpWorm, [FishTypes.Balloons] = Bait.Ragworm },
            [(Location.RhotanoSea, Time.Sunset)] = new Dictionary<FishTypes, Bait> { [FishTypes.Balloons] = Bait.Ragworm },
            [(Location.RhotanoSea, Time.Night)] = new Dictionary<FishTypes, Bait> { [FishTypes.Balloons] = Bait.Ragworm },
            [(Location.RothlytSound, Time.Day)] = new Dictionary<FishTypes, Bait> { [FishTypes.Balloons] = Bait.Krill, [FishTypes.Mantas] = Bait.PlumpWorm },
            [(Location.RothlytSound, Time.Night)] = new Dictionary<FishTypes, Bait> { [FishTypes.Balloons] = Bait.Krill },
            [(Location.SouthernStraight, Time.Sunset)] = new Dictionary<FishTypes, Bait> { [FishTypes.Jellyfish] = Bait.Ragworm },
            [(Location.SouthernStraight, Time.Night)] = new Dictionary<FishTypes, Bait> { [FishTypes.Jellyfish] = Bait.Ragworm }
        };

        private Dictionary<string, Location> localizedLocationStrings = new Dictionary<string, Location>();

        public OceanFishin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager,            
            [RequiredVersion("1.0")] ClientState clientState,
            [RequiredVersion("1.0")] Framework framework,
            [RequiredVersion("1.0")] GameGui gameGui,
            [RequiredVersion("1.0")] DataManager dataManager
            )
        {
            this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;
            this.ClientState = clientState;
            this.Framework = framework;
            this.GameGui = gameGui;
            this.DataManager = dataManager;

            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);
            this.MainWindow = new MainWindow(this, this.Configuration);
            this.WindowSystem.AddWindow(this.MainWindow);
            this.ConfigWindow = new ConfigWindow(this, this.Configuration);
            this.WindowSystem.AddWindow(this.ConfigWindow);

            var assemblyLocation = Assembly.GetExecutingAssembly().Location;

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
            /*try
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
            }*/

            Framework.Update += UpdateAddonPtrs;
            this.bait_window_addon_ptr = IntPtr.Zero;
            this.ocean_fishing_addon_ptr = IntPtr.Zero;

            this.PluginInterface.UiBuilder.Draw += DrawUI;
            this.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            LocationSheet = this.DataManager.GetExcelSheet<IKDSpot>();

            this.UserLanguage = DataManager.Language;
            switch (this.UserLanguage)
            {
                case Dalamud.ClientLanguage.English:
                    CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en");
                    break;
                case Dalamud.ClientLanguage.French:
                    CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("fr");
                    break;
                case Dalamud.ClientLanguage.German:
                    CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("de");
                    break;
                case Dalamud.ClientLanguage.Japanese:
                    CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("jp");
                    break;
                default:
                    CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en");
                    break;
            }
            PluginLog.Debug("Client langauge is " + this.UserLanguage.ToString() + " and default thread UI culture is " + CultureInfo.DefaultThreadCurrentUICulture.ToString());

            BuildLocationStringMap();
            PluginLog.Debug("Location string map filled a total of " + localizedLocationStrings.Count + "/7 entries.");
        }

        public unsafe void Dispose()
        {
            Framework.Update -= UpdateAddonPtrs;
            this.MainWindow.Dispose();
            this.CommandManager.RemoveHandler(commandName);
            this.CommandManager.RemoveHandler(altCommandName1);
            this.CommandManager.RemoveHandler(altCommandName2);
            WindowSystem.RemoveAllWindows();
            /*if (this.last_highlighted_bait_node != null)
            {
                change_node_border(this.last_highlighted_bait_node, false);
            }*/
        }
        private void OnCommand(string command, string args)
        {
            MainWindow.IsOpen = true;
        }

        private void DrawConfigUI()
        {
            this.ConfigWindow.IsOpen = true;
        }

        private void DrawUI()
        {
            WindowSystem.Draw();
        }

        // Since you have to be a fisher to get into the Duty, checking player job is probably unnecessary. 
        public bool InOceanFishingDuty()
        {
            if (this.Configuration.DebugMode || (int)ClientState.TerritoryType == endevor_territory_type)
                return true;
            else
                return false;
        }

        public unsafe Location GetFishingLocation()
        {
            if (this.Configuration.DebugMode){ return this.Configuration.DebugLocation; }

            AtkUnitBase* ptr = (AtkUnitBase*)this.ocean_fishing_addon_ptr;
            if (ptr == null || ptr->UldManager.NodeListCount < expected_nodelist_count)
                return Location.Unknown;

            AtkResNode* res_node = ptr->UldManager.NodeList[location_textnode_index];
            AtkTextNode* text_node = (AtkTextNode*)res_node;
            string? locationString = Marshal.PtrToStringUTF8(new IntPtr(text_node->NodeText.StringPtr));

            #pragma warning disable CS8604 // Possible null reference argument.
            return SpotStringToLocation(locationString);
        }

        public unsafe Time GetFishingTime()
        {
            if (this.Configuration.DebugMode)
            {
                return this.Configuration.DebugTime;
            }

            AtkUnitBase* ptr = (AtkUnitBase*)this.ocean_fishing_addon_ptr;
            if (ptr == null || ptr->UldManager.NodeListCount < expected_nodelist_count)
                return Time.Unknown;
            AtkResNode* res_node = ptr->UldManager.NodeList[day_imagenode_index];
            AtkImageNode* image_node = (AtkImageNode*)res_node;
            
            if (image_node->PartId == day_icon_lit)
                return Time.Day;
            res_node = ptr->UldManager.NodeList[sunset_imagenode_index];
            image_node = (AtkImageNode*)res_node;
            if (image_node->PartId == sunset_icon_lit)
                return Time.Sunset;
            res_node = ptr->UldManager.NodeList[night_imagenode_index];
            image_node = (AtkImageNode*)res_node;
            if (image_node->PartId == night_icon_lit)
                return Time.Night;
            return Time.Unknown;
        }

        // When the spectral current occurs, this AtkResNode becomes visible behind the IKDFishingLog window.
        public unsafe bool IsSpectralCurrent()
        {
            if (this.Configuration.DebugSpectral) return true;
            
            
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
        public unsafe bool HasIntuitionBuff()
        {
            if (this.Configuration.DebugIntution) return true;
            
            Dalamud.Game.ClientState.Statuses.StatusList? buff_list;
            PlayerCharacter? player_character = ClientState.LocalPlayer;
            
            if(player_character != null && player_character.StatusList != null)
            {
                buff_list = player_character.StatusList;
                for (int i = 0; i < buff_list.Length; i++)
                {

                    #pragma warning disable CS8602 // Dereference of a possibly null reference.
                    if (this.Configuration.DebugMode && buff_list[i].StatusId != 0) PluginLog.Debug("Status id " + i + " : " + buff_list[i].StatusId);

                    if (buff_list[i].StatusId == intuition_buff_id)
                    {
                        if (this.Configuration.DebugMode) PluginLog.Debug("Intuition was detected!");
                        return true;
                    }
                }
                return false;
            }
            return false;
        }

        private unsafe void UpdateAddonPtrs(Framework framework)
        {
            if (!InOceanFishingDuty())
            {
                this.bait_window_addon_ptr = IntPtr.Zero;
                this.ocean_fishing_addon_ptr = IntPtr.Zero;
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

        public Bait GetSpectralChanceBait(Location location)
        {
            //if(location == Location.GaladionBay && getWeather() == showers) { return Bait.PlumpWorm; }
            try { return SpectralChanceBaitDictionary[location]; }
            catch (KeyNotFoundException e) { return Bait.None; }
        }

        public Bait GetFishersIntuitionBait(Location location,  Time time)
        {
            try { return FishersIntutionBaitDictionary[location]; }
            catch (KeyNotFoundException e) { return Bait.None; }
        }

        public Bait GetSpectralIntuitionBait(Location location, Time time)
        {
            try 
            {
                if (SpectralIntuitionBaitDictionary.ContainsKey((location, time))) { return SpectralIntuitionBaitDictionary[(location, time)]; }
                else { return Bait.None; }
            }
            catch (KeyNotFoundException e) { return Bait.None; }
        }

        public Dictionary<FishTypes, Bait> GetMissionFishBaits(Location location)
        {
            return MissionFishBaitDictionary[location];
        }

        public Dictionary<FishTypes, Bait>? GetSpectralMissionFishBaits(Location location, Time time)
        {
            if (location == Location.GaladionBay || location == Location.Cieldales) { return GetMissionFishBaits(location); }
            else if (SpectralFishBaitDictionary.ContainsKey((location, time))){ return SpectralFishBaitDictionary[(location, time)]; }
            else{ return null; }
        }

        public Bait GetSpectralHighPointsBait(Location location, Time time)
        {
            try { return SpectralHighPointsBaitDictionary[location][time]; }
            catch (KeyNotFoundException e) { return Bait.None; }
        }

        //TODO this will also be used to determine what to highlight in the tacklebox
        public Bait GetSingleBestBait(Location location, Time time)
        {
            if (IsSpectralCurrent())
            {
                if (HasIntuitionBuff() && GetSpectralIntuitionBait(location, time) != Bait.None) return GetSpectralIntuitionBait(location, time);
                else return GetSpectralHighPointsBait(location, time);
            }
            if (HasIntuitionBuff()) return GetFishersIntuitionBait(location, time);
            //if (weather == clear) return getMissionFishBait(location, time); //TODO implement weather checking
            return GetSpectralChanceBait(location);
        }

        private OceanFishin.Location SpotStringToLocation(string location)
        {
            if(localizedLocationStrings.ContainsKey(location)) {  return localizedLocationStrings[location]; }
            else { return Location.Unknown; }
        }

        private void BuildLocationStringMap()
        {
            for (uint i = 1; i < LocationSheet.RowCount; ++i)
            {
                localizedLocationStrings[LocationSheet.GetRow(i).PlaceName.Value.Name.ToString()] = (Location)i;
            }
        }

        /*public unsafe void highlight_inventory_item(string bait)
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
        }*/

        /* Problem: Searching by icon doesn't work because multiple baits use the same icon.
         * Ideas:
         * Search from the back: ocean baits are always at the end of the list, but will have the same problemfor special intuition baits.
         * The bait list is based on ItemID numbers, not inventory order or alphabetically. Figure out how the bait list is populated, but that's a lot of work.
         * Find some sort of static, unique identifier in each node. There's plenty of different pointers in each node, but nothing yet found that's static.
         * Skip the first search result and use the second. Won't work if the player only has one of the other bait.
         * The bait has to know it's name somehow when it is hovered over for the tooltip, so figure out how the tooltip is being generated without having to hover.
         */
        /*public unsafe AtkComponentNode* find_bait_item_node(string bait)
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
        }*/


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


        /*public unsafe  void change_node_border(AtkComponentNode* node, bool higlight)
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

        public unsafe string? text_node_to_string(AtkTextNode* text_node)
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
            
        }*/
    }
}
