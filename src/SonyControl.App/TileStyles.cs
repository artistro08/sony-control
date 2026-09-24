using Microsoft.UI.Xaml;
using SonyControl.Presentation.Headsets;

namespace SonyControl.App;

/// <summary>
/// x:Bind functions for the noise control tiles and scene buttons: accent style when selected.
/// </summary>
public static class TileStyles
{
    public static Style Pick(bool selected) =>
        (Style)Application.Current.Resources[selected ? "TileButtonSelectedStyle" : "TileButtonStyle"];

    public static Style PickScene(bool active) =>
        (Style)Application.Current.Resources[active ? "SceneButtonActiveStyle" : "SceneButtonStyle"];

    /// <summary>
    /// A noise mode as the text SetNoiseModeCommand takes.
    /// </summary>
    public static string ModeName(NoiseMode mode) => mode.ToString();
}
