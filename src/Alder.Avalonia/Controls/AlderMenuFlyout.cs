using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;

namespace Alder.Avalonia.Controls;

/// <summary>An anchored menu flyout with Alder styling and entrance and exit motion.</summary>
public class AlderMenuFlyout : MenuFlyout
{
    private readonly MenuPopupMotion motion = new();
    internal PlacementMode? MotionPlacement { get; set; }

    /// <summary>Initializes a new instance of <see cref="AlderMenuFlyout"/>.</summary>
    public AlderMenuFlyout()
    {
        Popup.ShouldUseOverlayLayer = true;
        Popup.WindowManagerAddShadowHint = false;
        Items.CollectionChanged += (_, _) => UpdatePresenterItems();
    }

    internal void PreparePresenter(Control? owner = null, object? dataContext = null)
    {
        var presenter = Popup.Child ??= CreatePresenter();
        if (dataContext is not null && presenter.DataContext != dataContext)
            presenter.DataContext = dataContext;
        if (owner is not null) PrepareLayout(Popup, presenter, owner);
    }

    internal static void PrepareLayout(Popup popup, Control presenter, Control owner)
    {
        if (popup.Parent is not null || presenter.DesiredSize != default || !((ILogical)owner).IsAttachedToLogicalTree) return;
        var scope = (ISetLogicalParent)popup;
        scope.SetParent(owner);
        try { presenter.Measure(Size.Infinity); }
        finally { scope.SetParent(null); }
    }

    /// <inheritdoc />
    protected override Control CreatePresenter()
    {
        var presenter = new AlderMenuFlyoutPresenter
        {
            ItemsSource = Items.Source,
            Theme = FlyoutPresenterTheme,
            [!ItemsControl.ItemTemplateProperty] = this[!ItemTemplateProperty]
        };
        ApplyItemTheme(presenter);
        return presenter;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemContainerThemeProperty && Popup.Child is ItemsControl presenter)
            ApplyItemTheme(presenter);
        else if (change.Property == FlyoutPresenterThemeProperty && Popup.Child is { } child)
            child.Theme = FlyoutPresenterTheme;
        else if (change.Property == ItemsSourceProperty)
            UpdatePresenterItems();
    }

    private void UpdatePresenterItems()
    {
        // The first insertion and changes to ItemsSource can replace Avalonia's backing list.
        if (Popup.Child is AlderMenuFlyoutPresenter presenter && !ReferenceEquals(presenter.ItemsSource, Items.Source))
            presenter.ItemsSource = Items.Source;
    }

    private void ApplyItemTheme(ItemsControl presenter)
    {
        if (ItemContainerTheme is { } theme)
            presenter.ItemContainerTheme = theme;
        else
            presenter.ClearValue(ItemsControl.ItemContainerThemeProperty);
    }

    /// <inheritdoc />
    protected override bool ShowAtCore(Control placementTarget, bool showAtPointer = false)
    {
        using var rowActionInput = showAtPointer ? null : AlderDataGrid.KeepRowActionPointerInput(placementTarget, this);
        return base.ShowAtCore(placementTarget, showAtPointer);
    }

    /// <inheritdoc />
    protected override void OnOpened()
    {
        if (Target is { } target && Popup.Child is { } presenter)
            motion.Opened(Popup, presenter, target, preferredPlacement: MotionPlacement);
        base.OnOpened();
    }

    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs args)
    {
        if (motion.IsClosing)
        {
            motion.Closing(args);
            return;
        }
        base.OnClosing(args);
        motion.Closing(args);
    }

    /// <inheritdoc />
    protected override void OnClosed()
    {
        motion.Closed();
        base.OnClosed();
    }
}

internal sealed class AlderMenuFlyoutPresenter : MenuFlyoutPresenter
{
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new AlderMenuItem();
}
