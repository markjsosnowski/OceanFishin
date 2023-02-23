using Dalamud.Configuration;
using Dalamud.Data;
using Dalamud.Logging;
using FFXIVClientStructs.Havok;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static OceanFishin.OceanFishin;

namespace OceanFishin
{
    public class Localizer : IDisposable
    {
        private OceanFishin Plugin;
        private Configuration Configuration;
        private DataManager DataManager;
        private Lumina.Excel.ExcelSheet<Item>? ItemSheet;
        private Lumina.Excel.ExcelSheet<IKDSpot>? LocationSheet;


        private Dictionary<OceanFishin.FishTypes, Dictionary<Dalamud.ClientLanguage, string>> FishTypesSheet = new Dictionary<OceanFishin.FishTypes, Dictionary<Dalamud.ClientLanguage, string>>
        {
            [OceanFishin.FishTypes.Balloons] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "Balloons / Fugu",
                [Dalamud.ClientLanguage.French] = "Ballons / Fugu",
                [Dalamud.ClientLanguage.German] = "Ballons / Fugu",
                [Dalamud.ClientLanguage.Japanese] = "バルーン / フグ"
            },
            [OceanFishin.FishTypes.Crabs] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "Crabs",
                [Dalamud.ClientLanguage.French] = "Crabes",
                [Dalamud.ClientLanguage.German] = "Krabben",
                [Dalamud.ClientLanguage.Japanese] = "カニ"
            },
            [OceanFishin.FishTypes.Dragons] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "Seahorses",
                [Dalamud.ClientLanguage.French] = "Hippocampes",
                [Dalamud.ClientLanguage.German] = "Seepferdchen",
                [Dalamud.ClientLanguage.Japanese] = "タツノオトシゴ"
            },
            [OceanFishin.FishTypes.Jellyfish] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "Jellyfish",
                [Dalamud.ClientLanguage.French] = "Méduse",
                [Dalamud.ClientLanguage.German] = "Mantas",
                [Dalamud.ClientLanguage.Japanese] = "クラゲ"
            },
            [OceanFishin.FishTypes.Mantas] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "Jellyfish",
                [Dalamud.ClientLanguage.French] = "Raies",
                [Dalamud.ClientLanguage.German] = "Quallen",
                [Dalamud.ClientLanguage.Japanese] = "アカエイ"
            },
            [OceanFishin.FishTypes.Octopodes] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "Octopodes",
                [Dalamud.ClientLanguage.French] = "Poulpes",
                [Dalamud.ClientLanguage.German] = "Oktopusse",
                [Dalamud.ClientLanguage.Japanese] = "タコ"
            },
            [OceanFishin.FishTypes.Sharks] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "Sharks",
                [Dalamud.ClientLanguage.French] = "Requins",
                [Dalamud.ClientLanguage.German] = "Haie",
                [Dalamud.ClientLanguage.Japanese] = "サメ"
            }
        };

        private Dictionary<string, Dictionary<Dalamud.ClientLanguage, string>> WindowStrings = new Dictionary<string, Dictionary<Dalamud.ClientLanguage, string>>
        {
            ["min"] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "Minimal",
                [Dalamud.ClientLanguage.French] = "Minimal",
                [Dalamud.ClientLanguage.German] = "Minimal",
                [Dalamud.ClientLanguage.Japanese] = "最小限の"
            },
            ["default"] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "Default",
                [Dalamud.ClientLanguage.French] = "Défaut",
                [Dalamud.ClientLanguage.German] = "Default",
                [Dalamud.ClientLanguage.Japanese] = "デフォルト"
            },
            ["full"] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "Comprehensive",
                [Dalamud.ClientLanguage.French] = "Complet",
                [Dalamud.ClientLanguage.German] = "Umfassend",
                [Dalamud.ClientLanguage.Japanese] = "包括的"
            },
            ["min-desc"] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "Determines the single, best choice for you.",
                [Dalamud.ClientLanguage.French] = "Déterminez le meilleur choix singulier.",
                [Dalamud.ClientLanguage.German] = "Bestimmen Sie die singuläre beste Wahl.",
                [Dalamud.ClientLanguage.Japanese] = "最良の選択のみを決定します。"
            },
            ["default-desc"] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "Suggestions based on current conditions.",
                [Dalamud.ClientLanguage.French] = "Suggestions basées sur l'état actuel.",
                [Dalamud.ClientLanguage.German] = "Vorschlag basierend auf dem aktuellen Stand.",
                [Dalamud.ClientLanguage.Japanese] = "現状を踏まえたご提案。"
            },
            ["full-desc"] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "All possible area information at once.",
                [Dalamud.ClientLanguage.French] = "Toutes les informations possibles sur la région à la fois.",
                [Dalamud.ClientLanguage.German] = "Alle möglichen Gebietsinformationen auf einmal.",
                [Dalamud.ClientLanguage.Japanese] = "一度にすべての可能なエリア情報。"
            },
            ["achievementFish"] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "Include suggestions for mission and achievement fish.",
                [Dalamud.ClientLanguage.French] = "Inclure des suggestions de missions",
                [Dalamud.ClientLanguage.German] = "Missionsempfehlungen einschließen",
                [Dalamud.ClientLanguage.Japanese] = "ミッションの推奨事項を含める。"
            },
            ["countdown"] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "The next boat leaves in ",
                [Dalamud.ClientLanguage.French] = "Le prochain bateau part dans ",
                [Dalamud.ClientLanguage.German] = "Das nächste Boot legt ab ",
                [Dalamud.ClientLanguage.Japanese] = "次の船が出ます "
            },
            ["hours"] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "hours",
                [Dalamud.ClientLanguage.French] = "heures",
                [Dalamud.ClientLanguage.German] = "stunden",
                [Dalamud.ClientLanguage.Japanese] = "時間"
            },
            ["minutes"] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "minutes",
                [Dalamud.ClientLanguage.French] = "minutes",
                [Dalamud.ClientLanguage.German] = "minuten",
                [Dalamud.ClientLanguage.Japanese] = "分"
            },
            ["lessThanOne"] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "less than a minute!",
                [Dalamud.ClientLanguage.French] = "moins d'une minute!",
                [Dalamud.ClientLanguage.German] = "weniger als einer Minute!",
                [Dalamud.ClientLanguage.Japanese] = "一分未満！"
            },
            ["currentShip"] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "Boarding is open for ",
                [Dalamud.ClientLanguage.French] = "L'embarquement est ouvert pendant ",
                [Dalamud.ClientLanguage.German] = "Das Boarding ist für ",
                [Dalamud.ClientLanguage.Japanese] = "乗船時間:"
            },
            ["notOnShip"] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "Suggestions will appear here during Ocean Fishing",
                [Dalamud.ClientLanguage.French] = "Des suggestions apparaîtront ici pendant Pêche en mer",
                [Dalamud.ClientLanguage.German] = "Vorschläge werden hier während Hochseefischen angezeigt.",
                [Dalamud.ClientLanguage.Japanese] = "「オーシャンフィッシング」の提案がここに表示されます"
            },
            ["donateText"] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "Did this plugin help you?",
                [Dalamud.ClientLanguage.French] = "Ce plugin vous a-t-il aidé ?",
                [Dalamud.ClientLanguage.German] = "Hat Ihnen dieses Plugin geholfen?",
                [Dalamud.ClientLanguage.Japanese] = "このプラグインは役に立ちましたか?"
            },
            ["donateButton"] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "Consider Donating",
                [Dalamud.ClientLanguage.French] = "Envisagez de faire un don",
                [Dalamud.ClientLanguage.German] = "Erwägen Sie eine Spende",
                [Dalamud.ClientLanguage.Japanese] = "寄付を検討する"
            },
            ["displayMode"] = new Dictionary<Dalamud.ClientLanguage, string>
            {
                [Dalamud.ClientLanguage.English] = "Display Mode",
                [Dalamud.ClientLanguage.French] = "Mode d'affichage",
                [Dalamud.ClientLanguage.German] = "Anzeigemodus",
                [Dalamud.ClientLanguage.Japanese] = "ディスプレイモード"
            },
        };

        public Localizer(OceanFishin plugin, Configuration configuration) 
        { 
            this.Plugin = plugin;   
            this.Configuration = configuration;
            this.DataManager = plugin.DataManager;
            this.ItemSheet = DataManager.GetExcelSheet<Item>();
            this.LocationSheet = DataManager.GetExcelSheet<IKDSpot>();
        }

        public void Dispose() { }

        private Dalamud.ClientLanguage GetClientLanguage()
        {
            return DataManager.Language;
        }
               
        
        public string Localize(OceanFishin.FishTypes type)
        {
            return FishTypesSheet[type][GetClientLanguage()];
        }
        public string? Localize(OceanFishin.Bait bait)
        {
            #pragma warning disable CS8602 // Dereference of a possibly null reference.
            if (ItemSheet != null) { return ItemSheet.GetRow((uint)bait).Singular.ToString(); }
            return bait.ToString();
        }

        public string Localize(string windowtext)
        {
            try{ return WindowStrings[windowtext][GetClientLanguage()]; }
            catch (KeyNotFoundException e) { return windowtext; }
        }

        public string[] Localize(string[] stringArray)
        {
            string[] ret = new string[stringArray.Length];
            for(int i = 0; i < stringArray.Length; i++)
            {
                ret[i] = Localize(stringArray[i]);
            }
            return ret;
        }

        public OceanFishin.Location SpotStringToLocation(string location)
        {
            if (location == null || LocationSheet == null) { return Location.Unknown; }

            for (uint i = 0; i < LocationSheet.RowCount; i++)
            {
                #pragma warning disable CS8602 // Dereference of a possibly null reference.
                if (location == LocationSheet.GetRow(i).PlaceName.Value.Name.ToString()) { return (Location)i; }
            }

            return Location.Unknown;
        }




    }
}
