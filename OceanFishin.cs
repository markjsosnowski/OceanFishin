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
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Graphics;

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
        private const int endeavorTerritoryType = 900;
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
        private const int glowingBorderPartID = 5;
        private const int defaultBorderPartID = 0;
        private const int fishingBaitItemSortCategory = 12;
        private const int highlightRed = 155;
        private const int highlightGreen = 140;
        private const int highlightBlue = 104;
        private const int baitPageBorderNodeIndex = 1;
        private const int baitPageTextNodeIndex = 2;
        private const string fishingLogAddonName = "IKDFishingLog";
        private const string baitAddonName = "Bait";
       
        private List<InventoryType> inventoryTypes = new List<InventoryType>{ InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4 };
        private List<Item>? inventoryBaitList;
        public ExcelSheet<IKDSpot>? LocationSheet;
        public Lumina.Excel.ExcelSheet<Item>? itemSheet;
        public Dalamud.ClientLanguage UserLanguage;

        // Cached Values
        private IntPtr fishingLogAddonPtr;
        private IntPtr baitWindowAddonPtr;
        private Bait lastHighlightedBait = Bait.None;

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
            None = 0,
            VersatileLure = 29717
        }
      
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
        private int lastPage;

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
            this.itemSheet = DataManager.GetExcelSheet<Item>();
            this.inventoryBaitList = updateBaitInventory();
        }

        public unsafe void Dispose()
        {
            Framework.Update -= UpdateAddonPtrs;
            this.MainWindow.Dispose();
            this.CommandManager.RemoveHandler(CommandName);
            this.CommandManager.RemoveHandler(AltCommandName);
            WindowSystem.RemoveAllWindows();
            stopHightlighting();
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
            if (InOceanFishingDuty() && this.Configuration.HighlightRecommendedBait) {  highlightBaitInTacklebox(GetSingleBestBait(GetFishingLocation(), GetFishingTime())); }
            WindowSystem.Draw();
        }

        private unsafe List<Item> updateBaitInventory()
        {
            InventoryManager* inventoryManager = InventoryManager.Instance();
            inventoryBaitList = new List<Item>();
            foreach(InventoryType inventoryType in inventoryTypes) {
                InventoryContainer* inventoryContainer = inventoryManager->GetInventoryContainer(inventoryType);
                for (int i = 0; i < inventoryContainer->Size; i++)
                {
                    InventoryItem* item = inventoryContainer->GetInventorySlot(i);
                    Item LuminaItem = itemSheet.GetRow(item->ItemID);
                    if (LuminaItem.ItemSortCategory.Row == fishingBaitItemSortCategory) 
                    {
                        inventoryBaitList.Add(itemSheet.GetRow(item->ItemID)); 
                    }
                }
            }
            this.inventoryBaitList = gameLikeSort(ref inventoryBaitList);
            return inventoryBaitList;
        }

        private int getBaitIndex(Bait bait)
        {
            if (inventoryBaitList == null) { PluginLog.Debug("Inventory bait list was null?"); return -1; }
            return inventoryBaitList.IndexOf(itemSheet.GetRow((uint)bait));
        }

        //Inventory is first sorted by Category, but this list is already filtered to only be fishing tackle
        //Then it is sorted by highest LevelItem then then finally by ItemID
        private List<Item> gameLikeSort(ref List<Item> unsortedList)
        {
            return unsortedList
                .OrderByDescending(i => i.LevelItem.Row)
                .ThenBy(i => i.RowId == (uint)Bait.VersatileLure ? i.RowId - 4 : i.RowId)
                .ToList();
        }

        private void printDebugList(List<Item> unsortedList)
        {
            string str ="";
            for (int i = 0; i < unsortedList.Count; i++)
            {
                str += "[" +unsortedList[i].Name +"]";
            }
            PluginLog.Debug(str);
        }

        // Since you have to be a fisher to get into the Duty, checking player job is probably unnecessary. 
        public bool InOceanFishingDuty()
        {
            if (this.Configuration.DebugMode || (int)ClientState.TerritoryType == endeavorTerritoryType)
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
                if (this.Configuration.DebugTime > 0) { return this.Configuration.DebugTime + 8; }
                else { return this.Configuration.DebugTime; }
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
            if (isAddonOpen(this.fishingLogAddonPtr))
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
                //if (this.Configuration.DebugMode && statusList[i].StatusId != 0) PluginLog.Debug("Status id " + i + " : " + statusList[i].StatusId);

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
                this.inventoryBaitList = updateBaitInventory();
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
            catch (KeyNotFoundException e) { PluginLog.Debug("Bait for " + location.ToString() + " at " + time.ToString() + " was not found.");  return Bait.None; }
        }

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
            if(localizedLocationStrings.ContainsKey(location)) { return localizedLocationStrings[location]; }
            else { return Location.Unknown; }
        }

        private void BuildLocationStringMap()
        {
            for (uint i = 1; i < LocationSheet.RowCount; ++i)
            {
                localizedLocationStrings[LocationSheet.GetRow(i).PlaceName.Value.Name.ToString()] = (Location)i;
            }
        }

        public unsafe void highlightBaitInTacklebox(Bait bait)
        {
            if (lastHighlightedBait != Bait.None) 
            {
                AtkResNode* lastBaitNode = findBaitNode(lastHighlightedBait);
                highlightBaitAndPage((AtkComponentNode*)lastBaitNode, lastPage, false);
            }
            if(bait == Bait.None) { return; }
            (int _, int page) = getAdjsutedIndexAndPage(bait);
            AtkResNode*  baitNode = findBaitNode(bait);
            highlightBaitAndPage((AtkComponentNode*)baitNode, page, true);
            lastHighlightedBait = bait;
            lastPage = page;
        }
        
        public unsafe void highlightBaitAndPage(AtkComponentNode* node, int page, bool active)
        {
            highlightBaitPageNumber(page, active);
            if (!active) { changeNodeBorder(node, active); }
            if (active && onBaitPage(page)) {  changeNodeBorder(node, active); }
        }

        public void stopHightlighting()
        {
            highlightBaitInTacklebox(Bait.None);
        }

        private unsafe AtkResNode* findBaitNode(Bait bait)
        {
            if(!isAddonOpen(this.baitWindowAddonPtr)){  return null; }
            AtkUnitBase* baitWindowAddon = (AtkUnitBase*)this.baitWindowAddonPtr;
            if(baitWindowAddon->UldManager.NodeListCount < BaitListComponentNodeIndex) { return null; }
            AtkComponentNode* baitListComponentNode = (AtkComponentNode*)baitWindowAddon->UldManager.NodeList[BaitListComponentNodeIndex];
            (int index, int _) = getAdjsutedIndexAndPage(bait);
            if (index < 0) { return null; }
            return baitListComponentNode->Component->UldManager.NodeList[index + 1];
        }

        private unsafe void changeNodeBorder(AtkComponentNode* node, bool active)
        {
            if(node == null) { return; }
            AtkComponentNode* iconComponentNode = (AtkComponentNode*)node->Component->UldManager.NodeList[IconIDIndex];
            AtkImageNode* frameImageNode = (AtkImageNode*)iconComponentNode->Component->UldManager.NodeList[ItemBorderImageNodeIndex];
            if(active)
                frameImageNode->PartId = glowingBorderPartID;
            else
                frameImageNode->PartId = defaultBorderPartID;
        }

        private unsafe void highlightBaitPageNumber(int page, bool active)
        {
            if (!isAddonOpen(this.baitWindowAddonPtr)) { return; }
            if (page < 1) { return; }
            AtkUnitBase* baitWindowAddon = (AtkUnitBase*)this.baitWindowAddonPtr;
            AtkComponentNode* pageButtonsComponentNode = (AtkComponentNode*)getBaitPageResNode(page); //why is it in reverse order
            if (active)
            {
                pageButtonsComponentNode->AtkResNode.AddRed = highlightRed;
                pageButtonsComponentNode->AtkResNode.AddGreen = highlightGreen;
                pageButtonsComponentNode->AtkResNode.AddBlue = highlightBlue;
                pageButtonsComponentNode->Component->UldManager.NodeList[baitPageBorderNodeIndex]->ToggleVisibility(true);

            }
            else
            {
                pageButtonsComponentNode->AtkResNode.AddRed = 0;
                pageButtonsComponentNode->AtkResNode.AddGreen = 0;
                pageButtonsComponentNode->AtkResNode.AddBlue = 0;
                if(!onBaitPage(page)) { pageButtonsComponentNode->Component->UldManager.NodeList[baitPageBorderNodeIndex]->ToggleVisibility(false); }
            }
        }

        private unsafe bool onBaitPage(int page)
        {
            if (page < 1) { return false; }
            AtkComponentNode* baitPageNode = (AtkComponentNode*)getBaitPageResNode(page);
            AtkTextNode* baitPageTextNode = (AtkTextNode*)baitPageNode->Component->UldManager.NodeList[baitPageTextNodeIndex];
            return compareByteColor(baitPageTextNode->TextColor, 0xFF, 0xFF, 0xFF);
        }

        private unsafe AtkResNode* getBaitPageResNode(int page)
        {
            if (!isAddonOpen(this.baitWindowAddonPtr)) { return null; }
            AtkUnitBase* baitWindowAddon = (AtkUnitBase*)this.baitWindowAddonPtr;
            return baitWindowAddon->UldManager.NodeList[15 - page]; //why is it in reverse order
        }

        private bool compareByteColor(ByteColor byteColor, byte r, byte g, byte b)
        {
            return (byteColor.R == r && byteColor.G == g && byteColor.B == b); 
        }

        // Each bait page is 25 spots
        private (int, int) getAdjsutedIndexAndPage(Bait bait)
        {
            int index = getBaitIndex(bait);
            if (index < 0 ) { return (index, 0); }
            int page = 1;
            while (index > 25)
            {
                index -= 25;
                page++;
            }
            return (index, page);
        }

        public unsafe bool isAddonOpen(IntPtr addon)
        {
            return (addon != IntPtr.Zero);
        }
    }
}
