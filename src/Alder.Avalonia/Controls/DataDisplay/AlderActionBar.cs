using System.Diagnostics;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>A contextual toolbar that appears at the bottom of its layout host.</summary>
public sealed class AlderActionBar : ContentControl
{
    /// <summary>Identifies the <see cref="IsOpen"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<AlderActionBar, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="IsAttached"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsAttachedProperty =
        AvaloniaProperty.Register<AlderActionBar, bool>(nameof(IsAttached), true);
    /// <summary>Identifies the <see cref="Prefix"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> PrefixProperty =
        AvaloniaProperty.Register<AlderActionBar, object?>(nameof(Prefix));
    /// <summary>Identifies the <see cref="PrefixTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> PrefixTemplateProperty =
        AvaloniaProperty.Register<AlderActionBar, IDataTemplate?>(nameof(PrefixTemplate));
    /// <summary>Identifies the <see cref="Suffix"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> SuffixProperty =
        AvaloniaProperty.Register<AlderActionBar, object?>(nameof(Suffix));
    /// <summary>Identifies the <see cref="SuffixTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> SuffixTemplateProperty =
        AvaloniaProperty.Register<AlderActionBar, IDataTemplate?>(nameof(SuffixTemplate));
    /// <summary>Identifies the <see cref="Orientation"/> Avalonia property.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<AlderActionBar, Orientation>(nameof(Orientation));
    /// <summary>Identifies the <see cref="ShowSeparators"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowSeparatorsProperty =
        AvaloniaProperty.Register<AlderActionBar, bool>(nameof(ShowSeparators), true);
    /// <summary>Identifies the <see cref="CompactWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<AlderActionBar, double>(nameof(CompactWidth), 640, validate: value => double.IsFinite(value) && value >= 0);
    /// <summary>Identifies the <see cref="DismissOnEscape"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> DismissOnEscapeProperty =
        AvaloniaProperty.Register<AlderActionBar, bool>(nameof(DismissOnEscape), true);
    /// <summary>Identifies the <see cref="DismissCommand"/> Avalonia property.</summary>
    public static readonly StyledProperty<ICommand?> DismissCommandProperty =
        AvaloniaProperty.Register<AlderActionBar, ICommand?>(nameof(DismissCommand));
    /// <summary>Identifies the <see cref="DismissCommandParameter"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> DismissCommandParameterProperty =
        AvaloniaProperty.Register<AlderActionBar, object?>(nameof(DismissCommandParameter));

    /// <summary>Whether the action bar is shown.</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    /// <summary>Whether the bar uses the appearance for an attached surface.</summary>
    public bool IsAttached { get => GetValue(IsAttachedProperty); set => SetValue(IsAttachedProperty, value); }
    /// <summary>Optional leading content.</summary>
    public object? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }
    /// <summary>The data template used to present the prefix.</summary>
    public IDataTemplate? PrefixTemplate { get => GetValue(PrefixTemplateProperty); set => SetValue(PrefixTemplateProperty, value); }
    /// <summary>Optional trailing content.</summary>
    public object? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }
    /// <summary>The data template used to present the suffix.</summary>
    public IDataTemplate? SuffixTemplate { get => GetValue(SuffixTemplateProperty); set => SetValue(SuffixTemplateProperty, value); }
    /// <summary>Whether the control is laid out horizontally or vertically.</summary>
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    /// <summary>Whether to show the separators.</summary>
    public bool ShowSeparators { get => GetValue(ShowSeparatorsProperty); set => SetValue(ShowSeparatorsProperty, value); }
    /// <summary>The width threshold for switching to the compact arrangement.</summary>
    public double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }
    /// <summary>Whether Escape requests dismissal of the open bar.</summary>
    public bool DismissOnEscape { get => GetValue(DismissOnEscapeProperty); set => SetValue(DismissOnEscapeProperty, value); }
    /// <summary>The command to execute when the action bar is dismissed.</summary>
    public ICommand? DismissCommand { get => GetValue(DismissCommandProperty); set => SetValue(DismissCommandProperty, value); }
    /// <summary>The argument passed to the dismiss command.</summary>
    public object? DismissCommandParameter { get => GetValue(DismissCommandParameterProperty); set => SetValue(DismissCommandParameterProperty, value); }

    private readonly TranslateTransform offset = new();
    private Border? surface;
    private Control? host;
    private InputElement? returnFocus;
    private VisibilityAnimation? animation;
    private Action<TimeSpan>? onFrame;
    private bool framePending;

    /// <summary>Initializes a new instance of <see cref="AlderActionBar"/>.</summary>
    public AlderActionBar() => AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);

    /// <summary>Runs the dismissal command, or closes the bar when no command is provided.</summary>
    public bool TryDismiss()
    {
        if (!IsOpen) return false;
        if (DismissCommand is { } command)
        {
            if (!command.CanExecute(DismissCommandParameter)) return false;
            command.Execute(DismissCommandParameter);
        }
        else SetCurrentValue(IsOpenProperty, false);
        return true;
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        surface = e.NameScope.Find<Border>("PART_Surface");
        if (surface is not null) surface.RenderTransform = offset;
        UpdateSections();
        UpdatePresentation(animate: false);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        host = this.GetVisualParent() as Control ?? TopLevel.GetTopLevel(this);
        if (host is not null) host.SizeChanged += HostSizeChanged;
        UpdateCompactState();
        if (IsOpen) CaptureReturnFocus();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (host is not null) host.SizeChanged -= HostSizeChanged;
        host = null;
        returnFocus = null;
        UpdatePresentation(animate: false);
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            if (IsOpen) CaptureReturnFocus();
            else RestoreFocus();
            UpdatePresentation(animate: true);
        }
        else if (change.Property == PrefixProperty || change.Property == SuffixProperty)
        {
            if (change.OldValue is Control old) LogicalChildren.Remove(old);
            if (change.NewValue is Control next) LogicalChildren.Add(next);
            UpdateSections();
        }
        else if (change.Property == ContentProperty || change.Property == OrientationProperty ||
                 change.Property == ShowSeparatorsProperty || change.Property == IsAttachedProperty)
            UpdateSections();
        else if (change.Property == CompactWidthProperty)
            UpdateCompactState();
        else if (change.Property == AlderMotion.ReducedProperty && AlderMotion.GetReduced(this))
            UpdatePresentation(animate: false);
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsOpen) e.Handled = true;
        else if (DismissOnEscape && e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
            e.Handled = TryDismiss();
    }

    private void CaptureReturnFocus()
    {
        if (!IsKeyboardFocusWithin && TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is InputElement focused)
            returnFocus = focused;
    }

    private void RestoreFocus()
    {
        if (IsKeyboardFocusWithin && returnFocus is { IsEffectivelyEnabled: true, IsEffectivelyVisible: true } target &&
            TopLevel.GetTopLevel(target) == TopLevel.GetTopLevel(this))
            target.Focus(NavigationMethod.Tab);
        returnFocus = null;
    }

    private void HostSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateCompactState();

    private void UpdateCompactState() =>
        PseudoClasses.Set(":compact", host?.Bounds.Width is > 0 && host.Bounds.Width < CompactWidth);

    private void UpdateSections()
    {
        PseudoClasses.Set(":prefix", Prefix is not null);
        PseudoClasses.Set(":content", Content is not null);
        PseudoClasses.Set(":suffix", Suffix is not null);
        PseudoClasses.Set(":leading-separator", ShowSeparators && Prefix is not null && (Content is not null || Suffix is not null));
        PseudoClasses.Set(":trailing-separator", ShowSeparators && Content is not null && Suffix is not null);
        PseudoClasses.Set(":vertical", Orientation == Orientation.Vertical);
        PseudoClasses.Set(":detached", !IsAttached);
    }

    private void UpdatePresentation(bool animate)
    {
        if (surface is not { } border) return;
        animation = null;
        border.IsHitTestVisible = IsOpen;
        KeyboardNavigation.SetTabNavigation(border, IsOpen ? KeyboardNavigationMode.Continue : KeyboardNavigationMode.None);

        if (!animate || AlderMotion.GetReduced(this) || TopLevel.GetTopLevel(this) is null || !IsOpen && !border.IsVisible)
        {
            FinishAnimation();
            return;
        }

        if (!border.IsVisible)
        {
            border.Opacity = 0;
            offset.Y = 13;
            border.IsVisible = true;
        }
        animation = new VisibilityAnimation(border.Opacity, offset.Y, Stopwatch.GetTimestamp());
        RequestFrame();
    }

    private void RequestFrame()
    {
        if (framePending || TopLevel.GetTopLevel(this) is not { } root) return;
        framePending = true;
        root.RequestAnimationFrame(onFrame ??= RenderFrame);
    }

    private void RenderFrame(TimeSpan _)
    {
        framePending = false;
        if (animation is not { } transition || surface is not { } border) return;
        double elapsed = Stopwatch.GetElapsedTime(transition.Started).TotalMilliseconds;
        double progress = elapsed / (IsOpen ? 210 : 145);
        if (progress >= 1)
        {
            FinishAnimation();
            return;
        }

        double remaining = 1 - progress;
        double eased = IsOpen ? 1 - remaining * remaining * remaining : progress * progress;
        border.Opacity = transition.Opacity + ((IsOpen ? 1 : 0) - transition.Opacity) * eased;
        offset.Y = transition.Offset + ((IsOpen ? 0 : 13) - transition.Offset) * eased;
        RequestFrame();
    }

    private void FinishAnimation()
    {
        animation = null;
        if (surface is not { } border) return;
        border.Opacity = IsOpen ? 1 : 0;
        border.IsVisible = IsOpen;
        offset.Y = IsOpen ? 0 : 13;
    }

    private sealed record VisibilityAnimation(double Opacity, double Offset, long Started);
}
