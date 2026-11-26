using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>A desktop window with Alder caption controls and window-scoped dialog and toast hosts.</summary>
/// <remarks>
/// Add <see cref="Alder.Avalonia.Themes.AlderTheme"/> to the application styles to use the default template.
/// The default hosts are available once the window is open. Use a named host for an independent queue or dialog region.
/// </remarks>
[TemplatePart("PART_DialogHost", typeof(DialogHost))]
[TemplatePart("PART_ResizeGrips", typeof(Panel))]
[TemplatePart("PART_MaximizeIcon", typeof(Control))]
[TemplatePart("PART_MacMaximizeButton", typeof(Button))]
[PseudoClasses(":custom-chrome", ":restore")]
public partial class AlderWindow : Window
{
    /// <summary>Whether to reserve a title strip. Disable it when the content provides a draggable title region.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool ShowTitleBar { get; set; }

    /// <summary>The margin around the caption buttons in the borderless window template.</summary>
    [GeneratedStyledProperty]
    public partial Thickness CaptionMargin { get; set; }

    /// <summary>Identifies the <see cref="ToastSafePadding"/> property.</summary>
    public static readonly StyledProperty<Thickness> ToastSafePaddingProperty =
        AvaloniaProperty.Register<AlderWindow, Thickness>(nameof(ToastSafePadding), new Thickness(16));
    /// <summary>Identifies the <see cref="ToastMaxVisible"/> property.</summary>
    public static readonly StyledProperty<int> ToastMaxVisibleProperty =
        AvaloniaProperty.Register<AlderWindow, int>(nameof(ToastMaxVisible), ToastHost.DefaultMaxVisible,
            validate: value => value > 0);

    /// <summary>Whether the caption icon animates when the borderless window maximizes or restores.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool AnimateWindowState { get; set; }

    private static readonly Vector3D UnitScale = new(1, 1, 1);
    private static readonly CubicEaseOut CaptionEase = new();
    private Control? maximizeIcon;
    private Button? macMaximizeButton;
    private ToastHost? defaultToasts;
    private IDisposable? toastPadding;
    private IDisposable? toastLimit;
    private bool opened;

    static AlderWindow()
    {
        ExtendClientAreaToDecorationsHintProperty.OverrideDefaultValue<AlderWindow>(true);
        ExtendClientAreaTitleBarHeightHintProperty.OverrideDefaultValue<AlderWindow>(0);
    }

    /// <summary>Creates an Alder window with a platform-appropriate caption layout.</summary>
    public AlderWindow()
    {
        // Apply the default through the property store so it reaches the platform and
        // remains available when a consumer's style or local value is removed.
        SetValue(WindowDecorationsProperty, WindowDecorations.None, BindingPriority.Style);
        Classes.Set("mac", OperatingSystem.IsMacOS());
        UpdateChrome();
        AddHandler(Button.ClickEvent, OnCaptionClick);
    }

    /// <summary>The space between the default toast queue and the window edges.</summary>
    public Thickness ToastSafePadding
    {
        get => GetValue(ToastSafePaddingProperty);
        set => SetValue(ToastSafePaddingProperty, value);
    }

    /// <summary>The default toast host's capacity per placement. Defaults to three.</summary>
    public int ToastMaxVisible
    {
        get => GetValue(ToastMaxVisibleProperty);
        set => SetValue(ToastMaxVisibleProperty, value);
    }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(AlderWindow);

    internal DialogHost? DefaultDialogHost { get; private set; }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        ResetCaptionMotion();
        base.OnApplyTemplate(e);
        DefaultDialogHost = e.NameScope.Find<DialogHost>("PART_DialogHost");
        maximizeIcon = e.NameScope.Find<Control>("PART_MaximizeIcon");
        macMaximizeButton = e.NameScope.Find<Button>("PART_MacMaximizeButton");
        if (e.NameScope.Find<Panel>("PART_ResizeGrips") is { } grips)
            AlderWindowResize.Attach(this, grips);
        EnsureDefaultToastHost();
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        opened = true;
        EnsureDefaultToastHost();
        base.OnOpened(e);
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        opened = false;
        ResetCaptionMotion();
        defaultToasts?.Unregister();
        toastPadding?.Dispose();
        toastPadding = null;
        toastLimit?.Dispose();
        toastLimit = null;
        defaultToasts = null;
        DefaultDialogHost = null;
        base.OnClosed(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty)
        {
            var (oldState, state) = change.GetOldAndNewValue<WindowState>();
            UpdateChrome();
            if ((oldState == WindowState.Normal && state == WindowState.Maximized) ||
                (oldState == WindowState.Maximized && state == WindowState.Normal))
                PlayCaptionMotion();
            else if (oldState != state)
                ResetCaptionMotion();
        }
        else if (change.Property == WindowDecorationsProperty || change.Property == ExtendClientAreaToDecorationsHintProperty)
        {
            UpdateChrome();
            if (!UsesCustomChrome) ResetCaptionMotion();
        }
        else if ((change.Property == AnimateWindowStateProperty && !AnimateWindowState) ||
                 (change.Property == AlderMotion.ReducedProperty && AlderMotion.GetReduced(this)))
            ResetCaptionMotion();
    }

    private bool UsesCustomChrome => WindowDecorations == WindowDecorations.None && ExtendClientAreaToDecorationsHint;

    private void UpdateChrome()
    {
        PseudoClasses.Set(":custom-chrome", UsesCustomChrome);
        PseudoClasses.Set(":restore", WindowState is WindowState.Maximized or WindowState.FullScreen);
    }

    private void OnCaptionClick(object? sender, RoutedEventArgs e)
    {
        if (!UsesCustomChrome || e.Source is not Button button || button.TemplatedParent != this) return;

        switch (button.Name)
        {
            case "PART_CloseButton" or "PART_MacCloseButton":
                Close();
                break;
            case "PART_MinimizeButton" or "PART_MacMinimizeButton" when CanMinimize:
                WindowState = WindowState.Minimized;
                break;
            case "PART_MaximizeButton" or "PART_MacMaximizeButton":
                if (WindowState is WindowState.Maximized or WindowState.FullScreen) WindowState = WindowState.Normal;
                else if (CanMaximize) WindowState = WindowState.Maximized;
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    internal ToastHost? EnsureDefaultToastHost()
    {
        if (OverlayLayer.GetOverlayLayer(this) is null) return null;
        var host = ToastHost.Register(this);
        if (ReferenceEquals(defaultToasts, host)) return host;

        toastPadding?.Dispose();
        toastLimit?.Dispose();
        defaultToasts = host;
        toastPadding = host.Bind(ToastHost.SafePaddingProperty, this.GetBindingObservable(ToastSafePaddingProperty), BindingPriority.Template);
        toastLimit = host.Bind(ToastHost.MaxVisibleProperty, this.GetBindingObservable(ToastMaxVisibleProperty), BindingPriority.Template);
        return host;
    }

    private void PlayCaptionMotion()
    {
        if (!opened || !IsVisible || !UsesCustomChrome || !AnimateWindowState || AlderMotion.GetReduced(this)) return;
        var icon = maximizeIcon is { IsEffectivelyVisible: true } ? maximizeIcon : macMaximizeButton?.Presenter;
        if (icon is not { IsEffectivelyVisible: true, Bounds.Width: > 0, Bounds.Height: > 0 } ||
            ElementComposition.GetElementVisual(icon) is not { } visual) return;

        // The native resize already rearranges the page. Keep it at its final scale throughout the transition.
        visual.CenterPoint = new Vector3D(icon.Bounds.Width / 2, icon.Bounds.Height / 2, 0);
        var scale = visual.Compositor.CreateVector3DKeyFrameAnimation();
        scale.Duration = TimeSpan.FromMilliseconds(120);
        scale.InsertKeyFrame(0, new Vector3D(.8, .8, 1));
        scale.InsertKeyFrame(1, UnitScale, CaptionEase);
        visual.StartAnimation(nameof(CompositionVisual.Scale), scale);
    }

    private void ResetCaptionMotion()
    {
        Reset(maximizeIcon);
        Reset(macMaximizeButton?.Presenter);

        static void Reset(Control? icon)
        {
            if (icon is null || ElementComposition.GetElementVisual(icon) is not { } visual) return;
            visual.StopAnimation(nameof(CompositionVisual.Scale));
            visual.Scale = UnitScale;
        }
    }
}
