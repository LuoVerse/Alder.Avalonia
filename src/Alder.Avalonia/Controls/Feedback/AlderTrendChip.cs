using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>The direction indicated by a trend chip.</summary>
public enum TrendDirection
{
    /// <summary>The value increased.</summary>
    Up,
    /// <summary>The value decreased.</summary>
    Down,
    /// <summary>The value stayed level.</summary>
    Neutral
}

/// <summary>A compact chip combining a trend symbol, value, and optional affixes.</summary>
public sealed partial class AlderTrendChip : ContentControl
{
    /// <summary>The direction of the trend, independent of its semantic color.</summary>
    [GeneratedStyledProperty]
    public partial TrendDirection Trend { get; set; }
    /// <summary>The size of the chip.</summary>
    [GeneratedStyledProperty(DefaultValue = AlderSize.Small)]
    public partial AlderSize Size { get; set; }
    /// <summary>The visual emphasis of the chip.</summary>
    [GeneratedStyledProperty(DefaultValue = ChipVariant.Soft)]
    public partial ChipVariant Variant { get; set; }
    /// <summary>Overrides the default color for the trend direction.</summary>
    [GeneratedStyledProperty]
    public partial AlderStatusColor? Color { get; set; }
    /// <summary>Overrides the geometric indicator supplied by the theme.</summary>
    [GeneratedStyledProperty]
    public partial Geometry? Indicator { get; set; }
    /// <summary>Whether to show the trend indicator.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool ShowIndicator { get; set; }
    /// <summary>Content before the trend value.</summary>
    [GeneratedStyledProperty]
    public partial object? Prefix { get; set; }
    /// <summary>Content after the trend value.</summary>
    [GeneratedStyledProperty]
    public partial object? Suffix { get; set; }
    /// <summary>Uses equal-width digits in the value text.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool TabularNumbers { get; set; }

    /// <summary>Identifies the resolved color property.</summary>
    public static readonly DirectProperty<AlderTrendChip, AlderStatusColor> EffectiveColorProperty =
        AvaloniaProperty.RegisterDirect<AlderTrendChip, AlderStatusColor>(nameof(EffectiveColor), control => control.EffectiveColor);

    /// <summary>The semantic color currently in use.</summary>
    public AlderStatusColor EffectiveColor { get => field; private set => SetAndRaise(EffectiveColorProperty, ref field, value); } = AlderStatusColor.Success;

    private AlderChip? surface;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        surface = e.NameScope.Find<AlderChip>("TrendSurface");
        ApplyBrushOverride(BackgroundProperty);
        ApplyBrushOverride(ForegroundProperty);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ColorProperty || change.Property == TrendProperty)
            EffectiveColor = Color ?? (Trend switch
            {
                TrendDirection.Down => AlderStatusColor.Danger,
                TrendDirection.Neutral => AlderStatusColor.Default,
                _ => AlderStatusColor.Success
            });
        else if (change.Property == BackgroundProperty)
            ApplyBrushOverride(BackgroundProperty);
        else if (change.Property == ForegroundProperty)
            ApplyBrushOverride(ForegroundProperty);
    }

    private void ApplyBrushOverride(StyledProperty<IBrush?> property)
    {
        if (surface is null) return;
        // Keep the chip's semantic palette when the wrapper has no explicit brush.
        if (IsSet(property) && GetValue(property) is { } brush)
            surface.SetValue(property, brush);
        else
            surface.ClearValue(property);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TrendPeer(this);

    private sealed class TrendPeer(AlderTrendChip owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
        protected override string? GetNameCore()
        {
            if (base.GetNameCore() is { Length: > 0 } explicitName) return explicitName;
            string key = "AlderText.Trend" + owner.Trend;
            var direction = owner.TryFindResource(key, out var localized) ? localized as string : owner.Trend.ToString();
            return string.Join(" ", new[] { direction, owner.Prefix as string, owner.Content?.ToString(), owner.Suffix as string }
                .Where(text => !string.IsNullOrEmpty(text)));
        }
    }
}
