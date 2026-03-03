using System.Text;
using System.Text.Json;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace Main.Bluetooth;

/// <summary>
/// BLE service for ESP32 communication.
/// 
/// ESP32 GATT Configuration:
///   Service UUID:  0x00FF
///   Data Char:     0xFF01 (notify - sensor data from ESP32)
///   Command Char:  0xFF02 (write - commands to ESP32)
/// </summary>
public class BluetoothService : IBluetoothService
{
    // 16-bit UUIDs expanded to 128-bit
    private static readonly Guid ServiceUuid     = Guid.Parse("000000ff-0000-1000-8000-00805f9b34fb");
    private static readonly Guid DataCharUuid    = Guid.Parse("0000ff01-0000-1000-8000-00805f9b34fb");
    private static readonly Guid CommandCharUuid = Guid.Parse("0000ff02-0000-1000-8000-00805f9b34fb");

    private readonly IBluetoothLE _ble;
    private IAdapter? _adapter;
    private IDevice? _device;
    private ICharacteristic? _dataChar;
    private ICharacteristic? _commandChar;
    private CancellationTokenSource? _scanCts;
    private bool _disposed;

    public bool IsConnected => _device?.State == DeviceState.Connected;
    public string? ConnectedDeviceName { get; private set; }

    public event Action<BleDevice>? DeviceDiscovered;
    public event Action<SensorData>? SensorDataReceived;
    public event Action<bool>? ConnectionChanged;
    public event Action<string>? ErrorOccurred;

    public BluetoothService()
    {
        _ble = CrossBluetoothLE.Current;
        _adapter = _ble.Adapter;
    }

    public Task InitializeAsync()
    {
        _adapter.DeviceDiscovered   += OnDeviceDiscovered;
        _adapter.DeviceDisconnected += OnDeviceDisconnected;
        _adapter.DeviceConnected    += OnDeviceConnected;
        
        // Log BLE state for debugging
        System.Diagnostics.Debug.WriteLine($"BLE State: {_ble.State}");
        System.Diagnostics.Debug.WriteLine($"BLE IsAvailable: {_ble.IsAvailable}");
        System.Diagnostics.Debug.WriteLine($"BLE IsOn: {_ble.IsOn}");
        
        return Task.CompletedTask;
    }

    private void OnDeviceDiscovered(object? sender, DeviceEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Discovered: {e.Device.Name} ({e.Device.Id})");
        DeviceDiscovered?.Invoke(new BleDevice(e.Device.Id, e.Device.Name ?? "Unknown", e.Device.Rssi));
    }

    private void OnDeviceConnected(object? sender, DeviceEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Connected: {e.Device.Name}");
    }

    private void OnDeviceDisconnected(object? sender, DeviceEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Disconnected: {e.Device.Name}");
        _device = null;
        _dataChar = null;
        _commandChar = null;
        ConnectedDeviceName = null;
        ConnectionChanged?.Invoke(false);
    }

    public async Task StartScanAsync(int timeoutSeconds = 10)
    {
        if (_adapter.IsScanning) return;

        System.Diagnostics.Debug.WriteLine("Starting BLE scan...");
        
        _scanCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            // On Windows, try scanning without service UUID filter first
            // as Windows BLE scanning can be picky
            await _adapter.StartScanningForDevicesAsync(
                cancellationToken: _scanCts.Token);
        }
        catch (OperationCanceledException) 
        {
            System.Diagnostics.Debug.WriteLine("Scan completed/cancelled");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Scan error: {ex.Message}");
            ErrorOccurred?.Invoke($"Scan failed: {ex.Message}");
        }
    }

    public async Task StopScanAsync()
    {
        _scanCts?.Cancel();
        if (_adapter.IsScanning)
            await _adapter.StopScanningForDevicesAsync();
    }

    /// <summary>
    /// Connect to a device discovered via scanning.
    /// </summary>
    public async Task ConnectAsync(Guid deviceId)
    {
        await StopScanAsync();

        System.Diagnostics.Debug.WriteLine($"Connecting to device: {deviceId}");
        
        try
        {
            _device = await _adapter.ConnectToKnownDeviceAsync(deviceId,
                new ConnectParameters(autoConnect: false, forceBleTransport: true));

            await SetupCharacteristicsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Connection failed: {ex.Message}");
            ErrorOccurred?.Invoke($"Connection failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Connect using a MAC address string (useful for Windows where scanning is unreliable).
    /// Format: "AA:BB:CC:DD:EE:FF" or "AA-BB-CC-DD-EE-FF"
    /// </summary>
    public async Task ConnectByAddressAsync(string macAddress)
    {
        await StopScanAsync();

        System.Diagnostics.Debug.WriteLine($"Connecting to MAC: {macAddress}");

        try
        {
            // Convert MAC address to the format Plugin.BLE expects
            var cleanMac = macAddress.Replace(":", "").Replace("-", "").ToUpperInvariant();
            
            // On Windows, the GUID is derived from the MAC address
            // Format: 00000000-0000-0000-0000-XXXXXXXXXXXX where X is the MAC
            var guidString = $"00000000-0000-0000-0000-{cleanMac}";
            var deviceGuid = Guid.Parse(guidString);

            System.Diagnostics.Debug.WriteLine($"Device GUID: {deviceGuid}");

            _device = await _adapter.ConnectToKnownDeviceAsync(deviceGuid,
                new ConnectParameters(autoConnect: false, forceBleTransport: true));

            await SetupCharacteristicsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Connection by address failed: {ex.Message}");
            ErrorOccurred?.Invoke($"Connection failed: {ex.Message}");
            throw;
        }
    }

    private async Task SetupCharacteristicsAsync()
    {
        if (_device == null) throw new InvalidOperationException("No device connected");

        ConnectedDeviceName = _device.Name;
        System.Diagnostics.Debug.WriteLine($"Connected to: {ConnectedDeviceName}");

        // Get services
        var services = await _device.GetServicesAsync();
        System.Diagnostics.Debug.WriteLine($"Found {services.Count} services");
        
        foreach (var svc in services)
        {
            System.Diagnostics.Debug.WriteLine($"  Service: {svc.Id}");
        }

        var service = await _device.GetServiceAsync(ServiceUuid);
        if (service == null)
        {
            ErrorOccurred?.Invoke($"HydroMatrix service not found. Available services: {string.Join(", ", services.Select(s => s.Id))}");
            throw new Exception("HydroMatrix service not found");
        }

        // Get characteristics
        var characteristics = await service.GetCharacteristicsAsync();
        System.Diagnostics.Debug.WriteLine($"Found {characteristics.Count} characteristics");
        
        foreach (var ch in characteristics)
        {
            System.Diagnostics.Debug.WriteLine($"  Characteristic: {ch.Id}, Properties: {ch.Properties}");
        }

        _dataChar = await service.GetCharacteristicAsync(DataCharUuid);
        _commandChar = await service.GetCharacteristicAsync(CommandCharUuid);

        if (_dataChar == null) throw new Exception("Data characteristic not found");
        if (_commandChar == null) throw new Exception("Command characteristic not found");

        // Subscribe to notifications
        _dataChar.ValueUpdated += OnDataReceived;
        await _dataChar.StartUpdatesAsync();

        System.Diagnostics.Debug.WriteLine("Subscribed to sensor data notifications");
        ConnectionChanged?.Invoke(true);
    }

    public async Task DisconnectAsync()
    {
        if (_dataChar != null)
        {
            _dataChar.ValueUpdated -= OnDataReceived;
            try { await _dataChar.StopUpdatesAsync(); } catch { }
        }

        if (_device != null)
        {
            try { await _adapter.DisconnectDeviceAsync(_device); } catch { }
        }

        _device = null;
        _dataChar = null;
        _commandChar = null;
        ConnectedDeviceName = null;
        ConnectionChanged?.Invoke(false);
    }

    public Task<bool> SetWaterPumpAsync(bool on) =>
        SendCommandAsync("setwaterpump", new { state = on });

    public Task<bool> SetAirPumpAsync(bool on) =>
        SendCommandAsync("setairpump", new { state = on });

    public Task<bool> DoseNutrientsAsync(int durationMs) =>
        SendCommandAsync("dosenutrients", new { duration_ms = durationMs });

    private async Task<bool> SendCommandAsync(string cmd, object? parameters)
    {
        if (_commandChar == null || !IsConnected)
        {
            ErrorOccurred?.Invoke("Not connected");
            return false;
        }

        try
        {
            var json = JsonSerializer.Serialize(new { cmd, @params = parameters });
            var bytes = Encoding.UTF8.GetBytes(json);
            await _commandChar.WriteAsync(bytes);
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Parses ESP32 sensor data.
    /// Format: "T:21.5,H:62.0,PH:6.2,TDS:980,LVL:15.2,LUX:12500,DO:7.8,AT:24.3"
    /// </summary>
    private void OnDataReceived(object? sender, CharacteristicUpdatedEventArgs e)
    {
        try
        {
            var raw = Encoding.UTF8.GetString(e.Characteristic.Value);
            var data = new SensorData();

            foreach (var pair in raw.Split(','))
            {
                var kv = pair.Split(':');
                if (kv.Length != 2 || !double.TryParse(kv[1], out var val)) continue;

                data = kv[0].ToUpperInvariant() switch
                {
                    "PH" => data with { Ph = val },
                    "T" or "WT" => data with { WaterTemp = val },
                    "AT" => data with { AirTemp = val },
                    "H" => data with { Humidity = val },
                    "TDS" => data with { Tds = val },
                    "LVL" => data with { WaterLevel = val },
                    "LUX" => data with { Light = val },
                    "DO" => data with { DissolvedOxygen = val },
                    _ => data
                };
            }

            SensorDataReceived?.Invoke(data);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Parse error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _scanCts?.Cancel();
        _scanCts?.Dispose();

        DisconnectAsync().GetAwaiter().GetResult();

        GC.SuppressFinalize(this);
    }
}
