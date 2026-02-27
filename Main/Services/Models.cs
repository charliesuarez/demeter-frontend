using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Main.Services
{
    public record SensorDataResponse
    {
        public string DeviceId { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; }
        public SensorReading Readings { get; init; } = new();
        public string Status {  get; init; } = string.Empty;
    }

    public record SensorReading
    {
        public double Ph { get; init; }
        public double WaterTemp { get; init; }
        public double AirTemp { get; init; }
        public double Humidity { get; init; }
        public double Tds { get; init; }
        public double Ec { get; init; }
        public double WaterLevel { get; init; }
        public double LightIntensity { get; init; }
        public double DissolvedOxygen { get; init; }
    }

    public record AggregatedReading
    {
        public string sensorId {  get; init; } = string.Empty;
        public DateTime bucketTime { get; init; }
        public double AvgValue { get; init; }
        public double MinValue { get; init; }
        public double MaxValue { get; init; }
        public int ReadingCount { get; init; }
    }

    public record SensorHealthStatus
    {
        public string sensorId { get; init; } = string.Empty;
        public string SensorType { get; init; } = string.Empty;
        public string status { get; init; } = string.Empty;
        public DateTime? lastReading { get; init; }
        public double? lastValue { get; init; }
        public TimeSpan? timeSinceLastReading { get; init; }
        public bool isOnline { get; init; }
        public string? errorMessage { get; init; }
    }

    public record DeviceInfo
    {
        public string id { get; init; } = string.Empty;
        public string name { get; init; } = string.Empty;
        public string type { get; init; } = string.Empty;
        public string? locationId { get; init; } = string.Empty;
        public string? locationName { get; init; } = string.Empty;
        public string unit { get; init; } = string.Empty;
        public double? minValue { get; init; }
        public double? maxValue { get; init; }
        public double? calibrationOffset { get; init; }
        public DateTime? lastCalibration { get; init; }
        public bool isActive { get; init; }
        public DateTime createdAt { get; init; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AggregationInterval
    {
        Minute,
        Hour,
        Day,
        Week,
        Month
    }
}
