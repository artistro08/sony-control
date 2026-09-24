using SonyControl.Presentation.Headsets;

namespace SonyControl.Presentation.Scenes;

/// <summary>
/// A saved noise control combination shown as a button in the flyout.
/// </summary>
/// <param name="Name">Button label.</param>
/// <param name="Glyph">Segoe Fluent Icons glyph.</param>
/// <param name="Setting">What the button applies.</param>
public sealed record Scene(string Name, string Glyph, NoiseControlSetting Setting)
{
    /// <summary>
    /// Scenes a fresh install starts with.
    /// </summary>
    public static IReadOnlyList<Scene> Defaults { get; } =
    [
        new Scene("Focus", "", new NoiseControlSetting(NoiseMode.NoiseCancelling, 0, false)),
        new Scene("Office", "", new NoiseControlSetting(NoiseMode.Ambient, 10, true)),
        new Scene("Aware", "", new NoiseControlSetting(NoiseMode.Ambient, 20, false)),
    ];
}
