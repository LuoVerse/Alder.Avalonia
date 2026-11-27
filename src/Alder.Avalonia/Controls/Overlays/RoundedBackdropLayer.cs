using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

internal sealed class RoundedBackdropLayer : Border
{
    public static readonly StyledProperty<double> RadiusProperty =
        AvaloniaProperty.Register<RoundedBackdropLayer, double>(nameof(Radius));

    public double Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    public RoundedBackdropLayer()
    {
        ClipToBounds = true;
        IsHitTestVisible = false;
        IsVisible = false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != RadiusProperty) return;

        IsVisible = Radius > 0;
        if (!IsVisible)
            BackdropEffect = null;
        else if (BackdropEffect is BlurEffect blur)
            blur.Radius = Radius;
        else
            BackdropEffect = new BlurEffect { Radius = Radius };
    }
}
