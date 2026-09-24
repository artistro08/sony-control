using SonyControl.Presentation.Devices;
using SonyControl.Presentation.Notifications;
using SonyControl.Presentation.Settings;

namespace SonyControl.Presentation.Tests.Fakes;

internal sealed class FakeDeviceSource : IBluetoothDeviceSource
{
    public event EventHandler<BluetoothDeviceInfo>? DeviceChanged;

    public event EventHandler<string>? DeviceRemoved;

    public bool IsWatching { get; private set; }

    public void StartWatching() => IsWatching = true;

    public void StopWatching() => IsWatching = false;

    public void Report(BluetoothDeviceInfo device) => DeviceChanged?.Invoke(this, device);

    public void Remove(string deviceId) => DeviceRemoved?.Invoke(this, deviceId);
}

internal sealed class FakeNotificationService : INotificationService
{
    public List<(string DeviceName, int Level)> Shown { get; } = [];

    public void ShowLowBattery(string deviceName, int level) => Shown.Add((deviceName, level));
}

internal sealed class FakeStartupTaskService : IStartupTaskService
{
    public bool Enabled { get; set; }

    /// <summary>
    /// When true, enabling is refused like a policy-disabled startup task.
    /// </summary>
    public bool RefuseEnable { get; set; }

    public Task<bool> IsEnabledAsync() => Task.FromResult(Enabled);

    public Task<bool> SetEnabledAsync(bool enabled)
    {
        Enabled = enabled && !RefuseEnable;
        return Task.FromResult(Enabled);
    }
}
