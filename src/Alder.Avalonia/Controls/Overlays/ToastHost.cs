using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Manages toast stacks for a top level. Register it after the window template has been applied.</summary>
public sealed class ToastHost : Panel
{
    internal const int DefaultMaxVisible = 3;

    /// <summary>Identifies the <see cref="Placement"/> Avalonia property.</summary>
    public static readonly StyledProperty<ToastPlacement> PlacementProperty =
        AvaloniaProperty.Register<ToastHost, ToastPlacement>(nameof(Placement), ToastPlacement.Bottom);
    /// <summary>Identifies the <see cref="IsExpanded"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ToastHost, bool>(nameof(IsExpanded));
    /// <summary>Identifies the <see cref="MaxVisible"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> MaxVisibleProperty =
        AvaloniaProperty.Register<ToastHost, int>(nameof(MaxVisible), DefaultMaxVisible, validate: value => value > 0);
    /// <summary>Identifies the <see cref="SafePadding"/> Avalonia property.</summary>
    public static readonly StyledProperty<Thickness> SafePaddingProperty =
        AvaloniaProperty.Register<ToastHost, Thickness>(nameof(SafePadding), new Thickness(16));

    /// <summary>Identifies the <see cref="ToastWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> ToastWidthProperty =
        AvaloniaProperty.Register<ToastHost, double>(nameof(ToastWidth), 460,
            validate: value => double.IsFinite(value) && value > 0);
    /// <summary>Identifies the <see cref="Gap"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ToastHost, double>(nameof(Gap), 12,
            validate: value => double.IsFinite(value) && value >= 0);
    /// <summary>Identifies the <see cref="StackScaleFactor"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> StackScaleFactorProperty =
        AvaloniaProperty.Register<ToastHost, double>(nameof(StackScaleFactor), .05,
            validate: value => double.IsFinite(value) && value >= 0 && value < 1);

    private readonly Dictionary<ToastPlacement, ToastStack> stacks = [];

    static ToastHost() => AffectsMeasure<ToastHost>(SafePaddingProperty, ToastWidthProperty);

    private ToastHost(TopLevel owner, string? id)
    {
        HostId = id;
        Name = "AlderToastHost";
        Bind(FlowDirectionProperty, owner.GetBindingObservable(FlowDirectionProperty));
    }

    /// <summary>The identifier used to resolve the host within its top level.</summary>
    public string? HostId { get; }
    /// <summary>The default position for toasts displayed by this host.</summary>
    public ToastPlacement Placement { get => GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }
    /// <summary>Whether notifications are currently shown as an expanded stack.</summary>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    /// <summary>
    /// The maximum number of notifications in each placement's stack. Defaults to three.
    /// Adding a notification beyond this limit or lowering the limit evicts the oldest notifications.
    /// </summary>
    public int MaxVisible { get => GetValue(MaxVisibleProperty); set => SetValue(MaxVisibleProperty, value); }
    /// <summary>The width reserved for each notification.</summary>
    public double ToastWidth { get => GetValue(ToastWidthProperty); set => SetValue(ToastWidthProperty, value); }
    /// <summary>The spacing between items when the stack is expanded.</summary>
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }
    /// <summary>The scale change applied at each depth of the collapsed stack.</summary>
    public double StackScaleFactor { get => GetValue(StackScaleFactorProperty); set => SetValue(StackScaleFactorProperty, value); }
    /// <summary>The padding between the notification stack and the host's edges.</summary>
    public Thickness SafePadding { get => GetValue(SafePaddingProperty); set => SetValue(SafePaddingProperty, value); }

    /// <summary>Gets or registers a notification host in an open top level's overlay layer.</summary>
    /// <param name="owner">The top level that will contain the notification host.</param>
    /// <param name="hostId">An optional name for the host.</param>
    public static ToastHost Register(TopLevel owner, string? hostId = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Dispatcher.UIThread.VerifyAccess();
        if (Find(owner, hostId) is { } registered) return registered;
        var overlay = OverlayLayer.GetOverlayLayer(owner) ??
            throw new InvalidOperationException("Register the toast host after the window has opened.");
        var host = new ToastHost(owner, hostId);
        overlay.Children.Add(host);
        return host;
    }

    internal static ToastHost? Find(TopLevel owner, string? hostId) => OverlayLayer.GetOverlayLayer(owner)?
        .Children.OfType<ToastHost>().FirstOrDefault(host => host.HostId == hostId);

    /// <summary>Shows a notification at the front of its stack, evicting the oldest if the stack is full.</summary>
    /// <param name="options">The notification to display.</param>
    public ToastHandle Show(ToastOptions options)
    {
        Dispatcher.UIThread.VerifyAccess();
        Toast.Validate(options);
        if (!this.IsAttachedToVisualTree()) throw new InvalidOperationException("The toast host is detached.");
        var placement = options.Placement ?? Placement;
        if (!stacks.TryGetValue(placement, out var stack))
        {
            stack = new ToastStack(this, placement);
            stacks.Add(placement, stack);
            Children.Add(stack);
        }
        return stack.Show(options);
    }

    /// <summary>Dismisses all notifications owned by this host.</summary>
    public void CloseAll()
    {
        Dispatcher.UIThread.VerifyAccess();
        Clear(ToastCloseReason.Dismissed);
    }

    /// <summary>Removes the host from its top level's overlay layer.</summary>
    public void Unregister()
    {
        Dispatcher.UIThread.VerifyAccess();
        this.FindAncestorOfType<OverlayLayer>()?.Children.Remove(this);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty || change.Property == GapProperty ||
            change.Property == StackScaleFactorProperty)
            foreach (var stack in stacks.Values) stack.InvalidateMeasure();
        else if (change.Property == MaxVisibleProperty)
            foreach (var stack in stacks.Values) stack.TrimToLimit();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Clear(ToastCloseReason.HostRemoved);
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var space = availableSize.Deflate(SafePadding);
        foreach (var stack in stacks.Values)
        {
            stack.SetCompact(availableSize.Width < 640 || ToastWidth <= 360);
            stack.Measure(new Size(Math.Min(ToastWidth, space.Width), space.Height));
        }
        return availableSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var area = new Rect(finalSize).Deflate(SafePadding);
        foreach (var (placement, stack) in stacks)
        {
            var size = stack.DesiredSize;
            var x = placement switch
            {
                ToastPlacement.TopStart or ToastPlacement.BottomStart => area.X,
                ToastPlacement.TopEnd or ToastPlacement.BottomEnd => area.Right - size.Width,
                _ => area.X + (area.Width - size.Width) / 2
            };
            var y = stack.IsTop ? area.Y : area.Bottom - size.Height;
            stack.ArrangeRegion(new Rect(new Point(x, y), size));
        }
        return finalSize;
    }

    private void Clear(ToastCloseReason reason)
    {
        foreach (var stack in stacks.Values) stack.Clear(reason);
        stacks.Clear();
        Children.Clear();
    }
}
