using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Selects the surface used by a tag.</summary>
public enum TagVariant
{
    /// <summary>Uses the neutral surface.</summary>
    Default,
    /// <summary>Uses the theme's standard surface.</summary>
    Surface
}

/// <summary>Identifies the tag and source item the user requested to remove.</summary>
public sealed class TagRemoveRequestedEventArgs : RoutedEventArgs
{
    internal TagRemoveRequestedEventArgs(AlderTag tag, object? item, int index) : base(AlderTag.RemoveRequestedEvent)
    {
        Tag = tag;
        Item = item;
        Index = index;
    }

    /// <summary>Gets the tag that initiated the request.</summary>
    public AlderTag Tag { get; }
    /// <summary>Gets the source item, or the tag itself for standalone and inline tags.</summary>
    public object? Item { get; }
    /// <summary>Gets the index at the time of the request, or -1 for a standalone tag.</summary>
    public int Index { get; }
}

/// <summary>A compact label with optional icon and removal, selectable when hosted in a tag group.</summary>
public sealed class AlderTag : ListBoxItem
{
    /// <summary>Identifies the <see cref="Size"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderSize> SizeProperty =
        AvaloniaProperty.Register<AlderTag, AlderSize>(nameof(Size), AlderSize.Medium, inherits: true);
    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly StyledProperty<TagVariant> VariantProperty =
        AvaloniaProperty.Register<AlderTag, TagVariant>(nameof(Variant), inherits: true);
    /// <summary>Identifies the <see cref="Icon"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> IconProperty = AlderChip.IconProperty.AddOwner<AlderTag>();
    /// <summary>Identifies the <see cref="IsRemovable"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsRemovableProperty =
        AvaloniaProperty.Register<AlderTag, bool>(nameof(IsRemovable), inherits: true);
    /// <summary>Identifies the <see cref="RemoveButtonLabel"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> RemoveButtonLabelProperty =
        AvaloniaProperty.Register<AlderTag, string?>(nameof(RemoveButtonLabel), "Remove tag", inherits: true);
    /// <summary>Identifies the bubbling <see cref="RemoveRequested"/> event.</summary>
    public static readonly RoutedEvent<TagRemoveRequestedEventArgs> RemoveRequestedEvent =
        RoutedEvent.Register<AlderTag, TagRemoveRequestedEventArgs>(nameof(RemoveRequested), RoutingStrategies.Bubble);

    private Button? removeButton;

    static AlderTag() => FocusableProperty.OverrideDefaultValue<AlderTag>(false);

    /// <summary>Gets or sets the size preset.</summary>
    public AlderSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>Gets or sets the surface treatment.</summary>
    public TagVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>Gets or sets the optional leading icon.</summary>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    /// <summary>Gets or sets whether the tag offers a remove button and keyboard removal.</summary>
    public bool IsRemovable { get => GetValue(IsRemovableProperty); set => SetValue(IsRemovableProperty, value); }
    /// <summary>Gets or sets the accessible name of the remove button.</summary>
    public string? RemoveButtonLabel { get => GetValue(RemoveButtonLabelProperty); set => SetValue(RemoveButtonLabelProperty, value); }

    /// <summary>Occurs when the user requests removal. Handle this event to update the source collection.</summary>
    public event EventHandler<TagRemoveRequestedEventArgs>? RemoveRequested
    {
        add => AddHandler(RemoveRequestedEvent, value);
        remove => RemoveHandler(RemoveRequestedEvent, value);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TagAutomationPeer(this);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (removeButton is not null)
            removeButton.Click -= OnRemoveClick;

        base.OnApplyTemplate(e);
        removeButton = e.NameScope.Find<Button>("PART_RemoveButton");
        if (removeButton is not null)
            removeButton.Click += OnRemoveClick;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && (e.Source == this || IsRemoveSource(e.Source)) &&
            e.Key is Key.Delete or Key.Back && RequestRemoval())
            e.Handled = true;
    }

    internal bool IsRemoveSource(object? source) => removeButton is not null && source is Visual visual &&
        (visual == removeButton || removeButton.IsVisualAncestorOf(visual));

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RequestRemoval();
    }

    private bool RequestRemoval()
    {
        if (!IsRemovable || !IsEffectivelyEnabled)
            return false;

        var group = ItemsControl.ItemsControlFromItemContainer(this) as AlderTagGroup;
        var index = group?.IndexFromContainer(this) ?? -1;
        var item = group is not null && index >= 0 ? group.ItemsView[index] : this;
        var args = new TagRemoveRequestedEventArgs(this, item, index);
        RaiseEvent(args);
        if (!args.Handled && group?.RemoveCommand is { } command && command.CanExecute(item))
            command.Execute(item);
        return true;
    }

    private sealed class TagAutomationPeer(AlderTag owner) : ListItemAutomationPeer(owner)
    {
        protected override string? GetNameCore() => base.GetNameCore() ?? owner.Content?.ToString();
        protected override AutomationControlType GetAutomationControlTypeCore() =>
            owner.Parent is AlderTagGroup ? AutomationControlType.ListItem : AutomationControlType.Text;

        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(ISelectionItemProvider) && owner.Parent is not AlderTagGroup { SelectionMode: not TagSelectionMode.None }
                ? null : base.GetProviderCore(providerType);
    }
}
