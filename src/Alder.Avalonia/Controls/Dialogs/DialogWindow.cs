using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;

namespace Alder.Avalonia.Controls;

internal sealed class DialogWindow : AlderWindow
{
    private readonly DialogSurface surface;
    private readonly Control viewport;
    private readonly DialogOptions options;
    private IDisposable? closingTimer;
    private DialogResult? closingResult;
    private bool finishing;
    private bool opened;

    private DialogWindow(Window owner, object? content, DialogOptions options)
    {
        this.options = options;
        var screen = owner.Screens.ScreenFromWindow(owner) ?? owner.Screens.Primary;
        var workArea = screen?.WorkingArea;
        double scale = screen?.Scaling ?? owner.RenderScaling;
        double width = workArea is { } area ? area.Width / scale : 1024;
        double height = workArea is { } screenArea ? screenArea.Height / scale : 768;
        const double shadowMargin = 24;

        Title = options.Title ?? "";
        ShowInTaskbar = false;
        CanResize = false;
        WindowDecorations = WindowDecorations.None;
        ExtendClientAreaToDecorationsHint = false;
        ShowTitleBar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        this[!TransparencyBackgroundFallbackProperty] = new DynamicResourceExtension("AlderSurface");
        RequestedThemeVariant = owner.ActualThemeVariant;
        FlowDirection = options.FlowDirection ?? owner.FlowDirection;
        Width = Math.Min(options.MaxPanelWidth + shadowMargin * 2, width);
        MaxHeight = height;
        SizeToContent = SizeToContent.Height;
        if (options.Size is DialogSize.Cover or DialogSize.Full)
        {
            SizeToContent = SizeToContent.Manual;
            Height = height;
        }

        AlderMotion.SetReduced(this, AlderMotion.GetReduced(owner));
        surface = new DialogSurface(content, options, RequestClose, FlowDirection)
        {
            Margin = new Thickness(options.Size == DialogSize.Full ? 0 : shadowMargin)
        };
        if (options.Size == DialogSize.Full)
        {
            surface.CornerRadius = default;
            surface.BoxShadow = default;
        }
        viewport = surface.CreateViewport();
        KeyboardNavigation.SetTabNavigation(viewport, KeyboardNavigationMode.Cycle);
        Content = viewport;
        LayoutUpdated += OnFirstLayout;
    }

    public static Task<DialogResult> ShowAsync(Window owner, object? content, DialogOptions options)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Dispatcher.UIThread.VerifyAccess();
        return new DialogWindow(owner, content, options).ShowDialog<DialogResult>(owner);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Key != Key.Escape) return;
        e.Handled = true;
        if (options.CanEscapeDismiss) RequestClose(DialogResult.Cancel);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || finishing || e.CloseReason != WindowCloseReason.WindowClosing) return;
        e.Cancel = true;
        RequestClose(DialogResult.Cancel);
    }

    protected override void OnClosed(EventArgs e)
    {
        closingTimer?.Dispose();
        LayoutUpdated -= OnFirstLayout;
        surface.ReleaseContent();
        base.OnClosed(e);
    }

    private void OnFirstLayout(object? sender, EventArgs e)
    {
        if (surface.Bounds.Width <= 0 || surface.Bounds.Height <= 0) return;
        LayoutUpdated -= OnFirstLayout;
        opened = true;
        DialogMotion.Play(viewport, null, options, DialogPlacement.Center, entering: true);
        surface.FocusInitial();
    }

    private bool RequestClose(DialogResult result)
    {
        if (closingResult.HasValue) return false;
        closingResult = result;
        surface.IsHitTestVisible = false;
        var duration = opened ? DialogMotion.Play(viewport, null, options, DialogPlacement.Center, entering: false) : TimeSpan.Zero;
        // A platform Closing event must return before closing the window again.
        if (duration == TimeSpan.Zero)
            Dispatcher.UIThread.Post(Finish, DispatcherPriority.Input);
        else
            closingTimer = DispatcherTimer.RunOnce(Finish, duration, DispatcherPriority.Render);
        return true;
    }

    private void Finish()
    {
        if (!IsVisible) return;
        finishing = true;
        Close(closingResult ?? DialogResult.None);
    }
}
