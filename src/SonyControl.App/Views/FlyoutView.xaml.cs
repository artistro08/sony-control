using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using SonyControl.Presentation.Scenes;
using SonyControl.Presentation.ViewModels;

namespace SonyControl.App.Views;

/// <summary>
/// Everything inside the flyout window: the empty, picker and device pages plus the footer.
/// </summary>
public sealed partial class FlyoutView : UserControl
{
    public FlyoutView(FlyoutViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        // Pages, footer and every row of the device page (one level into its sections) fade
        // in and out and glide when the layout around them changes
        foreach (var element in ContentRoot.Children.Concat(DevicePanel.Children))
        {
            ImplicitMotion.Attach(element);
            if (element is Panel section && element != DevicePanel)
            {
                foreach (var row in section.Children)
                {
                    ImplicitMotion.Attach(row);
                }
            }
        }

        // Picker entrance animation only when the picker is where you land; with a default
        // headset set it just flashes by while that headset connects
        UpdateHeadsetListTransitions();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FlyoutViewModel.HasDefaultHeadset))
            {
                UpdateHeadsetListTransitions();
            }
        };
    }

    // Only the entrance slide, and only without a default headset. No add/remove slides: a
    // headset disconnecting should just show what's left, not animate the list.
    private void UpdateHeadsetListTransitions()
    {
        var transitions = new TransitionCollection();
        if (!ViewModel.HasDefaultHeadset)
        {
            transitions.Add(new EntranceThemeTransition { IsStaggeringEnabled = false });
        }
        HeadsetList.ItemContainerTransitions = transitions;
    }

    public event EventHandler? CloseRequested;

    public FlyoutViewModel ViewModel { get; }

    private void OnEscapeInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnHeadsetClick(object sender, ItemClickEventArgs e) =>
        ViewModel.PickCommand.Execute(e.ClickedItem as HeadsetViewModel);

    // Hover and press backgrounds for a picker row; the row's BrushTransition fades between them
    private void OnPickerRowHover(object sender, PointerRoutedEventArgs e) => SetRowBackground(sender, "RowHoverBrush");

    private void OnPickerRowPressed(object sender, PointerRoutedEventArgs e) => SetRowBackground(sender, "RowPressedBrush");

    private void OnPickerRowRest(object sender, PointerRoutedEventArgs e) => SetRowBackground(sender, "RowRestBrush");

    private static void SetRowBackground(object sender, string brushKey)
    {
        if (sender is Grid row)
        {
            row.Background = (Brush)row.Resources[brushKey];
        }
    }

    private void OnSceneClick(object sender, RoutedEventArgs e)
    {
        // Scene rides on Tag: ItemsRepeater doesn't reliably set the button's DataContext
        if (sender is FrameworkElement { Tag: Scene scene })
        {
            ViewModel.CurrentHeadset?.ApplySceneCommand.Execute(scene);
        }
    }
}
