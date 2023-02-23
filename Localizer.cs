using Dalamud.Configuration;
using Dalamud.Data;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
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
