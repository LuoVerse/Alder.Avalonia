using Avalonia;
using Avalonia.Layout;

namespace Alder.Avalonia.Controls;

/// <summary>Controls how the checkbox cards in a group are arranged.</summary>
public enum CheckboxButtonGroupLayout
{
    /// <summary>Arranges cards in the direction specified by the group orientation.</summary>
    Flex,
    /// <summary>Arranges cards in equal-width columns that adapt to the available space.</summary>
    Grid
}

/// <summary>Arranges checkbox cards and keeps their checked state synchronized with the selected keys.</summary>
public sealed partial class AlderCheckboxButtonGroup : AlderChoiceGroup
{
    /// <summary>The arrangement of cards in the group.</summary>
    [GeneratedStyledProperty]
    public partial CheckboxButtonGroupLayout Layout { get; set; }

    /// <summary>The maximum number of columns in grid layout.</summary>
    [GeneratedStyledProperty(DefaultValue = 3)]
    public partial int Columns { get; set; }

    /// <summary>The minimum width used when deciding how many grid columns fit.</summary>
    [GeneratedStyledProperty(DefaultValue = 156)]
    public partial double MinColumnWidth { get; set; }

    /// <summary>Prevents user changes while allowing the selected keys to be set programmatically.</summary>
    [GeneratedStyledProperty]
    public partial bool IsReadOnly { get; set; }

    private readonly ChoiceCardGridLayout grid = new();

    static AlderCheckboxButtonGroup()
    {
        ModeProperty.OverrideMetadata<AlderCheckboxButtonGroup>(
            new StyledPropertyMetadata<ChoiceMode>(ChoiceMode.Multiple, coerce: (_, _) => ChoiceMode.Multiple));
        AffectsMeasure<AlderCheckboxButtonGroup>(LayoutProperty, ColumnsProperty, MinColumnWidthProperty);
    }

    /// <summary>Initializes the group with detached, vertically arranged cards.</summary>
    public AlderCheckboxButtonGroup()
    {
        Orientation = Orientation.Vertical;
        Spacing = 12;
    }

    /// <inheritdoc />
    protected override bool IsSelectionReadOnly => IsReadOnly;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => Layout == CheckboxButtonGroupLayout.Grid
        ? grid.Measure(Children, availableSize, Columns, MinColumnWidth, Spacing)
        : base.MeasureOverride(availableSize);

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize) => Layout == CheckboxButtonGroupLayout.Grid
        ? grid.Arrange(Children, finalSize, Spacing)
        : base.ArrangeOverride(finalSize);
}
