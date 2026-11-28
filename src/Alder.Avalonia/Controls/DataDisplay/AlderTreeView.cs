using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Alder.Avalonia.Controls;

/// <summary>Displays and selects hierarchical data using Avalonia tree templates and Alder styling.</summary>
public sealed class AlderTreeView : TreeView
{
    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new AlderTreeViewItem();
}

/// <summary>An explicitly declared or generated item in an <see cref="AlderTreeView"/>.</summary>
public sealed class AlderTreeViewItem : TreeViewItem
{
    private Control? subtree;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        subtree = e.NameScope.Find<Control>("Subtree");
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == IsExpandedProperty && !IsExpanded && subtree?.IsKeyboardFocusWithin == true)
            Focus(NavigationMethod.Tab);
        base.OnPropertyChanged(change);
    }
}
