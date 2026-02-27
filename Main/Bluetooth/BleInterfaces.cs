using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Main.Bluetooth;

/// <summary>
/// Cross-platform BLE access. Provided by Plugin.BLE.
/// </summary>
public static class CrossBluetoothLE
{
    private static IBluetoothLE? _current;

    public static IBluetoothLE Current
    {
        get => _current ?? throw new InvalidOperationException("Plugin.BLE not initialized");
        set => _current = value;
    }
}

public interface IBluetoothLE
{
    IAdapter Adapter { get; }
    BluetoothState State { get; }
}

public interface IAdapter
{
    bool IsScanning { get; }

    event EventHandler<DeviceEventArgs>? DeviceDiscovered;
    event EventHandler<DeviceEventArgs>? DeviceConnected;
    event EventHandler<DeviceEventArgs>? DeviceDisconnected;

    Task StartScanningForDevicesAsync(
        Guid[]? serviceUuids = null,
        Func<IDevice, bool>? deviceFilter = null,
        bool allowDuplicatesKey = false,
        CancellationToken cancellationToken = default);

    Task StopScanningForDevicesAsync();

    Task<IDevice> ConnectToKnownDeviceAsync(
        Guid deviceGuid,
        ConnectParameters connectParameters = default,
        CancellationToken cancellationToken = default);

    Task DisconnectDeviceAsync(IDevice device);
}

public interface IDevice
{
    Guid Id { get; }
    string Name { get; }
    int Rssi { get; }
    DeviceState State { get; }
    bool IsConnectable { get; }
    Task<IService?> GetServiceAsync(Guid serviceId, CancellationToken cancellationToken = default);
}

public interface IService
{
    Guid Id { get; }
    Task<ICharacteristic?> GetCharacteristicAsync(Guid characteristicId);
}

public interface ICharacteristic
{
    Guid Id { get; }
    byte[] Value { get; }
    event EventHandler<CharacteristicUpdatedEventArgs>? ValueUpdated;
    Task<byte[]> ReadAsync(CancellationToken cancellationToken = default);
    Task<bool> WriteAsync(byte[] data, CancellationToken cancellationToken = default);
    Task StartUpdatesAsync(CancellationToken cancellationToken = default);
    Task StopUpdatesAsync(CancellationToken cancellationToken = default);
}

public enum BluetoothState { 
    Unknown, 
    Unavailable, 
    Unauthorized, 
    TurningOn, 
    On, 
    TurningOff, 
    Off 
}
public enum DeviceState { 
    Disconnected, 
    Connecting, 
    Connected, 
    Disconnecting, 
    Limited 
}

public struct ConnectParameters
{
    public bool AutoConnect { get; }
    public bool ForceBleTransport { get; }
    public ConnectParameters(bool autoConnect = false, bool forceBleTransport = false)
    {
        AutoConnect = autoConnect;
        ForceBleTransport = forceBleTransport;
    }
}

public class DeviceEventArgs : EventArgs
{
    public IDevice Device { get; }
    public DeviceEventArgs(IDevice device) => Device = device;
}

public class CharacteristicUpdatedEventArgs : EventArgs
{
    public ICharacteristic Characteristic { get; }
    public CharacteristicUpdatedEventArgs(ICharacteristic characteristic) => Characteristic = characteristic;
}


public enum CommandType
{
    SetWaterPump,
    SetAirPump,
    DoseNutrients,
    SetLightSchedule,
    Calibrate,
    Reboot,
    GetStatus
}