using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NativeSelectionMode = Avalonia.Controls.SelectionMode;

namespace Alder.Avalonia.Controls;

/// <summary>Controls how user input changes the selection in a tag group.</summary>
public enum TagSelectionMode
{
    /// <summary>Disables selection by pointer and keyboard.</summary>
    None,
    /// <summary>Allows one tag to be selected at a time.</summary>
    Single,
    /// <summary>Allows tags to be toggled independently.</summary>
    Multiple
}

/// <summary>A wrapping collection of tags with optional selection and removal.</summary>
public sealed class AlderTagGroup : ListBox
{
    /// <summary>Identifies the <see cref="SelectionMode"/> Avalonia property.</summary>
    public static readonly new StyledProperty<TagSelectionMode> SelectionModeProperty =
        AvaloniaProperty.Register<AlderTagGroup, TagSelectionMode>(nameof(SelectionMode));
    /// <summary>Identifies the <see cref="Size"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderSize> SizeProperty = AlderTag.SizeProperty.AddOwner<AlderTagGroup>();
    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly StyledProperty<TagVariant> VariantProperty = AlderTag.VariantProperty.AddOwner<AlderTagGroup>();
    /// <summary>Identifies the <see cref="IsRemovable"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsRemovableProperty = AlderTag.IsRemovableProperty.AddOwner<AlderTagGroup>();
    /// <summary>Identifies the <see cref="RemoveButtonLabel"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> RemoveButtonLabelProperty = AlderTag.RemoveButtonLabelProperty.AddOwner<AlderTagGroup>();
    /// <summary>Identifies the <see cref="Label"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<AlderTagGroup, string?>(nameof(Label));
    /// <summary>Identifies the <see cref="Description"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<AlderTagGroup, string?>(nameof(Description));
    /// <summary>Identifies the <see cref="EmptyContent"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> EmptyContentProperty = AvaloniaProperty.Register<AlderTagGroup, object?>(nameof(EmptyContent));
    /// <summary>Identifies the <see cref="RemoveCommand"/> Avalonia property.</summary>
    public static readonly StyledProperty<ICommand?> RemoveCommandProperty = AvaloniaProperty.Register<AlderTagGroup, ICommand?>(nameof(RemoveCommand));

    static AlderTagGroup()
    {
        ListBox.SelectionModeProperty.OverrideDefaultValue<AlderTagGroup>(NativeSelectionMode.Toggle);
        AutoScrollToSelectedItemProperty.OverrideDefaultValue<AlderTagGroup>(false);
        FocusableProperty.OverrideDefaultValue<AlderTagGroup>(true);
        KeyboardNavigation.IsTabStopProperty.OverrideDefaultValue<AlderTagGroup>(false);
    }

    /// <summary>Initializes a new tag group.</summary>
    public AlderTagGroup() => ItemsView.CollectionChanged += OnItemsChanged;

    /// <summary>Gets or sets how user input changes the selection. Defaults to <see cref="TagSelectionMode.None"/>.</summary>
    public new TagSelectionMode SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }
    /// <summary>Gets or sets the size inherited by tags in the group.</summary>
    public AlderSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>Gets or sets the surface treatment inherited by tags in the group.</summary>
    public TagVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>Gets or sets whether tags offer removal by default.</summary>
    public bool IsRemovable { get => GetValue(IsRemovableProperty); set => SetValue(IsRemovableProperty, value); }
    /// <summary>Gets or sets the accessible name inherited by the remove buttons.</summary>
    public string? RemoveButtonLabel { get => GetValue(RemoveButtonLabelProperty); set => SetValue(RemoveButtonLabelProperty, value); }
    /// <summary>Gets or sets the text displayed above the tags.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>Gets or sets the supporting text displayed below the tags.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>Gets or sets the content displayed when the group has no tags.</summary>
    public object? EmptyContent { get => GetValue(EmptyContentProperty); set => SetValue(EmptyContentProperty, value); }
    /// <summary>Gets or sets the command that receives the source item when removal is requested.</summary>
    public ICommand? RemoveCommand { get => GetValue(RemoveCommandProperty); set => SetValue(RemoveCommandProperty, value); }

    /// <summary>Occurs before the remove command. Mark the event handled when it takes care of the request.</summary>
    public event EventHandler<TagRemoveRequestedEventArgs>? RemoveRequested
    {
        add => AddHandler(AlderTag.RemoveRequestedEvent, value);
        remove => RemoveHandler(AlderTag.RemoveRequestedEvent, value);
    }

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<AlderTag>(item, out recycleKey);

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new AlderTag();

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TagGroupAutomationPeer(this);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectionModeProperty)
        {
            base.SelectionMode = SelectionMode == TagSelectionMode.Multiple
                ? NativeSelectionMode.Multiple | NativeSelectionMode.Toggle
                : NativeSelectionMode.Toggle;
            if (SelectionMode == TagSelectionMode.None)
                Selection.Clear();
        }
    }

    /// <inheritdoc />
    public override bool UpdateSelectionFromEvent(Control container, RoutedEventArgs eventArgs)
    {
        if (SelectionMode == TagSelectionMode.None || container is not AlderTag { IsEffectivelyEnabled: true } tag ||
            tag.IsRemoveSource(eventArgs.Source) || eventArgs is FocusChangedEventArgs ||
            eventArgs is PointerEventArgs { Properties.PointerUpdateKind: PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased })
            return false;

        return base.UpdateSelectionFromEvent(container, eventArgs);
    }

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (FindTag(e.Source) is { } tag)
            KeyboardNavigation.SetTabOnceActiveElement(this, tag);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || FindTag(e.Source) is not { } tag || (e.Source != tag && !tag.IsRemoveSource(e.Source)))
            return;

        var index = IndexFromContainer(tag);
        var rtl = FlowDirection == FlowDirection.RightToLeft;
        var next = e.Key switch
        {
            Key.Home => FindEnabledTag(0, 1),
            Key.End => FindEnabledTag(ItemCount - 1, -1),
            Key.Left => FindEnabledTag(index + (rtl ? 1 : -1), rtl ? 1 : -1),
            Key.Right => FindEnabledTag(index + (rtl ? -1 : 1), rtl ? -1 : 1),
            Key.Up => FindAdjacentRowTag(tag, down: false),
            Key.Down => FindAdjacentRowTag(tag, down: true),
            _ => null
        };

        if (next?.Focus(NavigationMethod.Directional) == true)
            e.Handled = true;
    }

    private AlderTag? FindTag(object? source)
    {
        var tag = (source as Visual)?.FindAncestorOfType<AlderTag>(includeSelf: true);
        return tag is not null && IndexFromContainer(tag) >= 0 ? tag : null;
    }

    private static bool CanFocusTag(Control? control) =>
        control is AlderTag { Focusable: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true };

    private AlderTag? FindEnabledTag(int start, int step)
    {
        for (var index = start; index >= 0 && index < ItemCount; index += step)
        {
            if (ContainerFromIndex(index) is AlderTag tag && CanFocusTag(tag))
                return tag;
        }

        return null;
    }

    private AlderTag? FindAdjacentRowTag(AlderTag current, bool down)
    {
        var bounds = current.Bounds;
        return GetRealizedContainers().OfType<AlderTag>().Where(CanFocusTag)
            .Where(tag => down ? tag.Bounds.Top >= bounds.Bottom : tag.Bounds.Bottom <= bounds.Top)
            .OrderBy(tag => down ? tag.Bounds.Top : -tag.Bounds.Bottom)
            .ThenBy(tag => Math.Abs(tag.Bounds.Center.X - bounds.Center.X))
            .FirstOrDefault();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is not (NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace or NotifyCollectionChangedAction.Reset) ||
            TopLevel.GetTopLevel(this)?.FocusManager is not { } focusManager ||
            FindTag(focusManager.GetFocusedElement()) is not { } tag)
            return;

        var index = IndexFromContainer(tag);
        Dispatcher.UIThread.Post(() =>
        {
            if (!this.IsAttachedToVisualTree() || IndexFromContainer(tag) >= 0 ||
                (focusManager.GetFocusedElement() is Visual focused && focused != this && !this.IsVisualAncestorOf(focused)))
                return;

            var neighbor = FindEnabledTag(index, 1) ?? FindEnabledTag(Math.Min(index - 1, ItemCount - 1), -1);
            (neighbor as Control ?? this).Focus(NavigationMethod.Directional);
        }, DispatcherPriority.Loaded);
    }

    private sealed class TagGroupAutomationPeer(AlderTagGroup owner) : ListBoxAutomationPeer(owner)
    {
        protected override string? GetNameCore() => base.GetNameCore() ?? owner.Label;
        protected override string? GetHelpTextCore() => base.GetHelpTextCore() ?? owner.Description;
        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(ISelectionProvider) && owner.SelectionMode == TagSelectionMode.None
                ? null : base.GetProviderCore(providerType);
    }
}
