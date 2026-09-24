using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SonyControl.Presentation.ViewModels;

namespace SonyControl.App.Views;

/// <summary>
/// Content of the tray icon's hover tooltip: the connected headsets with their batteries.
/// </summary>
public sealed partial class TrayTooltip : UserControl
{
    public TrayTooltip()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows these headsets, or "No headphones connected" when there are none.
    /// </summary>
    public void Show(IReadOnlyList<HeadsetViewModel> headsets)
    {
        HeadsetList.ItemsSource = headsets;
        EmptyText.Visibility = headsets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
