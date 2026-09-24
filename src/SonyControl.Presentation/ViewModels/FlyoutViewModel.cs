using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SonyControl.Presentation.Common;
using SonyControl.Presentation.Devices;
using SonyControl.Presentation.Navigation;

namespace SonyControl.Presentation.ViewModels;

/// <summary>
/// The tray flyout: which page shows, the headsets, and the footer actions.
/// </summary>
public sealed class FlyoutViewModel : ObservableObject, IDisposable
{
    private readonly HeadsetManager _manager;
    private readonly FlyoutNavigator _navigator;
    private readonly Func<ManagedHeadset, HeadsetViewModel> _createHeadsetViewModel;
    private readonly UiContext _ui = new();

    private FlyoutRoute _route = new(FlyoutPageKind.Empty, null, false);
    private HeadsetViewModel? _currentHeadset;

    public FlyoutViewModel(
        HeadsetManager manager,
        FlyoutNavigator navigator,
        Func<ManagedHeadset, HeadsetViewModel> createHeadsetViewModel)
    {
        ArgumentNullException.ThrowIfNull(manager);

        _manager = manager;
        _navigator = navigator;
        _createHeadsetViewModel = createHeadsetViewModel;

        PickCommand = new RelayCommand<HeadsetViewModel>(Pick);
        BackCommand = new RelayCommand(Back);
        ReconnectCommand = new RelayCommand(Reconnect);
        OpenSettingsCommand = new RelayCommand(() => SettingsRequested?.Invoke(this, EventArgs.Empty));
        QuitCommand = new RelayCommand(() => QuitRequested?.Invoke(this, EventArgs.Empty));

        _manager.HeadsetAdded += OnHeadsetAdded;
        _manager.HeadsetRemoved += OnHeadsetRemoved;
        _manager.ConnectionStateChanged += OnConnectionStateChanged;
        foreach (var headset in _manager.Headsets)
        {
            Add(headset);
        }
        Refresh();
    }

    public event EventHandler? SettingsRequested;

    public event EventHandler? QuitRequested;

    public ObservableCollection<HeadsetViewModel> Headsets { get; } = [];

    /// <summary>
    /// Headsets with an open control link, for the tray icon's hover tooltip. A snapshot:
    /// read it each time the tooltip opens.
    /// </summary>
    public IReadOnlyList<HeadsetViewModel> ConnectedHeadsets => [.. Headsets.Where(headset => headset.IsConnected)];

    public HeadsetViewModel? CurrentHeadset
    {
        get => _currentHeadset;
        private set => SetProperty(ref _currentHeadset, value);
    }

    public FlyoutPageKind Page => _route.Kind;

    public bool IsEmptyVisible => _route.Kind == FlyoutPageKind.Empty;

    public bool IsPickerVisible => _route.Kind == FlyoutPageKind.Picker;

    public bool IsDeviceVisible => _route.Kind == FlyoutPageKind.Device;

    public bool ShowBack => _route.ShowBack;

    /// <summary>
    /// A headset was picked as the default. The picker then only shows briefly while that
    /// headset connects, so it skips its entrance animation.
    /// </summary>
    public bool HasDefaultHeadset => _navigator.HasRemembered;

    public IRelayCommand<HeadsetViewModel> PickCommand { get; }

    public IRelayCommand BackCommand { get; }

    public IRelayCommand ReconnectCommand { get; }

    public IRelayCommand OpenSettingsCommand { get; }

    public IRelayCommand QuitCommand { get; }

    /// <summary>
    /// Refreshes battery. Called each time the flyout opens.
    /// </summary>
    public Task OnOpenedAsync()
    {
        Refresh();
        return CurrentHeadset?.RefreshBatteryAsync() ?? Task.CompletedTask;
    }

    public void Dispose()
    {
        _manager.HeadsetAdded -= OnHeadsetAdded;
        _manager.HeadsetRemoved -= OnHeadsetRemoved;
        _manager.ConnectionStateChanged -= OnConnectionStateChanged;
        foreach (var headset in Headsets)
        {
            headset.Dispose();
        }
        Headsets.Clear();
    }

    private void Pick(HeadsetViewModel? headset)
    {
        if (headset is null)
        {
            return;
        }
        _navigator.Pick(headset.Id, headset.IsAvailable);
        Refresh();
    }

    private void Back()
    {
        _navigator.Back();
        Refresh();
    }

    private void Reconnect()
    {
        if (CurrentHeadset is not null)
        {
            _manager.Reconnect(CurrentHeadset.Id);
        }
    }

    private void OnHeadsetAdded(object? sender, ManagedHeadset headset) => _ui.Post(() =>
    {
        Add(headset);
        Refresh();
    });

    private void OnHeadsetRemoved(object? sender, ManagedHeadset headset) => _ui.Post(() =>
    {
        var viewModel = Headsets.FirstOrDefault(item => item.Id == headset.Id);
        if (viewModel is not null)
        {
            Headsets.Remove(viewModel);
            viewModel.AutoConnectChanged -= OnAutoConnectChanged;
            viewModel.ReconnectRequested -= OnReconnectRequested;
            viewModel.Dispose();
        }
        Refresh();
    });

    // Also covers Windows connecting or disconnecting the headset, which can change the page
    private void OnConnectionStateChanged(object? sender, ManagedHeadset headset) => _ui.Post(() =>
    {
        Headsets.FirstOrDefault(item => item.Id == headset.Id)?.UpdateConnectionState(headset.ConnectionState);
        Refresh();
    });

    private void OnAutoConnectChanged(object? sender, EventArgs e)
    {
        if (sender is HeadsetViewModel headset)
        {
            _manager.ApplyAutoConnect(headset.Id);
            Refresh();
        }
    }

    private void OnReconnectRequested(object? sender, EventArgs e)
    {
        if (sender is HeadsetViewModel headset)
        {
            _manager.Reconnect(headset.Id);
            headset.RaiseAutoConnectChanged();
            Refresh();
        }
    }

    private void Add(ManagedHeadset headset)
    {
        if (Headsets.Any(item => item.Id == headset.Id))
        {
            return;
        }
        var viewModel = _createHeadsetViewModel(headset);
        viewModel.AutoConnectChanged += OnAutoConnectChanged;
        viewModel.ReconnectRequested += OnReconnectRequested;
        Headsets.Add(viewModel);
    }

    private void Refresh()
    {
        _route = _navigator.Resolve([.. Headsets.Select(headset => new HeadsetAvailability(headset.Id, headset.IsAvailable))]);
        CurrentHeadset = _route.HeadsetId is null ? null : Headsets.FirstOrDefault(headset => headset.Id == _route.HeadsetId);
        OnPropertyChanged(nameof(Page));
        OnPropertyChanged(nameof(IsEmptyVisible));
        OnPropertyChanged(nameof(IsPickerVisible));
        OnPropertyChanged(nameof(IsDeviceVisible));
        OnPropertyChanged(nameof(ShowBack));
        OnPropertyChanged(nameof(HasDefaultHeadset));
    }
}
