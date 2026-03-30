using SQLite;
using System.ComponentModel.DataAnnotations.Schema;
using Table = SQLite.TableAttribute;

namespace Main.Services
{
    [Table("Batches")]
    public class Batch
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    [Table("Crops")]
    public class Crop
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int BatchId { get; set; }
        public int SlotNumber { get; set; }
        public string Type { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    [Table("SensorReadings")]
    public class SensorReading
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int BatchId { get; set; }
        public DateTime Timestamp { get; set; }
        public double Ph { get; set; }
        public double WaterTemp { get; set; }
        public double AirTemp { get; set; }
        public double Humidity { get; set; }
        public double Tds { get; set; }
        public double Ec { get; set; }
        public double WaterLevel { get; set; }
        public double ReservoirLevel { get; set; }
        public double LightIntensity { get; set; }
        public double DissolvedOxygen { get; set; }
    }

    [Table("SystemSettings")]
    public class SystemSettings
    {
        [PrimaryKey]
        public int Id { get; set; } = 1;

        // pH
        public double PhMin { get; set; } = 5.5;
        public double PhMax { get; set; } = 6.5;
        public double PhWarnMin { get; set; } = 5.8;
        public double PhWarnMax { get; set; } = 6.2;

        // Water Temperature (°C)
        public double WaterTempMin { get; set; } = 18;
        public double WaterTempMax { get; set; } = 24;
        public double WaterTempWarnMin { get; set; } = 19;
        public double WaterTempWarnMax { get; set; } = 23;

        // Air Temperature (°C)
        public double AirTempMin { get; set; } = 16;
        public double AirTempMax { get; set; } = 26;
        public double AirTempWarnMin { get; set; } = 17;
        public double AirTempWarnMax { get; set; } = 25;

        // Humidity (%)
        public double HumidityMin { get; set; } = 50;
        public double HumidityMax { get; set; } = 70;
        public double HumidityWarnMin { get; set; } = 55;
        public double HumidityWarnMax { get; set; } = 65;

        // TDS (PPM)
        public double TdsMin { get; set; } = 400;
        public double TdsMax { get; set; } = 800;
        public double TdsWarnMin { get; set; } = 500;
        public double TdsWarnMax { get; set; } = 700;

        // Dissolved Oxygen (mg/L) — lower bound only
        public double DoMin { get; set; } = 6;
        public double DoWarnMin { get; set; } = 7;

        // Water Level (%) — lower bound only
        public double WaterLevelMin { get; set; } = 20;
        public double WaterLevelWarnMin { get; set; } = 30;

        // Light Intensity (lux)
        public double LightMin { get; set; } = 2000;
        public double LightMax { get; set; } = 4000;
        public double LightWarnMin { get; set; } = 2500;
        public double LightWarnMax { get; set; } = 3500;

        // Preset name last applied
        public string LastPreset { get; set; } = "Iceberg Lettuce";
    }
}
