using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

internal sealed class DialogSurface : Border
{
    private readonly List<Control> dragZones = [];
    private readonly ContentPresenter body;
    private readonly ContentPresenter? customFooter;
    private readonly AlderButton? secondaryAction;
    private readonly AlderButton? primaryAction;
    private readonly Button? closeButton;
    private readonly bool isAlert;
    private readonly bool scrollOutside;
    private readonly Func<DialogResult, bool> close;
    private readonly IDialogContext? context;

    public DialogSurface(object? content, OverlayOptions options, Func<DialogResult, bool> close, FlowDirection? direction = null)
    {
        this.close = close;
        if (direction is { } flow) FlowDirection = flow;
        context = content as IDialogContext ?? (content as Control)?.DataContext as IDialogContext;
        isAlert = options is MessageBoxOptions;
        scrollOutside = options is DialogOptions { ScrollMode: DialogScrollMode.Outside, IsContentFullBleed: false };
        Name = "AlderDialogSurface";
        Focusable = true;
        KeyboardNavigation.SetIsTabStop(this, false);
        CornerRadius = new CornerRadius(24);
        this[!BackgroundProperty] = new DynamicResourceExtension("AlderSurface");
        this[!BoxShadowProperty] = new DynamicResourceExtension("AlderToolbarShadow");
        AutomationProperties.SetName(this, options.Title ?? "");
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Pane);

        var shell = new Grid();
        bool sheet = options is SheetOptions;
        bool fullBleed = options is DialogOptions { IsContentFullBleed: true };
        if (fullBleed)
        {
            Background = Brushes.Transparent;
            CornerRadius = new CornerRadius(16);
        }
        var frame = new DockPanel
        {
            LastChildFill = true,
            Margin = fullBleed ? default : sheet ? new Thickness(20, 16, 20, 16) : new Thickness(24)
        };
        var header = new StackPanel { Spacing = sheet ? 4 : 12, Background = Brushes.Transparent };
        var icon = CreateStatusIcon(options);
        if (icon is not null) header.Children.Add(icon);
        if (!string.IsNullOrEmpty(options.Title))
        {
            var title = new AlderTextBlock
            {
                Text = options.Title, FontSize = sheet ? 18 : 16, FontWeight = sheet ? FontWeight.SemiBold : FontWeight.Medium,
                TextWrapping = TextWrapping.Wrap
            };
            title[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("AlderInk");
            AutomationProperties.SetHeadingLevel(title, 2);
            header.Children.Add(title);
        }

        if (header.Children.Count > 0)
        {
            if (options.ShowCloseButton) header.Margin = new Thickness(0, 0, 32, 0);
            DockPanel.SetDock(header, Dock.Top);
            frame.Children.Add(header);
            dragZones.Add(header);
        }

        if (options.Footer is not null)
        {
            customFooter = new ContentPresenter
            {
                Content = options.Footer, Margin = new Thickness(0, sheet ? 8 : 20, 0, 0), Background = Brushes.Transparent
            };
            DockPanel.SetDock(customFooter, Dock.Bottom);
            frame.Children.Add(customFooter);
            dragZones.Add(customFooter);
        }
        else if (options.Buttons != DialogButtons.None)
        {
            var row = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right, Background = Brushes.Transparent,
                ItemSpacing = 8, LineSpacing = 8,
                Margin = new Thickness(0, sheet ? 8 : 20, 0, 0)
            };
            DockPanel.SetDock(row, Dock.Bottom);
            var primary = options.Buttons is DialogButtons.OK or DialogButtons.OKCancel ? DialogResult.OK : DialogResult.Yes;
            if (options.Buttons is DialogButtons.OKCancel or DialogButtons.YesNoCancel)
            {
                secondaryAction = CreateAction(DialogResult.Cancel, options.CancelText,
                    isAlert ? ButtonVariant.Tertiary : ButtonVariant.Secondary);
                row.Children.Add(secondaryAction);
            }
            if (options.Buttons is DialogButtons.YesNo or DialogButtons.YesNoCancel)
            {
                var no = CreateAction(DialogResult.No, null, isAlert ? ButtonVariant.Tertiary : ButtonVariant.Secondary);
                row.Children.Add(no);
                secondaryAction ??= no;
            }
            primaryAction = CreateAction(primary, options.PrimaryText,
                options.IsDestructive ? ButtonVariant.Danger : ButtonVariant.Primary);
            row.Children.Add(primaryAction);
            frame.Children.Add(row);
            dragZones.Add(row);
        }

        body = new ContentPresenter
        {
            Content = content is string message && options.ContentTemplate is null ? new AlderTextBlock
            {
                Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 14, LineHeight = 20
            } : content,
            ContentTemplate = options.ContentTemplate,
            Margin = fullBleed ? default : new Thickness(3, header.Children.Count > 0 ? sheet ? 16 : 8 : options.ShowCloseButton ? 24 : 3, 3, 3)
        };
        body[!TemplatedControl.ForegroundProperty] = new DynamicResourceExtension("AlderMuted");
        if (fullBleed) frame.Children.Add(body);
        else if (scrollOutside)
        {
            frame.Children.Add(body);
            // Initial focus belongs to the frame. Bring individual controls into view as they gain focus.
            AddHandler(RequestBringIntoViewEvent, (_, e) =>
            {
                if (e.TargetObject == this) e.Handled = true;
            });
        }
        else
        {
            var scroll = new AlderScrollViewer
            {
                Content = body, Margin = new Thickness(-3, 0, -3, 0),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            if (sheet)
            {
                scroll.Name = "AlderSheetBodyScroll";
                SheetBody = scroll;
            }
            frame.Children.Add(scroll);
        }

        // Docking the footer above the scrollable body keeps all actions reachable.
        shell.Children.Add(frame);
        if (options.ShowCloseButton)
        {
            var closeIcon = new AlderPathIcon { Width = 12, Height = 12 };
            closeIcon[!PathIcon.DataProperty] = new DynamicResourceExtension("AlderIconXmark");
            closeButton = new AlderButton
            {
                Name = "AlderDialogCloseButton",
                Content = closeIcon,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(sheet ? 12 : 16)
            };
            closeButton[!ThemeProperty] = new DynamicResourceExtension("AlderCloseButtonTheme");
            closeButton.Click += (_, _) => RequestClose(DialogResult.Cancel);
            shell.Children.Add(closeButton);
        }

        if (options is DrawerOptions { ShowHandle: true } drawer &&
            (sheet || drawer.Placement is DrawerPlacement.Top or DrawerPlacement.Bottom))
        {
            bool top = drawer.Placement == DrawerPlacement.Top;
            bool left = (drawer.Placement == DrawerPlacement.Left) != (sheet && direction == FlowDirection.RightToLeft);
            bool side = drawer.Placement is DrawerPlacement.Left or DrawerPlacement.Right;
            double inset = sheet ? 20 : 24;
            frame.Margin = side
                ? new Thickness(left ? inset : inset + 8, 16, left ? inset + 8 : inset, 16)
                : sheet
                    ? new Thickness(20, top ? 16 : 44, 20, top ? 44 : 16)
                    : new Thickness(inset, top ? inset : inset + 12, inset, top ? inset + 4 : inset);
            var handle = new Border
            {
                Name = sheet ? "AlderSheetHandle" : "AlderDrawerHandle", Background = Brushes.Transparent,
                Height = side ? 56 : sheet ? 28 : 20, Width = side ? 20 : 56,
                Focusable = options is SheetOptions { SnapPoints.Count: > 0 },
                HorizontalAlignment = side ? left ? HorizontalAlignment.Right : HorizontalAlignment.Left : HorizontalAlignment.Center,
                VerticalAlignment = side ? VerticalAlignment.Center : top ? VerticalAlignment.Bottom : VerticalAlignment.Top,
                Child = new Border
                {
                    Width = side ? 5 : sheet ? 32 : 36, Height = side ? 32 : sheet ? 5 : 4,
                    CornerRadius = new CornerRadius(3),
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                }
            };
            if (!sheet)
                ((Border)handle.Child)[!BackgroundProperty] = new DynamicResourceExtension("AlderSeparator");
            if (sheet)
            {
                SheetHandle = handle;
                handle.Classes.Add("alder-sheet-handle");
                handle.Cursor = new Cursor(StandardCursorType.Hand);
                handle[!AutomationProperties.NameProperty] = new DynamicResourceExtension("AlderText.ResizeSheet");
            }
            shell.Children.Add(handle);
            dragZones.Add(handle);
        }

        Child = shell;
    }

    public Control CreateViewport()
    {
        if (!scrollOutside) return this;
        return new AlderScrollViewer
        {
            Name = "AlderDialogOutsideScroll",
            Content = new Border { Child = this, Padding = new Thickness(3) },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            FlowDirection = FlowDirection
        };
    }

    public Control? SheetHandle { get; }
    public ScrollViewer? SheetBody { get; }
    public IReadOnlyList<Control> DragZones => dragZones;
    public bool RequestClose(DialogResult result) => close(result);

    public void FocusInitial()
    {
        if (scrollOutside && Focus()) return;
        if (isAlert && secondaryAction?.Focus() == true) return;
        foreach (var input in body.GetVisualDescendants().OfType<InputElement>())
        {
            if (KeyboardNavigation.GetIsTabStop(input) && input.Focus()) return;
        }

        if (primaryAction?.Focus() == true || closeButton?.Focus() == true) return;
        Focus();
    }

    public void ReleaseContent()
    {
        body.Content = null;
        if (customFooter is not null) customFooter.Content = null;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (context is not null) context.RequestClose += OnContextClose;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (context is not null) context.RequestClose -= OnContextClose;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnContextClose(object? sender, DialogResult result)
    {
        // Process requests after the host has attached the view, including requests raised by asynchronous work.
        Dispatcher.UIThread.Post(() =>
        {
            if (this.IsAttachedToVisualTree()) RequestClose(result);
        });
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);

    private AlderButton CreateAction(DialogResult result, string? text, ButtonVariant variant)
    {
        var button = new AlderButton { Name = "AlderDialog" + result, Variant = variant };
        if (text is not null) button.Content = text;
        else button[!ContentControl.ContentProperty] = new DynamicResourceExtension("AlderText.Dialog." + result);
        button.Click += (_, _) => RequestClose(result);
        return button;
    }

    private static Border? CreateStatusIcon(OverlayOptions options)
    {
        if (options is not MessageBoxOptions && options.Icon is null) return null;
        var (fill, ink, geometry) = (options as MessageBoxOptions)?.Status switch
        {
            MessageBoxStatus.Accent => ("AlderSoft", "AlderSoftInk", "AlderIconInfo"),
            MessageBoxStatus.Success => ("AlderDialogSuccessSoft", "AlderDialogSuccessInk", "AlderIconCheck"),
            MessageBoxStatus.Warning => ("AlderDialogWarningSoft", "AlderDialogWarningInk", "AlderIconWarning"),
            MessageBoxStatus.Danger => ("AlderDangerSoft", "AlderDangerInk", "AlderIconAlert"),
            _ => ("AlderNeutral", "AlderInk", "AlderIconInfo")
        };
        var icon = new AlderPathIcon { Width = 20, Height = 20, IsHitTestVisible = false };
        icon[!PathIcon.ForegroundProperty] = new DynamicResourceExtension(ink);
        if (options.Icon is not null) icon.Data = options.Icon;
        else icon[!PathIcon.DataProperty] = new DynamicResourceExtension(geometry);
        var badge = new Border
        {
            Width = 40, Height = 40, CornerRadius = new CornerRadius(20),
            HorizontalAlignment = HorizontalAlignment.Left, Child = icon
        };
        badge[!BackgroundProperty] = new DynamicResourceExtension(fill);
        return badge;
    }
}
