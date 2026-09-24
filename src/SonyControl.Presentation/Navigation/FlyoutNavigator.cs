using SonyControl.Presentation.Settings;

namespace SonyControl.Presentation.Navigation;

/// <summary>
/// Which page the flyout shows.
/// </summary>
public enum FlyoutPageKind
{
    Empty,
    Picker,
    Device,
}

/// <summary>
/// The page to show, the headset it's for, and whether the back arrow is visible.
/// </summary>
public sealed record FlyoutRoute(FlyoutPageKind Kind, string? HeadsetId, bool ShowBack);

/// <summary>
/// Picks the flyout page from the connected headsets and the remembered choice.
/// </summary>
/// <remarks>
/// Rules:
/// 1. No headsets: empty page.
/// 2. One headset: its device page, no back arrow.
/// 3. Several headsets and the remembered one is among them: its device page with a back arrow.
/// 4. Several headsets otherwise: the picker.
/// The remembered choice survives disconnects, so the device page comes back when that headset
/// reconnects. Only <see cref="Back"/> clears it.
/// </remarks>
public sealed class FlyoutNavigator
{
    private readonly AppSettings _settings;

    public FlyoutNavigator(AppSettings settings)
    {
        _settings = settings;
    }

    public FlyoutRoute Resolve(IReadOnlyList<string> connectedHeadsetIds)
    {
        ArgumentNullException.ThrowIfNull(connectedHeadsetIds);

        if (connectedHeadsetIds.Count == 0)
        {
            return new FlyoutRoute(FlyoutPageKind.Empty, null, false);
        }
        if (connectedHeadsetIds.Count == 1)
        {
            return new FlyoutRoute(FlyoutPageKind.Device, connectedHeadsetIds[0], false);
        }

        var remembered = _settings.RememberedHeadsetId;
        if (remembered is not null && connectedHeadsetIds.Contains(remembered))
        {
            return new FlyoutRoute(FlyoutPageKind.Device, remembered, true);
        }
        return new FlyoutRoute(FlyoutPageKind.Picker, null, false);
    }

    /// <summary>
    /// True once a headset was picked and not backed out of.
    /// </summary>
    public bool HasRemembered => _settings.RememberedHeadsetId is not null;

    public void Pick(string headsetId) => _settings.RememberedHeadsetId = headsetId;

    public void Back() => _settings.RememberedHeadsetId = null;
}
