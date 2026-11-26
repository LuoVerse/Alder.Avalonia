using Avalonia;
using Avalonia.Controls;

namespace Alder.Avalonia.Controls;

/// <summary>Specifies whether the accordion uses a plain layout or a containing surface.</summary>
public enum AccordionVariant
{
    /// <summary>Uses the standard accordion treatment.</summary>
    Default,
    /// <summary>Wraps the accordion items in a containing surface.</summary>
    Surface
}

/// <summary>Coordinates accordion items and supplies the accordion's surface and separators.</summary>
public sealed class AlderAccordion : AlderDisclosureGroup
{
    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly StyledProperty<AccordionVariant> VariantProperty =
        AvaloniaProperty.Register<AlderAccordion, AccordionVariant>(nameof(Variant));
    /// <summary>Identifies the <see cref="HideSeparator"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> HideSeparatorProperty =
        AvaloniaProperty.Register<AlderAccordion, bool>(nameof(HideSeparator));

    /// <summary>The visual treatment applied to the control.</summary>
    public AccordionVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>Whether the accordion omits dividers between items.</summary>
    public bool HideSeparator { get => GetValue(HideSeparatorProperty); set => SetValue(HideSeparatorProperty, value); }

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<AlderAccordionItem>(item, out recycleKey);

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new AlderAccordionItem();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VariantProperty || change.Property == HideSeparatorProperty)
            OnLayoutItemsChanged();
    }

    /// <inheritdoc />
    protected override void OnLayoutItemsChanged()
    {
        var last = DisclosureItems.Keys.Where(item => item.IsVisible).MaxBy(item => DisclosureItems[item]);
        foreach (var item in DisclosureItems.Keys)
        {
            item.Classes.Set("surface", Variant == AccordionVariant.Surface);
            item.Classes.Set("with-separator", !HideSeparator && item.IsVisible && item != last);
        }
    }

    /// <inheritdoc />
    protected override void OnItemRemoved(AlderDisclosure item)
    {
        item.Classes.Remove("with-separator");
        item.Classes.Remove("surface");
    }
}

/// <summary>A disclosure styled as an accordion row.</summary>
public sealed class AlderAccordionItem : AlderDisclosure;
