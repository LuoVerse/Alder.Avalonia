using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia;

namespace Alder.Avalonia.Controls;

/// <summary>Provides the native RepeatButton interaction contract for Alder templates.</summary>
public class AlderRepeatButton : RepeatButton
{
    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(RepeatButton);
}

/// <summary>Provides the native Thumb interaction contract for Alder templates.</summary>
public class AlderThumb : Thumb
{
    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(Thumb);
}

/// <summary>A scroll viewport with Alder scrollbars, retaining native scrolling and input support.</summary>
public class AlderScrollViewer : ScrollViewer;

/// <summary>A scrollbar whose track and handle are styled by Alder.</summary>
public class AlderScrollBar : ScrollBar;

/// <summary>Provides the native GridSplitter interaction contract for Alder templates.</summary>
public class AlderGridSplitter : GridSplitter
{
    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(GridSplitter);
}

/// <summary>A native selection list whose items and scroll viewport use Alder controls.</summary>
public class AlderListBox : ListBox
{
    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new AlderListBoxItem();
}

/// <summary>Provides the native ListBoxItem interaction contract for Alder templates.</summary>
public class AlderListBoxItem : ListBoxItem
{
    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(ListBoxItem);
}

/// <summary>A menu item with Alder styling and support for nested menu items.</summary>
public class AlderMenuItem : MenuItem
{
    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new AlderMenuItem();
}

/// <summary>Selectable text with Alder selection and copy-menu styling.</summary>
public class AlderSelectableTextBlock : SelectableTextBlock;

/// <summary>Provides the native TabItem interaction contract for Alder templates.</summary>
public class AlderTabItem : TabItem
{
    /// <summary>Identifies whether this tab can be closed.</summary>
    public static readonly StyledProperty<bool> CanCloseProperty =
        AvaloniaProperty.Register<AlderTabItem, bool>(nameof(CanClose), true);

    /// <summary>Whether this tab can be closed when the tabs control provides a close command.</summary>
    public bool CanClose { get => GetValue(CanCloseProperty); set => SetValue(CanCloseProperty, value); }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(TabItem);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CanCloseProperty) PseudoClasses.Set(":pinned", !CanClose);
    }
}

/// <summary>Plain text for composing Alder controls with inherited typography.</summary>
public class AlderTextBlock : TextBlock
{
    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(TextBlock);
}

/// <summary>An image element for Alder content templates.</summary>
public class AlderImage : Image
{
    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(Image);
}

/// <summary>A vector geometry icon that inherits the current Alder foreground.</summary>
public class AlderPathIcon : PathIcon
{
    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(PathIcon);
}

/// <summary>A content host that keeps the standard content presenter contract.</summary>
public class AlderContentControl : ContentControl
{
    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(ContentControl);
}

/// <summary>An item template host for unselectable Alder collections.</summary>
public class AlderItemsControl : ItemsControl
{
    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(ItemsControl);
}

/// <summary>The popup host used by Alder's anchored overlays.</summary>
public class AlderPopup : Popup
{
    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(Popup);
}
