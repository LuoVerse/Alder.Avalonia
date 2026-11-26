using Avalonia;
using Avalonia.Controls;

namespace Alder.Avalonia.Controls;

/// <summary>Clips the body to its animated height while keeping the content at its natural layout size.</summary>
public sealed class AlderAccordionPanel : Decorator
{
    /// <summary>Identifies the <see cref="Progress"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<AlderAccordionPanel, double>(nameof(Progress), coerce: (_, progress) => Math.Clamp(progress, 0, 1));

    static AlderAccordionPanel() => AffectsMeasure<AlderAccordionPanel>(ProgressProperty);

    /// <summary>The expansion fraction used to measure and arrange the content.</summary>
    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child) return default;
        child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        return new Size(child.DesiredSize.Width, child.DesiredSize.Height * Progress);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Child.DesiredSize.Height));
        return finalSize;
    }
}
