using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Positions the text label before or after the switch track.</summary>
public enum SwitchLabelPlacement
{
    /// <summary>Places the text after the switch track.</summary>
    After,
    /// <summary>Places the text before the switch track.</summary>
    Before
}

/// <summary>A ToggleSwitch with an Alder control theme and bindable appearance properties.</summary>
public class AlderSwitch : ToggleSwitch
{
    /// <summary>Identifies Size for styling and binding.</summary>
    public static readonly StyledProperty<AlderSize> SizeProperty =
        AvaloniaProperty.Register<AlderSwitch, AlderSize>(nameof(Size), AlderSize.Medium);

    /// <summary>Identifies Description for styling and binding.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AlderSwitch, string?>(nameof(Description));

    /// <summary>Identifies LabelPlacement for styling and binding.</summary>
    public static readonly StyledProperty<SwitchLabelPlacement> LabelPlacementProperty =
        AvaloniaProperty.Register<AlderSwitch, SwitchLabelPlacement>(nameof(LabelPlacement));

    /// <summary>Identifies OnIcon for styling and binding.</summary>
    public static readonly StyledProperty<Geometry?> OnIconProperty =
        AvaloniaProperty.Register<AlderSwitch, Geometry?>(nameof(OnIcon));

    /// <summary>Identifies OffIcon for styling and binding.</summary>
    public static readonly StyledProperty<Geometry?> OffIconProperty =
        AvaloniaProperty.Register<AlderSwitch, Geometry?>(nameof(OffIcon));

    /// <summary>Identifies ShowStateLabel for styling and binding.</summary>
    public static readonly StyledProperty<bool> ShowStateLabelProperty =
        AvaloniaProperty.Register<AlderSwitch, bool>(nameof(ShowStateLabel));

    /// <summary>Identifies CheckedTrackBrush for styling and binding.</summary>
    public static readonly StyledProperty<IBrush?> CheckedTrackBrushProperty =
        AvaloniaProperty.Register<AlderSwitch, IBrush?>(nameof(CheckedTrackBrush));

    /// <summary>Identifies CheckedHoverBrush for styling and binding.</summary>
    public static readonly StyledProperty<IBrush?> CheckedHoverBrushProperty =
        AvaloniaProperty.Register<AlderSwitch, IBrush?>(nameof(CheckedHoverBrush));

    /// <summary>Gets or sets the Size appearance setting.</summary>
    public AlderSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>Gets or sets the Description appearance setting.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>Gets or sets the LabelPlacement appearance setting.</summary>
    public SwitchLabelPlacement LabelPlacement { get => GetValue(LabelPlacementProperty); set => SetValue(LabelPlacementProperty, value); }
    /// <summary>Gets or sets the OnIcon appearance setting.</summary>
    public Geometry? OnIcon { get => GetValue(OnIconProperty); set => SetValue(OnIconProperty, value); }
    /// <summary>Gets or sets the OffIcon appearance setting.</summary>
    public Geometry? OffIcon { get => GetValue(OffIconProperty); set => SetValue(OffIconProperty, value); }
    /// <summary>Gets or sets the ShowStateLabel appearance setting.</summary>
    public bool ShowStateLabel { get => GetValue(ShowStateLabelProperty); set => SetValue(ShowStateLabelProperty, value); }
    /// <summary>Gets or sets the CheckedTrackBrush appearance setting.</summary>
    public IBrush? CheckedTrackBrush { get => GetValue(CheckedTrackBrushProperty); set => SetValue(CheckedTrackBrushProperty, value); }
    /// <summary>Gets or sets the CheckedHoverBrush appearance setting.</summary>
    public IBrush? CheckedHoverBrush { get => GetValue(CheckedHoverBrushProperty); set => SetValue(CheckedHoverBrushProperty, value); }
}
