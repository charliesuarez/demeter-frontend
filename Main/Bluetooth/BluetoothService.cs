using System.Text;
using System.Text.Json;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Main.Services;

namespace Main.Bluetooth;

public class BluetoothService : IBluetoothService
{
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

    // Events - Matched to IBluetoothService to fix CS0738/CS0102
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
        return Task.CompletedTask;
    }

    private void OnDeviceDiscovered(object? sender, DeviceEventArgs e) =>
        DeviceDiscovered?.Invoke(new BleDevice(e.Device.Id, e.Device.Name ?? "Unknown", e.Device.Rssi));

    private void OnDeviceConnected(object? sender, DeviceEventArgs e) => System.Diagnostics.Debug.WriteLine("Connected");

    private void OnDeviceDisconnected(object? sender, DeviceEventArgs e)
    {
        _device = null;
        _dataChar = null;
        _commandChar = null;
        ConnectionChanged?.Invoke(false);
    }

    public async Task StartScanAsync(int timeoutSeconds = 10)
    {
        if (_adapter.IsScanning) return;
        _scanCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        try { await _adapter.StartScanningForDevicesAsync(cancellationToken: _scanCts.Token); }
        catch (Exception ex) { ErrorOccurred?.Invoke(ex.Message); }
    }

    public async Task StopScanAsync()
    {
        _scanCts?.Cancel();
        if (_adapter.IsScanning) await _adapter.StopScanningForDevicesAsync();
    }

    public async Task ConnectAsync(Guid deviceId)
    {
        await StopScanAsync();
        _device = await _adapter.ConnectToKnownDeviceAsync(deviceId, new ConnectParameters(false, true));
        await SetupCharacteristicsAsync();
    }

    public async Task ConnectByAddressAsync(string macAddress)
    {
        var cleanMac = macAddress.Replace(":", "").Replace("-", "").ToUpperInvariant();
        var deviceGuid = Guid.Parse($"00000000-0000-0000-0000-{cleanMac}");
        await ConnectAsync(deviceGuid);
    }

    private async Task SetupCharacteristicsAsync()
    {
        if (_device == null) return;
        var service = await _device.GetServiceAsync(ServiceUuid);
        if (service == null) return;

        _dataChar = await service.GetCharacteristicAsync(DataCharUuid);
        _commandChar = await service.GetCharacteristicAsync(CommandCharUuid);

        if (_dataChar != null)
        {
            _dataChar.ValueUpdated += OnDataReceived;
            await _dataChar.StartUpdatesAsync();
        }
        ConnectionChanged?.Invoke(true);
    }

    // FIXES THE 0.00 ISSUE: Maps ESP32 JSON to the SensorData model
    private void OnDataReceived(object? sender, CharacteristicUpdatedEventArgs e)
    {
        try
        {
            var rawJson = Encoding.UTF8.GetString(e.Characteristic.Value);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // Teammate firmware sends JSON
            var reading = JsonSerializer.Deserialize<SensorReading>(rawJson, options);

            if (reading != null)
            {
                // Convert to SensorData for the UI
                var data = new SensorData
                {
                    Ph = reading.Ph,
                    WaterTemp = reading.WaterTemp,
                    AirTemp = reading.AirTemp,
                    Humidity = reading.Humidity,
                    Tds = reading.Tds,
                    WaterLevel = reading.WaterLevel,
                    Light = reading.LightIntensity,
                    DissolvedOxygen = reading.DissolvedOxygen
                };
                SensorDataReceived?.Invoke(data);
            }
        }
        catch { }
    }

    public async Task DisconnectAsync()
    {
        if (_dataChar != null) _dataChar.ValueUpdated -= OnDataReceived;
        if (_device != null) await _adapter.DisconnectDeviceAsync(_device);
        _device = null;
        ConnectionChanged?.Invoke(false);
    }

    public Task<bool> SetWaterPumpAsync(bool on) => SendCommandAsync("setwaterpump", new { state = on });
    public Task<bool> SetAirPumpAsync(bool on) => SendCommandAsync("setairpump", new { state = on });
    public Task<bool> DoseNutrientsAsync(int durationMs) => SendCommandAsync("dosenutrients", new { duration_ms = durationMs });

    private async Task<bool> SendCommandAsync(string cmd, object? parameters)
    {
        if (_commandChar == null || !IsConnected) return false;
        var json = JsonSerializer.Serialize(new { cmd, @params = parameters });
        await _commandChar.WriteAsync(Encoding.UTF8.GetBytes(json));
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _scanCts?.Cancel();
        DisconnectAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}