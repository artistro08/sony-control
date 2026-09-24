using Microsoft.UI.Xaml;

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
}
