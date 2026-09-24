using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace SonyControl.App;

/// <summary>
/// Product picture for a headset model, shown at the top of the flyout.
/// </summary>
/// <remarks>
/// Pictures live in Assets as "&lt;model name&gt;.png" (e.g. "WF-1000XM6.png"). Models without a
/// picture show none; drop a matching file in Assets to add one.
/// </remarks>
public static class DeviceArt
{
    // A file path, not ms-appx:, so it works for both the MSIX and the classic install
    public static ImageSource? For(string? modelName) => Has(modelName)
        ? new BitmapImage(new Uri(PathFor(modelName!)))
        : null;

    public static Visibility VisibleFor(string? modelName) => Has(modelName) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Visible when the model has no picture, for a fallback icon.
    /// </summary>
    public static Visibility HiddenFor(string? modelName) => Has(modelName) ? Visibility.Collapsed : Visibility.Visible;

    private static bool Has(string? modelName) => !string.IsNullOrEmpty(modelName) && File.Exists(PathFor(modelName));

    private static string PathFor(string modelName) => Path.Combine(AppContext.BaseDirectory, "Assets", modelName + ".png");
}
