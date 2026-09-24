using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SonyControl.Presentation.Common;
using SonyControl.Presentation.Devices;
using SonyControl.Presentation.Headsets;
using SonyControl.Presentation.Logging;
using SonyControl.Presentation.Notifications;
using SonyControl.Presentation.Scenes;
using SonyControl.Presentation.Settings;

namespace SonyControl.Presentation.ViewModels;

/// <summary>
/// One headset's controls for the flyout device page and the settings window.
/// </summary>
/// <remarks>
/// Controls change right away when used, then send the command. When a command fails, every
/// control goes back to what the headset last confirmed and <see cref="ErrorMessage"/> shows
/// for five seconds. The ambient slider sends at most one command per 150 ms and always sends
/// where the drag stops. Headset notifications update the controls, except the noise controls
/// while slider commands are still queued.
/// </remarks>
public sealed class HeadsetViewModel : ObservableObject, IDisposable
{
    public static readonly TimeSpan SliderInterval = TimeSpan.FromMilliseconds(150);
    public static readonly TimeSpan ErrorDuration = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How often the battery is re-read while one earbud is missing.
    /// </summary>
    public static readonly TimeSpan MissingEarbudPollInterval = TimeSpan.FromSeconds(5);

    private const string NoValue = "—";

    private readonly ManagedHeadset _managed;
    private readonly IHeadset _headset;
    private readonly AppSettings _settings;
    private readonly LowBatteryMonitor _lowBattery;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly UiContext _ui = new();
    private readonly Throttler _noiseThrottler;

    private HeadsetSnapshot _snapshot = HeadsetSnapshot.Empty;
    private HeadsetConnectionState _connectionState;
    private NoiseMode _noiseMode;
    private double _ambientLevel = 10;
    private bool _focusOnVoice;
    private int _selectedEqualizerIndex = -1;
    private int _dseeIndex;
    private bool _speakToChat;
    private bool _adaptiveVolume;
    private int _autoPowerOffIndex;
    private double _clearBass;
    private double _band1;
    private double _band2;
    private double _band3;
    private double _band4;
    private double _band5;
    private string? _errorMessage;
    private ITimer? _errorTimer;
    private ITimer? _missingEarbudTimer;
    private bool _applying;
    private int _noiseCommandsInFlight;

    public HeadsetViewModel(
        ManagedHeadset managed,
        AppSettings settings,
        LowBatteryMonitor lowBattery,
        TimeProvider timeProvider,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(managed);

        _managed = managed;
        _headset = managed.Headset;
        _settings = settings;
        _lowBattery = lowBattery;
        _timeProvider = timeProvider;
        _logger = logger;
        _noiseThrottler = new Throttler(SliderInterval, timeProvider);
        _connectionState = managed.ConnectionState;

        SetNoiseModeCommand = new RelayCommand<string>(mode =>
        {
            if (Enum.TryParse<NoiseMode>(mode, out var parsed))
            {
                SelectNoiseMode(parsed);
            }
        });
        ApplySceneCommand = new RelayCommand<Scene>(scene =>
        {
            if (scene is not null)
            {
                ApplyScene(scene);
            }
        });
        ApplyCustomEqualizerCommand = new AsyncRelayCommand(ApplyCustomEqualizerAsync);
        PowerOffCommand = new AsyncRelayCommand(() => RunCommandAsync(_headset.PowerOffAsync, "power"));
        SwitchPlaybackCommand = new RelayCommand<PlaybackDevice>(device =>
        {
            if (device is not null)
            {
                SwitchPlayback(device);
            }
        });
        DisconnectCommand = new RelayCommand(() => AutoConnect = false);
        ReconnectCommand = new RelayCommand(() => ReconnectRequested?.Invoke(this, EventArgs.Empty));

        _headset.StateChanged += OnHeadsetStateChanged;
        SceneItems = [.. settings.Scenes.Select(scene => new SceneItemViewModel(scene))];
        ApplySnapshot(_headset.State);
    }

    // =========================================================================
    // IDENTITY
    // =========================================================================

    public string Id => _managed.Id;

    public string DeviceName => _managed.Name;

    public string ModelName => _headset.ModelName;

    public bool IsKnownModel => _headset.IsKnownModel;

    public HeadsetFeatures Features => _headset.Features;

    public string Firmware => string.IsNullOrEmpty(_snapshot.Firmware) ? NoValue : _snapshot.Firmware;

    public string Codec => string.IsNullOrEmpty(_snapshot.Codec) ? NoValue : _snapshot.Codec;

    // =========================================================================
    // CONNECTION
    // =========================================================================

    public HeadsetConnectionState ConnectionState => _connectionState;

    public bool IsConnected => _connectionState == HeadsetConnectionState.Connected;

    public bool IsConnecting => _connectionState == HeadsetConnectionState.Connecting;

    /// <summary>
    /// Whether Windows has the headset connected (audio), apart from the app's control link.
    /// </summary>
    public bool IsWindowsConnected => _managed.IsWindowsConnected;

    /// <summary>
    /// The app let go of the headset (Disconnect) and stays off it until Reconnect.
    /// </summary>
    public bool IsReleased => !AutoConnect;

    /// <summary>
    /// Worth showing as a destination: Windows has it and the app isn't holding off.
    /// </summary>
    public bool IsAvailable => IsWindowsConnected && !IsReleased;

    /// <summary>
    /// The warning bar and disabled controls show while there's no control link and none is
    /// on the way.
    /// </summary>
    public bool ShowDisconnectedWarning => _connectionState == HeadsetConnectionState.Disconnected;

    /// <summary>
    /// Reconnect takes the Disconnect button's place while disconnected, but only while Windows
    /// has the headset; without that there's nothing to reconnect to.
    /// </summary>
    public bool ShowReconnect => _connectionState == HeadsetConnectionState.Disconnected && IsWindowsConnected;

    public string StatusText => _connectionState switch
    {
        HeadsetConnectionState.Connected when !string.IsNullOrEmpty(_snapshot.Codec) => $"Connected · {_snapshot.Codec}",
        HeadsetConnectionState.Connected when !IsKnownModel => "Connected · Unverified model",
        HeadsetConnectionState.Connected => "Connected",
        HeadsetConnectionState.Connecting => "Connecting…",
        _ => "Disconnected",
    };

    /// <summary>
    /// The status line only shows while not connected; once connected the header shows the
    /// battery and <see cref="AudioText"/> instead, like Sony's app.
    /// </summary>
    public bool ShowStatus => !IsConnected;

    /// <summary>
    /// Codec, plus "DSEE Extreme" while DSEE is on, e.g. "AAC · DSEE Extreme".
    /// </summary>
    public string AudioText => string.Join(" · ", new[] { _snapshot.Codec, _dseeIndex == 1 ? "DSEE Extreme" : "" }.Where(part => part.Length > 0));

    public bool AutoConnect
    {
        get => _settings.IsAutoConnectEnabled(Id);
        set
        {
            if (value == AutoConnect)
            {
                return;
            }
            _settings.SetAutoConnectEnabled(Id, value);
            RaiseAutoConnectChanged();
            AutoConnectChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? AutoConnectChanged;

    /// <summary>
    /// Raised by <see cref="ReconnectCommand"/>; the flyout asks the headset manager to reconnect.
    /// </summary>
    public event EventHandler? ReconnectRequested;

    /// <summary>
    /// Lets go of the headset until Reconnect (see <see cref="HeadsetManager.Release"/>).
    /// </summary>
    public IRelayCommand DisconnectCommand { get; }

    /// <summary>
    /// Connects again, undoing Disconnect.
    /// </summary>
    public IRelayCommand ReconnectCommand { get; }

    // =========================================================================
    // BATTERY
    // =========================================================================

    /// <summary>
    /// The battery row hides while Windows doesn't have the headset; the levels would only be stale.
    /// </summary>
    public bool ShowBattery => IsWindowsConnected;

    public bool ShowDualBattery => Features.DualBattery && (ShowLeftBattery || ShowRightBattery);

    /// <summary>
    /// False while that earbud isn't connected (no level reported), so its badge hides.
    /// </summary>
    public bool ShowLeftBattery => _snapshot.Battery.Left is not null;

    public bool ShowRightBattery => _snapshot.Battery.Right is not null;

    public bool ShowSingleBattery => !ShowDualBattery;

    public string LeftBatteryText => Percent(_snapshot.Battery.Left);

    public string RightBatteryText => Percent(_snapshot.Battery.Right);

    public string CaseBatteryText => Percent(_snapshot.Battery.Case);

    public bool ShowCaseBattery => _snapshot.Battery.Case is not null;

    public string MainBatteryText => Percent(_snapshot.Battery.Main);

    public string MainBatteryGlyph => Glyphs.Battery(_snapshot.Battery.Main);

    // =========================================================================
    // NOISE CONTROL
    // =========================================================================

    public IRelayCommand<string> SetNoiseModeCommand { get; }

    public IRelayCommand<Scene> ApplySceneCommand { get; }

    public IReadOnlyList<SceneItemViewModel> SceneItems { get; private set; } = [];

    public NoiseMode NoiseMode => _noiseMode;

    public bool IsNoiseOff => _noiseMode == NoiseMode.Off;

    public bool IsNoiseCancelling => _noiseMode == NoiseMode.NoiseCancelling;

    public bool IsAmbient => _noiseMode == NoiseMode.Ambient;

    public double AmbientLevel
    {
        get => _ambientLevel;
        set
        {
            var level = Math.Clamp(Math.Round(value), 1, 20);
            if (!SetProperty(ref _ambientLevel, level) || _applying)
            {
                return;
            }
            OnPropertyChanged(nameof(AmbientLevelText));
            UpdateActiveScene();
            _noiseThrottler.Run(SendNoiseControlAsync);
        }
    }

    public string AmbientLevelText => $"{_ambientLevel:0}";

    public bool FocusOnVoice
    {
        get => _focusOnVoice;
        set
        {
            if (!SetProperty(ref _focusOnVoice, value) || _applying)
            {
                return;
            }
            UpdateActiveScene();
            _ = SendNoiseControlAsync();
        }
    }

    // =========================================================================
    // SOUND
    // =========================================================================

    public IReadOnlyList<EqualizerPresetOption> EqualizerPresets => _headset.EqualizerPresets;

    /// <summary>
    /// Index into <see cref="EqualizerPresets"/>. A -1 from the UI is ignored: a ComboBox
    /// writes it back when its preset list is swapped (switching headphones), which would
    /// otherwise leave the dropdown blank.
    /// </summary>
    public int SelectedEqualizerIndex
    {
        get => _selectedEqualizerIndex;
        set
        {
            if (value < 0 && !_applying && _selectedEqualizerIndex >= 0)
            {
                // Tell the control again, once it has finished swapping lists
                _ui.Defer(() => OnPropertyChanged(nameof(SelectedEqualizerIndex)));
                return;
            }
            if (!SetProperty(ref _selectedEqualizerIndex, value) || _applying || value < 0 || value >= EqualizerPresets.Count)
            {
                return;
            }
            var preset = EqualizerPresets[value].Value;
            _ = RunCommandAsync(() => _headset.SetEqualizerPresetAsync(preset), "equalizer");
        }
    }

    /// <summary>
    /// 0 is Off, 1 is Auto.
    /// </summary>
    public int DseeIndex
    {
        get => _dseeIndex;
        set
        {
            if (!SetProperty(ref _dseeIndex, value))
            {
                return;
            }
            OnPropertyChanged(nameof(AudioText));
            if (_applying)
            {
                return;
            }
            _ = RunCommandAsync(() => _headset.SetDseeAsync(value == 1), "DSEE");
        }
    }

    public static IReadOnlyList<string> DseeOptions { get; } = ["Off", "Auto"];

    public double ClearBass
    {
        get => _clearBass;
        set => SetProperty(ref _clearBass, Math.Clamp(Math.Round(value), -10, 10));
    }

    public double Band1
    {
        get => _band1;
        set => SetProperty(ref _band1, Math.Clamp(Math.Round(value), -10, 10));
    }

    public double Band2
    {
        get => _band2;
        set => SetProperty(ref _band2, Math.Clamp(Math.Round(value), -10, 10));
    }

    public double Band3
    {
        get => _band3;
        set => SetProperty(ref _band3, Math.Clamp(Math.Round(value), -10, 10));
    }

    public double Band4
    {
        get => _band4;
        set => SetProperty(ref _band4, Math.Clamp(Math.Round(value), -10, 10));
    }

    public double Band5
    {
        get => _band5;
        set => SetProperty(ref _band5, Math.Clamp(Math.Round(value), -10, 10));
    }

    public IAsyncRelayCommand ApplyCustomEqualizerCommand { get; }

    /// <summary>
    /// Turns the headset off (the footer's "Turn off" button).
    /// </summary>
    public IAsyncRelayCommand PowerOffCommand { get; }

    // =========================================================================
    // PLAYBACK
    // =========================================================================

    /// <summary>
    /// Devices connected to the headset (multipoint), for moving the audio between them.
    /// Empty when the headset can't switch.
    /// </summary>
    public IReadOnlyList<PlaybackDevice> PlaybackDevices { get; private set; } = [];

    /// <summary>
    /// The Playback section shows while connected with at least two devices to choose from.
    /// </summary>
    public bool ShowPlayback => IsConnected && PlaybackDevices.Count >= 2;

    /// <summary>
    /// Moves the audio to the given device; the one already playing is left alone.
    /// </summary>
    public IRelayCommand<PlaybackDevice> SwitchPlaybackCommand { get; }

    // =========================================================================
    // SYSTEM
    // =========================================================================

    public bool SpeakToChat
    {
        get => _speakToChat;
        set
        {
            if (!SetProperty(ref _speakToChat, value) || _applying)
            {
                return;
            }
            _ = RunCommandAsync(() => _headset.SetSpeakToChatAsync(value), "Speak-to-Chat");
        }
    }

    public bool AdaptiveVolume
    {
        get => _adaptiveVolume;
        set
        {
            if (!SetProperty(ref _adaptiveVolume, value) || _applying)
            {
                return;
            }
            _ = RunCommandAsync(() => _headset.SetAdaptiveVolumeAsync(value), "adaptive volume");
        }
    }

    public int AutoPowerOffIndex
    {
        get => _autoPowerOffIndex;
        set
        {
            if (!SetProperty(ref _autoPowerOffIndex, value) || _applying || value < 0)
            {
                return;
            }
            _ = RunCommandAsync(() => _headset.SetAutoPowerOffAsync(value), "auto power-off");
        }
    }

    public static IReadOnlyList<string> AutoPowerOffOptions { get; } =
        ["Off", "After 5 minutes", "After 30 minutes", "After 1 hour", "After 3 hours", "When taken off"];

    // =========================================================================
    // ERRORS
    // =========================================================================

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => _errorMessage is not null;

    // =========================================================================
    // METHODS
    // =========================================================================

    public void SelectNoiseMode(NoiseMode mode)
    {
        if (mode == _noiseMode)
        {
            return;
        }
        _noiseMode = mode;
        RaiseNoiseModeChanged();
        _ = SendNoiseControlAsync();
    }

    public void ApplyScene(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        _applying = true;
        try
        {
            _noiseMode = scene.Setting.Mode;
            if (scene.Setting.Mode == NoiseMode.Ambient)
            {
                AmbientLevel = scene.Setting.AmbientLevel;
            }
            FocusOnVoice = scene.Setting.FocusOnVoice;
        }
        finally
        {
            _applying = false;
        }
        RaiseNoiseModeChanged();
        OnPropertyChanged(nameof(AmbientLevelText));
        _ = SendNoiseControlAsync();
    }

    /// <summary>
    /// Asks the headset for fresh battery levels. Failures are logged, not shown.
    /// </summary>
    public async Task RefreshBatteryAsync()
    {
        if (!IsConnected)
        {
            return;
        }
        try
        {
            await _headset.RefreshBatteryAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogMessages.BatteryRefreshFailed(_logger, ex, DeviceName);
        }
    }

    /// <summary>
    /// Settings changed outside this view model (the manager's Reconnect turns auto-connect back on).
    /// </summary>
    internal void RaiseAutoConnectChanged()
    {
        OnPropertyChanged(nameof(AutoConnect));
        OnPropertyChanged(nameof(IsReleased));
        OnPropertyChanged(nameof(IsAvailable));
    }

    internal void UpdateConnectionState(HeadsetConnectionState state)
    {
        // Windows connection and name changes arrive on the same event
        OnPropertyChanged(nameof(DeviceName));
        OnPropertyChanged(nameof(IsWindowsConnected));
        OnPropertyChanged(nameof(IsAvailable));
        OnPropertyChanged(nameof(ShowBattery));
        OnPropertyChanged(nameof(ShowReconnect));
        if (!SetProperty(ref _connectionState, state, nameof(ConnectionState)))
        {
            return;
        }
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsConnecting));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ShowStatus));
        OnPropertyChanged(nameof(ShowDisconnectedWarning));
        OnPropertyChanged(nameof(ShowReconnect));
        OnPropertyChanged(nameof(ShowPlayback));
        UpdateMissingEarbudPolling();
        if (state == HeadsetConnectionState.Connected)
        {
            ApplySnapshot(_headset.State);
        }
    }

    internal void RefreshScenes()
    {
        SceneItems = [.. _settings.Scenes.Select(scene => new SceneItemViewModel(scene))];
        UpdateActiveScene();
        OnPropertyChanged(nameof(SceneItems));
    }

    public void Dispose()
    {
        _headset.StateChanged -= OnHeadsetStateChanged;
        _noiseThrottler.Dispose();
        _errorTimer?.Dispose();
        _missingEarbudTimer?.Dispose();
    }

    private static string Percent(int? level) => level is null ? NoValue : $"{level}%";

    // The earbuds don't reliably announce an earbud coming back, so while one side is missing
    // the battery is re-read every few seconds until it reports again
    private void UpdateMissingEarbudPolling()
    {
        var oneMissing = IsConnected && Features.DualBattery && (_snapshot.Battery.Left is null ^ _snapshot.Battery.Right is null);
        if (!oneMissing)
        {
            _missingEarbudTimer?.Dispose();
            _missingEarbudTimer = null;
            return;
        }
        _missingEarbudTimer ??= _timeProvider.CreateTimer(_ => _ = RefreshBatteryAsync(), null, MissingEarbudPollInterval, MissingEarbudPollInterval);
    }

    private void OnHeadsetStateChanged(object? sender, HeadsetSnapshot snapshot) => _ui.Post(() => ApplySnapshot(snapshot));

    private void ApplySnapshot(HeadsetSnapshot snapshot)
    {
        var skipNoise = _noiseThrottler.HasPending || Volatile.Read(ref _noiseCommandsInFlight) > 0;

        // Taking an earbud out briefly reports neither side; keep the last levels until the
        // real ones follow, so the battery row doesn't blank out
        var battery = snapshot.Battery;
        if (Features.DualBattery && battery.Left is null && battery.Right is null && (_snapshot.Battery.Left is not null || _snapshot.Battery.Right is not null))
        {
            battery = battery with { Left = _snapshot.Battery.Left, Right = _snapshot.Battery.Right };
        }

        // The case only reports while an earbud sits in it; otherwise show the last level it
        // reported, even from an earlier run, like Sony's app does
        if (battery.Case is { } caseLevel)
        {
            _settings.SetLastCaseBattery(Id, caseLevel);
        }
        else if (Features.DualBattery)
        {
            battery = battery with { Case = _settings.GetLastCaseBattery(Id) };
        }
        snapshot = snapshot with { Battery = battery };
        _snapshot = snapshot;
        UpdateMissingEarbudPolling();
        _applying = true;
        try
        {
            if (!skipNoise)
            {
                _noiseMode = snapshot.NoiseControl.Mode;
                if (snapshot.NoiseControl.Mode == NoiseMode.Ambient && snapshot.NoiseControl.AmbientLevel > 0)
                {
                    AmbientLevel = snapshot.NoiseControl.AmbientLevel;
                }
                FocusOnVoice = snapshot.NoiseControl.FocusOnVoice;
            }

            SelectedEqualizerIndex = EqualizerPresets
                .Select((option, index) => (option, index))
                .FirstOrDefault(pair => pair.option.Value == snapshot.Equalizer.Preset, (null!, -1)).index;
            ClearBass = snapshot.Equalizer.ClearBass;
            Band1 = snapshot.Equalizer.Bands[0];
            Band2 = snapshot.Equalizer.Bands[1];
            Band3 = snapshot.Equalizer.Bands[2];
            Band4 = snapshot.Equalizer.Bands[3];
            Band5 = snapshot.Equalizer.Bands[4];
            DseeIndex = snapshot.Dsee ? 1 : 0;
            SpeakToChat = snapshot.SpeakToChat;
            AdaptiveVolume = snapshot.AdaptiveVolume;
            AutoPowerOffIndex = snapshot.AutoPowerOff;
        }
        finally
        {
            _applying = false;
        }
        SetPlaybackDevices(snapshot.PlaybackDevices);

        RaiseNoiseModeChanged();
        OnPropertyChanged(nameof(AmbientLevelText));
        OnPropertyChanged(nameof(ShowDualBattery));
        OnPropertyChanged(nameof(ShowLeftBattery));
        OnPropertyChanged(nameof(ShowRightBattery));
        OnPropertyChanged(nameof(ShowSingleBattery));
        OnPropertyChanged(nameof(LeftBatteryText));
        OnPropertyChanged(nameof(RightBatteryText));
        OnPropertyChanged(nameof(CaseBatteryText));
        OnPropertyChanged(nameof(ShowCaseBattery));
        OnPropertyChanged(nameof(MainBatteryText));
        OnPropertyChanged(nameof(MainBatteryGlyph));
        OnPropertyChanged(nameof(Firmware));
        OnPropertyChanged(nameof(Codec));
        OnPropertyChanged(nameof(AudioText));
        OnPropertyChanged(nameof(StatusText));

        _lowBattery.Update(Id, DeviceName, snapshot.Battery);
    }

    private void SwitchPlayback(PlaybackDevice device)
    {
        if (device.Playing)
        {
            return;
        }

        // Show it straight away; a refusal (on a call, say) puts the headset's answer back
        SetPlaybackDevices([.. PlaybackDevices.Select(item => item with { Playing = item.Address == device.Address })]);
        _ = RunCommandAsync(
            () => _headset.SwitchPlaybackAsync(device.Address),
            "playback",
            "Couldn't switch the audio. The other device may be on a call.");
    }

    private void SetPlaybackDevices(IReadOnlyList<PlaybackDevice> devices)
    {
        if (devices.SequenceEqual(PlaybackDevices))
        {
            return;
        }
        PlaybackDevices = devices;
        OnPropertyChanged(nameof(PlaybackDevices));
        OnPropertyChanged(nameof(ShowPlayback));
    }

    private void RaiseNoiseModeChanged()
    {
        OnPropertyChanged(nameof(NoiseMode));
        OnPropertyChanged(nameof(IsNoiseOff));
        OnPropertyChanged(nameof(IsNoiseCancelling));
        OnPropertyChanged(nameof(IsAmbient));
        UpdateActiveScene();
    }

    private void UpdateActiveScene()
    {
        foreach (var item in SceneItems)
        {
            item.Update(_noiseMode, (int)_ambientLevel, _focusOnVoice);
        }
    }

    private Task SendNoiseControlAsync()
    {
        var setting = new NoiseControlSetting(_noiseMode, (int)_ambientLevel, _focusOnVoice);
        Interlocked.Increment(ref _noiseCommandsInFlight);
        return RunNoiseCommandAsync(setting);
    }

    // Headset echoes of earlier values are ignored while any noise command is in flight;
    // once the last one finishes, the controls take the headset's confirmed state.
    private async Task RunNoiseCommandAsync(NoiseControlSetting setting)
    {
        try
        {
            await RunCommandAsync(() => _headset.SetNoiseControlAsync(setting), "noise control").ConfigureAwait(false);
        }
        finally
        {
            if (Interlocked.Decrement(ref _noiseCommandsInFlight) == 0)
            {
                _ui.Post(() => ApplySnapshot(_headset.State));
            }
        }
    }

    private Task ApplyCustomEqualizerAsync()
    {
        var setting = new EqualizerSetting(
            EqualizerSetting.ManualPreset,
            (int)_clearBass,
            ImmutableArray.Create((int)_band1, (int)_band2, (int)_band3, (int)_band4, (int)_band5));
        return RunCommandAsync(() => _headset.SetEqualizerCustomAsync(setting), "custom equalizer");
    }

    // refusedMessage replaces the generic text when the headset answers "no" (a refused
    // multipoint switch comes back as an invalid-data error)
    private async Task RunCommandAsync(Func<Task> command, string setting, string? refusedMessage = null)
    {
        try
        {
            await command().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogMessages.CommandFailed(_logger, ex, setting, DeviceName);
            _ui.Post(() =>
            {
                ApplySnapshot(_headset.State);
                ShowError(refusedMessage is not null && ex.HResult == HeadsetErrorMessages.InvalidDataHResult
                    ? refusedMessage
                    : HeadsetErrorMessages.Describe(ex));
            });
        }
    }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        _errorTimer?.Dispose();
        _errorTimer = _timeProvider.CreateTimer(_ => _ui.Post(() => ErrorMessage = null), null, ErrorDuration, Timeout.InfiniteTimeSpan);
    }
}
