using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace SonyControl.App;

/// <summary>
/// Makes an element fade in and out when its Visibility changes, and glide instead of jump
/// when layout moves it.
/// </summary>
/// <remarks>
/// Composition implicit animations, so they run on their own once attached:
/// https://learn.microsoft.com/windows/apps/design/motion/xaml-property-animations
/// Timing follows Fluent motion (250 ms moves, 167 ms fades, "fast out, slow in").
/// </remarks>
public static class ImplicitMotion
{
    private static readonly TimeSpan MoveDuration = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(167);

    public static void Attach(UIElement element)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;
        var decelerate = compositor.CreateCubicBezierEasingFunction(new Vector2(0, 0), new Vector2(0, 1));

        // Glide To New Position
        var move = compositor.CreateVector3KeyFrameAnimation();
        move.Target = "Offset";
        move.InsertExpressionKeyFrame(1, "this.FinalValue", decelerate);
        move.Duration = MoveDuration;
        var implicitAnimations = compositor.CreateImplicitAnimationCollection();
        implicitAnimations["Offset"] = move;
        visual.ImplicitAnimations = implicitAnimations;

        // Fade In And Out
        var show = compositor.CreateScalarKeyFrameAnimation();
        show.Target = "Opacity";
        show.InsertKeyFrame(0, 0);
        show.InsertKeyFrame(1, 1, decelerate);
        show.Duration = FadeDuration;
        ElementCompositionPreview.SetImplicitShowAnimation(element, show);

        var hide = compositor.CreateScalarKeyFrameAnimation();
        hide.Target = "Opacity";
        hide.InsertKeyFrame(1, 0);
        hide.Duration = FadeDuration;
        ElementCompositionPreview.SetImplicitHideAnimation(element, hide);
    }
}
