using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;


namespace Alder.Avalonia.Controls;

public sealed partial class AlderRichTextEditor
{
    private WrapPanel? toolbar;
    private TextBlock? counter, placeholder;
    private Popup? popup;
    private enum Menu { None, Bubble, Floating, Slash }
    private Menu menu;
    private AlderPopover? linkPopover;
    private bool contextQueued;
    private int highlighted;
    private string? currentQuery;
    private IReadOnlyList<RichTextSuggestion> matches = [];

    private static readonly RichTextCommand[] Commands = Enum.GetValues<RichTextCommand>();
    private static readonly RichTextCommand[] InlineCommands = [RichTextCommand.Bold, RichTextCommand.Italic, RichTextCommand.Underline, RichTextCommand.Strike, RichTextCommand.Link];
    private static readonly RichTextCommand[] Blocks = [RichTextCommand.Heading1, RichTextCommand.Heading2, RichTextCommand.Heading3,
        RichTextCommand.BulletList, RichTextCommand.OrderedList, RichTextCommand.Quote, RichTextCommand.CodeBlock];

    private void BuildToolbar()
    {
        if (toolbar is null) return;
        toolbar.Children.Clear();
        int lastGroup = -1;
        foreach (var command in ToolbarCommands ?? Commands)
        {
            int group = command switch
            {
                <= RichTextCommand.Redo => 0, <= RichTextCommand.Code => 1, <= RichTextCommand.CodeBlock => 2,
                <= RichTextCommand.Link => 3, _ => 4
            };
            if (lastGroup >= 0 && group != lastGroup)
            {
                var separator = new Border { Width = 1, Height = 24, Margin = new Thickness(4, 0), VerticalAlignment = VerticalAlignment.Center };
                separator[!Border.BackgroundProperty] = new DynamicResourceExtension("AlderSeparator");
                toolbar.Children.Add(separator);
            }
            toolbar.Children.Add(CommandButton(command));
            lastGroup = group;
        }
        Refresh();
    }

    private Button CommandButton(RichTextCommand command)
    {
        var button = new AlderButton { Tag = command, Classes = { "rich-command" } };
        button[!AutomationProperties.NameProperty] = new DynamicResourceExtension("AlderText.Rich" + command);
        button[!AlderToolTip.TipProperty] = new DynamicResourceExtension("AlderText.Rich" + command);
        button.Content = Icon(CommandIcon(command));
        button.Classes.Set("tertiary", command is RichTextCommand.Undo or RichTextCommand.Redo or RichTextCommand.ClearFormatting or RichTextCommand.ClearContent);
        if (command == RichTextCommand.Link)
        {
            button.Classes.Add("rich-link");
            button.Click += (_, _) => OpenLink(button);
        }
        else button.Click += (_, _) => Execute(command);
        UpdateButton(button);
        return button;
    }

    private static string CommandIcon(RichTextCommand command) => command switch
    {
        RichTextCommand.Bold or RichTextCommand.Italic or RichTextCommand.Underline => "AlderIcon" + command,
        RichTextCommand.ClearContent => "AlderIconTrash",
        _ => "AlderRichIcon" + command
    };

    private static PathIcon Icon(string resourceKey)
    {
        var icon = new AlderPathIcon();
        icon[!ThemeProperty] = new DynamicResourceExtension("AlderIcon16Theme");
        icon[!PathIcon.DataProperty] = new DynamicResourceExtension(resourceKey);
        return icon;
    }

    private void UpdateButton(Button button)
    {
        if (button.Tag is not RichTextCommand command) return;
        button.Classes.Set("active", input is not null && IsActive(command));
        button.IsEnabled = Editable && command switch
        {
            RichTextCommand.Undo => document.CanUndo,
            RichTextCommand.Redo => document.CanRedo,
            RichTextCommand.ClearContent => document.Length > 0 || document.Line(0).Kind != RichBlock.Paragraph,
            _ => true
        };
    }

    private string Caption(string name) => this.TryFindResource("AlderText.Rich" + name, out var value) ? (string)value! : name;

    private void Refresh()
    {
        if (input is null) return;
        if (toolbar is not null) foreach (var button in toolbar.Children.OfType<Button>()) UpdateButton(button);
        if (placeholder is not null) placeholder.IsVisible = document.Length == 0 && document.Line(0).Kind == RichBlock.Paragraph;
        if (counter is not null)
        {
            var characters = MaxLength > 0 ? $"{CharacterCount} / {MaxLength}" : CharacterCount.ToString(CultureInfo.CurrentCulture);
            counter.Text = string.Format(CultureInfo.CurrentCulture, Caption(ShowWordCount ? "Counts" : "Characters"), characters, WordCount);
            counter.Classes.Set("over-limit", MaxLength > 0 && CharacterCount > MaxLength);
        }
    }

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        QueueContext();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        QueueContext();
    }

    private void QueueContext()
    {
        if (input is null || contextQueued) return;
        contextQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            contextQueued = false;
            if (input is null || popup is null || !Editable || linkPopover is not null) return;
            if (!input.IsKeyboardFocusWithin && popup.Child?.IsKeyboardFocusWithin != true)
            {
                ClosePopup();
                return;
            }
            var offset = Math.Min(input.CaretIndex, document.Length);
            var row = document.Row(offset);
            var line = document.Line(row);
            var before = PlainText.AsSpan(document.Start(row), offset - document.Start(row));
            if (ShowSlashMenu && Selection.Length == 0 && !before.IsEmpty && before[0] == '/' && !before.Contains(' '))
                OpenSuggestions(before[1..].ToString());
            else if (ShowBubbleMenu && Selection.Length > 0) OpenContext(Menu.Bubble, InlineCommands);
            else if (ShowFloatingMenu && line.Length == 0 && Selection.Length == 0)
                OpenContext(Menu.Floating, [RichTextCommand.Heading2, RichTextCommand.BulletList, RichTextCommand.Quote]);
            else ClosePopup();
        });
    }

    private void OpenContext(Menu kind, RichTextCommand[] commands)
    {
        if (menu != kind)
        {
            var panel = new WrapPanel { Orientation = Orientation.Horizontal };
            if (kind == Menu.Floating && FloatingCommands is { } custom)
            {
                foreach (var entry in custom)
                {
                    var button = new AlderButton { Classes = { "rich-command", "tertiary" },
                        Content = entry.IconResourceKey is { } key ? Icon(key) : entry.Title };
                    AutomationProperties.SetName(button, entry.Title);
                    AlderToolTip.SetTip(button, entry.Title);
                    button.Click += (_, _) => { ClosePopup(); entry.Execute(this); };
                    panel.Children.Add(button);
                }
            }
            else foreach (var cmd in commands) panel.Children.Add(CommandButton(cmd));
            ShowPopup(kind, panel);
        }
        else if (popup?.Child is Border { Child: Panel buttons })
            foreach (var button in buttons.Children.OfType<Button>()) UpdateButton(button);
        PositionPopup();
    }

    private void OpenSuggestions(string query)
    {
        if (menu == Menu.Slash && currentQuery == query) { PositionPopup(); return; }
        var entries = Blocks.Select(cmd => new RichTextSuggestion(Caption(cmd.ToString()), editor => editor.Execute(cmd))
            { Description = Caption(cmd + "Hint"), IconResourceKey = CommandIcon(cmd), Keywords = cmd.ToString() });
        matches = entries.Concat(Suggestions ?? []).Where(entry => entry.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            entry.Keywords?.Contains(query, StringComparison.OrdinalIgnoreCase) == true).ToArray();
        var panel = new StackPanel { Spacing = 4 };
        highlighted = 0;
        for (int index = 0; index < matches.Count; index++)
        {
            int choice = index;
            var entry = matches[index];
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            if (entry.IconResourceKey is { } key)
            {
                var badge = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(18), Child = Icon(key) };
                badge[!Border.BackgroundProperty] = new DynamicResourceExtension("AlderNeutral");
                content.Children.Add(badge);
            }
            var labels = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            labels.Children.Add(new AlderTextBlock { Text = entry.Title, FontSize = 14, FontWeight = FontWeight.Medium });
            if (entry.Description is { } hint)
            {
                var detail = new AlderTextBlock { Text = hint, FontSize = 12 };
                detail[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("AlderMuted");
                labels.Children.Add(detail);
            }
            content.Children.Add(labels);
            var button = new AlderButton { Classes = { "rich-suggestion" }, Content = content, Focusable = false };
            AutomationProperties.SetName(button, entry.Title);
            button.Click += (_, _) => ApplySuggestion(choice);
            button.PointerEntered += (_, _) => Highlight(choice);
            panel.Children.Add(button);
        }
        if (matches.Count == 0) panel.Children.Add(new AlderTextBlock { Text = Caption("NoCommands"), Margin = new Thickness(8) });
        currentQuery = query;
        ShowPopup(Menu.Slash, new AlderScrollViewer { Width = Math.Min(312, Math.Max(120, Bounds.Width - 28)), MaxHeight = 376, Content = panel });
        Highlight(0);
    }

    private void Highlight(int index)
    {
        highlighted = index;
        if (popup?.Child is not Border { Child: ScrollViewer { Content: StackPanel panel } }) return;
        foreach (var button in panel.Children.OfType<Button>()) button.Classes.Set("active", panel.Children.IndexOf(button) == index);
    }

    private void ApplySuggestion(int index)
    {
        if (input is null || !Editable || index < 0 || index >= matches.Count) return;
        var action = matches[index];
        var start = document.Start(document.Row(input.CaretIndex));
        document.Replace(start, input.CaretIndex - start, "", default);
        Synchronize(true);
        Select(start, 0);
        ClosePopup();
        action.Execute(this);
        FocusContent();
    }

    private void OpenLink(Button? trigger = null)
    {
        const double padding = 12;
        const double inset = 12;
        const double width = 320;
        if (input is null || !Editable || TopLevel.GetTopLevel(this) is not { } topLevel) return;
        if (linkPopover is { IsOpen: true } current)
        {
            if (!current.IsClosing && (trigger is null || ReferenceEquals(current.Target, trigger)))
            {
                current.Hide();
                FocusContent();
                return;
            }
            current.HideImmediately();
        }
        trigger ??= toolbar?.Children.OfType<Button>().FirstOrDefault(button => button.Tag is RichTextCommand.Link);
        var anchor = (Control?)trigger ?? presenter;
        if (anchor is null || !anchor.IsAttachedToVisualTree()) return;
        if (trigger is null || toolbar?.IsVisualAncestorOf(trigger) == true) ClosePopup();
        var initial = Selection.Length > 0 ? document.At(Selection.Start) : document.InputStyle(input.CaretIndex);
        var url = new AlderInput { Text = initial.Link, PlaceholderText = "https://example.com",
            Variant = InputVariant.Secondary, FullWidth = true };
        url[!AutomationProperties.NameProperty] = new DynamicResourceExtension("AlderText.RichLinkUrl");
        var remove = new AlderButton { Content = Caption("Remove"), Variant = ButtonVariant.Tertiary,
            Size = AlderSize.Small, IsEnabled = initial.Link is not null };
        var apply = new AlderButton { Content = Caption("Apply"), Size = AlderSize.Small,
            IsEnabled = !string.IsNullOrWhiteSpace(url.Text) };
        url.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) apply.IsEnabled = !string.IsNullOrWhiteSpace(url.Text);
        };
        remove.Click += (_, _) => SetLink(null);
        apply.Click += (_, _) => SetLink(url.Text);
        url.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            if (apply.IsEnabled) SetLink(url.Text);
        };
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right,
            ItemSpacing = 8, LineSpacing = 8, Children = { remove, apply } };
        var content = new StackPanel { Spacing = 12, Children = { url, actions } };
        var popover = new AlderPopover
        {
            Content = content,
            ContentPadding = new Thickness(padding),
            SurfaceCornerRadius = new CornerRadius(16),
            MaxContentWidth = width,
            ShowArrow = true,
            Placement = PlacementMode.Custom,
            CustomPopupPlacementCallback = PlaceLink,
            OverlayInputPassThroughElement = trigger,
            OverlayDismissEventPassThrough = true,
            FocusContentOnOpen = false,
            FocusReturnTarget = input,
            AnchorRect = trigger is null ? presenter?.TextLayout.HitTestTextPosition(input.CaretIndex) : null
        };
        content.Width = LinkContentWidth(topLevel.ClientSize.Width);
        popover.Closed += (_, _) =>
        {
            topLevel.SizeChanged -= ResizeContent;
            trigger?.Classes.Remove("link-open");
            linkPopover = null;
            QueueContext();
        };
        linkPopover = popover;
        trigger?.Classes.Add("link-open");
        popover.ShowAt(anchor);
        if (popover.IsOpen) topLevel.SizeChanged += ResizeContent;

        void ResizeContent(object? sender, SizeChangedEventArgs e) => content.Width = LinkContentWidth(e.NewSize.Width);
        static double LinkContentWidth(double available) => Math.Clamp(available - 2 * (inset + padding), 0, width - 2 * padding);

        void PlaceLink(CustomPopupPlacement placement)
        {
            placement.Anchor = PopupAnchor.Bottom | PopupAnchor.Left;
            placement.Gravity = PopupGravity.Bottom | PopupGravity.Right;
            placement.AnchorRectangle = placement.AnchorRectangle.Inflate(new Thickness(0, 8));
            placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.FlipY |
                PopupPositionerConstraintAdjustment.SlideX | PopupPositionerConstraintAdjustment.SlideY;
            double x = placement.AnchorRectangle.X;
            double surfaceWidth = placement.PopupSize.Deflate(placement.Deflate).Width;
            double right = Math.Max(inset, topLevel.ClientSize.Width - surfaceWidth - inset);
            placement.Offset = new Point(Math.Clamp(x, inset, right) - x, 0);
        }
    }

    private void CloseLink(bool immediately = false)
    {
        if (immediately) linkPopover?.HideImmediately();
        else linkPopover?.Hide();
    }

    private void ShowPopup(Menu kind, Control content)
    {
        if (popup is null || input is null) return;
        menu = kind;
        // Popup attaches its child to the host on open. Keep that root when the query or menu changes.
        if (popup.Child is not Border border)
            popup.Child = border = new Border { Classes = { "rich-popup" } };
        border.Classes.Set("floating", kind == Menu.Floating);
        border.Child = content;
        PositionPopup();
        popup.IsOpen = true;
    }

    private void PositionPopup()
    {
        if (popup is null || input is null) return;
        if (presenter is null) return;
        int anchor = menu == Menu.Slash ? document.Start(document.Row(input.CaretIndex)) : input.CaretIndex;
        var bounds = presenter.TextLayout.HitTestTextPosition(anchor);
        popup.PlacementTarget = presenter;
        popup.PlacementRect = menu == Menu.Floating
            ? new Rect(bounds.X + 2, bounds.Y, 1, bounds.Height)
            : new Rect(bounds.X, bounds.Y, 1, bounds.Height);
        popup.VerticalOffset = menu == Menu.Floating ? -(bounds.Height + 8) : 4;
        popup.Placement = menu == Menu.Bubble ? PlacementMode.TopEdgeAlignedLeft : PlacementMode.BottomEdgeAlignedLeft;
    }

    private void PopupClosed(object? sender, EventArgs e)
    {
        menu = Menu.None;
        currentQuery = null;
    }

    private void ClosePopup()
    {
        menu = Menu.None;
        currentQuery = null;
        if (popup is not null) popup.IsOpen = false;
    }

    private void EditorKeyDown(object? sender, KeyEventArgs e)
    {
        backspace = e.Key == Key.Back;
        if (e.Key == Key.Escape && menu != Menu.None) { ClosePopup(); e.Handled = true; return; }
        if (!Editable) return;
        if (menu == Menu.Slash && matches.Count > 0)
        {
            if (e.Key is Key.Up or Key.Down)
            {
                Highlight((highlighted + matches.Count + (e.Key == Key.Up ? -1 : 1)) % matches.Count);
                if (popup?.Child is Border { Child: ScrollViewer { Content: StackPanel panel } }) panel.Children[highlighted].BringIntoView();
                e.Handled = true;
                return;
            }
            if (e.Key is Key.Enter or Key.Tab) { ApplySuggestion(highlighted); e.Handled = true; return; }
        }
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) == 0) return;
        RichTextCommand? command = e.Key switch
        {
            Key.B => RichTextCommand.Bold, Key.I => RichTextCommand.Italic, Key.U => RichTextCommand.Underline,
            Key.K => RichTextCommand.Link, Key.Z => e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? RichTextCommand.Redo : RichTextCommand.Undo,
            Key.Y => RichTextCommand.Redo, _ => null
        };
        if (command.HasValue) { Execute(command.Value); e.Handled = true; }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CloseLink(immediately: true);
        ClosePopup();
        base.OnDetachedFromVisualTree(e);
    }
}
