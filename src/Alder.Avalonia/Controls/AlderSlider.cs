using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>A Slider with an Alder control theme and bindable appearance properties.</summary>
public class AlderSlider : Slider
{
    /// <summary>Identifies Marks for styling and binding.</summary>
    public static readonly StyledProperty<IEnumerable<SliderMark>?> MarksProperty =
        AvaloniaProperty.Register<AlderSlider, IEnumerable<SliderMark>?>(nameof(Marks));

    /// <summary>Identifies MarkFormat for styling and binding.</summary>
    public static readonly StyledProperty<string?> MarkFormatProperty =
        AvaloniaProperty.Register<AlderSlider, string?>(nameof(MarkFormat));

    /// <summary>Identifies MarkCulture for styling and binding.</summary>
    public static readonly StyledProperty<CultureInfo?> MarkCultureProperty =
        AvaloniaProperty.Register<AlderSlider, CultureInfo?>(nameof(MarkCulture));

    /// <summary>Identifies TrackBrush for styling and binding.</summary>
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<AlderSlider, IBrush?>(nameof(TrackBrush));

    /// <summary>Identifies FillBrush for styling and binding.</summary>
    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<AlderSlider, IBrush?>(nameof(FillBrush));

    /// <summary>Identifies KnobBrush for styling and binding.</summary>
    public static readonly StyledProperty<IBrush?> KnobBrushProperty =
        AvaloniaProperty.Register<AlderSlider, IBrush?>(nameof(KnobBrush));

    /// <summary>Gets or sets the Marks appearance setting.</summary>
    public IEnumerable<SliderMark>? Marks { get => GetValue(MarksProperty); set => SetValue(MarksProperty, value); }
    /// <summary>Gets or sets the MarkFormat appearance setting.</summary>
    public string? MarkFormat { get => GetValue(MarkFormatProperty); set => SetValue(MarkFormatProperty, value); }
    /// <summary>Gets or sets the MarkCulture appearance setting.</summary>
    public CultureInfo? MarkCulture { get => GetValue(MarkCultureProperty); set => SetValue(MarkCultureProperty, value); }
    /// <summary>Gets or sets the TrackBrush appearance setting.</summary>
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    /// <summary>Gets or sets the FillBrush appearance setting.</summary>
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    /// <summary>Gets or sets the KnobBrush appearance setting.</summary>
    public IBrush? KnobBrush { get => GetValue(KnobBrushProperty); set => SetValue(KnobBrushProperty, value); }
}
