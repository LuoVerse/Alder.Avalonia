using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>A compact icon tab strip that opens a panel above the selected tab and permits an empty selection.</summary>
public sealed class AlderExpandableTabs : TabControl
{
    /// <summary>Identifies the <see cref="ExpandedMinWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> ExpandedMinWidthProperty =
        AvaloniaProperty.Register<AlderExpandableTabs, double>(nameof(ExpandedMinWidth), validate: IsValidMinimum);
    /// <summary>Identifies the <see cref="ExpandedMinHeight"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> ExpandedMinHeightProperty =
        AvaloniaProperty.Register<AlderExpandableTabs, double>(nameof(ExpandedMinHeight), validate: IsValidMinimum);

    private TopLevel? root;
    private AlderExpandableTabsShell? shell;
    private AlderExpandableTabsContent? body;

    static AlderExpandableTabs() => SelectionModeProperty.OverrideDefaultValue<AlderExpandableTabs>(SelectionMode.Toggle);

    /// <summary>Minimum outer width of the open shell, constrained by the space available.</summary>
    public double ExpandedMinWidth { get => GetValue(ExpandedMinWidthProperty); set => SetValue(ExpandedMinWidthProperty, value); }

    /// <summary>Minimum outer height of the open shell, constrained by the space available.</summary>
    public double ExpandedMinHeight { get => GetValue(ExpandedMinHeightProperty); set => SetValue(ExpandedMinHeightProperty, value); }

    private static bool IsValidMinimum(double size) => double.IsFinite(size) && size >= 0;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        shell = e.NameScope.Find<AlderExpandableTabsShell>("PART_ExpandableShell");
        body = e.NameScope.Find<AlderExpandableTabsContent>("PART_ExpandableContent");
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedContentProperty || change.Property == SelectedContentTemplateProperty)
            body?.InvalidateMeasure();
        if (change.Property == SelectedIndexProperty || change.Property == AlderMotion.ReducedProperty ||
            change.Property == ExpandedMinWidthProperty || change.Property == ExpandedMinHeightProperty)
            shell?.InvalidateMeasure();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        root = TopLevel.GetTopLevel(this);
        root?.AddHandler(PointerPressedEvent, DismissFromOutside, RoutingStrategies.Tunnel, handledEventsToo: true);
        root?.AddHandler(KeyDownEvent, DismissFromEscape, RoutingStrategies.Bubble);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        root?.RemoveHandler(PointerPressedEvent, DismissFromOutside);
        root?.RemoveHandler(KeyDownEvent, DismissFromEscape);
        root = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override bool ShouldTriggerSelection(Visual selectable, PointerEventArgs eventArgs) =>
        eventArgs is PointerReleasedEventArgs { InitialPressMouseButton: MouseButton.Left } &&
        new Rect(selectable.Bounds.Size).Contains(eventArgs.GetPosition(selectable));

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || e.Source is not Visual source) return;
        var tab = source as TabItem ?? source.FindAncestorOfType<TabItem>();
        if (tab is null || ItemsControlFromItemContainer(tab) != this) return;
        int index = IndexFromContainer(tab);

        if (e.Key is Key.Enter or Key.Space)
        {
            SetCurrentValue(SelectedIndexProperty, SelectedIndex == index ? -1 : index);
            e.Handled = true;
            return;
        }

        int direction = e.Key switch
        {
            Key.Left => FlowDirection == FlowDirection.RightToLeft ? 1 : -1,
            Key.Right => FlowDirection == FlowDirection.RightToLeft ? -1 : 1,
            Key.Home => 1,
            Key.End => -1,
            _ => 0
        };
        if (direction == 0 || ItemCount == 0) return;

        int candidate = e.Key switch { Key.Home => -1, Key.End => ItemCount, _ => index };
        for (int i = 0; i < ItemCount; i++)
        {
            candidate = (candidate + direction + ItemCount) % ItemCount;
            if (ContainerFromIndex(candidate) is not TabItem { IsEffectivelyEnabled: true, IsVisible: true } next) continue;
            SetCurrentValue(SelectedIndexProperty, candidate);
            next.Focus(NavigationMethod.Directional);
            next.BringIntoView();
            break;
        }
        e.Handled = true;
    }

    private void DismissFromOutside(object? sender, PointerPressedEventArgs e)
    {
        if (SelectedIndex >= 0 && e.Source is Visual source && source != this && !this.IsVisualAncestorOf(source))
            SetCurrentValue(SelectedIndexProperty, -1);
    }

    private void DismissFromEscape(object? sender, KeyEventArgs e)
    {
        if (SelectedIndex < 0 || e.Key != Key.Escape) return;
        if (IsKeyboardFocusWithin && ContainerFromIndex(SelectedIndex) is TabItem tab)
        {
            tab.Focus(NavigationMethod.Tab);
            e.Handled = true;
        }
        SetCurrentValue(SelectedIndexProperty, -1);
    }
}
