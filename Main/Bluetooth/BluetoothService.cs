using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static MudBlazor.CategoryTypes;

namespace Main.Bluetooth;

public class BluetoothService : IBluetoothService, IDisposable
{
    #region Member Variables
    private readonly ILogger<BluetoothService> _logger;

    // Standard UUID format for 16-bit UUIDs
    private const string UUID_BASE = "0000{0}-0000-1000-8000-00805f9b34fb";

    // ESP32 HydroMatrix BLE UUIDs (from firmware config)
    public static readonly Guid ServiceUuid = Guid.Parse(string.Format(UUID_BASE, "00ff"));
    public static readonly Guid DataCharacteristicUuid = Guid.Parse(string.Format(UUID_BASE, "ff01"));
    public static readonly Guid CommandCharacteristicUuid = Guid.Parse(string.Format(UUID_BASE, "ff02"));

    private IBluetoothLE? _ble;
    private IAdapter? _adapter;
    private IDevice? _connectedDevice;
    private ICharacteristic? _dataCharacteristic;
    private ICharacteristic? _commandCharacteristic;
    private CancellationTokenSource? _scanCts;
    private bool _isDisposed;

    public event Action<BleDeviceInfo>? OnDeviceDiscovered;
    public event Action<SensorDataPacket>? OnSensorDataReceived;
    public event Action<BleConnectionState>? OnConnectionStateChanged;
    public event Action<string>? OnError;

    public bool IsConnected => _connectedDevice?.State == DeviceState.Connected;
    public string? ConnectedDeviceId => _connectedDevice?.Id.ToString();
    public string? ConnectedDeviceName { get; private set; }
    public BleConnectionState ConnectionState { get; private set; } = BleConnectionState.Disconnected;

    public BluetoothService(ILogger<BluetoothService> logger)
    {
        _logger = logger;
    }

    #endregion

    #region Bluetooth Connection

    /// <summary>
    /// Initializes the Bluetooth adapter.
    /// </summary>
    public Task IBluetoothService.InitializeAsync()
    {
        try
        {
            _ble = CrossBluetoothLE.Current;
            _adapter = _ble.Adapter;

            if (_adapter == null)
                throw new BluetoothException("Bluetooth adapter not available");

            _adapter.DeviceDiscovered += OnAdapterDeviceDiscovered;
            _adapter.DeviceConnected += OnAdapterDeviceConnected;
            _adapter.DeviceDisconnected += OnAdapterDeviceDisconnected;

            _logger.LogInformation("Bluetooth adapter initialized, state: {State}", _ble.State);

            // Try to auto-connect to default device
            await TryAutoConnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Bluetooth adapter");
            throw new BluetoothException("Failed to initialize Bluetooth", ex);
        }
    }

    private async Task TryAutoConnectAsync()
    {
        try
        {
            var defaultDevice = await _repository.GetDefaultDeviceAsync();
            if (defaultDevice != null)
            {
                _logger.LogInformation("Attempting auto-connect to {Device}", defaultDevice.DeviceName);
                await this.ConnectAsync(Guid.Parse(defaultDevice.MacAddress));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto-connect failed, will require manual connection");
        }
    }

    /// <summary>
    /// Starts scanning for HydroMatrix ESP32 devices.
    /// </summary>
    public Task IBluetoothService.StartScanAsync(TimeSpan? timeout = null)
    {
        if (_adapter == null)
            throw new BluetoothException("Bluetooth not initialized");

        if (_adapter.IsScanning)
        {
            _logger.LogDebug("Already scanning");
            return;
        }

        _scanCts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(30));

        try
        {
            UpdateConnectionState(BleConnectionState.Scanning);
            _logger.LogInformation("Starting BLE scan for HydroMatrix devices");

            await _adapter.StartScanningForDevicesAsync(
                serviceUuids: new[] { ServiceUuid },
                allowDuplicatesKey: false,
                cancellationToken: _scanCts.Token
            );
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Scan completed or cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan failed");
            OnError?.Invoke($"Scan failed: {ex.Message}");
        }
        finally
        {
            if (ConnectionState == BleConnectionState.Scanning)
                UpdateConnectionState(BleConnectionState.Disconnected);
        }
    }

    /// <summary>
    /// Stops the current BLE scan.
    /// </summary>
    public Task IBluetoothService.StopScanAsync()
    {
        _scanCts?.Cancel();

        if (_adapter?.IsScanning == true)
            await _adapter.StopScanningForDevicesAsync();
    }

    /// <summary>
    /// Connects to an ESP32 device by its ID.
    /// </summary>
    public Task IBluetoothService.ConnectAsync(
        Guid deviceId, 
        CancellationToken cancellationToken = default)
    {
        if (_adapter == null)
            throw new BluetoothException("Bluetooth not initialized");

        try
        {
            UpdateConnectionState(BleConnectionState.Connecting);

            _connectedDevice = await _adapter.ConnectToKnownDeviceAsync(
                deviceId,
                new ConnectParameters(autoConnect: false, forceBleTransport: true),
                cancellationToken
            );

            ConnectedDeviceName = _connectedDevice.Name ?? "HydroMatrix Controller";

            await SetupCharacteristicsAsync();
            await StartNotificationsAsync();

            UpdateConnectionState(BleConnectionState.Connected);
            _logger.LogInformation("Connected to {Device}", ConnectedDeviceName);
        }
        catch (Exception ex)
        {
            UpdateConnectionState(BleConnectionState.Disconnected);
            _logger.LogError(ex, "Failed to connect to device {DeviceId}", deviceId);
            throw new BluetoothException($"Connection failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Disconnects from the current device.
    /// </summary>
    public Task IBluetoothService.DisconnectAsync(
        CommandType commandType, 
        object? parameters = null)
    {
        if (_connectedDevice == null) return;

        try
        {
            await StopNotificationsAsync();
            await _adapter!.DisconnectDeviceAsync(_connectedDevice);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disconnect");
        }
        finally
        {
            _connectedDevice = null;
            _dataCharacteristic = null;
            _commandCharacteristic = null;
            ConnectedDeviceName = null;
            UpdateConnectionState(BleConnectionState.Disconnected);
        }
    }

    #endregion

    #region Sending Commands

    /// <summary>
    /// Sends a command to the connected ESP32.
    /// </summary>
    public Task<bool> SendCommandAsync(CommandType commandType, object? parameters = null)
    {
        if (!IsConnected || _commandCharacteristic == null)
        {
            _logger.LogWarning("Cannot send command: not connected");
            return false;
        }

        

        try
        {
            var command = new
            {
                cmd = commandType.ToString().ToLowerInvariant(),
                @params = parameters
            };

            var json = JsonSerializer.Serialize(command);
            var bytes = Encoding.UTF8.GetBytes(json);

            await _commandCharacteristic.WriteAsync(bytes);

            _logger.LogInformation("Command sent: {Type}", commandType);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send command: {Type}", commandType);
            OnError?.Invoke($"Command failed: {ex.Message}");
            return false;
        }
    }

    public Task<bool> IBluetoothService.SetWaterPumpAsync(bool on) =>
        SendCommandAsync(CommandType.SetWaterPump, new { state = on });
    
    public Task<bool> IBluetoothService.SetAirPumpAsync(bool on) =>
        SendCommandAsync(CommandType.SetAirPump, new { state = on });

    public async Task<bool> IBluetoothService.DoseNutrientsAsync(int durationMs)
    {
        var currentTds = await _repository.GetLatestReadingAsync(ConnectedDeviceId!, SensorType.Tds);

        var success = await SendCommandAsync(CommandType.DoseNutrients, new { duration_ms = durationMs });

        if (success)
        {
            await _repository.SaveDosingEventAsync(new DosingEvent
            {
                DeviceId = ConnectedDeviceId!,
                DosingType = DosingType.NutrientMix,
                DurationMs = durationMs,
                VolumeEstimateMl = EstimateVolume(durationMs),
                SensorValueBefore = currentTds?.Value,
                WasAutomatic = false
            });
        }

        return success;
    }

    public Task<bool> IBluetoothService.RequestStatusAsync() =>
        SendCommandAsync(CommandType.GetStatus);

    // Estimate volume based on peristaltic pump flow rate (100 mL/min from spec)
    private static double EstimateVolume(int durationMs) => (durationMs / 60000.0) * 100.0;

    #endregion

    #region Private Methods

    private async Task SetupCharacteristicsAsync()
    {
        if (_connectedDevice == null) return;

        var service = await _connectedDevice.GetServiceAsync(ServiceUuid);
        if (service == null)
            throw new BluetoothException("HydroMatrix service not found on device");

        _dataCharacteristic = await service.GetCharacteristicAsync(DataCharacteristicUuid);
        _commandCharacteristic = await service.GetCharacteristicAsync(CommandCharacteristicUuid);

        if (_dataCharacteristic == null)
            throw new BluetoothException("Data characteristic not found");

        _logger.LogDebug("BLE characteristics configured");
    }

    private async Task StartNotificationsAsync()
    {
        if (_dataCharacteristic == null) return;

        _dataCharacteristic.ValueUpdated += OnCharacteristicValueUpdated;
        await _dataCharacteristic.StartUpdatesAsync();

        _logger.LogDebug("Subscribed to sensor data notifications");
    }

    private async Task StopNotificationsAsync()
    {
        if (_dataCharacteristic == null) return;

        try
        {
            _dataCharacteristic.ValueUpdated -= OnCharacteristicValueUpdated;
            await _dataCharacteristic.StopUpdatesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping notifications");
        }
    }

    private void OnCharacteristicValueUpdated(object? sender, CharacteristicUpdatedEventArgs args)
    {
        try
        {
            var data = args.Characteristic.Value;
            var packet = ParseSensorData(data);

            if (packet != null)
            {
                OnSensorDataReceived?.Invoke(packet);
                _ = SaveSensorDataAsync(packet);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process sensor data");
        }
    }

    /// <summary>
    /// Parses raw BLE data into sensor readings.
    /// Format: "T:21.5,H:62.0,PH:6.2,TDS:980,LVL:15.2,LUX:12500,DO:7.8,AT:24.3"
    /// </summary>
    private SensorDataPacket? ParseSensorData(byte[] data)
    {
        try
        {
            var dataString = Encoding.UTF8.GetString(data);
            _logger.LogDebug("Received BLE data: {Data}", dataString);

            var packet = new SensorDataPacket
            {
                DeviceId = ConnectedDeviceId ?? "unknown",
                Timestamp = DateTime.UtcNow,
                RawData = dataString
            };

            var parts = dataString.Split(',');
            foreach (var part in parts)
            {
                var kv = part.Split(':');
                if (kv.Length != 2) continue;

                var key = kv[0].Trim().ToUpperInvariant();
                if (!double.TryParse(kv[1].Trim(), out var value)) continue;

                switch (key)
                {
                    case "T": case "WT": packet.WaterTemperature = value; break;
                    case "AT": packet.AirTemperature = value; break;
                    case "H": packet.Humidity = value; break;
                    case "PH": packet.Ph = value; break;
                    case "TDS": packet.Tds = value; break;
                    case "EC": packet.Ec = value; break;
                    case "LVL": packet.WaterLevel = value; break;
                    case "LUX": packet.LightIntensity = value; break;
                    case "DO": packet.DissolvedOxygen = value; break;
                }
            }

            return packet;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse sensor data");
            return null;
        }
    }

    private async Task SaveSensorDataAsync(SensorDataPacket packet)
    {
        var readings = new List<SensorReading>();
        var deviceId = packet.DeviceId;
        var timestamp = packet.Timestamp;

        void AddReading(SensorType type, double? value)
        {
            if (value.HasValue)
            {
                readings.Add(new SensorReading
                {
                    DeviceId = deviceId,
                    SensorType = type,
                    Value = value.Value,
                    Timestamp = timestamp
                });
            }
        }

        AddReading(SensorType.Ph, packet.Ph);
        AddReading(SensorType.WaterTemperature, packet.WaterTemperature);
        AddReading(SensorType.AirTemperature, packet.AirTemperature);
        AddReading(SensorType.Humidity, packet.Humidity);
        AddReading(SensorType.Tds, packet.Tds);
        AddReading(SensorType.Ec, packet.Ec);
        AddReading(SensorType.WaterLevel, packet.WaterLevel);
        AddReading(SensorType.LightIntensity, packet.LightIntensity);
        AddReading(SensorType.DissolvedOxygen, packet.DissolvedOxygen);

        if (readings.Count > 0)
        {
            await _repository.SaveReadingsAsync(readings);
            await CheckAlertsAsync(readings);
        }
    }

    private async Task CheckAlertsAsync(IEnumerable<SensorReading> readings)
    {
        // Load thresholds from settings
        var phMin = await _repository.GetSettingAsync("PhMin", 5.5);
        var phMax = await _repository.GetSettingAsync("PhMax", 6.5);
        var tdsMin = await _repository.GetSettingAsync("TdsMin", 800.0);
        var tdsMax = await _repository.GetSettingAsync("TdsMax", 1200.0);
        var waterTempMin = await _repository.GetSettingAsync("WaterTempMin", 18.0);
        var waterTempMax = await _repository.GetSettingAsync("WaterTempMax", 24.0);
        var waterLevelMin = await _repository.GetSettingAsync("WaterLevelMin", 10.0);

        foreach (var reading in readings)
        {
            (double min, double max, string message)? check = reading.SensorType switch
            {
                SensorType.Ph => (phMin, phMax, "pH out of range"),
                SensorType.Tds => (tdsMin, tdsMax, "TDS out of range"),
                SensorType.WaterTemperature => (waterTempMin, waterTempMax, "Water temperature out of range"),
                SensorType.WaterLevel => (waterLevelMin, 999, "Water level low"),
                _ => null
            };

            if (check.HasValue && (reading.Value < check.Value.min || reading.Value > check.Value.max))
            {
                await _repository.SaveAlertAsync(new Alert
                {
                    DeviceId = reading.DeviceId,
                    SensorType = reading.SensorType,
                    Value = reading.Value,
                    ThresholdMin = check.Value.min,
                    ThresholdMax = check.Value.max,
                    Message = $"{check.Value.message}: {reading.Value:F2}",
                    Severity = AlertSeverity.Warning
                });
            }
        }
    }

    private void OnAdapterDeviceDiscovered(object? sender, DeviceEventArgs args)
    {
        var device = args.Device;
        var deviceInfo = new BleDeviceInfo
        {
            Id = device.Id,
            Name = device.Name ?? "HydroMatrix Controller",
            Rssi = device.Rssi,
            IsConnectable = device.IsConnectable
        };

        _logger.LogDebug("Discovered: {Name} ({Id}) RSSI: {Rssi}", deviceInfo.Name, deviceInfo.Id, deviceInfo.Rssi);
        OnDeviceDiscovered?.Invoke(deviceInfo);
    }

    private void OnAdapterDeviceConnected(object? sender, DeviceEventArgs args)
    {
        _logger.LogInformation("Device connected event: {Name}", args.Device.Name);
    }

    private void OnAdapterDeviceDisconnected(object? sender, DeviceEventArgs args)
    {
        _logger.LogInformation("Device disconnected: {Name}", args.Device.Name);

        _connectedDevice = null;
        _dataCharacteristic = null;
        _commandCharacteristic = null;
        ConnectedDeviceName = null;

        UpdateConnectionState(BleConnectionState.Disconnected);
    }

    private void UpdateConnectionState(BleConnectionState state)
    {
        ConnectionState = state;
        OnConnectionStateChanged?.Invoke(state);
    }

    #endregion

    public void Dispose()
    {
        if (_isDisposed) return;

        _scanCts?.Cancel();
        _scanCts?.Dispose();

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}

public enum BleConnectionState
{
    Disconnected,
    Scanning,
    Connecting,
    Connected
}

public record BleDeviceInfo 
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public int Rssi { get; set; }
    public bool IsConnectable { get; set; }
}

public record SensorDataPacket
{
    public string DeviceId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public string RawData { get; init; } = string.Empty;

    public double? Ph { get; set; }
    public double? WaterTemperature { get; set; }
    public double? AirTemperature { get; set; }
    public double? Humidity { get; set; }
    public double? Tds { get; set; }
    public double? Ec { get; set; }
    public double? WaterLevel { get; set; }
    public double? LightIntensity { get; set; }
    public double? DissolvedOxygen { get; set; }
}

public class BluetoothException : Exception
{
    public BluetoothException(string message) : base(message) { }
    public BluetoothException(string message, Exception inner) : base(message, inner) { }
}