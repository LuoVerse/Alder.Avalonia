using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.Localization;
using Avalonia.Data.Converters;
using ReactiveUI;
using MediaColor = global::Avalonia.Media.Color;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class ColorsViewModel : GalleryViewModel
{
    public ColorsViewModel()
    {
        ShuffleColor = ReactiveCommand.Create(() =>
        {
            var random = Random.Shared;
            ControlledColor = AlderColorValue.FromHsl(random.Next(360), .5 + random.NextDouble() * .5,
                .4 + random.NextDouble() * .3);
        });
    }

    public System.Windows.Input.ICommand ShuffleColor { get; }

    public AlderColorValue ControlledColor
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = AlderColorValue.Parse("#325578");

    public static IReadOnlyList<AlderColorValue> PickerPresets { get; } =
        new[] { "#EF4444", "#F97316", "#EAB308", "#22C55E", "#06B6D4", "#3B82F6", "#8B5CF6", "#EC4899", "#F43F5E" }
            .Select(AlderColorValue.Parse).ToArray();

    public static IReadOnlyList<string> PickerSpaceNames { get; } = ["HSB", "HSL", "RGB"];
    public static FuncValueConverter<int, ColorSpace> PickerSpace { get; } =
        new(index => index switch { 0 => ColorSpace.Hsb, 2 => ColorSpace.Rgb, _ => ColorSpace.Hsl });
    public static FuncValueConverter<int, ColorChannel> PickerFirstChannel { get; } =
        new(index => index == 2 ? ColorChannel.Red : ColorChannel.Hue);
    public static FuncValueConverter<int, ColorChannel> PickerSecondChannel { get; } =
        new(index => index == 2 ? ColorChannel.Green : ColorChannel.Saturation);
    public static FuncValueConverter<int, ColorChannel> PickerThirdChannel { get; } =
        new(index => index switch { 0 => ColorChannel.Brightness, 2 => ColorChannel.Blue, _ => ColorChannel.Lightness });
    public static FuncValueConverter<ColorChannel, string> PickerChannelName { get; } =
        new(channel => GalleryLocalization.Current.Text("Color" + channel));

    public AlderColorValue Color
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = AlderColorValue.FromColor(MediaColor.Parse("#0485F7"));

    public AlderColorValue AreaColor
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = AlderColorValue.FromRgb(116, 52, 255);

    public AlderColorValue DottedColor
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = AlderColorValue.FromHsl(200, 1, .5);

    public AlderColorValue HueColor
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = AlderColorValue.FromHsl(0, 1, .5);

    public AlderColorValue TranslucentColor
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = AlderColorValue.FromHsl(0, 1, .5, .5);

    public AlderColorValue AlternateColor
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = AlderColorValue.FromHsl(278, .62, .55);

    public IReadOnlyList<MediaColor> Presets { get; } =
        new[] { "#F43F5E", "#D946EF", "#8B5CF6", "#3B82F6", "#06B6D4", "#10B981", "#84CC16" }.Select(MediaColor.Parse).ToArray();

    public object? Preset
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

}
