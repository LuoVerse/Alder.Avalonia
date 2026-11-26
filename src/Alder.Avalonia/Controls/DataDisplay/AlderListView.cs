using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>The surface treatment of a selectable list.</summary>
public enum ListViewVariant
{
    /// <summary>Rows sit on a common surface.</summary>
    Primary,
    /// <summary>Rows are presented on a transparent background.</summary>
    Secondary
}

/// <summary>A virtualized list with native selection, row activation and independent embedded actions.</summary>
public class AlderListView : ListBox
{
    /// <summary>Identifies the <see cref="Variant"/> property.</summary>
    public static readonly StyledProperty<ListViewVariant> VariantProperty =
        AvaloniaProperty.Register<AlderListView, ListViewVariant>(nameof(Variant));
    /// <summary>Identifies the <see cref="SelectionEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> SelectionEnabledProperty =
        AvaloniaProperty.Register<AlderListView, bool>(nameof(SelectionEnabled), true);
    /// <summary>Identifies the <see cref="ShowSelectionIndicator"/> property.</summary>
    public static readonly StyledProperty<bool> ShowSelectionIndicatorProperty =
        AvaloniaProperty.Register<AlderListView, bool>(nameof(ShowSelectionIndicator), true);
    /// <summary>Identifies the <see cref="ActivateCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> ActivateCommandProperty =
        AvaloniaProperty.Register<AlderListView, ICommand?>(nameof(ActivateCommand));
    /// <summary>Identifies the <see cref="IsItemEnabled"/> property.</summary>
    public static readonly StyledProperty<Func<object, bool>?> IsItemEnabledProperty =
        AvaloniaProperty.Register<AlderListView, Func<object, bool>?>(nameof(IsItemEnabled));

    /// <summary>Gets or sets the variant.</summary>
    public ListViewVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>Gets or sets the selection enabled.</summary>
    public bool SelectionEnabled { get => GetValue(SelectionEnabledProperty); set => SetValue(SelectionEnabledProperty, value); }
    /// <summary>Gets or sets the show selection indicator.</summary>
    public bool ShowSelectionIndicator { get => GetValue(ShowSelectionIndicatorProperty); set => SetValue(ShowSelectionIndicatorProperty, value); }
    /// <summary>Gets or sets the activate command.</summary>
    public ICommand? ActivateCommand { get => GetValue(ActivateCommandProperty); set => SetValue(ActivateCommandProperty, value); }
    /// <summary>Gets or sets the is item enabled.</summary>
    public Func<object, bool>? IsItemEnabled { get => GetValue(IsItemEnabledProperty); set => SetValue(IsItemEnabledProperty, value); }

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<AlderListViewItem>(item, out recycleKey);

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new AlderListViewItem();

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        container.Classes.Set("selection-indicator", SelectionEnabled && ShowSelectionIndicator);
        if (!ReferenceEquals(container, item) && item is not null && IsItemEnabled is { } canSelect)
            container.SetCurrentValue(IsEnabledProperty, canSelect(item));
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(Control container)
    {
        container.Classes.Set("selection-indicator", false);
        container.ClearValue(IsEnabledProperty);
        base.ClearContainerForItemOverride(container);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectionEnabledProperty && !SelectionEnabled) Selection.Clear();
        if (change.Property == SelectionEnabledProperty || change.Property == ShowSelectionIndicatorProperty)
            foreach (var container in GetRealizedContainers())
                container.Classes.Set("selection-indicator", SelectionEnabled && ShowSelectionIndicator);
        if (change.Property == IsItemEnabledProperty)
        {
            foreach (var container in GetRealizedContainers())
            {
                var index = IndexFromContainer(container);
                if (index >= 0 && ItemsView[index] is { } item && !ReferenceEquals(container, item))
                    container.SetCurrentValue(IsEnabledProperty, IsItemEnabled?.Invoke(item) ?? true);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (SelectionEnabled) base.OnKeyDown(e);
    }

    internal bool Activate(Control container)
    {
        int index = IndexFromContainer(container);
        if (index < 0 || !container.IsEffectivelyEnabled || ActivateCommand is not { } command) return false;
        var item = ItemsView[index];
        if (!command.CanExecute(item)) return false;
        command.Execute(item);
        return true;
    }
}

/// <summary>A selectable row with optional title, description, graphic and trailing action.</summary>
public sealed class AlderListViewItem : ListBoxItem
{
    /// <summary>Identifies the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AlderListViewItem, string?>(nameof(Title));
    /// <summary>Identifies the <see cref="Description"/> property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AlderListViewItem, string?>(nameof(Description));
    /// <summary>Identifies the <see cref="Icon"/> property.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<AlderListViewItem, object?>(nameof(Icon));
    /// <summary>Identifies the <see cref="Action"/> property.</summary>
    public static readonly StyledProperty<object?> ActionProperty =
        AvaloniaProperty.Register<AlderListViewItem, object?>(nameof(Action));

    /// <summary>Gets or sets the title.</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    /// <summary>Gets or sets the description.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>Gets or sets the icon.</summary>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    /// <summary>Gets or sets the action.</summary>
    public object? Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }

    private AlderListView? Owner => this.FindAncestorOfType<AlderListView>();

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!IsRowInput(e.Source)) return;
        if (e.ClickCount == 2 && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && Owner?.Activate(this) == true)
            e.Handled = true;
        else if (Owner?.SelectionEnabled == true) base.OnPointerPressed(e);
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (Owner?.SelectionEnabled == true && IsRowInput(e.Source)) base.OnPointerReleased(e);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsRowInput(e.Source)) return;
        if (e.Key == Key.Enter && Owner?.Activate(this) == true) e.Handled = true;
        else if (Owner?.SelectionEnabled == true) base.OnKeyDown(e);
    }

    private bool IsRowInput(object? source)
    {
        for (var visual = source as Visual; visual is not null && visual != this; visual = visual.GetVisualParent())
            if (visual is InputElement { Focusable: true }) return false;
        return true;
    }
}
