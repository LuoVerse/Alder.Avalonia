using System.Windows.Input;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

internal sealed class ToastCard : Border
{
    private readonly Action<ToastCloseReason> close;
    private IPointer? dragPointer;
    private double dragOrigin;
    private double dragOffset;
    private Button? dismissButton;
    private Grid? messageLayout;
    private AlderButton? actionButton;
    private bool hasIndicator;
    private bool compact;
    private bool loading;
    private PathIcon? settlingIndicator;

    public int SwipeDirection { get; private set; }

    public ToastCard(ToastOptions options, Action<ToastCloseReason> close)
    {
        this.close = close;
        Name = "AlderToastCard";
        CornerRadius = new CornerRadius(24);
        this[!BackgroundProperty] = new DynamicResourceExtension("AlderSurface");
        this[!BoxShadowProperty] = new DynamicResourceExtension("AlderToolbarShadow");
        SetContent(options);
    }

    public void SetContent(ToastOptions options)
    {
        dismissButton = null;
        actionButton = null;
        settlingIndicator = null;
        hasIndicator = options.ShowIndicator && options.Content is null;
        AutomationProperties.SetName(this, string.IsNullOrEmpty(options.Description)
            ? options.Title : options.Title + ". " + options.Description);
        AutomationProperties.SetLiveSetting(this, options.Variant == ToastVariant.Danger
            ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);

        var layout = new Grid
        {
            Margin = new Thickness(16, 12), ColumnSpacing = 6, RowSpacing = 8
        };
        var ink = options.Variant switch
        {
            ToastVariant.Accent => "AlderSoftInk",
            ToastVariant.Success => "AlderDialogSuccessInk",
            ToastVariant.Warning => "AlderDialogWarningInk",
            ToastVariant.Danger => "AlderDangerInk",
            _ => "AlderInk"
        };
        if (options.Content is { } content)
        {
            var custom = new ContentPresenter
            {
                Content = content, ContentTemplate = options.ContentTemplate
            };
            Grid.SetColumn(custom, hasIndicator ? 1 : 0);
            layout.Children.Add(custom);
        }
        else
        {
            var words = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            words.Children.Add(Text(options.Title, ink, FontWeight.Medium));
            if (!string.IsNullOrEmpty(options.Description))
                words.Children.Add(Text(options.Description, "AlderMuted", FontWeight.Normal));
            Grid.SetColumn(words, hasIndicator ? 1 : 0);
            layout.Children.Add(words);
        }

        if (hasIndicator)
        {
            var (key, color) = options.Variant switch
            {
                ToastVariant.Accent => ("AlderIconInfo", "AlderSoftInk"),
                ToastVariant.Success => ("AlderIconCheck", "AlderDialogSuccessInk"),
                ToastVariant.Warning => ("AlderIconWarning", "AlderDialogWarningInk"),
                ToastVariant.Danger => ("AlderIconAlert", "AlderDangerInk"),
                ToastVariant.Loading => ("AlderSpinnerGlyph", "AlderSoftInk"),
                _ => ("AlderIconInfo", "AlderInk")
            };
            PathIcon indicator = options is { Variant: ToastVariant.Loading, Indicator: null }
                ? new ButtonSpinner() : new AlderPathIcon();
            indicator.Width = indicator.Height = 16;
            indicator.Margin = new Thickness(4);
            indicator.VerticalAlignment = VerticalAlignment.Top;
            if (indicator is ButtonSpinner)
                indicator[!OpacityMaskProperty] = new DynamicResourceExtension("AlderSpinnerMask");
            if (options.Indicator is { } data) indicator.Data = data;
            else indicator[!PathIcon.DataProperty] = new DynamicResourceExtension(key);
            indicator[!PathIcon.ForegroundProperty] = new DynamicResourceExtension(color);
            layout.Children.Add(indicator);
            if (loading && options.Variant != ToastVariant.Loading) settlingIndicator = indicator;
        }
        loading = options.Variant == ToastVariant.Loading;

        if (!string.IsNullOrEmpty(options.ActionText) && options.Action is { } command)
        {
            var action = new AlderButton
            {
                Content = options.ActionText, Size = AlderSize.Small, Variant = ButtonVariant.Secondary,
                Command = new CloseAfterAction(command, close), CommandParameter = options.ActionParameter,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            actionButton = action;
            layout.Children.Add(action);
        }

        messageLayout = layout;
        var root = new Grid { Children = { layout } };
        if (options.ShowCloseButton)
        {
            var icon = new AlderPathIcon { Width = 8, Height = 8, IsHitTestVisible = false };
            icon[!PathIcon.DataProperty] = new DynamicResourceExtension("AlderIconXmark");
            var dismiss = new AlderButton
            {
                Name = "AlderToastCloseButton", Content = icon,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -4, -4, 0)
            };
            dismiss[!ThemeProperty] = new DynamicResourceExtension("AlderToastCloseButtonTheme");
            dismiss.Click += (_, _) => close(ToastCloseReason.Dismissed);
            root.Children.Add(dismiss);
            dismissButton = dismiss;
        }
        Child = root;
        ConfigureColumns();
        UpdateCloseVisibility();
    }

    public void SetCompact(bool value)
    {
        if (compact == value) return;
        compact = value;
        ConfigureColumns();
    }

    private void ConfigureColumns()
    {
        if (messageLayout is not { } layout) return;
        var columns = hasIndicator ? "Auto,*" : "*";
        if (!compact && actionButton is not null) columns += ",Auto";
        layout.ColumnDefinitions = new ColumnDefinitions(columns);
        layout.RowDefinitions = new RowDefinitions(compact && actionButton is not null ? "Auto,Auto" : "Auto");
        if (actionButton is { } action)
        {
            Grid.SetColumn(action, (hasIndicator ? 1 : 0) + (compact ? 0 : 1));
            Grid.SetRow(action, compact ? 1 : 0);
        }
        dismissButton?.Classes.Set("compact", compact);
    }

    public void SetExpandedContent(bool visible)
    {
        if (messageLayout is not { } layout || layout.IsEnabled == visible) return;
        layout.Transitions = AlderMotion.GetReduced(this) ? null : new Transitions
        {
            new DoubleTransition
            {
                Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(200),
                Easing = new SplineEasing(.32, .72, 0, 1)
            }
        };
        layout.Opacity = visible ? 1 : 0;
        layout.IsEnabled = visible;
    }

    public void StopInteraction()
    {
        var pointer = dragPointer;
        dragPointer = null;
        pointer?.Capture(null);
        IsHitTestVisible = false;
        IsEnabled = false;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        if (settlingIndicator is not { } indicator) return size;
        settlingIndicator = null;
        if (!AlderMotion.GetReduced(this))
            OverlayMotion.Animate(indicator, true, default, .92, TimeSpan.FromMilliseconds(200),
                TimeSpan.FromMilliseconds(150), new Rect(indicator.Bounds.Size).Center, new CubicEaseOut());
        return size;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPointerOverProperty || change.Property == IsKeyboardFocusWithinProperty)
            UpdateCloseVisibility();
    }

    private void UpdateCloseVisibility()
    {
        if (dismissButton is not { } button) return;
        bool visible = IsPointerOver || IsKeyboardFocusWithin;
        button.Opacity = visible ? 1 : 0;
        button.IsHitTestVisible = visible;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().TakeWhile(visual => visual != this)
                .OfType<InputElement>().Any(input => input.Focusable)) return;
        dragPointer = e.Pointer;
        dragOrigin = e.GetPosition(this.GetVisualParent()).X;
        dragOffset = 0;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer != dragPointer) return;
        dragOffset = e.GetPosition(this.GetVisualParent()).X - dragOrigin;
        if (!AlderMotion.GetReduced(this)) DialogMotion.SetDragOffset(this, new Vector3D(dragOffset, 0, 0));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer != dragPointer) return;
        dragPointer = null;
        e.Pointer.Capture(null);
        if (Math.Abs(dragOffset) > Bounds.Width * .22)
        {
            SwipeDirection = Math.Sign(dragOffset);
            close(ToastCloseReason.Dismissed);
        }
        else if (!AlderMotion.GetReduced(this)) DialogMotion.SnapBack(this);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (e.Pointer != dragPointer) return;
        dragPointer = null;
        if (!AlderMotion.GetReduced(this)) DialogMotion.SnapBack(this);
    }

    private sealed class CloseAfterAction(ICommand command, Action<ToastCloseReason> close) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add => command.CanExecuteChanged += value;
            remove => command.CanExecuteChanged -= value;
        }

        public bool CanExecute(object? parameter) => command.CanExecute(parameter);

        public void Execute(object? parameter)
        {
            command.Execute(parameter);
            close(ToastCloseReason.Action);
        }
    }

    private static TextBlock Text(string value, string ink, FontWeight weight)
    {
        var text = new AlderTextBlock
        {
            Text = value, FontWeight = weight, FontSize = 14, LineHeight = 19, TextWrapping = TextWrapping.Wrap
        };
        text[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(ink);
        return text;
    }
}
