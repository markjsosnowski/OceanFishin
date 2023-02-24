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

        public const string CommandName = "/oceanfishing";
        public const string AltCommandName = "/bait";

        private DalamudPluginInterface PluginInterface { get; init; }
        private CommandManager CommandManager { get; init; }
        private Configuration Configuration { get; init; }
        private MainWindow MainWindow { get; init; }
        private ClientState ClientState { get; init; }
        private Framework Framework { get; init; }
        private GameGui GameGui { get; init; }
        private ConfigWindow ConfigWindow { get; init; }
        private WindowSystem WindowSystem = new("Ocean Fishin'");
        public DataManager DataManager { get; init; }

        // This is the TerritoryType for the entire instance and does not
        // provide any information on fishing spots, routes, etc.
        private const int endevor_territory_type = 900;

        // NodeList indexes, known via addon inspector.
        private const int LocationTextNodeIndex = 20;
        private const int NightImageNodeIndex = 22;
        private const int SunsetImageNodeIndex = 23;
        private const int DayImageNodeIndex = 24;
        private const int ExpectedFishingLogNodeListCount = 24;
        private const int CruisingResNodeIndex = 2;
        private const int ExpectedBaitNodeListCount = 14;
        private const int BaitListComponentNodeIndex = 3;
        private const int IconIDIndex = 2;
        private const int ItemBorderImageNodeIndex = 4;

        private const int intuitionStatusID = 568;

        // Inventory icon texture part ids
        private const int glowingBorderPartID = 5;
        private const int defaultBorderPartID = 0;
        private const string fishingLogAddonName = "IKDFishingLog";
        private const string baitAddonName = "Bait";

        // Cached Values
        private IntPtr fishingLogAddonPtr;
        private IntPtr baitWindowAddonPtr;
        //private string last_highlighted_bait = "";
        //private string last_location_string = "";
        //private unsafe AtkComponentNode* last_highlighted_bait_node = null;

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

        // Three image nodes make up the time of day indicator.
        // They all use the same texture, so the part_id determines
        // which part of the texture is used. Those part_ids are:
        // Day      Active = 9  Inactive = 4
        // Sunset   Active = 10 Inactive = 5
        // Night    Active = 11 Inactive = 6
        public enum Time
        {
            Unknown = 0,
            Day = 9,
            Sunset = 10,
            Night = 11
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

        /*private Dictionary<Bait, Int64> baitIconID = new Dictionary<Bait, Int64>()
        {
            {Bait.None, 0},
            {Bait.Krill,  27023},
            {Bait.PlumpWorm, 27015},
            {Bait.Ragworm, 27004}
        };*/
       
        //Bait Dictionaries

        private Dictionary<Location, Bait> spectralChanceBaitDictionary = new Dictionary<Location, Bait>
        {
            [Location.BloodbrineSea] = Bait.Krill,
            [Location.Cieldales] = Bait.Ragworm,
            [Location.GaladionBay] = Bait.Krill,
            [Location.NorthernStrait] = Bait.Ragworm,
            [Location.RhotanoSea] = Bait.PlumpWorm,
            [Location.RothlytSound] = Bait.PlumpWorm,
            [Location.SouthernStraight] = Bait.Krill
        };

        private Dictionary<Location, Bait> fishersIntutionBaitDictionary = new Dictionary<Location, Bait>
        {
            [Location.BloodbrineSea] = Bait.Krill,
            [Location.Cieldales] = Bait.Krill,
            [Location.GaladionBay] = Bait.Krill,
            [Location.NorthernStrait] = Bait.Ragworm,
            [Location.RhotanoSea] = Bait.Krill,
            [Location.RothlytSound] = Bait.Ragworm,
            [Location.SouthernStraight] = Bait.PlumpWorm
        };

        private Dictionary<(Location,Time), Bait> spectralIntuitionBaitDictionary = new Dictionary<(Location, Time), Bait>
        {
            [(Location.BloodbrineSea, Time.Day)] =  Bait.PillBug,
            [(Location.Cieldales, Time.Night)] =  Bait.SquidStrip,
            [(Location.GaladionBay, Time.Night)] = Bait.Glowworm,
            [(Location.NorthernStrait, Time.Day)] = Bait.HeavySteelJig,
            [(Location.RhotanoSea, Time.Sunset)] = Bait.RatTail,
            [(Location.RothlytSound, Time.Sunset)] = Bait.Ragworm,
            [(Location.SouthernStraight, Time.Night)] = Bait.ShrimpCageFeeder
        };

        private Dictionary<Location, Dictionary<Time, Bait>> spectralHighPointsBaitDictionary = new Dictionary<Location, Dictionary<Time, Bait>>
        {
            [Location.BloodbrineSea] = new Dictionary<Time, Bait>{ [Time.Day] = Bait.Ragworm, [Time.Sunset] = Bait.PlumpWorm, [Time.Night] = Bait.Krill },
            [Location.Cieldales] = new Dictionary<Time, Bait>{ [Time.Day] = Bait.Krill, [Time.Sunset] = Bait.PlumpWorm, [Time.Night] = Bait.Krill },
            [Location.GaladionBay] = new Dictionary<Time, Bait> { [Time.Day] = Bait.Ragworm, [Time.Sunset]= Bait.PlumpWorm, [Time.Night]=Bait.Krill },
            [Location.NorthernStrait] = new Dictionary<Time, Bait> { [Time.Day] = Bait.PlumpWorm, [Time.Sunset] = Bait.Krill, [Time.Night] = Bait.Krill },
            [Location.RhotanoSea] = new Dictionary<Time, Bait> { [Time.Day] = Bait.PlumpWorm, [Time.Sunset] = Bait.Ragworm, [Time.Night] = Bait.Krill },
            [Location.RothlytSound] = new Dictionary<Time, Bait> { [Time.Day] = Bait.Krill, [Time.Sunset] = Bait.Krill, [Time.Night] = Bait.Krill },
            [Location.SouthernStraight] = new Dictionary<Time, Bait> { [Time.Day] = Bait.Krill, [Time.Sunset] = Bait.Ragworm, [Time.Night] = Bait.Ragworm }
        };

        private Dictionary<Location, Dictionary<FishTypes, Bait>> missionFishBaitDictionary = new Dictionary<Location, Dictionary<FishTypes, Bait>>
        {
            [Location.BloodbrineSea] = new Dictionary<FishTypes, Bait> { [FishTypes.Crabs] = Bait.Ragworm },
            [Location.Cieldales] = new Dictionary<FishTypes, Bait> { [FishTypes.Balloons] = Bait.Ragworm, [FishTypes.Crabs] = Bait.Krill, [FishTypes.Mantas] = Bait.PlumpWorm },
            [Location.GaladionBay] = new Dictionary<FishTypes, Bait> { [FishTypes.Octopodes] = Bait.Krill , [FishTypes.Sharks] = Bait.PlumpWorm },
            [Location.NorthernStrait] = new Dictionary<FishTypes, Bait> { [FishTypes.Crabs] = Bait.Krill, [FishTypes.Balloons] = Bait.Krill},
            [Location.RhotanoSea] = new Dictionary<FishTypes, Bait> { [FishTypes.Sharks] = Bait.PlumpWorm, [FishTypes.Balloons] = Bait.Ragworm},
            [Location.RothlytSound] = new Dictionary<FishTypes, Bait> { [FishTypes.Balloons] = Bait.Ragworm, [FishTypes.Jellyfish] = Bait.Krill, [FishTypes.Sharks] = Bait.Krill},
            [Location.SouthernStraight] = new Dictionary<FishTypes, Bait> { [FishTypes.Jellyfish] = Bait.Ragworm, [FishTypes.Dragons] = Bait.Ragworm , [FishTypes.Balloons] = Bait.Krill }
        };

        private Dictionary<(Location, Time), Dictionary<FishTypes, Bait>> spectralMissionFishBaitDictionary = new Dictionary<(Location, Time), Dictionary<FishTypes, Bait>>
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
                    CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("ja");
                    break;
                default:
                    CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en");
                    break;
            }
            PluginLog.Debug("Client langauge is " + this.UserLanguage.ToString() + " and default thread UI culture is " + CultureInfo.DefaultThreadCurrentUICulture.ToString());

            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);
            
            this.MainWindow = new MainWindow(this, this.Configuration);
            this.WindowSystem.AddWindow(this.MainWindow);
            
            this.ConfigWindow = new ConfigWindow(this, this.Configuration);
            this.WindowSystem.AddWindow(this.ConfigWindow);

            var assemblyLocation = Assembly.GetExecutingAssembly().Location;

            this.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand){HelpMessage = Properties.Strings.Displays_bait_suggestions});

            this.CommandManager.AddHandler(AltCommandName, new CommandInfo(OnCommand){HelpMessage = Properties.Strings.Alternate_command});

            Framework.Update += UpdateAddonPtrs;
            this.baitWindowAddonPtr = IntPtr.Zero;
            this.fishingLogAddonPtr = IntPtr.Zero;

            this.PluginInterface.UiBuilder.Draw += DrawUI;
            this.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            LocationSheet = this.DataManager.GetExcelSheet<IKDSpot>();
            BuildLocationStringMap();
            PluginLog.Debug("Location string map filled a total of " + localizedLocationStrings.Count + "/7 entries.");
        }

        public unsafe void Dispose()
        {
            Framework.Update -= UpdateAddonPtrs;
            this.MainWindow.Dispose();
            this.CommandManager.RemoveHandler(CommandName);
            this.CommandManager.RemoveHandler(AltCommandName);
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

            AtkUnitBase* ptr = (AtkUnitBase*)this.fishingLogAddonPtr;
            if (ptr == null || ptr->UldManager.NodeListCount < ExpectedFishingLogNodeListCount)
                return Location.Unknown;

            AtkResNode* res_node = ptr->UldManager.NodeList[LocationTextNodeIndex];
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

            AtkUnitBase* ptr = (AtkUnitBase*)this.fishingLogAddonPtr;
            if (ptr == null || ptr->UldManager.NodeListCount < ExpectedFishingLogNodeListCount)
                return Time.Unknown;
            AtkResNode* res_node = ptr->UldManager.NodeList[DayImageNodeIndex];
            AtkImageNode* image_node = (AtkImageNode*)res_node;

            if (image_node->PartId == (ushort)Time.Day)
                return Time.Day;
            
            res_node = ptr->UldManager.NodeList[SunsetImageNodeIndex];
            image_node = (AtkImageNode*)res_node;
            if (image_node->PartId == (ushort)Time.Sunset)
                return Time.Sunset;
            
            res_node = ptr->UldManager.NodeList[NightImageNodeIndex];
            image_node = (AtkImageNode*)res_node;
            if (image_node->PartId == (ushort)Time.Night)
                return Time.Night;
            
            return Time.Unknown;
        }

        // When the spectral current occurs, this AtkResNode becomes visible behind the IKDFishingLog window.
        public unsafe bool IsSpectralCurrent()
        {
            if (this.Configuration.DebugSpectral) return true;
            
            
            AtkUnitBase* addon;
            if (this.fishingLogAddonPtr != IntPtr.Zero)
                 addon = (AtkUnitBase*)this.fishingLogAddonPtr;
            else
                return false;
            if(addon->UldManager.NodeListCount < ExpectedFishingLogNodeListCount)
                return false;
            AtkResNode* crusing_resnode = (AtkResNode*)addon->UldManager.NodeList[CruisingResNodeIndex];
            if (crusing_resnode->IsVisible)
                return true;
            else
                return false;
        }

        public unsafe bool HasIntuitionBuff()
        {
            if (this.Configuration.DebugIntution) return true;
            
            Dalamud.Game.ClientState.Statuses.StatusList? statusList;
            PlayerCharacter? playerCharacter = ClientState.LocalPlayer;
            if (playerCharacter == null || playerCharacter.StatusList == null) { return false; }

            statusList = playerCharacter.StatusList;
            for (int i = 0; i < statusList.Length; i++)
            {
                #pragma warning disable CS8602 // Dereference of a possibly null reference.
                if (this.Configuration.DebugMode && statusList[i].StatusId != 0) PluginLog.Debug("Status id " + i + " : " + statusList[i].StatusId);

                if (statusList[i].StatusId == intuitionStatusID)
                {
                    if (this.Configuration.DebugMode) PluginLog.Debug("Intuition was detected!");
                    return true;
                }
            }
            return false;
        }

        private unsafe void UpdateAddonPtrs(Framework framework)
        {
            if (!InOceanFishingDuty())
            {
                this.baitWindowAddonPtr = IntPtr.Zero;
                this.fishingLogAddonPtr = IntPtr.Zero;
                return;
            }
            try
            {
                // "IKDFishingLog" is the name of the blue window that appears during ocean fishing 
                // that displays location, time, and what you caught. This is known via Addon Inspector.
                this.fishingLogAddonPtr = this.GameGui.GetAddonByName(fishingLogAddonName, 1);
                // "Bait" is the Bait & Tackle window that fishers use to select their bait.
                this.baitWindowAddonPtr = this.GameGui.GetAddonByName(baitAddonName, 1);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                PluginLog.Verbose("Ocean Fishin' caught an exception: " + e);
            }
        }

        public Bait GetSpectralChanceBait(Location location)
        {
            //if(location == Location.GaladionBay && getWeather() == Weather.showers) { return Bait.PlumpWorm; }
            try { return spectralChanceBaitDictionary[location]; }
            catch (KeyNotFoundException e) { return Bait.None; }
        }

        public Bait GetFishersIntuitionBait(Location location,  Time time)
        {
            try { return fishersIntutionBaitDictionary[location]; }
            catch (KeyNotFoundException e) { return Bait.None; }
        }

        public Bait GetSpectralIntuitionBait(Location location, Time time)
        {
            try 
            {
                if (spectralIntuitionBaitDictionary.ContainsKey((location, time))) { return spectralIntuitionBaitDictionary[(location, time)]; }
                else { return Bait.None; }
            }
            catch (KeyNotFoundException e) { return Bait.None; }
        }

        public Dictionary<FishTypes, Bait> GetMissionFishBaits(Location location)
        {
            return missionFishBaitDictionary[location];
        }

        public Dictionary<FishTypes, Bait>? GetSpectralMissionFishBaits(Location location, Time time)
        {
            if (location == Location.GaladionBay || location == Location.Cieldales) { return GetMissionFishBaits(location); } //These just happen to be identical
            else if (spectralMissionFishBaitDictionary.ContainsKey((location, time))){ return spectralMissionFishBaitDictionary[(location, time)]; }
            else{ return null; }
        }

        public Bait GetSpectralHighPointsBait(Location location, Time time)
        {
            try { return spectralHighPointsBaitDictionary[location][time]; }
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
            //if (GetWeather() == Weather.clear) return getMissionFishBait(location, time); //TODO implement weather checking
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



        // Treated the node as an InventoryItem does not work 
        // Treating the node as a ListItemTrenderer doesn't work
        /* public unsafe AtkComponentNode* find_bait_item_node(string bait_name)
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
