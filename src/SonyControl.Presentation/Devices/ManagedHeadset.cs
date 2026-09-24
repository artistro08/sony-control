using SonyControl.Presentation.Headsets;

namespace SonyControl.Presentation.Devices;

/// <summary>
/// A Sony headset Windows reports as connected, plus the state of the app's control link to it.
/// </summary>
public sealed class ManagedHeadset
{
    internal ManagedHeadset(string deviceId, string address, string name, IHeadset headset)
    {
        DeviceId = deviceId;
        Id = address;
        Name = name;
        Headset = headset;
    }

    /// <summary>
    /// Windows device ID.
    /// </summary>
    public string DeviceId { get; }

    /// <summary>
    /// Upper-case Bluetooth address. Stable across restarts, so settings are keyed on it.
    /// </summary>
    public string Id { get; }

    public string Name { get; }

    public IHeadset Headset { get; }

    public HeadsetConnectionState ConnectionState { get; internal set; }

    internal CancellationTokenSource? ConnectLoop { get; set; }

    internal Task? ConnectTask { get; set; }
}
