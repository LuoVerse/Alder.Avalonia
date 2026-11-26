using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>A path of links whose last visible item represents the current page.</summary>
public sealed class AlderBreadcrumbs : ItemsControl
{
    /// <summary>Identifies the <see cref="Separator"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> SeparatorProperty =
        AvaloniaProperty.Register<AlderBreadcrumbs, string?>(nameof(Separator), inherits: true);
    /// <summary>Identifies the <see cref="SeparatorIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> SeparatorIconProperty =
        AvaloniaProperty.Register<AlderBreadcrumbs, Geometry?>(nameof(SeparatorIcon), inherits: true);

    private readonly Dictionary<AlderBreadcrumbItem, int> items = [];

    /// <summary>The default content displayed between adjacent breadcrumbs.</summary>
    public string? Separator { get => GetValue(SeparatorProperty); set => SetValue(SeparatorProperty, value); }
    /// <summary>The geometry used for the separator icon.</summary>
    public Geometry? SeparatorIcon { get => GetValue(SeparatorIconProperty); set => SetValue(SeparatorIconProperty, value); }

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<AlderBreadcrumbItem>(item, out recycleKey);

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new AlderBreadcrumbItem();

    /// <inheritdoc />
    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        var crumb = (AlderBreadcrumbItem)container;
        items.Add(crumb, index);
        crumb.PropertyChanged += OnCrumbChanged;
        UpdatePath();
    }

    /// <inheritdoc />
    protected override void ContainerIndexChangedOverride(Control container, int oldIndex, int newIndex)
    {
        base.ContainerIndexChangedOverride(container, oldIndex, newIndex);
        items[(AlderBreadcrumbItem)container] = newIndex;
        UpdatePath();
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(Control container)
    {
        RemoveCrumb((AlderBreadcrumbItem)container);
        base.ClearContainerForItemOverride(container);
    }

    private void OnCrumbChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        var crumb = (AlderBreadcrumbItem)sender!;
        if (change.Property == ParentProperty && crumb.Parent != this)
            RemoveCrumb(crumb);
        else if (change.Property == IsVisibleProperty)
            UpdatePath();
    }

    private void RemoveCrumb(AlderBreadcrumbItem crumb)
    {
        if (!items.Remove(crumb)) return;
        crumb.PropertyChanged -= OnCrumbChanged;
        crumb.SetPathState(false, false);
        UpdatePath();
    }

    private void UpdatePath()
    {
        var last = items.Keys.Where(item => item.IsVisible).MaxBy(item => items[item]);
        foreach (var crumb in items.Keys)
            crumb.SetPathState(crumb == last, crumb.IsVisible && crumb != last);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new BreadcrumbsPeer(this);

    private sealed class BreadcrumbsPeer(AlderBreadcrumbs owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}

/// <summary>Uses a native command or NavigateUri to navigate to an ancestor in the path.</summary>
public sealed class AlderBreadcrumbItem : HyperlinkButton
{
    /// <summary>Identifies the <see cref="IsCurrent"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderBreadcrumbItem, bool> IsCurrentProperty =
        AvaloniaProperty.RegisterDirect<AlderBreadcrumbItem, bool>(nameof(IsCurrent), item => item.IsCurrent);
    /// <summary>Identifies the <see cref="Separator"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> SeparatorProperty = AlderBreadcrumbs.SeparatorProperty.AddOwner<AlderBreadcrumbItem>();
    /// <summary>Identifies the <see cref="SeparatorIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> SeparatorIconProperty = AlderBreadcrumbs.SeparatorIconProperty.AddOwner<AlderBreadcrumbItem>();

    private bool current;

    /// <summary>Whether this item represents the current page and is rendered as the final destination.</summary>
    public bool IsCurrent => current;
    /// <summary>An override for the content between this item and the next breadcrumb.</summary>
    public string? Separator { get => GetValue(SeparatorProperty); set => SetValue(SeparatorProperty, value); }
    /// <summary>The geometry used for the separator icon.</summary>
    public Geometry? SeparatorIcon { get => GetValue(SeparatorIconProperty); set => SetValue(SeparatorIconProperty, value); }

    /// <inheritdoc />
    protected override bool IsEnabledCore => !IsCurrent && base.IsEnabledCore;

    internal void SetPathState(bool isCurrent, bool showSeparator)
    {
        if (SetAndRaise(IsCurrentProperty, ref current, isCurrent))
        {
            PseudoClasses.Set(":current", isCurrent);
            UpdateIsEffectivelyEnabled();
        }
        Classes.Set("with-separator", showSeparator);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new BreadcrumbItemPeer(this);

    private sealed class BreadcrumbItemPeer(AlderBreadcrumbItem owner) : ButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() =>
            owner.IsCurrent ? AutomationControlType.Text : AutomationControlType.Hyperlink;
    }
}
