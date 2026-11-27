using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Views a single image or a selectable collection with zoom, pan, and an immersive preview.</summary>
public sealed partial class AlderImageView : SelectingItemsControl
{
    /// <summary>A standalone image shown when there is no selected <see cref="AlderImageViewItem"/>.</summary>
    [GeneratedStyledProperty]
    public partial IImage? Source { get; set; }

    /// <summary>The inline image canvas height, in device-independent pixels.</summary>
    [GeneratedStyledProperty(DefaultValue = 320d, ValidateMethodName = nameof(IsValidViewportHeight))]
    public partial double ViewportHeight { get; set; }

    /// <summary>The transition played when the displayed image changes. Reduced motion skips this transition.</summary>
    [GeneratedStyledProperty(DefaultValue = AlderImageViewTransition.CrossFade)]
    public partial AlderImageViewTransition ImageTransition { get; set; }

    /// <summary>Whether to show the image title, dimensions, and position when available.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool ShowDetails { get; set; }

    /// <summary>Whether a collection shows its thumbnail and navigation strip.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool ShowThumbnails { get; set; }

    /// <summary>Whether the viewer can open in the owning top level's overlay.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool AllowImmersivePreview { get; set; }

    /// <summary>Identifies the observable, read-only <see cref="IsImmersive"/> property.</summary>
    public static readonly DirectProperty<AlderImageView, bool> IsImmersiveProperty =
        AvaloniaProperty.RegisterDirect<AlderImageView, bool>(nameof(IsImmersive), view => view.IsImmersive);

    /// <summary>Identifies the observable, read-only <see cref="Zoom"/> property.</summary>
    public static readonly DirectProperty<AlderImageView, double> ZoomProperty =
        AvaloniaProperty.RegisterDirect<AlderImageView, double>(nameof(Zoom), view => view.Zoom);

    /// <summary>Whether the viewer currently occupies the immersive overlay.</summary>
    public bool IsImmersive => isImmersive;

    /// <summary>The current zoom factor. A value of 1 displays bitmap pixels at the monitor's physical pixel size.</summary>
    public double Zoom => zoom;

    private bool isImmersive;
    private double zoom = 1;
    private AlderImageViewItem? item;
    private IImage? shownSource;
    private AlderImageViewport? viewport;
    private Decorator? inlineHost;
    private Border? surface, stage, canvasClip;
    private Control? header, tools, strip, empty;
    private TextBlock? title, dimensions, position, percent;
    private Button? minus, plus, fit, actual, open, close, previous, next;
    private StackPanel? thumbnails;
    private AlderFilmstrip? filmstrip;
    private ScrollViewer? thumbnailScroll;
    private readonly List<Button> thumbnailButtons = [];
    private int markedThumbnail = -1;
    private bool thumbnailRevealQueued;
    private readonly List<(Button Button, EventHandler<RoutedEventArgs> Handler)> buttonHandlers = [];
    private TopLevel? topLevel;

    /// <summary>Initializes the view with single selection, including an automatically selected first item.</summary>
    public AlderImageView()
    {
        SelectionMode = SelectionMode.AlwaysSelected;
        ItemsView.CollectionChanged += (_, change) => RefreshThumbnails(!AlderFilmstrip.IsTailChange(change, ItemCount));
    }

    private static bool IsValidViewportHeight(double height) => double.IsFinite(height) && height > 0;

    /// <summary>Fits the current image within the viewport and centers it.</summary>
    public void Fit() => viewport?.Fit();

    /// <summary>Centers the current image at its actual size.</summary>
    public void ShowActualSize() => viewport?.ActualSize();

    /// <summary>Opens the immersive view if a source and an overlay host are available.</summary>
    public bool TryOpenImmersive() => OpenImmersive(NavigationMethod.Unspecified);

    /// <summary>Closes the immersive view and returns focus to the inline viewer.</summary>
    public void CloseImmersive() => CloseImmersive(restoreFocus: true);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        CloseImmersive(restoreFocus: false);
        if (viewport is not null)
        {
            viewport.ViewChanged -= OnViewChanged;
            viewport.GotFocus -= OnViewportFocusChanged;
            viewport.LostFocus -= OnViewportFocusChanged;
        }
        PseudoClasses.Set(":viewport-focus-visible", false);
        if (surface is not null) surface.KeyDown -= OnViewKeyDown;
        if (thumbnailScroll is not null) thumbnailScroll.SizeChanged -= ThumbnailScrollSizeChanged;
        foreach (var (button, handler) in buttonHandlers) button.Click -= handler;
        buttonHandlers.Clear();
        thumbnailButtons.Clear();
        base.OnApplyTemplate(e);

        var scope = e.NameScope;
        inlineHost = scope.Find<Decorator>("PART_InlineHost");
        surface = scope.Find<Border>("PART_Surface");
        stage = scope.Find<Border>("PART_Stage");
        canvasClip = scope.Find<Border>("PART_CanvasClip");
        viewport = scope.Find<AlderImageViewport>("PART_Viewport");
        header = scope.Find<Control>("PART_Header");
        tools = scope.Find<Control>("PART_Tools");
        strip = scope.Find<Control>("PART_Strip");
        empty = scope.Find<Control>("PART_Empty");
        title = scope.Find<TextBlock>("PART_Title");
        dimensions = scope.Find<TextBlock>("PART_Dimensions");
        position = scope.Find<TextBlock>("PART_Position");
        percent = scope.Find<TextBlock>("PART_Zoom");
        var thumbnailPart = scope.Find<Control>("PART_Thumbnails");
        thumbnails = thumbnailPart as StackPanel;
        filmstrip = thumbnailPart as AlderFilmstrip;
        thumbnailScroll = scope.Find<ScrollViewer>("PART_ThumbnailScroll");
        if (filmstrip is not null) filmstrip.CreateButton = CreateThumbnail;
        if (thumbnailScroll is not null) thumbnailScroll.SizeChanged += ThumbnailScrollSizeChanged;
        minus = Hook("PART_ZoomOut", (_, _) => viewport?.ZoomBy(0.8));
        plus = Hook("PART_ZoomIn", (_, _) => viewport?.ZoomBy(1.25));
        fit = Hook("PART_Fit", (_, _) => Fit());
        actual = Hook("PART_Actual", (_, _) => ShowActualSize());
        open = Hook("PART_Open", (_, _) => OpenImmersive(open?.IsKeyboardFocusWithin == true ? NavigationMethod.Tab : NavigationMethod.Pointer));
        close = Hook("PART_Close", (_, _) => CloseImmersive(restoreFocus: true,
            close?.IsKeyboardFocusWithin == true ? NavigationMethod.Tab : NavigationMethod.Pointer));
        previous = Hook("PART_Previous", (_, _) => MoveSelection(-1));
        next = Hook("PART_Next", (_, _) => MoveSelection(1));

        if (viewport is not null)
        {
            viewport.ViewChanged += OnViewChanged;
            viewport.GotFocus += OnViewportFocusChanged;
            viewport.LostFocus += OnViewportFocusChanged;
        }
        if (surface is not null) surface.KeyDown += OnViewKeyDown;

        RefreshThumbnails();
        RefreshImage(reset: true, animate: false);
        RefreshLayout();

        Button? Hook(string name, EventHandler<RoutedEventArgs> handler)
        {
            var button = scope.Find<Button>(name);
            if (button is not null)
            {
                button.Click += handler;
                buttonHandlers.Add((button, handler));
            }
            return button;
        }
    }

    private void OnViewportFocusChanged(object? sender, FocusChangedEventArgs e) =>
        PseudoClasses.Set(":viewport-focus-visible", viewport?.Classes.Contains(":focus-visible") == true);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedItemProperty)
        {
            var direction = ImageTransition == AlderImageViewTransition.Slide && item is not null &&
                            SelectedIndex >= 0 && ItemsView.IndexOf(item) > SelectedIndex ? -1 : 1;
            if (item is not null) item.PropertyChanged -= OnItemChanged;
            item = SelectedItem as AlderImageViewItem;
            if (item is not null && topLevel is not null) item.PropertyChanged += OnItemChanged;
            RefreshImage(reset: true, animate: true, direction);
        }
        else if (change.Property == SelectedIndexProperty)
        {
            UpdateNavigation();
        }
        else if (change.Property == SourceProperty)
        {
            RefreshImage(reset: false, animate: true);
        }
        else if (change.Property == ViewportHeightProperty || change.Property == ShowDetailsProperty ||
                 change.Property == ShowThumbnailsProperty || change.Property == AllowImmersivePreviewProperty ||
                 change.Property == CornerRadiusProperty || change.Property == BorderThicknessProperty ||
                 change.Property == PaddingProperty)
        {
            if (!AllowImmersivePreview && IsImmersive) CloseImmersive();
            if (change.Property == ShowThumbnailsProperty) RefreshThumbnails();
            else RefreshLayout();
        }
        else if (change.Property == ImageTransitionProperty)
        {
            viewport?.FinishImageTransition();
        }
        else if (change.Property == AlderMotion.ReducedProperty)
        {
            SyncOverlayAppearance();
            if (AlderMotion.GetReduced(this)) viewport?.CompleteMotion();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not null) topLevel.ScalingChanged += OnScalingChanged;
        if (item is not null) item.PropertyChanged += OnItemChanged;
        ActualThemeVariantChanged += OnThemeChanged;
        RefreshImage(reset: false, animate: false);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CloseImmersive(restoreFocus: false);
        if (topLevel is not null) topLevel.ScalingChanged -= OnScalingChanged;
        topLevel = null;
        if (item is not null) item.PropertyChanged -= OnItemChanged;
        ActualThemeVariantChanged -= OnThemeChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnThemeChanged(object? sender, EventArgs e) => SyncOverlayAppearance();
    private void OnScalingChanged(object? sender, EventArgs e) => viewport?.ResizeView();
    private void OnItemChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => RefreshImage(reset: false, animate: true);

    private void RefreshImage(bool reset, bool animate, int direction = 1)
    {
        if (viewport is null) return;
        var source = item is null ? Source : item.Source;
        if (reset || !ReferenceEquals(source, shownSource))
        {
            shownSource = source;
            viewport.ShowImage(source, animate ? ImageTransition : AlderImageViewTransition.None,
                FlowDirection == FlowDirection.RightToLeft ? -direction : direction);
        }
        AutomationProperties.SetName(viewport, item?.Description ?? item?.Title);
        if (title is not null)
        {
            title.Text = item?.Title;
            title.IsVisible = !string.IsNullOrEmpty(title.Text);
        }
        if (dimensions is not null)
        {
            var size = AlderImageViewport.GetUnitSize(source);
            var extent = source is null ? null : string.Create(CultureInfo.CurrentCulture, $"{size.Width:0.##} × {size.Height:0.##} px");
            dimensions.Text = string.IsNullOrEmpty(item?.FileName) ? extent
                : extent is null ? item.FileName : $"{item.FileName}  ·  {extent}";
            dimensions.IsVisible = dimensions.Text is not null;
        }

        if (source is null && IsImmersive && ItemCount == 0) CloseImmersive();
        RefreshLayout();
        UpdateNavigation();
    }

    private void RefreshLayout()
    {
        if (viewport is not null) viewport.Height = IsImmersive ? double.NaN : ViewportHeight;
        if (empty is not null) empty.IsVisible = shownSource is null;
        if (tools is not null) tools.IsVisible = shownSource is not null || IsImmersive;
        if (header is not null) header.IsVisible = ShowDetails && !IsImmersive && (shownSource is not null || ItemCount > 1);
        if (strip is not null) strip.IsVisible = ShowThumbnails && ItemCount > 1;
        if (open is not null) open.IsVisible = !IsImmersive && AllowImmersivePreview;
        if (close is not null) close.IsVisible = IsImmersive;
        if (stage is not null) stage.CornerRadius = IsImmersive ? default : new CornerRadius(14);
        if (canvasClip is not null) canvasClip.CornerRadius = IsImmersive ? default : new CornerRadius(14, 14, 0, 0);
        if (surface is not null)
        {
            surface.SetCurrentValue(Border.CornerRadiusProperty, IsImmersive ? default : CornerRadius);
            surface.SetCurrentValue(Border.BorderThicknessProperty, IsImmersive ? default : BorderThickness);
            surface.SetCurrentValue(Border.PaddingProperty, IsImmersive ? new Thickness(0, 0, 0, 10) : Padding);
        }
    }

    private void OnViewChanged(object? sender, EventArgs e)
    {
        if (viewport is null) return;
        SetAndRaise(ZoomProperty, ref zoom, viewport.Scale);
        if (percent is not null) percent.Text = Zoom.ToString("P0", CultureInfo.CurrentCulture);
        if (minus is not null) minus.IsEnabled = shownSource is not null && Zoom > viewport.FitScale + 0.00001;
        if (plus is not null) plus.IsEnabled = shownSource is not null && Zoom < 4;
        if (fit is not null)
        {
            fit.IsEnabled = shownSource is not null;
            fit.Classes.Set("selected", viewport.IsFit);
        }
        if (actual is not null)
        {
            actual.IsEnabled = shownSource is not null;
            actual.Classes.Set("selected", !viewport.IsFit && Math.Abs(Zoom - 1) < 0.00001);
        }
    }

    private void MoveSelection(int delta)
    {
        var index = SelectedIndex + delta;
        if (index >= 0 && index < ItemCount) SetCurrentValue(SelectedIndexProperty, index);
    }

    private void OnViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || viewport is null || (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0) return;
        switch (e.Key)
        {
            case Key.Left when ItemCount > 1: MoveSelection(FlowDirection == FlowDirection.RightToLeft ? 1 : -1); break;
            case Key.Right when ItemCount > 1: MoveSelection(FlowDirection == FlowDirection.RightToLeft ? -1 : 1); break;
            case Key.Add:
            case Key.OemPlus: if (shownSource is null) return; viewport.ZoomBy(1.25); break;
            case Key.Subtract:
            case Key.OemMinus: if (shownSource is null) return; viewport.ZoomBy(0.8); break;
            case Key.D0:
            case Key.NumPad0: if (shownSource is null) return; Fit(); break;
            case Key.D1:
            case Key.NumPad1: if (shownSource is null) return; ShowActualSize(); break;
            case Key.F when IsImmersive:
            case Key.Escape when IsImmersive: CloseImmersive(restoreFocus: true, NavigationMethod.Tab); break;
            case Key.F: if (!OpenImmersive(NavigationMethod.Tab)) return; break;
            default: return;
        }

        e.Handled = true;
    }

    private void RefreshThumbnails(bool reset = true)
    {
        int count = ShowThumbnails && ItemCount > 1 ? ItemCount : 0;
        if (filmstrip is not null)
            filmstrip.UpdateCount(count, reset);
        else if (thumbnails is not null)
        {
            thumbnails.Children.Clear();
            thumbnailButtons.Clear();
            for (int i = 0; i < count; i++)
            {
                var button = CreateThumbnail(i);
                thumbnails.Children.Add(button);
                thumbnailButtons.Add(button);
            }
        }
        RefreshLayout();
        UpdateNavigation();
        RequestThumbnailReveal();
    }

    private Button CreateThumbnail(int index)
    {
        this.TryFindResource("AlderCarouselThumbnailTheme", out var theme);
        var photo = ItemsView[index] as AlderImageViewItem;
        var image = new Image { Stretch = Stretch.UniformToFill };
        var button = new Button { Theme = theme as ControlTheme, Width = 68, Height = 48, Content = image };
        button.Classes.Set("selected", index == SelectedIndex);
        UpdateThumbnail();
        if (photo is not null)
        {
            image.AttachedToVisualTree += (_, _) =>
            {
                UpdateThumbnail();
                photo.PropertyChanged += OnPhotoChanged;
            };
            image.DetachedFromVisualTree += (_, _) => photo.PropertyChanged -= OnPhotoChanged;
        }
        button.Click += (_, _) => SetCurrentValue(SelectedIndexProperty, index);
        return button;

        void OnPhotoChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => UpdateThumbnail();
        void UpdateThumbnail()
        {
            image.Source = photo?.Thumbnail ?? photo?.Source;
            AutomationProperties.SetName(button, photo?.Title ?? photo?.Description ?? (index + 1).ToString(CultureInfo.CurrentCulture));
        }
    }

    private void UpdateNavigation()
    {
        if (position is not null)
        {
            position.IsVisible = ItemCount > 1 && SelectedIndex >= 0;
            position.Text = string.Create(CultureInfo.CurrentCulture, $"{SelectedIndex + 1} / {ItemCount}");
        }
        if (previous is not null) previous.IsEnabled = SelectedIndex > 0;
        if (next is not null) next.IsEnabled = SelectedIndex >= 0 && SelectedIndex < ItemCount - 1;
        if (markedThumbnail != SelectedIndex)
        {
            ThumbnailAt(markedThumbnail)?.Classes.Set("selected", false);
            markedThumbnail = SelectedIndex;
            ThumbnailAt(markedThumbnail)?.Classes.Set("selected", true);
            RequestThumbnailReveal();
        }
    }

    private Button? ThumbnailAt(int index) => index < 0 ? null :
        filmstrip is not null ? filmstrip.ContainerFromIndex(index) as Button :
        index < thumbnailButtons.Count ? thumbnailButtons[index] : null;

    private void ThumbnailScrollSizeChanged(object? sender, SizeChangedEventArgs e) => RequestThumbnailReveal();

    private void RequestThumbnailReveal()
    {
        if (thumbnailRevealQueued || SelectedIndex < 0 || (filmstrip is null && thumbnails is null)) return;
        thumbnailRevealQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            thumbnailRevealQueued = false;
            if (filmstrip is not null) filmstrip.Reveal(thumbnailScroll, SelectedIndex);
            else if (ThumbnailAt(SelectedIndex) is { } selected && selected.IsAttachedToVisualTree())
                RevealThumbnail(selected);
        }, DispatcherPriority.Loaded);
    }

    private static void RevealThumbnail(Button selected)
    {
        if (selected.FindAncestorOfType<ScrollViewer>() is not { } scroll ||
            selected.TranslatePoint(default, scroll) is not { } origin) return;
        // Adjust only the filmstrip, keeping the gallery's vertical scroll position.
        var delta = origin.X < 0 ? origin.X : Math.Max(0, origin.X + selected.Bounds.Width - scroll.Viewport.Width);
        if (delta != 0)
            scroll.Offset = scroll.Offset.WithX(scroll.Offset.X + delta);
    }
}
