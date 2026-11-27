using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>A Bloom Menu action with an optional icon and the standard button command API.</summary>
public sealed class AlderBloomMenuItem : Button
{
    /// <summary>Identifies the optional geometry shown above the label.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<AlderBloomMenuItem, object?>(nameof(Icon));

    /// <summary>Gets or sets a geometry or an icon control.</summary>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    internal static readonly StyledProperty<Thickness> SeparatorThicknessProperty =
        AvaloniaProperty.Register<AlderBloomMenuItem, Thickness>(nameof(SeparatorThickness));

    internal Thickness SeparatorThickness
    {
        get => GetValue(SeparatorThicknessProperty);
        set => SetValue(SeparatorThicknessProperty, value);
    }

    private Control? glyphAndLabel;

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new BloomItemPeer(this);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        glyphAndLabel = e.NameScope.Find<Control>("PART_ItemContent");
    }

    internal void Bloom(TimeSpan delay, bool reduced)
    {
        if (glyphAndLabel is not { } content || ElementComposition.GetElementVisual(content) is not { } visual) return;
        BloomMenuMotion.Item.Fade(visual, 1, delay, reduced ? 0 : .12f);
        if (reduced) return;

        visual.CenterPoint = new Vector3D(content.Bounds.Width / 2, content.Bounds.Height / 2, 0);
        BloomMenuMotion.Item.Scale(visual, new Vector3D(1, 1, 1), new Vector3D(.85, .85, 1), delay);
        // Blur radius is a styled effect in Avalonia. Its short transition only
        // updates this glyph layer, while scale and opacity run on the compositor.
        var blur = new BlurEffect { Radius = 4 };
        content.Effect = blur;
        blur.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = BlurEffect.RadiusProperty, Delay = delay,
                Duration = TimeSpan.FromMilliseconds(110), Easing = BloomMenuMotion.Body.Ease
            }
        };
        blur.Radius = 0;
    }

    internal void FinishBloom()
    {
        if (glyphAndLabel is not { } content) return;
        OverlayMotion.Reset(content);
        content.Effect = null;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        FinishBloom();
        base.OnDetachedFromVisualTree(e);
    }

    private sealed class BloomItemPeer(AlderBloomMenuItem owner) : ButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.MenuItem;
    }
}
