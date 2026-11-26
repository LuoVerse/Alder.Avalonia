using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Controls whether swatches wrap into multiple rows or use a stack.</summary>
public enum ColorSwatchPickerLayout
{
    /// <summary>Arranges swatches in a wrapping grid.</summary>
    Grid,
    /// <summary>Arranges swatches in a single stack.</summary>
    Stack
}

/// <summary>A single-selection palette. Items can be colors or explicit swatch picker items.</summary>
public sealed class AlderColorSwatchPicker : ListBox
{
    /// <summary>Identifies the <see cref="Size"/> Avalonia property.</summary>
    public static readonly StyledProperty<ColorSwatchSize> SizeProperty = AlderColorSwatch.SizeProperty.AddOwner<AlderColorSwatchPicker>();
    /// <summary>Identifies the <see cref="Shape"/> Avalonia property.</summary>
    public static readonly StyledProperty<ColorSwatchShape> ShapeProperty = AlderColorSwatch.ShapeProperty.AddOwner<AlderColorSwatchPicker>();
    /// <summary>Identifies the <see cref="Layout"/> Avalonia property.</summary>
    public static readonly StyledProperty<ColorSwatchPickerLayout> LayoutProperty =
        AvaloniaProperty.Register<AlderColorSwatchPicker, ColorSwatchPickerLayout>(nameof(Layout));

    /// <summary>The size preset used for the control.</summary>
    public ColorSwatchSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>The shared outline used by swatches in this picker.</summary>
    public ColorSwatchShape Shape { get => GetValue(ShapeProperty); set => SetValue(ShapeProperty, value); }
    /// <summary>How the selectable swatches are arranged.</summary>
    public ColorSwatchPickerLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new AlderColorSwatchPickerItem();

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<AlderColorSwatchPickerItem>(item, out recycleKey);

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is AlderColorSwatchPickerItem swatch && !ReferenceEquals(container, item))
            swatch.Color = item switch
            {
                Color color => color,
                AlderColorValue color => color.ToColor(),
                _ => Colors.Transparent
            };
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && e.Key == Key.Space && e.Source is AlderColorSwatchPickerItem item && item.IsEffectivelyEnabled)
        {
            SetCurrentValue(SelectedIndexProperty, IndexFromContainer(item));
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }
}

/// <summary>Presents a selectable swatch and exposes its accessible display name.</summary>
public sealed class AlderColorSwatchPickerItem : ListBoxItem
{
    /// <summary>Identifies the <see cref="Color"/> Avalonia property.</summary>
    public static readonly StyledProperty<Color> ColorProperty = AlderColorSwatch.ColorProperty.AddOwner<AlderColorSwatchPickerItem>();
    /// <summary>Identifies the <see cref="ColorName"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> ColorNameProperty = AlderColorSwatch.ColorNameProperty.AddOwner<AlderColorSwatchPickerItem>();
    /// <summary>Identifies the <see cref="Size"/> Avalonia property.</summary>
    public static readonly StyledProperty<ColorSwatchSize> SizeProperty = AlderColorSwatch.SizeProperty.AddOwner<AlderColorSwatchPickerItem>();
    /// <summary>Identifies the <see cref="Shape"/> Avalonia property.</summary>
    public static readonly StyledProperty<ColorSwatchShape> ShapeProperty = AlderColorSwatch.ShapeProperty.AddOwner<AlderColorSwatchPickerItem>();
    /// <summary>Identifies the <see cref="DisplayName"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderColorSwatchPickerItem, string> DisplayNameProperty =
        AvaloniaProperty.RegisterDirect<AlderColorSwatchPickerItem, string>(nameof(DisplayName), item => item.DisplayName);

    /// <summary>The color applied to the control.</summary>
    public Color Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    /// <summary>An optional display name for this swatch's color.</summary>
    public string? ColorName { get => GetValue(ColorNameProperty); set => SetValue(ColorNameProperty, value); }
    /// <summary>The size preset used for the control.</summary>
    public ColorSwatchSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>The outline of this item's swatch.</summary>
    public ColorSwatchShape Shape { get => GetValue(ShapeProperty); set => SetValue(ShapeProperty, value); }
    /// <summary>The supplied swatch name or a formatted color value when the name is absent.</summary>
    public string DisplayName => ColorName ?? AlderColorValue.FormatHex(Color);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ColorProperty && ColorName is null)
            RaisePropertyChanged(DisplayNameProperty, AlderColorValue.FormatHex(change.GetOldValue<Color>()), DisplayName);
        else if (change.Property == ColorNameProperty)
            RaisePropertyChanged(DisplayNameProperty, change.GetOldValue<string?>() ?? AlderColorValue.FormatHex(Color), DisplayName);
    }
}
