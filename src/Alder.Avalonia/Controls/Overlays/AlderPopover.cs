using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Rendering.Composition;
using Avalonia.Metadata;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>An anchored flyout for interactive content, using native popup placement and light dismissal.</summary>
public sealed class AlderPopover : PopupFlyoutBase
{
    /// <summary>Identifies the <see cref="Content"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> ContentProperty = ContentControl.ContentProperty.AddOwner<AlderPopover>();
    /// <summary>Identifies the <see cref="ContentTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty = ContentControl.ContentTemplateProperty.AddOwner<AlderPopover>();
    /// <summary>Identifies the <see cref="Heading"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> HeadingProperty = AvaloniaProperty.Register<AlderPopover, string?>(nameof(Heading));
    /// <summary>Identifies the <see cref="ShowArrow"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowArrowProperty = AvaloniaProperty.Register<AlderPopover, bool>(nameof(ShowArrow));
    /// <summary>Identifies the <see cref="Offset"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> OffsetProperty =
        AvaloniaProperty.Register<AlderPopover, double>(nameof(Offset), double.NaN);

    /// <summary>Identifies the <see cref="SurfaceBackground"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> SurfaceBackgroundProperty =
        AvaloniaProperty.Register<AlderPopover, IBrush?>(nameof(SurfaceBackground));
    /// <summary>Identifies the <see cref="SurfaceCornerRadius"/> Avalonia property.</summary>
    public static readonly StyledProperty<CornerRadius> SurfaceCornerRadiusProperty =
        AvaloniaProperty.Register<AlderPopover, CornerRadius>(nameof(SurfaceCornerRadius), new CornerRadius(24));
    /// <summary>Identifies the <see cref="ContentPadding"/> Avalonia property.</summary>
    public static readonly StyledProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.Register<AlderPopover, Thickness>(nameof(ContentPadding), new Thickness(16));
    /// <summary>Identifies the <see cref="MaxContentWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> MaxContentWidthProperty =
        AvaloniaProperty.Register<AlderPopover, double>(nameof(MaxContentWidth), 360, validate: value => value > 0);
    /// <summary>Identifies the <see cref="SurfaceBorderBrush"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> SurfaceBorderBrushProperty =
        AvaloniaProperty.Register<AlderPopover, IBrush?>(nameof(SurfaceBorderBrush));
    /// <summary>Identifies the <see cref="SurfaceBorderThickness"/> Avalonia property.</summary>
    public static readonly StyledProperty<Thickness> SurfaceBorderThicknessProperty =
        AvaloniaProperty.Register<AlderPopover, Thickness>(nameof(SurfaceBorderThickness));
    /// <summary>Identifies the <see cref="SurfaceShadow"/> Avalonia property.</summary>
    public static readonly StyledProperty<BoxShadows?> SurfaceShadowProperty =
        AvaloniaProperty.Register<AlderPopover, BoxShadows?>(nameof(SurfaceShadow));

    private AnchoredSurface? surface;
    private TextBlock? heading;
    private ContentPresenter? body;
    private static readonly FuncDataTemplate<string> StringTemplate = new((text, _) => new AlderTextBlock
    {
        Text = text, TextWrapping = TextWrapping.Wrap, LineHeight = 20
    });
    private IDisposable? closeTimer;
    internal bool IsClosing => closeTimer is not null;

    internal void FinishExit()
    {
        if (closeTimer is not null) HideCore(false);
    }
    private bool waitingForLayout;
    private bool restoreFocus;
    private bool openedFromKeyboard;

    internal bool FocusContentOnOpen { get; set; } = true;
    internal Control? FocusReturnTarget { get; init; }
    internal Rect? AnchorRect { get; init; }
    internal AnchoredSurfaceMotion Motion { get; init; }
    private bool UsesArrangedMotion => Motion is AnchoredSurfaceMotion.ListBox or AnchoredSurfaceMotion.InlineSelect;
    internal void HideImmediately() => HideCore(false);

    /// <summary>Initializes a new instance of <see cref="AlderPopover"/>.</summary>
    public AlderPopover()
    {
        Placement = PlacementMode.Bottom;
        Popup.ShouldUseOverlayLayer = true;
    }

    internal void PreparePresenter(Control owner)
    {
        var presenter = Popup.Child ??= CreatePresenter();
        AlderMenuFlyout.PrepareLayout(Popup, presenter, owner);
    }

    /// <summary>The body content shown when the popover opens.</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }
    /// <summary>The data template used to present the content.</summary>
    public IDataTemplate? ContentTemplate { get => GetValue(ContentTemplateProperty); set => SetValue(ContentTemplateProperty, value); }
    /// <summary>Optional heading content shown above the popover body.</summary>
    public string? Heading { get => GetValue(HeadingProperty); set => SetValue(HeadingProperty, value); }
    /// <summary>Whether the surface includes an arrow pointing to its anchor.</summary>
    public bool ShowArrow { get => GetValue(ShowArrowProperty); set => SetValue(ShowArrowProperty, value); }
    /// <summary>Distance in DIPs between the panel and the trigger.</summary>
    public double Offset { get => GetValue(OffsetProperty); set => SetValue(OffsetProperty, value); }

    /// <summary>The background brush of the anchored surface.</summary>
    public IBrush? SurfaceBackground { get => GetValue(SurfaceBackgroundProperty); set => SetValue(SurfaceBackgroundProperty, value); }
    /// <summary>The corner radius of the anchored surface.</summary>
    public CornerRadius SurfaceCornerRadius { get => GetValue(SurfaceCornerRadiusProperty); set => SetValue(SurfaceCornerRadiusProperty, value); }
    /// <summary>The space between the anchored surface and its content.</summary>
    public Thickness ContentPadding { get => GetValue(ContentPaddingProperty); set => SetValue(ContentPaddingProperty, value); }
    /// <summary>The maximum width of the content in device-independent pixels.</summary>
    public double MaxContentWidth { get => GetValue(MaxContentWidthProperty); set => SetValue(MaxContentWidthProperty, value); }
    /// <summary>The brush used to draw the border around the anchored surface.</summary>
    public IBrush? SurfaceBorderBrush { get => GetValue(SurfaceBorderBrushProperty); set => SetValue(SurfaceBorderBrushProperty, value); }
    /// <summary>The thickness of the border around the anchored surface.</summary>
    public Thickness SurfaceBorderThickness { get => GetValue(SurfaceBorderThicknessProperty); set => SetValue(SurfaceBorderThicknessProperty, value); }
    /// <summary>The shadow drawn around the anchored surface.</summary>
    public BoxShadows? SurfaceShadow { get => GetValue(SurfaceShadowProperty); set => SetValue(SurfaceShadowProperty, value); }

    /// <inheritdoc />
    protected override Control CreatePresenter()
    {
        heading = new AlderTextBlock
        {
            FontSize = 14, FontWeight = FontWeight.Medium, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6), IsVisible = !string.IsNullOrWhiteSpace(Heading)
        };
        body = new ContentPresenter { FontSize = 14, ContentTemplate = EffectiveContentTemplate };
        heading[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("AlderInk");
        body[!TemplatedControl.ForegroundProperty] = new DynamicResourceExtension("AlderMuted");
        heading.Bind(TextBlock.TextProperty, this.GetBindingObservable(HeadingProperty));
        body.Bind(ContentPresenter.ContentProperty, this.GetBindingObservable(ContentProperty));
        surface = new AnchoredSurface(tooltip: false)
        {
            Focusable = FocusContentOnOpen || ShowMode == FlyoutShowMode.Standard,
            FocusAdorner = null,
            Child = new StackPanel { Children = { heading, body } }
        };
        ApplyAppearance();
        KeyboardNavigation.SetIsTabStop(surface, false);
        surface.AddHandler(InputElement.KeyDownEvent, OnSurfaceKeyDown, RoutingStrategies.Bubble);
        return surface;
    }

    private void OnSurfaceKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            restoreFocus = true;
            Hide();
        }
        else if (e.Key == Key.Tab && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift &&
                 surface is { IsFocused: true } frame && FirstTabStop(frame) is null &&
                 (FocusReturnTarget ?? Target) is { } anchor)
        {
            e.Handled = true;
            restoreFocus = true;
            Hide();
            if (anchor.IsFocused)
            {
                var direction = e.KeyModifiers == KeyModifiers.Shift ? NavigationDirection.Previous : NavigationDirection.Next;
                TopLevel.GetTopLevel(anchor)?.FocusManager?.FindNextElement(direction)?.Focus(NavigationMethod.Tab, e.KeyModifiers);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeadingProperty && heading is not null)
            heading.IsVisible = !string.IsNullOrWhiteSpace(Heading);
        if ((change.Property == ContentTemplateProperty || change.Property == ContentProperty) && body is not null)
            body.ContentTemplate = EffectiveContentTemplate;
        if (change.Property == SurfaceBackgroundProperty || change.Property == SurfaceCornerRadiusProperty ||
            change.Property == ContentPaddingProperty || change.Property == MaxContentWidthProperty ||
            change.Property == SurfaceBorderBrushProperty || change.Property == SurfaceBorderThicknessProperty ||
            change.Property == SurfaceShadowProperty)
            ApplyAppearance();
        if (IsOpen && (change.Property == ShowArrowProperty || change.Property == OffsetProperty ||
                       change.Property == HorizontalOffsetProperty || change.Property == VerticalOffsetProperty ||
                       change.Property == PlacementProperty))
            ApplyPlacement(change.Property == PlacementProperty);
    }

    private IDataTemplate? EffectiveContentTemplate => ContentTemplate ?? (Content is string ? StringTemplate : null);

    private void ApplyAppearance()
    {
        if (surface is null) return;
        surface.MaxWidth = MaxContentWidth;
        surface.ModifyAppearance(SurfaceBackground, SurfaceCornerRadius, ContentPadding,
            SurfaceBorderBrush, SurfaceBorderThickness, SurfaceShadow);
    }

    /// <inheritdoc />
    protected override void OnOpening(CancelEventArgs args)
    {
        base.OnOpening(args);
        if (args.Cancel) return;
        Popup.PlacementRect = AnchorRect;
    }

    /// <inheritdoc />
    protected override void OnOpened()
    {
        if (surface is not { } frame || Target is not { } anchor)
        {
            base.OnOpened();
            return;
        }
        openedFromKeyboard = (FocusReturnTarget ?? anchor).Classes.Contains(":focus-visible");
        ApplyPlacement();
        if (!AlderMotion.GetReduced(anchor))
        {
            if (UsesArrangedMotion) frame.Opacity = 0;
            else if (ElementComposition.GetElementVisual(frame) is { } visual) visual.Opacity = 0;
        }
        waitingForLayout = true;
        frame.LayoutUpdated += OnPlaced;
        frame.InvalidateArrange();
        base.OnOpened();
    }

    private void ApplyPlacement(bool placementChanged = false)
    {
        if (surface is not { } frame) return;
        frame.ShowArrow = ShowArrow;
        var gap = double.IsFinite(Offset) ? Offset : ShowArrow ? 10 : 8;
        if (Motion == AnchoredSurfaceMotion.Default && AnchoredPopupPlacement.IsVertical(Placement))
        {
            Popup.CustomPopupPlacementCallback = PlaceVertical;
            AnchoredSurface.SetOffsets(Popup, Placement, gap, HorizontalOffset, VerticalOffset);
            if (placementChanged) Popup.Placement = Placement;
            Popup.Placement = PlacementMode.Custom;
        }
        else
        {
            Popup.CustomPopupPlacementCallback = CustomPopupPlacementCallback;
            Popup.Placement = Placement;
            AnchoredSurface.SetOffsets(Popup, Placement, gap, HorizontalOffset, VerticalOffset);
        }
        frame.InvalidateVisual();
    }

    private void PlaceVertical(CustomPopupPlacement placement)
    {
        var gap = double.IsFinite(Offset) ? Offset : ShowArrow ? 10 : 8;
        gap += AnchoredPopupPlacement.PrefersTop(Placement) ? -VerticalOffset : VerticalOffset;
        AnchoredPopupPlacement.PlaceVertical(placement, Placement, gap, HorizontalOffset);
    }

    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs args)
    {
        base.OnClosing(args);
        if (args.Cancel)
        {
            restoreFocus = false;
            return;
        }
        if (closeTimer is not null)
        {
            args.Cancel = true;
            return;
        }

        if (surface is not { } frame || Target is not { } anchor) return;

        // Move focus before Avalonia detaches the popup and restores focus without the keyboard navigation method.
        var returnTarget = FocusReturnTarget ?? anchor;
        if (returnTarget.IsAttachedToVisualTree() && frame.IsKeyboardFocusWithin &&
            (restoreFocus || FocusContentOnOpen && (openedFromKeyboard ||
                TopLevel.GetTopLevel(frame)?.FocusManager?.GetFocusedElement() is InputElement focused &&
                focused.Classes.Contains(":focus-visible"))))
            returnTarget.Focus(NavigationMethod.Tab);

        if (waitingForLayout || !frame.IsAttachedToVisualTree() || !anchor.IsAttachedToVisualTree() || AlderMotion.GetReduced(anchor))
            return;

        args.Cancel = true;
        frame.IsHitTestVisible = false;
        frame.Play(anchor, Placement, entering: false, Motion, AnchorRect);
        closeTimer = DispatcherTimer.RunOnce(() => HideCore(false), TimeSpan.FromMilliseconds(100), DispatcherPriority.Render);
    }

    /// <inheritdoc />
    protected override void OnClosed()
    {
        closeTimer?.Dispose();
        closeTimer = null;
        if (surface is { } frame)
        {
            frame.LayoutUpdated -= OnPlaced;
            frame.LayoutUpdated -= UpdateArrow;
            frame.IsHitTestVisible = true;
            frame.Opacity = 1;
            OverlayMotion.Reset(frame);
        }
        waitingForLayout = false;
        restoreFocus = false;
        openedFromKeyboard = false;
        base.OnClosed();
    }

    private void OnPlaced(object? sender, EventArgs e)
    {
        if (surface is not { Bounds.Height: > 0 } frame || Target is not { } anchor ||
            !frame.IsAttachedToVisualTree() || !anchor.IsAttachedToVisualTree()) return;
        if (frame.FindAncestorOfType<OverlayPopupHost>() is { } host && double.IsNaN(Canvas.GetTop(host))) return;
        if (!waitingForLayout) return;
        waitingForLayout = false;
        if (UsesArrangedMotion) frame.Opacity = 1;
        if (AlderMotion.GetReduced(anchor)) frame.Position(anchor, Placement, AnchorRect);
        else if (UsesArrangedMotion && ElementComposition.GetElementVisual(frame) is { } visual)
        {
            // Apply the keyframes after the renderer has synchronized the first arranged visual.
            visual.Compositor.RequestCompositionUpdate(() =>
            {
                if (IsOpen && closeTimer is null && ReferenceEquals(surface, frame) && ReferenceEquals(Target, anchor) &&
                    frame.IsAttachedToVisualTree() && anchor.IsAttachedToVisualTree())
                    frame.Play(anchor, Placement, entering: true, Motion, AnchorRect);
            });
        }
        else frame.Play(anchor, Placement, entering: true, Motion, AnchorRect);
        // Keep the arrow anchored when the popup is repositioned after a resize.
        frame.LayoutUpdated -= OnPlaced;
        frame.LayoutUpdated += UpdateArrow;
        if (!FocusContentOnOpen) return;
        var first = FirstTabStop(frame);
        (first ?? frame).Focus(openedFromKeyboard ? NavigationMethod.Tab : NavigationMethod.Unspecified);
    }

    private static InputElement? FirstTabStop(Visual frame) =>
        frame.GetVisualDescendants().OfType<InputElement>().FirstOrDefault(input =>
            input.Focusable && input.IsEffectivelyEnabled && input.IsEffectivelyVisible && KeyboardNavigation.GetIsTabStop(input));

    private void UpdateArrow(object? sender, EventArgs e)
    {
        if (ShowArrow && surface is { } frame && Target is { } anchor &&
            frame.IsAttachedToVisualTree() && anchor.IsAttachedToVisualTree())
            frame.Position(anchor, Placement, AnchorRect);
    }
}
