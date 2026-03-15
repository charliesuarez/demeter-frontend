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
}