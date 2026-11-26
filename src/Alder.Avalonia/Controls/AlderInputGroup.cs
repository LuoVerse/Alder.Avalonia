using Avalonia;
using Avalonia.Controls;

namespace Alder.Avalonia.Controls;

/// <summary>A text editor with inline prefix and suffix content in the same field surface.</summary>
public class AlderInputGroup : AlderInput
{
    /// <summary>Identifies the <see cref="Prefix"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> PrefixProperty =
        AvaloniaProperty.Register<AlderInputGroup, object?>(nameof(Prefix));
    /// <summary>Identifies the <see cref="Suffix"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> SuffixProperty =
        AvaloniaProperty.Register<AlderInputGroup, object?>(nameof(Suffix));
    /// <summary>Identifies the <see cref="PrefixPadding"/> Avalonia property.</summary>
    public static readonly StyledProperty<Thickness> PrefixPaddingProperty =
        AvaloniaProperty.Register<AlderInputGroup, Thickness>(nameof(PrefixPadding), new Thickness(12, 0));
    /// <summary>Identifies the <see cref="SuffixPadding"/> Avalonia property.</summary>
    public static readonly StyledProperty<Thickness> SuffixPaddingProperty =
        AvaloniaProperty.Register<AlderInputGroup, Thickness>(nameof(SuffixPadding), new Thickness(12, 0));

    /// <summary>Optional leading content.</summary>
    public object? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }
    /// <summary>Optional trailing content.</summary>
    public object? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }
    /// <summary>The room reserved inside the field for leading content.</summary>
    public Thickness PrefixPadding { get => GetValue(PrefixPaddingProperty); set => SetValue(PrefixPaddingProperty, value); }
    /// <summary>The room reserved inside the field for trailing content.</summary>
    public Thickness SuffixPadding { get => GetValue(SuffixPaddingProperty); set => SetValue(SuffixPaddingProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == InnerLeftContentProperty)
            PseudoClasses.Set(":has-prefix", InnerLeftContent is not null);
        else if (change.Property == InnerRightContentProperty)
            PseudoClasses.Set(":has-suffix", InnerRightContent is not null);
    }
}
