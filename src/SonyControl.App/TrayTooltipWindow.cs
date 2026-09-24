using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using SonyControl.App.Views;
using SonyControl.Presentation.Placement;
using SonyControl.Presentation.Settings;
using SonyControl.Presentation.ViewModels;
using Windows.Foundation;
using Windows.Graphics;
using WinRT.Interop;

namespace SonyControl.App;

/// <summary>
/// Hosts the tray icon's hover tooltip: the connected headsets and their batteries.
/// </summary>
/// <remarks>
/// A frameless, transparent, always-on-top window sized to the tooltip and placed at the icon,
/// away from the taskbar. It's shown without activating, so hovering never takes focus.
/// </remarks>
internal sealed partial class TrayTooltipWindow : Window
{
    private readonly TrayTooltip _tooltip = new();
    private readonly FlyoutViewModel _flyout;
    private readonly IntPtr _hwnd;

    public TrayTooltipWindow(FlyoutViewModel flyout)
    {
        _flyout = flyout;
        Content = _tooltip;
        _hwnd = WindowNative.GetWindowHandle(this);

        // Window Chrome
        var presenter = OverlappedPresenter.Create();
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        presenter.SetBorderAndTitleBar(false, false);
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;

        // Frameless and see-through around the tooltip's rounded corners
        PopupWindowStyle.Apply(_hwnd);
        SystemBackdrop = new TransparentBackdrop();
    }

    /// <summary>
    /// Opens the tooltip at a screen point (physical pixels), growing away from the taskbar
    /// and kept inside the work area.
    /// </summary>
    public void ShowAt(int x, int y, TaskbarEdge edge, PixelRect workArea, double scale)
    {
        _tooltip.Show(_flyout.ConnectedHeadsets);

        // Size to the content, in physical pixels
        _tooltip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = (int)Math.Ceiling(_tooltip.DesiredSize.Width * scale);
        var height = (int)Math.Ceiling(_tooltip.DesiredSize.Height * scale);

        // Right edge at the icon for a top or bottom taskbar; on a side taskbar it grows up from
        // the icon, like the right-click menu
        var (left, top) = edge switch
        {
            TaskbarEdge.Top => (x - width, y),
            TaskbarEdge.Left => (x, y - height),
            TaskbarEdge.Right => (x - width, y - height),
            _ => (x - width, y - height),
        };
        left = Math.Clamp(left, workArea.Left, Math.Max(workArea.Left, workArea.Right - width));
        top = Math.Clamp(top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height));

        AppWindow.MoveAndResize(new RectInt32(left, top, width, height));
        AppWindow.Show(false);
    }

    public void Hide() => AppWindow.Hide();

    public void ApplyTheme(AppTheme theme) => _tooltip.RequestedTheme = theme switch
    {
        AppTheme.Light => ElementTheme.Light,
        AppTheme.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };
}
