using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

internal sealed class SwitchTrack : Border
{
    public static readonly StyledProperty<IBrush?> IdleBackgroundProperty =
        AvaloniaProperty.Register<SwitchTrack, IBrush?>(nameof(IdleBackground));
    public static readonly StyledProperty<IBrush?> HoverBackgroundProperty =
        AvaloniaProperty.Register<SwitchTrack, IBrush?>(nameof(HoverBackground));
    public static readonly StyledProperty<IBrush?> CheckedBackgroundProperty =
        AvaloniaProperty.Register<SwitchTrack, IBrush?>(nameof(CheckedBackground));
    public static readonly StyledProperty<IBrush?> CheckedHoverBackgroundProperty =
        AvaloniaProperty.Register<SwitchTrack, IBrush?>(nameof(CheckedHoverBackground));
    public static readonly StyledProperty<bool?> IsCheckedProperty = ToggleButton.IsCheckedProperty.AddOwner<SwitchTrack>();
    public static readonly StyledProperty<bool> IsHighlightedProperty =
        AvaloniaProperty.Register<SwitchTrack, bool>(nameof(IsHighlighted));

    public IBrush? IdleBackground { get => GetValue(IdleBackgroundProperty); set => SetValue(IdleBackgroundProperty, value); }
    public IBrush? HoverBackground { get => GetValue(HoverBackgroundProperty); set => SetValue(HoverBackgroundProperty, value); }
    public IBrush? CheckedBackground { get => GetValue(CheckedBackgroundProperty); set => SetValue(CheckedBackgroundProperty, value); }
    public IBrush? CheckedHoverBackground { get => GetValue(CheckedHoverBackgroundProperty); set => SetValue(CheckedHoverBackgroundProperty, value); }
    public bool? IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }
    public bool IsHighlighted { get => GetValue(IsHighlightedProperty); set => SetValue(IsHighlightedProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != IsCheckedProperty && change.Property != IsHighlightedProperty &&
            change.Property != IdleBackgroundProperty && change.Property != HoverBackgroundProperty &&
            change.Property != CheckedBackgroundProperty && change.Property != CheckedHoverBackgroundProperty)
            return;

        var idle = IsChecked == true ? CheckedBackground : IdleBackground;
        var hover = IsChecked == true ? CheckedHoverBackground : HoverBackground;
        // Blend the selected brush directly with the surrounding surface.
        Background = IsHighlighted ? hover ?? idle : idle;
    }
}
