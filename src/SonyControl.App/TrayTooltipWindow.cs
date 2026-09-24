using Microsoft.UI.Dispatching;
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
/// A frameless, transparent, always-on-top window sized to the tooltip (plus room for its
/// shadow) and placed at the icon, away from the taskbar. It's shown without activating, so
/// hovering never takes focus.
/// </remarks>
internal sealed partial class TrayTooltipWindow : Window
{
    private readonly TrayTooltip _tooltip = new();
    private readonly FlyoutViewModel _flyout;
    private readonly IntPtr _hwnd;
    private readonly DispatcherQueueTimer _delay;
    private (int X, int Y, TaskbarEdge Edge, PixelRect WorkArea, double Scale)? _pending;

    public TrayTooltipWindow(FlyoutViewModel flyout)
    {
        _flyout = flyout;
        Content = _tooltip;
        _hwnd = WindowNative.GetWindowHandle(this);

        // Windows' tooltip delay: the double-click time (TTDT_INITIAL's default)
        _delay = DispatcherQueue.CreateTimer();
        _delay.Interval = TimeSpan.FromMilliseconds(NativeMethods.GetDoubleClickTime());
        _delay.IsRepeating = false;
        _delay.Tick += (_, _) => ShowPending();
        _tooltip.FadedOut += (_, _) => AppWindow.Hide();

        // Window Chrome
        var presenter = OverlappedPresenter.Create();
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        presenter.SetBorderAndTitleBar(false, false);
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;

        // Frameless, See-Through Chrome (around the panel and its shadow)
        PopupWindowStyle.Apply(_hwnd);
        SystemBackdrop = new TransparentBackdrop();
    }

    /// <summary>
    /// Opens the tooltip after Windows' tooltip delay at a screen point (physical pixels),
    /// growing away from the taskbar and kept inside the work area.
    /// </summary>
    public void ShowAt(int x, int y, TaskbarEdge edge, PixelRect workArea, double scale)
    {
        _pending = (x, y, edge, workArea, scale);
        _delay.Start();
    }

    /// <summary>
    /// Cancels a pending tooltip, or fades out an open one and then hides the window.
    /// </summary>
    public void Hide()
    {
        _pending = null;
        _delay.Stop();
        if (AppWindow.IsVisible)
        {
            _tooltip.FadeOut();
        }
    }

    public void ApplyTheme(AppTheme theme) => _tooltip.RequestedTheme = theme switch
    {
        AppTheme.Light => ElementTheme.Light,
        AppTheme.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };

    private void ShowPending()
    {
        if (_pending is not { } pending)
        {
            return;
        }
        _pending = null;
        var (x, y, edge, workArea, scale) = pending;

        _tooltip.SetHeadsets(_flyout.ConnectedHeadsets);

        // Size to the content, in physical pixels; the panel sits inside a margin left for its shadow
        _tooltip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var margin = (int)Math.Round(_tooltip.ShadowMargin.Left * scale);
        var width = (int)Math.Ceiling(_tooltip.DesiredSize.Width * scale) - (2 * margin);
        var height = (int)Math.Ceiling(_tooltip.DesiredSize.Height * scale) - (2 * margin);

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

        AppWindow.MoveAndResize(new RectInt32(left - margin, top - margin, width + (2 * margin), height + (2 * margin)));
        AppWindow.Show(false);
        _tooltip.FadeIn();
    }
}
