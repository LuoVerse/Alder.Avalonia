using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Controls which dimension is preserved when the resizable group changes size.</summary>
public enum ResizableGroupResizeBehavior
{
    /// <summary>Preserves the panel's share of the group.</summary>
    PreserveRelativeSize,
    /// <summary>Preserves the panel's size in device-independent pixels.</summary>
    PreservePixelSize
}

/// <summary>A content pane within an <see cref="AlderResizable"/> group.</summary>
public sealed partial class AlderResizablePanel : ContentControl
{
    /// <summary>Identifies the <see cref="DefaultSize"/> Avalonia property.</summary>
    public static readonly StyledProperty<GridLength> DefaultSizeProperty =
        AvaloniaProperty.Register<AlderResizablePanel, GridLength>(nameof(DefaultSize), GridLength.Auto, validate: IsValidSize);

    /// <summary>The initial size. An absolute value is in DIPs, a star value is a percentage, and Auto shares the remaining space.</summary>
    public GridLength DefaultSize { get => GetValue(DefaultSizeProperty); set => SetValue(DefaultSizeProperty, value); }

    /// <summary>The requested expanded size. Dragging or using the keyboard writes the current size back to this binding.</summary>
    /// <remarks>A null request uses <see cref="DefaultSize"/>. <see cref="GroupResizeBehavior"/> determines the stored unit.</remarks>
    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay, ValidateMethodName = nameof(IsValidRequestedSize))]
    public partial GridLength? Size { get; set; }

    /// <summary>The minimum expanded size, expressed in DIPs or as a star percentage.</summary>
    [GeneratedStyledProperty(ValidateMethodName = nameof(IsValidSize))]
    public partial GridLength MinSize { get; set; }

    /// <summary>Identifies the <see cref="MaxSize"/> Avalonia property.</summary>
    public static readonly StyledProperty<GridLength> MaxSizeProperty =
        AvaloniaProperty.Register<AlderResizablePanel, GridLength>(nameof(MaxSize), GridLength.Auto, validate: IsValidSize);

    /// <summary>The maximum expanded size, expressed in DIPs or as a star percentage. Auto allows the full available space.</summary>
    public GridLength MaxSize { get => GetValue(MaxSizeProperty); set => SetValue(MaxSizeProperty, value); }

    /// <summary>Allows shrinking past the minimum expanded size to <see cref="CollapsedSize"/>.</summary>
    [GeneratedStyledProperty]
    public partial bool IsCollapsible { get; set; }

    /// <summary>The size used while collapsed, expressed in DIPs or as a star percentage.</summary>
    [GeneratedStyledProperty(ValidateMethodName = nameof(IsValidSize))]
    public partial GridLength CollapsedSize { get; set; }

    /// <summary>The current collapsed state. The last expanded size is retained while collapsed.</summary>
    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial bool IsCollapsed { get; set; }

    /// <summary>Controls whether interactive resizing stores a relative or an absolute size.</summary>
    [GeneratedStyledProperty]
    public partial ResizableGroupResizeBehavior GroupResizeBehavior { get; set; }

    internal AlderResizable? Group { get; private set; }
    internal double InitialSpace { get; set; }
    internal bool Collapsed => IsCollapsible && IsCollapsed;
    internal bool KeepsPixels => GroupResizeBehavior == ResizableGroupResizeBehavior.PreservePixelSize;
    internal GridLength RequestedSize => Size ?? DefaultSize;

    private static bool IsValidSize(GridLength size) => double.IsFinite(size.Value);
    private static bool IsValidRequestedSize(GridLength? size) => size is null || IsValidSize(size.Value);

    /// <summary>Collapses the pane if collapsing is enabled.</summary>
    public void Collapse()
    {
        if (IsCollapsible) SetCurrentValue(IsCollapsedProperty, true);
    }

    /// <summary>Restores the pane's previous expanded size.</summary>
    public void Expand() => SetCurrentValue(IsCollapsedProperty, false);

    internal void SetGroup(AlderResizable group)
    {
        if (Group == group) return;
        Group = group;
        InitialSpace = 0;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Group = null;
        InitialSpace = 0;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DefaultSizeProperty || change.Property == GroupResizeBehaviorProperty)
            InitialSpace = 0;
        if (change.Property == IsCollapsibleProperty || change.Property == IsCollapsedProperty ||
            change.Property == CollapsedSizeProperty)
            PseudoClasses.Set(":collapsed-empty", Collapsed && (CollapsedSize.IsAuto || CollapsedSize.Value == 0));
        if (change.Property == SizeProperty || change.Property == DefaultSizeProperty ||
            change.Property == MinSizeProperty || change.Property == MaxSizeProperty ||
            change.Property == IsCollapsibleProperty || change.Property == IsCollapsedProperty ||
            change.Property == CollapsedSizeProperty || change.Property == GroupResizeBehaviorProperty)
            Group?.InvalidateMeasure();
    }
}
