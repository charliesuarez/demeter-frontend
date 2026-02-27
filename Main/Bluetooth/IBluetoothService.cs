using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Main.Bluetooth;

public interface IBluetoothService : IDisposable
{
    BleConnectionState ConnectionState { get; }

    bool IsConnected { get; }
    string? ConnectedDeviceId { get; }
    string? ConnectedDeviceName { get; }

    event Action<BleDeviceInfo>? OnDeviceDiscovered;
    event Action<SensorDataPacket>? OnSensorDataReceived;
    event Action<BleConnectionState>? OnConnectionStateChanged;
    event Action<string>? OnError;

    public Task InitializeAsync();
    public Task StartScanAsync(TimeSpan? timeout = null);
    public Task StopScanAsync();
    public Task ConnectAsync(Guid deviceId, CancellationToken cancellationToken = default);
    public Task DisconnectAsync(CommandType commandType, object? parameters = null);

    public Task<bool> SendCommandAsync();
    public Task<bool> SetWaterPumpAsyncOn();
    public Task<bool> SetAirPumpAsyncOn();
    public Task<bool> DoseNutrientAsync();
    public Task<bool> RequestStatusAsync();
}
