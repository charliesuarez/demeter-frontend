namespace Main.Bluetooth;

/// <summary>
/// Interface for BLE communication with ESP32.
/// </summary>
public interface IBluetoothService : IDisposable
{
    // State
    bool IsConnected { get; }
    string? ConnectedDeviceName { get; }

    // Events
    event Action<BleDevice>?  DeviceDiscovered;
    event Action<SensorData>? SensorDataReceived;
    event Action<bool>?       ConnectionChanged;
    event Action<string>?     ErrorOccurred;

    // Connection
    Task InitializeAsync();
    Task StartScanAsync(int timeoutSeconds = 10);
    Task StopScanAsync();
    Task ConnectAsync(Guid deviceId);
    Task ConnectByAddressAsync(string macAddress);
    Task DisconnectAsync();

    // Commands
    Task<bool> SetWaterPumpAsync(bool on);
    Task<bool> SetAirPumpAsync(bool on);
    Task<bool> DoseNutrientsAsync(int durationMs);
}

/// <summary>
/// Discovered BLE device info.
/// </summary>
public record BleDevice(Guid Id, string Name, int Rssi);

/// <summary>
/// Sensor data received from ESP32.
/// </summary>
public record SensorData
{
    public double? Ph { get; init; }
    public double? WaterTemp { get; init; }
    public double? AirTemp { get; init; }
    public double? Humidity { get; init; }
    public double? Tds { get; init; }
    public double? WaterLevel { get; init; }
    public double? Light { get; init; }
    public double? DissolvedOxygen { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
}
