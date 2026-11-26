using Avalonia;
using Avalonia.Controls;

namespace Alder.Avalonia.Controls;

/// <summary>Controls which details are shown in a conversation row.</summary>
public enum ChatListDensity
{
    /// <summary>Shows the title, preview and trailing metadata when supplied.</summary>
    Comfortable,
    /// <summary>Shows only the title and leading icon for a compact sidebar.</summary>
    Compact
}

/// <summary>A virtualized conversation history supporting selection, activation and per-row actions.</summary>
public sealed partial class AlderChatListView : AlderListView
{
    static AlderChatListView()
    {
        VariantProperty.OverrideDefaultValue<AlderChatListView>(ListViewVariant.Secondary);
        ShowSelectionIndicatorProperty.OverrideDefaultValue<AlderChatListView>(false);
    }

    /// <summary>Changes the density of existing and newly realized rows.</summary>
    [GeneratedStyledProperty] public partial ChatListDensity Density { get; set; }

    /// <inheritdoc/>
    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        ApplyRowLayout(container, index);
    }

    /// <inheritdoc/>
    protected override void ContainerIndexChangedOverride(Control container, int oldIndex, int newIndex)
    {
        base.ContainerIndexChangedOverride(container, oldIndex, newIndex);
        ApplyRowLayout(container, newIndex);
    }

    /// <inheritdoc/>
    protected override void ClearContainerForItemOverride(Control container)
    {
        container.Classes.Set("chat-compact", false);
        container.Classes.Set("chat-last-row", false);
        base.ClearContainerForItemOverride(container);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemCountProperty || change.Property == DensityProperty)
            foreach (var container in GetRealizedContainers())
                ApplyRowLayout(container, IndexFromContainer(container));
    }

    private void ApplyRowLayout(Control container, int index)
    {
        container.Classes.Set("chat-compact", Density == ChatListDensity.Compact);
        // The final realized row may be followed by virtualized items.
        container.Classes.Set("chat-last-row", index >= 0 && index == ItemCount - 1);
    }
}
