using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;

namespace Alder.Avalonia.Controls;

/// <summary>A standalone disclosure with a customizable trigger and an animated content panel.</summary>
public class AlderDisclosure : Expander
{
    /// <summary>Identifies the <see cref="Key"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<AlderDisclosure, string?>(nameof(Key));
    /// <summary>Identifies the <see cref="Icon"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<AlderDisclosure, object?>(nameof(Icon));
    /// <summary>Identifies the <see cref="IndicatorIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> IndicatorIconProperty =
        AvaloniaProperty.Register<AlderDisclosure, Geometry?>(nameof(IndicatorIcon));
    /// <summary>Identifies the <see cref="ExpandedIndicatorIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> ExpandedIndicatorIconProperty =
        AvaloniaProperty.Register<AlderDisclosure, Geometry?>(nameof(ExpandedIndicatorIcon));
    /// <summary>Identifies the <see cref="IndicatorRotation"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> IndicatorRotationProperty =
        AvaloniaProperty.Register<AlderDisclosure, double>(nameof(IndicatorRotation), -180);

    /// <summary>Identifies the <see cref="TriggerTheme"/> Avalonia property.</summary>
    public static readonly StyledProperty<ControlTheme?> TriggerThemeProperty =
        AvaloniaProperty.Register<AlderDisclosure, ControlTheme?>(nameof(TriggerTheme));

    /// <summary>The optional theme applied to the button that toggles the disclosure.</summary>
    public ControlTheme? TriggerTheme { get => GetValue(TriggerThemeProperty); set => SetValue(TriggerThemeProperty, value); }
    /// <summary>The identifier used when a containing group tracks expanded items.</summary>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }
    /// <summary>An optional geometry or icon control displayed with the content.</summary>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    /// <summary>The geometry shown in the collapsed state.</summary>
    public Geometry? IndicatorIcon { get => GetValue(IndicatorIconProperty); set => SetValue(IndicatorIconProperty, value); }
    /// <summary>The optional replacement geometry shown in the expanded state.</summary>
    public Geometry? ExpandedIndicatorIcon { get => GetValue(ExpandedIndicatorIconProperty); set => SetValue(ExpandedIndicatorIconProperty, value); }
    /// <summary>The current rotation applied to the disclosure indicator.</summary>
    public double IndicatorRotation { get => GetValue(IndicatorRotationProperty); set => SetValue(IndicatorRotationProperty, value); }

    internal ToggleButton? Trigger { get; private set; }
    private AlderAccordionPanel? panel;
    private PathIcon? indicator;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Trigger = e.NameScope.Find<ToggleButton>("PART_Trigger");
        panel = e.NameScope.Find<AlderAccordionPanel>("PART_Panel");
        indicator = e.NameScope.Find<PathIcon>("PART_Indicator");
        UpdateIndicator();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == IsExpandedProperty && !IsExpanded && panel?.IsKeyboardFocusWithin == true)
            Trigger?.Focus(NavigationMethod.Directional);

        base.OnPropertyChanged(change);
        if (change.Property == HeaderProperty || change.Property == HeaderTemplateProperty)
            PseudoClasses.Set(":plain-header", Header is string && HeaderTemplate is null);
        if (change.Property == IsExpandedProperty || change.Property == IndicatorIconProperty ||
            change.Property == ExpandedIndicatorIconProperty || change.Property == IndicatorRotationProperty)
            UpdateIndicator();
    }

    private void UpdateIndicator()
    {
        if (indicator is null) return;
        indicator.Data = IsExpanded ? ExpandedIndicatorIcon ?? IndicatorIcon : IndicatorIcon;
        var rotation = IsExpanded ? IndicatorRotation : 0;
        var transform = new TransformOperations.Builder(1);
        transform.AppendRotate(rotation * Math.PI / 180);
        indicator.RenderTransform = transform.Build();
    }
}
