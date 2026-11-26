using Avalonia;
using Avalonia.Data;

namespace Alder.Avalonia.Controls;

/// <summary>Determines how radio cards are arranged within their group.</summary>
public enum RadioButtonGroupLayout
{
    /// <summary>Arranges cards along the group's orientation.</summary>
    Flex,
    /// <summary>Arranges cards into equal columns that adapt to available space.</summary>
    Grid
}

/// <summary>Coordinates single selection and arrow-key navigation among radio cards.</summary>
public sealed partial class AlderRadioButtonGroup : AlderRadioGroup
{
    /// <summary>The currently selected card key, or null if no card is selected.</summary>
    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial string? SelectedKey { get; set; }

    /// <summary>The arrangement of cards in the group.</summary>
    [GeneratedStyledProperty]
    public partial RadioButtonGroupLayout Layout { get; set; }

    /// <summary>The maximum number of grid columns.</summary>
    [GeneratedStyledProperty(DefaultValue = 3)]
    public partial int Columns { get; set; }

    /// <summary>The minimum width used to determine how many columns fit.</summary>
    [GeneratedStyledProperty(DefaultValue = 156)]
    public partial double MinColumnWidth { get; set; }

    private readonly ChoiceCardGridLayout grid = new();
    private bool applyingKey;

    static AlderRadioButtonGroup() =>
        AffectsMeasure<AlderRadioButtonGroup>(LayoutProperty, ColumnsProperty, MinColumnWidthProperty);

    /// <summary>Initializes a vertical group of separate radio cards.</summary>
    public AlderRadioButtonGroup() => Spacing = 12;

    /// <inheritdoc />
    protected override void OnSelectionApplied()
    {
        base.OnSelectionApplied();
        var key = SelectedKeys.FirstOrDefault();
        if (SelectedKey == key) return;
        applyingKey = true;
        try { SetCurrentValue(SelectedKeyProperty, key); }
        finally { applyingKey = false; }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedKeyProperty && !applyingKey)
            SetCurrentValue(SelectedKeysProperty, SelectedKey is { } key ? [key] : []);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => Layout == RadioButtonGroupLayout.Grid
        ? grid.Measure(Children, availableSize, Columns, MinColumnWidth, Spacing)
        : base.MeasureOverride(availableSize);

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize) => Layout == RadioButtonGroupLayout.Grid
        ? grid.Arrange(Children, finalSize, Spacing)
        : base.ArrangeOverride(finalSize);
}
