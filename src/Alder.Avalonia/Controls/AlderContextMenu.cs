using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>A menu attached to a control's <see cref="Control.ContextMenu"/> property.</summary>
public sealed class AlderContextMenu : ContextMenu
{
    /// <summary>Identifies the <see cref="IsMenuOpen"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsMenuOpenProperty =
        AvaloniaProperty.Register<AlderContextMenu, bool>(nameof(IsMenuOpen), defaultBindingMode: BindingMode.TwoWay);

    private readonly MenuPopupMotion motion = new();
    private Control? requestedTarget;
    private PixelPoint? pointerAnchor;
    private IInputElement? previousFocus;
    private bool publishingState;

    static AlderContextMenu()
    {
        ContextRequestedEvent.AddClassHandler<Control>((control, e) =>
        {
            if (control.ContextMenu is AlderContextMenu { IsOpen: false } menu)
                menu.RememberRequest(control, e);
        });
        KeyUpEvent.AddClassHandler<Control>((control, e) =>
        {
            if (!control.IsEffectivelyEnabled || control.ContextMenu is not AlderContextMenu { IsOpen: false, IsEnabled: true })
                return;

            // Avalonia's default key map includes the Menu key. Also support the
            // keyboard shortcuts used by desktop context menus without that key.
            if ((e.Key == Key.F10 && e.KeyModifiers == KeyModifiers.Shift) ||
                (OperatingSystem.IsMacOS() && e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control))
            {
                var request = new ContextRequestedEventArgs { Source = control };
                control.RaiseEvent(request);
                e.Handled = request.Handled;
            }
        });
    }

    /// <summary>Initializes a new instance of <see cref="AlderContextMenu"/>.</summary>
    public AlderContextMenu() : base(new ContextMenuInteractionHandler())
    {
        Opening += (_, e) => e.Cancel |= !IsEnabled;
        Opened += OnMenuOpened;
        Closed += OnMenuClosed;
    }

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new AlderMenuItem();

    /// <summary>Gets or requests the open state. Set <see cref="ContextMenu.PlacementTarget"/> to the attached trigger for programmatic opening.</summary>
    public bool IsMenuOpen { get => GetValue(IsMenuOpenProperty); set => SetValue(IsMenuOpenProperty, value); }

    /// <inheritdoc />
    public override void Open() => Open(PlacementTarget);

    /// <inheritdoc cref="ContextMenu.Open(Control)" />
    public new void Open(Control? control)
    {
        if (!IsEnabled || IsOpen) return;
        if (control is { } target && target.ContextMenu == this && target.IsAttachedToVisualTree())
        {
            if (target.IsEffectivelyEnabled)
                target.RaiseEvent(new ContextRequestedEventArgs { Source = target });
            return;
        }
        base.Open(control);
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        if (Parent is Popup popup)
            popup.ShouldUseOverlayLayer = true;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PlacementTargetProperty)
        {
            if (change.GetOldValue<Control?>() is { } oldTarget)
                oldTarget.AttachedToVisualTree -= TargetAttached;
            if (PlacementTarget is { } target)
                target.AttachedToVisualTree += TargetAttached;
            if (IsMenuOpen && !IsOpen) RequestOpen();
        }
        else if (change.Property == IsEnabledProperty && !IsEnabled && IsOpen)
        {
            Close();
        }
        else if (change.Property == IsMenuOpenProperty && !publishingState)
        {
            if (IsMenuOpen) RequestOpen();
            else
            {
                Close();
                if (IsOpen && !motion.IsClosing) PublishState(true);
            }
        }
    }

    private void TargetAttached(object? sender, VisualTreeAttachmentEventArgs e) => RequestOpen();

    private void RequestOpen()
    {
        // Wait until the trigger has attached its native ContextMenu handlers.
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsMenuOpen || PlacementTarget is not { } target || !target.IsAttachedToVisualTree()) return;
            if (motion.IsClosing) motion.FinishClose();
            if (IsOpen) return;

            if (target.ContextMenu == this) Open(target);
            if (!IsOpen) PublishState(false);
        });
    }

    private void RememberRequest(Control target, ContextRequestedEventArgs args)
    {
        requestedTarget = target;
        previousFocus = TopLevel.GetTopLevel(target)?.FocusManager?.GetFocusedElement();
        pointerAnchor = args.TryGetPosition(target, out var point) ? target.PointToScreen(point) : null;
    }

    private void OnMenuOpened(object? sender, RoutedEventArgs e)
    {
        if (e.Source != this) return;
        if (Parent is Popup popup && (popup.Parent as Control ?? popup.PlacementTarget) is { } target)
        {
            if (requestedTarget != target)
            {
                requestedTarget = target;
                pointerAnchor = null;
                previousFocus = null;
            }
            motion.Opened(popup, this, target, pointerAnchor, previousFocus);
        }
        Closing += AnimateClose;
        PublishState(true);
    }

    private void AnimateClose(object? sender, CancelEventArgs e) => motion.Closing(e);

    private void OnMenuClosed(object? sender, RoutedEventArgs e)
    {
        if (e.Source != this) return;

        Closing -= AnimateClose;
        motion.Closed();
        var target = requestedTarget ?? (Parent as Popup)?.PlacementTarget;
        var restore = previousFocus ?? target;
        requestedTarget = null;
        pointerAnchor = null;
        previousFocus = null;
        PublishState(false);

        if (target is null || restore is not Visual visual || !visual.IsAttachedToVisualTree()) return;
        var focusManager = TopLevel.GetTopLevel(target)?.FocusManager;
        if (focusManager?.GetFocusedElement() is null)
            restore.Focus();
    }

    private void PublishState(bool isOpen)
    {
        publishingState = true;
        try { SetCurrentValue(IsMenuOpenProperty, isOpen); }
        finally { publishingState = false; }
    }

    private sealed class ContextMenuInteractionHandler() : DefaultMenuInteractionHandler(true)
    {
        protected override void KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Source is MenuItem { FlowDirection: FlowDirection.RightToLeft } && e.Key is Key.Left or Key.Right)
            {
                var mirrored = new KeyEventArgs
                {
                    Source = e.Source, Key = e.Key == Key.Left ? Key.Right : Key.Left, KeyModifiers = e.KeyModifiers
                };
                base.KeyDown(sender, mirrored);
                e.Handled = mirrored.Handled;
            }
            else
            {
                base.KeyDown(sender, e);
            }
        }
    }
}

/// <summary>A non-interactive heading for the following menu items.</summary>
public sealed class AlderContextMenuHeading : Separator
{
    /// <summary>Identifies the <see cref="Text"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AlderContextMenuHeading, string?>(nameof(Text));

    /// <summary>The non-interactive label identifying a group of menu entries.</summary>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
}

internal static class ContextMenuPlacement
{
    public static CustomPopupPlacementCallback Submenu { get; } = PlaceSubmenu;
    public static IValueConverter ViewportHeight { get; } =
        new FuncValueConverter<double, double>(height => Math.Max(0, height - 24));

    private static void PlaceSubmenu(CustomPopupPlacement placement)
    {
        const double offset = 8;
        var target = placement.AnchorRectangle;
        var window = TopLevel.GetTopLevel(placement.Target);
        var safeArea = window?.InsetsManager?.SafeAreaPadding ?? default;
        double left = target.Left - safeArea.Left;
        double right = (window?.ClientSize.Width ?? target.Right) - safeArea.Right - target.Right;
        bool prefersRight = placement.Target.FlowDirection == FlowDirection.LeftToRight;
        double preferred = prefersRight ? right : left;
        double alternate = prefersRight ? left : right;
        double required = placement.PopupSize.Deflate(placement.Deflate).Width + offset;

        // The positioner mirrors the logical anchor in RTL. When neither side fits,
        // choose the side with more room so sliding covers less of the parent menu.
        bool usePreferred = preferred >= required || preferred >= alternate;
        placement.Anchor = usePreferred ? PopupAnchor.TopRight : PopupAnchor.TopLeft;
        placement.Gravity = usePreferred ? PopupGravity.BottomRight : PopupGravity.BottomLeft;
        placement.Offset = new Point(usePreferred == prefersRight ? offset : -offset, 0);
        placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.SlideX |
                                         PopupPositionerConstraintAdjustment.FlipY |
                                         PopupPositionerConstraintAdjustment.SlideY;
    }
}
