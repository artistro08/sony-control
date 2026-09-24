using Core = SonyControl.Core;

namespace SonyControl.Presentation.Headsets;

/// <summary>
/// <see cref="IHeadset"/> backed by the native <see cref="Core.HeadsetClient"/>.
/// </summary>
public sealed class WinRtHeadset : IHeadset
{
    private readonly Core.HeadsetClient _client;

    public WinRtHeadset(string deviceName)
    {
        _client = new Core.HeadsetClient(deviceName);
        Features = ToFeatures(_client.Capabilities);
        EqualizerPresets = [.. Core.HeadsetClient.GetEqualizerPresets().Select(preset => new EqualizerPresetOption(preset.Value, preset.Name))];

        _client.StateChanged += OnStateChanged;
        _client.Disconnected += OnDisconnected;
    }

    public event EventHandler<HeadsetSnapshot>? StateChanged;

    public event EventHandler? Disconnected;

    public string DeviceName => _client.DeviceName;

    public string ModelName => _client.ModelName;

    public bool IsKnownModel => _client.IsKnownModel;

    public HeadsetFeatures Features { get; }

    public HeadsetSnapshot State => ToSnapshot(_client.State);

    public IReadOnlyList<EqualizerPresetOption> EqualizerPresets { get; }

    public Task ConnectAsync(string bluetoothAddress) => _client.ConnectAsync(bluetoothAddress).AsTask();

    public void Disconnect() => _client.Disconnect();

    public Task RefreshBatteryAsync() => _client.RefreshBatteryAsync().AsTask();

    public Task SetNoiseControlAsync(NoiseControlSetting value) =>
        _client.SetNoiseControlAsync(new Core.NoiseControlInfo((Core.NoiseMode)value.Mode, value.AmbientLevel, value.FocusOnVoice)).AsTask();

    public Task SetEqualizerPresetAsync(int preset) => _client.SetEqualizerPresetAsync(preset).AsTask();

    public Task SetEqualizerCustomAsync(EqualizerSetting value) =>
        _client.SetEqualizerCustomAsync(new Core.EqualizerInfo(
            value.Preset,
            value.ClearBass,
            value.Bands[0],
            value.Bands[1],
            value.Bands[2],
            value.Bands[3],
            value.Bands[4])).AsTask();

    public Task SetDseeAsync(bool enabled) => _client.SetDseeAsync(enabled).AsTask();

    public Task SetSpeakToChatAsync(bool enabled) => _client.SetSpeakToChatAsync(enabled).AsTask();

    public Task SetAdaptiveVolumeAsync(bool enabled) => _client.SetAdaptiveVolumeAsync(enabled).AsTask();

    public Task SetAutoPowerOffAsync(int index) => _client.SetAutoPowerOffAsync(index).AsTask();

    public void Dispose()
    {
        _client.StateChanged -= OnStateChanged;
        _client.Disconnected -= OnDisconnected;
        _client.Dispose();
    }

    internal static HeadsetSnapshot ToSnapshot(Core.HeadsetState state) => new(
        ToBattery(state.Battery),
        new NoiseControlSetting((NoiseMode)state.NoiseControl.Mode, state.NoiseControl.AmbientLevel, state.NoiseControl.FocusOnVoice),
        new EqualizerSetting(
            state.Equalizer.Preset,
            state.Equalizer.ClearBass,
            [state.Equalizer.Band1, state.Equalizer.Band2, state.Equalizer.Band3, state.Equalizer.Band4, state.Equalizer.Band5]),
        state.Dsee,
        state.SpeakToChat,
        state.AdaptiveVolume,
        state.AutoPowerOff,
        state.Firmware ?? "",
        state.Codec ?? "");

    private static int? Known(int level) => level < 0 ? null : level;

    // Earbuds: a side that isn't connected reports 0, and native "main" is just the lower side,
    // so it drops to 0 too. Treat 0 as "not connected" and leave main out for earbuds.
    private static BatteryLevels ToBattery(Core.BatteryInfo battery)
    {
        var earbuds = battery.Left >= 0 || battery.Right >= 0;
        return new BatteryLevels(
            earbuds ? null : Known(battery.Main),
            Earbud(battery.Left),
            Earbud(battery.Right),
            Known(battery.CaseBattery),
            battery.Charging);
    }

    private static int? Earbud(int level) => level > 0 ? level : null;

    private static HeadsetFeatures ToFeatures(Core.HeadsetCapabilities capabilities) => new(
        capabilities.DualBattery,
        capabilities.NoiseCancelling,
        capabilities.AmbientSound,
        capabilities.FocusOnVoice,
        capabilities.Equalizer,
        capabilities.ClearBass,
        capabilities.Dsee,
        capabilities.SpeakToChat,
        capabilities.AdaptiveVolume,
        capabilities.AutoPowerOff,
        capabilities.FirmwareInfo,
        capabilities.CodecInfo);

    private void OnStateChanged(Core.HeadsetClient sender, Core.HeadsetState args) => StateChanged?.Invoke(this, ToSnapshot(args));

    private void OnDisconnected(Core.HeadsetClient sender, object args) => Disconnected?.Invoke(this, EventArgs.Empty);
}
