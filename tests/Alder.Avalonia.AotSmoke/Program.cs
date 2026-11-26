using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Alder.Avalonia.AotSmoke;
using Alder.Avalonia.Controls;
using Alder.Avalonia.Themes;
using SkiaSharp;

var bindingLog = new BindingLog();
Logger.Sink = bindingLog;
AppBuilder.Configure<SmokeApp>().UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

var model = new SmokeModel();
var window = new SmokeWindow { DataContext = model };
AlderMotion.SetReduced(window, true);
window.Show();
try
{
    VerifyBindings();
    VerifyCollections();
    VerifyPopups();
    VerifyNavigation();
    VerifyMorphing();
    VerifyExpandableTabs();
    VerifyDialogs();
    VerifyOverlays();
    VerifyAlderWindow();
    VerifyAlderWindowStateRendering();
    VerifyFeedback();
    VerifyLayout();
    VerifyPickers();
    VerifyFormControls();
    ControlEdgeRenderSmoke.Run();
    VerifyEmojiControls();
    AiTraceButtonRenderSmoke.Run();
    AiStreamingRenderSmoke.Run();
    VerifyAiControls();
    VerifyChatToolStatusIcons();
    VerifyShimmerFrames();
    VerifyRichEditor();
    VerifyRichLinkArrow();
    Task.Run(EditorProtocolSmoke.Run).GetAwaiter().GetResult();
    VerifyTypography();
    VerifyDataDisplay();
    VerifyMetricCardEdges();
    VerifyMapProjection();
    VerifyCardPreviews();
    VerifyHoloCardLighting();
    VerifyDataGridDragPreview();
    VerifyScrollShadow();
    window.RequestedThemeVariant = ThemeVariant.Dark;
    Pump();
    var image = window.CaptureRenderedFrame();
    Check(image is not null, "rendering a Skia frame");
    image?.Dispose();
}
finally { window.Close(); }
Check(bindingLog.Warnings.Count == 0, $"binding diagnostics:{Environment.NewLine}{string.Join(Environment.NewLine, bindingLog.Warnings)}");
Console.WriteLine($"Alder smoke passed ({(RuntimeFeature.IsDynamicCodeSupported ? "managed" : "NativeAOT")}, no binding warnings).");

void VerifyAiControls()
{
    var document = new Alder.NativeMarkdown.Core.Streaming.StreamingDocument();
    document.LoadNow("# Build status\n\n```json\n{\"ok\":true}\n```");
    var markdown = new AlderMarkdown { Document = document };
    var code = new AlderCodeBlock { Language = "cs", Code = "int n = 1;" };
    var host = new Window { Width = 420, Height = 320, Content = new StackPanel { Children = { markdown, code } } };
    AlderMotion.SetReduced(host, true);
    host.Show();
    try
    {
        markdown.ApplyLatest();
        code.ApplyLatest();
        host.UpdateLayout();
        using var frame = host.CaptureRenderedFrame();
        Check(frame is not null && host.GetVisualDescendants().OfType<Alder.NativeMarkdown.UI.Rendering.CodeBlockView>().Count() == 2,
            "native AI Markdown and literal code share their visual renderer");
        using var session = new AlderCodeHighlighter().CreateSession("json", Alder.NativeMarkdown.Core.Highlighting.HighlightThemeKind.Light);
        Check(session.Update("{\"ok\":true}")[0].Length >= 2, "AOT Monarch tokenization");
    }
    finally { host.Close(); }
}

void VerifyChatToolStatusIcons()
{
    var stack = new StackPanel { Margin = new Thickness(12), Spacing = 8 };
    foreach (var status in new[] { AiRunStatus.Completed, AiRunStatus.Failed, AiRunStatus.Pending, AiRunStatus.Stopped })
        stack.Children.Add(new AlderChatTool { Header = status.ToString(), Status = status, Arguments = "{}",
            Approval = status == AiRunStatus.Pending ? "Review" : null });
    var host = new Window { Width = 320, Height = 230, Content = stack };
    AlderMotion.SetReduced(host, true);
    host.Show();
    try
    {
        foreach (var (variant, background) in new[] { (ThemeVariant.Light, Brushes.White), (ThemeVariant.Dark, Brushes.Black) })
        {
            host.RequestedThemeVariant = variant;
            host.Background = background;
            foreach (int scale in new[] { 1, 2 })
            {
                host.SetRenderScaling(scale);
                Pump();
                using var frame = host.CaptureRenderedFrame() ?? throw new InvalidOperationException("Missing Chat Tool frame.");
                using var stream = new MemoryStream();
                frame.Save(stream, PngBitmapEncoderOptions.Default);
                stream.Position = 0;
                using var pixels = SKBitmap.Decode(stream);
                foreach (var tool in stack.Children.OfType<AlderChatTool>())
                {
                    var icon = tool.GetVisualDescendants().OfType<AlderPathIcon>().Single(part => part.Name == "StatusIcon");
                    var point = icon.TranslatePoint(default, host)!.Value;
                    int left = (int)Math.Round(point.X * scale);
                    int top = (int)Math.Round(point.Y * scale);
                    int size = 14 * scale;
                    var backdrop = pixels.GetPixel(left, top);
                    int painted = 0;
                    for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        var ink = pixels.GetPixel(left + x, top + y);
                        int difference = Math.Max(Math.Abs(ink.Red - backdrop.Red),
                            Math.Max(Math.Abs(ink.Green - backdrop.Green), Math.Abs(ink.Blue - backdrop.Blue)));
                        if (difference < 30) continue;
                        painted++;
                        Check(x >= scale && x < size - scale && y >= scale && y < size - scale,
                            $"Chat Tool {tool.Status} ink clears the slot edge at {scale}× in {variant}");
                    }
                    Check(painted > 12 * scale * scale,
                        $"Chat Tool {tool.Status} paints its status icon at {scale}× in {variant}");
                }
            }
        }
    }
    finally { host.Close(); }
}

void VerifyShimmerFrames()
{
    var text = new AlderTextShimmer { Text = "Generating a response…", FontSize = 22, Foreground = Brushes.Black };
    var host = new Window { Width = 270, Height = 40, Background = Brushes.White, Content = text };
    host.Show();
    try
    {
        AlderMotion.SetReduced(host, false);
        Pump();
        var first = Capture();
        bool moved = false;
        for (int frame = 0; frame < 30 && !moved; frame++)
        {
            Thread.Sleep(30);
            Pump();
            moved = !first.AsSpan().SequenceEqual(Capture());
        }
        Check(moved, "AI shimmer paints new glyph opacity as the highlight passes");
        AlderMotion.SetReduced(host, true);
        Pump();
        var reduced = Capture();
        Thread.Sleep(60);
        Pump();
        Check(reduced.AsSpan().SequenceEqual(Capture()), "reduced-motion AI shimmer stays at rest");
    }
    finally { host.Close(); }

    byte[] Capture()
    {
        using var frame = host.CaptureRenderedFrame()!;
        using var png = new MemoryStream();
        frame.Save(png, PngBitmapEncoderOptions.Default);
        png.Position = 0;
        using var pixelMap = SKBitmap.Decode(png);
        return pixelMap.Bytes;
    }
}

void VerifyRichEditor()
{
    var editor = new AlderRichTextEditor();
    var host = new Window { Width = 580, Height = 260, Content = editor };
    host.Show();
    try
    {
        editor.FocusContent();
        editor.InsertText("Hello 世界");
        editor.Select(0, 5);
        editor.Execute(RichTextCommand.Bold);
        Check(editor.Value.Contains("\"bold\"") && editor.PlainText == "Hello 世界", "native rich marks and text");
        var value = editor.Value;
        editor.Execute(RichTextCommand.ClearContent);
        editor.Value = value;
        Check(editor.WordCount == 2 && editor.CharacterCount == 8, "native rich document reload");
        editor.Value = """
            {"type":"doc","content":[
              {"type":"orderedList","attrs":{"start":9},"content":[
                {"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"9. The first item wraps at compact widths so its marker must stay on the first visual line."}]}]},
                {"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"10. Another item"}]}]}]},
              {"type":"orderedList","attrs":{"start":100},"content":[
                {"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"100. A separate list"}]}]}]}]}
            """;
        editor.Select(editor.PlainText.Length, 0);
        editor.IsReadOnly = true;
        host.Width = 330;
        host.Height = 375;
        foreach (double scale in new[] { 1d, 2d })
        {
            host.SetRenderScaling(scale);
            Pump();
            using var frame = host.CaptureRenderedFrame()!;
            using var png = new MemoryStream();
            frame.Save(png, PngBitmapEncoderOptions.Default);
            png.Position = 0;
            using var pixels = SKBitmap.Decode(png);
            var presenter = Part<RichTextPresenter>(editor, "PART_TextPresenter");
            var origin = presenter.TranslatePoint(default, host)!.Value;
            var gutter = Part<RichTextGutter>(editor, "PART_Gutter").TranslatePoint(default, host)!.Value;
            foreach (int offset in new[] { 0, editor.PlainText.IndexOf("10.", StringComparison.Ordinal), editor.PlainText.IndexOf("100.", StringComparison.Ordinal) })
            {
                var line = presenter.TextLayout.HitTestTextPosition(offset);
                int top = (int)((origin.Y + line.Y) * scale);
                int end = (int)((origin.Y + line.Bottom) * scale);
                int textX = (int)(origin.X * scale);
                // Each item begins with its expected marker. Compare painted ink, including multiple digits.
                int afterLabel = editor.PlainText.IndexOf('.', offset) + 1;
                int labelEnd = (int)((origin.X + presenter.TextLayout.HitTestTextPosition(afterLabel).X) * scale);
                var marker = Ink((int)(gutter.X * scale), textX);
                var text = Ink(textX, labelEnd);
                Check(marker.Width > 0 && text.Width > 0 && Math.Abs(marker.Bottom - text.Bottom) <= 1 &&
                    Math.Abs(marker.Width - text.Width) <= 2 && marker.Right < textX - 3 * scale,
                    $"ordered marker matches its text and clears the item at {scale}×: {marker}, {text}");
                (int Bottom, int Right, int Width) Ink(int left, int right)
                {
                    int min = right, max = left - 1, bottom = -1;
                    for (int y = top; y < end; y++)
                        for (int x = left; x < right; x++)
                            if (pixels.GetPixel(x, y).Red < 120) { min = Math.Min(min, x); max = Math.Max(max, x); bottom = y; }
                    return (bottom, max, max - min + 1);
                }
            }
        }
    }
    finally { host.Close(); }
}

void VerifyRichLinkArrow()
{
    var editor = new AlderRichTextEditor
    {
        Width = 245, Height = 190, Background = Brushes.Gray, ToolbarCommands = [RichTextCommand.Link]
    };
    var canvas = new Canvas { Background = Brushes.Gray, ClipToBounds = true, Children = { editor } };
    Canvas.SetLeft(editor, 22);
    var host = new Window { Width = 320, Height = 350, Content = canvas };
    AlderMotion.SetReduced(host, true);
    host.Show();
    try
    {
        foreach (var (theme, scale, flip) in new[]
        {
            (ThemeVariant.Light, 1d, false), (ThemeVariant.Dark, 2d, true)
        })
        {
            host.RequestedThemeVariant = theme;
            host.SetRenderScaling(scale);
            Canvas.SetTop(editor, flip ? 150 : 42);
            editor.ToolbarAtBottom = flip;
            host.UpdateLayout();
            editor.Execute(RichTextCommand.Link);
            host.UpdateLayout();
            Pump();

            var panel = Part<Control>(host, "AlderPopoverSurface").GetVisualChildren().OfType<Border>().Single();
            var trigger = editor.GetVisualDescendants().OfType<Button>().Single(b => b.Tag is RichTextCommand.Link);
            var origin = panel.TranslatePoint(default, host)!.Value;
            var anchor = trigger.TranslatePoint(new Rect(trigger.Bounds.Size).Center, host)!.Value;
            double edge = flip ? origin.Y + panel.Bounds.Height : origin.Y;
            double direction = flip ? 1 : -1;
            using var frame = host.CaptureRenderedFrame()!;
            using var png = new MemoryStream();
            frame.Save(png, PngBitmapEncoderOptions.Default);
            png.Position = 0;
            using var pixels = SKBitmap.Decode(png);
            double arrowX = Math.Clamp(anchor.X, origin.X + 18, origin.X + panel.Bounds.Width - 18);
            var fill = Sample(arrowX, edge - 3 * direction);
            Check(Math.Abs((edge - anchor.Y) * -direction - 24) <= 1,
                $"the Rich Text link popover preserves the gap when placed {(flip ? "above" : "below")}");
            Check(Sample(arrowX, edge + 2 * direction) == fill && Sample(arrowX + 8, edge + 2 * direction) != fill,
                $"the Rich Text link arrow extends outside the surface at {scale}×");
            host.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();

            SKColor Sample(double x, double y) => pixels.GetPixel((int)(x * scale), (int)(y * scale));
        }
    }
    finally { host.Close(); }
}

void VerifyScrollShadow()
{
    var scroll = Find<AlderScrollShadow>("SmokeScrollShadow");
    var state = Find<TextBlock>("SmokeScrollShadowState");
    var presenter = Part<ScrollContentPresenter>(scroll, "PART_ContentPresenter");
    Check(presenter.Child is TextBlock label && label.Height == 144 && label.Text == model.Description,
        "compiled scroll shadow content template");
    Check(state.Text == "Bottom" && presenter.OpacityMask is LinearGradientBrush, "compiled scroll shadow state");
    scroll.Offset = new Vector(0, 32);
    Pump();
    Check(state.Text == "Both", "compiled scroll shadow updates on scroll");
    model.ShadowVisibility = ScrollShadowVisibility.None;
    Pump();
    Check(state.Text == "None" && presenter.OpacityMask is null, "compiled controlled scroll shadow visibility");
    model.ShadowVisibility = ScrollShadowVisibility.Auto;
}

void VerifyFormControls()
{
    var drop = new AlderDropZone { Title = "Choose files", MaxFileSize = 8 };
    var inline = new AlderInlineSelect { Items = { "Design", "Research" }, SelectedIndex = 0 };
    var select = new AlderNativeSelect { Items = { "Ready", "Done" }, SelectedIndex = 0 };
    var scope = new ThemeVariantScope { RequestedThemeVariant = ThemeVariant.Dark, Child = select };
    var number = new AlderNumberStepper { Minimum = 0, Maximum = 4, Value = 3, Size = AlderSize.Small };
    var option = new AlderRadioButtonGroupItem { Content = "Standard" };
    AlderChoiceGroup.SetKey(option, "standard");
    var radios = new AlderRadioButtonGroup { SelectedKey = "standard", Children = { option } };
    var owner = new Window
    {
        Width = 450, Height = 620, RequestedThemeVariant = ThemeVariant.Light,
        Content = new StackPanel { Margin = new Thickness(12), Spacing = 12, Children = { drop, inline, scope, number, radios } }
    };
    AlderMotion.SetReduced(owner, true);
    owner.Show();
    try
    {
        Pump();
        Check(Part<AlderButton>(drop, "PART_Browse").Command == drop.BrowseCommand && drop.Files.Count == 0,
            "Drop Zone uses the compiled browse command and starts empty");
        Check(inline.SelectionText == "Design" && option.IsChecked == true, "Inline Select and Radio Button Group initial values");
        Check(Part<RepeatButton>(number, "PART_IncreaseButton").Bounds.Width == 28, "small Number Stepper has a sized native button");
        select.IsDropDownOpen = true;
        Pump();
        var border = Part<Control>(owner, "AlderPopoverSurface").GetVisualChildren().OfType<Border>().Single();
        Check(border.Background is ISolidColorBrush brush && brush.Color == Color.Parse("#18181B"),
            "Native Select resolves its local dark popup theme under a light window");
        using var frame = owner.CaptureRenderedFrame();
        Check(frame is not null, "Skia renders all five form controls");
    }
    finally { owner.Close(); }
}

void VerifyEmojiControls()
{
    var reaction = new AlderEmojiReactionButton { Emoji = "❤️", Count = 12, IsChecked = true };
    var picker = new AlderEmojiPicker { IsInline = true, SelectedEmoji = "😀" };
    var host = new Window
    {
        Width = 420, Height = 460, Background = Brushes.White, RequestedThemeVariant = ThemeVariant.Light,
        Content = new StackPanel { Margin = new Thickness(20), Spacing = 10, Children = { reaction, picker } }
    };
    AlderMotion.SetReduced(host, true);
    host.Show();
    try
    {
        Pump();
        Check(Part<TextBlock>(reaction, "PART_Count").Text == "12", "Emoji Reaction Button binds its count");
        var grid = Part<ListBox>(picker, "PART_Grid");
        Check(grid.ItemCount > 250 && grid.GetRealizedContainers().Count() < 40, "Emoji Picker loads and virtualizes the bundled catalog");
        var query = Part<AlderSearchField>(picker, "PART_Search");
        query.Text = "火箭";
        Pump();
        Check(grid.ItemsSource!.Cast<EmojiPickerRow>().SelectMany(row => row.Cells).Any(cell => cell.Glyph == "🚀"),
            "Emoji Picker searches its bundled CLDR annotations");
        var panel = picker.GetVisualDescendants().OfType<AlderEmojiPickerPanel>().Single();
        var origin = panel.TranslatePoint(default, host)!.Value;
        using (var light = Capture())
        {
            var center = origin + new Vector(panel.Bounds.Width / 2, panel.Bounds.Height);
            byte near = light.GetPixel((int)center.X, (int)center.Y + 4).Red;
            byte far = light.GetPixel((int)center.X, (int)center.Y + 17).Red;
            Check(near < far && near < 250, "Emoji Picker paints its light shadow outside the content clip");
        }
        host.RequestedThemeVariant = ThemeVariant.Dark;
        host.SetRenderScaling(2);
        Pump();
        using (var dark = Capture())
        {
            var edge = panel.TranslatePoint(new Point(0, panel.Bounds.Height / 2), host)!.Value;
            int x = (int)(2 * edge.X);
            int y = (int)(2 * edge.Y);
            Check(dark.GetPixel(x, y).Red > dark.GetPixel(x + 8, y).Red + 8,
                "Emoji Picker paints its dark inset edge above the clipped content at 2×");
        }
    }
    finally { host.Close(); }

    SKBitmap Capture()
    {
        using var frame = host.CaptureRenderedFrame() ?? throw new InvalidOperationException("Missing emoji frame.");
        using var stream = new MemoryStream();
        frame.Save(stream, PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        return SKBitmap.Decode(stream);
    }
}

void VerifyDataDisplay()
{
    var badge = Find<AlderBadge>("SmokeBadge");
    var chip = Find<AlderChip>("SmokeChip");
    var table = Find<AlderTable>("SmokeTable");
    var view = table.GetVisualDescendants().OfType<TableView>().Single();
    Check(Equals(badge.Content, model.Code) && Equals(chip.Content, model.Description), "compiled badge and chip content");
    Check(table.VisibleRowCount == model.Items.Count && ReferenceEquals(view.SelectedItem, model.Items[0]), "compiled table source and keyed selection");
    Check(view.GetVisualDescendants().OfType<TableViewCell>().Any(cell => Equals(cell.Content, model.Items[0].Label)), "compiled column binding");
    var dataGrid = Find<AlderDataGrid>("SmokeDataGrid");
    var gridView = dataGrid.GetVisualDescendants().OfType<TableView>().Single();
    var gridScroll = (ScrollViewer)gridView.Scroll!;
    var firstColumn = dataGrid.Columns[0];
    var lastColumn = dataGrid.Columns[2];
    var fixedHeader = gridView.GetVisualDescendants().OfType<TableViewColumnHeader>().Single(header => header.Column == lastColumn);
    var initial = fixedHeader.TranslatePoint(default, dataGrid);
    gridScroll.Offset = new Vector(100, 0);
    Pump();
    Check(gridScroll.Offset.X > 0 && initial == fixedHeader.TranslatePoint(default, dataGrid), "pinned DataGrid layout");
    Check(ReferenceEquals(gridView.SelectedItem, model.Items[0]) && gridView.GetVisualDescendants().OfType<TableViewCell>()
        .Any(cell => cell.Column == firstColumn && Equals(cell.Content, model.Items[0].Label)), "compiled DataGrid cells and keyed selection");
    var action = gridView.ContainerFromIndex(0)!.GetVisualDescendants().OfType<AlderButton>().Single();
    var dropdown = (AlderDropdownFlyout)action.Flyout!;
    Press(action, PhysicalKey.Enter);
    Check(dropdown.IsOpen && dropdown.Popup.Child is MenuFlyoutPresenter presenter &&
        presenter.FindAncestorOfType<OverlayPopupHost>() is not null &&
        presenter.Items[0] is MenuItem item && Equals(item.Header, model.Items[0].Label),
        "compiled DataGrid row dropdown opens over the table and binds the current row");
    dropdown.Hide();
    Pump();
    model.Items.Add(new("table-extra", "Additional row", "Additional description"));
    Pump();
    Check(table.VisibleRowCount == model.Items.Count && ReferenceEquals(view.SelectedItem, model.Items[0]), "table updates the projection while keeping selection");
    model.Items.RemoveAt(model.Items.Count - 1);
    Pump();
}

void VerifyMetricCardEdges()
{
    var metric = new AlderKpi { Title = "Revenue", Value = 42, Width = 150, Height = 92 };
    var group = new AlderKpiGroup
    {
        Width = 150, Height = 92, Items = { new AlderKpi { Title = "Costs", Value = 12 } }
    };
    var widget = new AlderWidget
    {
        Width = 150, Title = "Usage", ClipContent = true, ContentPadding = default,
        Content = new Border { Height = 42, Background = Brushes.DodgerBlue }
    };
    var canvas = new Canvas { Children = { metric, group, widget }, ClipToBounds = true };
    Canvas.SetLeft(metric, 18);
    Canvas.SetLeft(group, 186);
    Canvas.SetTop(metric, 18);
    Canvas.SetTop(group, 18);
    Canvas.SetLeft(widget, 354);
    Canvas.SetTop(widget, 18);
    var owner = new Window { Width = 522, Height = 128, Content = canvas };
    owner.Show();
    try
    {
        foreach (var (variant, background) in new[] { (ThemeVariant.Light, "#F5F5F5"), (ThemeVariant.Dark, "#060607") })
        {
            owner.RequestedThemeVariant = variant;
            canvas.Background = new SolidColorBrush(Color.Parse(background));
            foreach (double scale in new[] { 1d, 2d })
            {
                owner.SetRenderScaling(scale);
                Pump();
                using var frame = owner.CaptureRenderedFrame() ?? throw new InvalidOperationException("Missing KPI frame.");
                using var stream = new MemoryStream();
                frame.Save(stream, PngBitmapEncoderOptions.Default);
                stream.Position = 0;
                using var pixels = SKBitmap.Decode(stream);
                foreach (var card in new Control[] { metric, group })
                {
                    var origin = card.TranslatePoint(default, owner)!.Value;
                    double x = origin.X + card.Bounds.Width / 2;
                    double bottom = origin.Y + card.Bounds.Height;
                    var edge = Sample(x, origin.Y + .5);
                    var fill = Sample(x, origin.Y + 6);
                    var near = Sample(x, bottom + .5);
                    var fade = Sample(x, bottom + 2.5);
                    var far = Sample(x, bottom + 8);
                    Check(edge == fill && fill != far, "the KPI card's outline follows its fill at the straight edge");
                    Check(Sample(origin.X + 1, origin.Y + 1) != fill, "the KPI card retains its rounded corner");
                    Check(variant == ThemeVariant.Light ? near.Red < fade.Red && fade.Red < far.Red : near == far && fade == far,
                        $"{card.GetType().Name} exterior shadow matches {variant} at {scale}×");
                }

                var surface = Part<Border>(widget, "ContentSurface");
                var surfaceOrigin = surface.TranslatePoint(default, owner)!.Value;
                double centerX = surfaceOrigin.X + surface.Bounds.Width / 2;
                double innerBottom = surfaceOrigin.Y + surface.Bounds.Height;
                var widgetFill = Sample(centerX, surfaceOrigin.Y + 6);
                Check(widgetFill == SKColors.DodgerBlue && Sample(surfaceOrigin.X + 1, surfaceOrigin.Y + 1) != widgetFill,
                    $"Widget clips edge-to-edge content to the rounded surface in {variant} at {scale}×");
                Check(Sample(centerX, innerBottom + .5).Red < Sample(centerX, innerBottom + 4.5).Red,
                    $"Widget shadow remains visible outside its content clip in {variant} at {scale}×");

                SKColor Sample(double x, double y) => pixels.GetPixel((int)(x * scale), (int)(y * scale));
            }
        }
    }
    finally { owner.Close(); }
}

void VerifyAlderWindow()
{
    var content = new TextBlock { Text = "Window content" };
    var owner = new AlderWindow { Width = 400, Height = 280, Content = content };
    AlderMotion.SetReduced(owner, true);
    owner.Show();
    try
    {
        var body = new TextBlock { Text = "Owned dialog" };
        var dialog = Dialog.ShowModalAsync(body, owner: content);
        var toast = Toast.Show(new ToastOptions("Owned notification") { Duration = TimeSpan.Zero }, content);
        Pump();
        Check(Part<DialogHost>(owner, "PART_DialogHost").IsVisualAncestorOf(body),
            "AlderWindow supplies a modal host for its content");
        using var frame = owner.CaptureRenderedFrame();
        Check(frame is not null && owner.GetVisualDescendants().OfType<ToastHost>().Count() == 1,
            "AlderWindow renders its automatically registered toast host");
        Check(Dialog.Close(body, DialogResult.OK) && dialog.IsCompletedSuccessfully && dialog.Result == DialogResult.OK,
            "AlderWindow returns its hosted dialog result");
        toast.Close();
    }
    finally { owner.Close(); }
}

void VerifyAlderWindowStateRendering()
{
    var owner = new AlderWindow
    {
        Width = 400, Height = 300, Background = Brushes.White,
        Content = new Border { Margin = new Thickness(24), Background = Brushes.DodgerBlue }
    };
    owner.Show();
    try
    {
        var normal = CaptureContent();
        owner.WindowState = WindowState.Maximized;
        Check(normal.AsSpan().SequenceEqual(CaptureContent()), "maximize leaves content pixels stable at the new layout scale");
        owner.WindowState = WindowState.Normal;
        Check(normal.AsSpan().SequenceEqual(CaptureContent()), "restore leaves content pixels stable at the new layout scale");
    }
    finally { owner.Close(); }

    SKColor[] CaptureContent()
    {
        Pump();
        using var frame = owner.CaptureRenderedFrame() ?? throw new InvalidOperationException("Missing window frame.");
        using var stream = new MemoryStream();
        frame.Save(stream, PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        using var pixels = SKBitmap.Decode(stream);
        return pixels.Pixels.AsSpan(pixels.Width * 52).ToArray();
    }
}

void VerifyMapProjection()
{
    var marker = new AlderMapMarker(new MapCoordinate(0, -179));
    var path = Enumerable.Range(0, 140).Select(i => new MapCoordinate(i / 100d, 178 + i / 40d)).ToArray();
    var map = new AlderMap
    {
        Center = new MapCoordinate(0, 179), Zoom = 4, Bearing = 35,
        ShowControls = false, Markers = [marker], Paths = [new AlderMapPath(path)]
    };
    var owner = new Window { Width = 340, Height = 240, Content = map };
    owner.Show();
    try
    {
        Pump();
        using var frame = owner.CaptureRenderedFrame();
        var point = map.TranslatePoint(map.LocationToPoint(marker.Location), owner);
        Check(frame is not null && point.HasValue, "Skia map with a path crossing the antimeridian");
        owner.MouseDown(point!.Value, MouseButton.Left);
        owner.MouseUp(point.Value, MouseButton.Left);
        Check(ReferenceEquals(map.SelectedMarker, marker), "map marker hit testing after rotating the viewport");
    }
    finally { owner.Close(); }
}

void VerifyCardPreviews()
{
    var holo = new AlderHoloCard { Width = 116, Height = 160, Content = new TextBlock { Text = "Ed. 7" } };
    var action = new Button { Content = "Details" };
    var hover = new AlderHoverCard
    {
        Trigger = new Button { Content = "Preview" }, Content = action, IsOpen = true,
        Placement = PlacementMode.Right, ShowArrow = true
    };
    var owner = new Window
    {
        Width = 400, Height = 310,
        Content = new StackPanel { Margin = new Thickness(24), Spacing = 12, Children = { holo, hover } }
    };
    AlderMotion.SetReduced(owner, true);
    owner.Show();
    try
    {
        Pump();
        Check(Part<Border>(holo, "PART_Rotator").Bounds.Width == 116 && !holo.IsActive,
            "Holo Card is themed and keeps still in reduced motion");
        Check(hover.IsOpen && Part<Control>(owner, "AlderHoverCardSurface") is { } panel &&
              panel.IsVisualAncestorOf(action), "Hover Card opens its interactive overlay from its initial state");
        hover.IsOpen = false;
        Pump();
        Check(!action.IsAttachedToVisualTree(), "Hover Card detaches preview content after closing");
    }
    finally { owner.Close(); }
}

void VerifyHoloCardLighting()
{
    var card = new AlderHoloCard
    {
        Width = 302, Height = 453, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
    };
    var owner = new Window { Width = 440, Height = 590, Content = card, Background = Brushes.White };
    AlderMotion.SetReduced(owner, false);
    owner.Show();
    try
    {
        var rotator = Part<Border>(card, "PART_Rotator");
        var shadow = Part<Border>(card, "PART_Shadow");
        foreach (double scale in new[] { 1d, 2d })
        {
            owner.SetRenderScaling(scale);
            Pump();
            var centerRight = new Point(302 * .79, 453 * .24);
            var centerLeft = new Point(302 * .21, 453 * .76);
            using var rest = Capture();
            var restPoint = card.TranslatePoint(centerRight, owner)!.Value;
            var restShadowPoint = card.TranslatePoint(new Point(151, 459), owner)!.Value;
            Check(Sample(rest, restShadowPoint) == SKColors.White, "Holo Card has no exterior shadow at rest");

            owner.MouseMove(restPoint);
            WaitFor(() => shadow.Opacity > .999 && rotator.RenderTransform?.Value.M13 < -.00024);
            using var lit = Capture();
            var near = rotator.TranslatePoint(centerRight, owner)!.Value;
            var far = rotator.TranslatePoint(centerLeft, owner)!.Value;
            Check(Chroma(lit, near) > Chroma(rest, restPoint) + 7 && Chroma(lit, near) > Chroma(lit, far) + 7,
                $"Holo Card illuminates the pointer quadrant at {scale}×");
            var shadowPoint = rotator.TranslatePoint(new Point(151, 459), owner)!.Value;
            Check(Sample(lit, shadowPoint).Red < 248, $"Holo Card paints its exterior hover shadow at {scale}×");

            owner.MouseMove(new Point(2, 2));
            WaitFor(() => !card.IsActive);
            using var settled = Capture();
            Check(Sample(settled, restShadowPoint) == SKColors.White, "Holo Card clears its exterior shadow after release");

            SKColor Sample(SKBitmap frame, Point point) => frame.GetPixel((int)(point.X * scale), (int)(point.Y * scale));

            double Chroma(SKBitmap frame, Point point)
            {
                var total = 0;
                for (int y = -7; y <= 7; y++)
                for (int x = -10; x <= 10; x++)
                {
                    var color = Sample(frame, point + new Vector(x, y));
                    total += Math.Max(color.Red, Math.Max(color.Green, color.Blue)) -
                             Math.Min(color.Red, Math.Min(color.Green, color.Blue));
                }
                return total / 315d;
            }
        }
    }
    finally { owner.Close(); }

    SKBitmap Capture()
    {
        using var frame = owner.CaptureRenderedFrame() ?? throw new InvalidOperationException("Missing Holo Card frame.");
        using var stream = new MemoryStream();
        frame.Save(stream, PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        return SKBitmap.Decode(stream);
    }

    static void WaitFor(Func<bool> condition)
    {
        for (int i = 0; i < 125; i++)
        {
            Pump();
            if (condition()) return;
            Thread.Sleep(12);
        }
        Check(condition(), "Holo Card spring settles");
    }
}

void VerifyDataGridDragPreview()
{
    var grid = new AlderDataGrid
    {
        RowHeight = 48,
        ItemsSource = new[] { 1, 2, 3 },
        CanReorderRows = true,
        ReorderCommand = model.Activate
    };
    grid.Columns.Add(MarkerColumn("Start", Brushes.Magenta));
    grid.Columns.Add(MarkerColumn("End", Brushes.Cyan));
    var owner = new Window { Width = 400, Height = 260, Content = grid, RequestedThemeVariant = ThemeVariant.Light };
    owner.Show();
    try
    {
        const double scale = 2;
        owner.SetRenderScaling(scale);
        Pump();
        var table = grid.GetVisualDescendants().OfType<TableView>().Single();
        var row = table.ContainerFromIndex(0)!;
        var start = Part<Border>(row, "Start");
        var end = Part<Border>(row, "End");
        var origin = start.TranslatePoint(new Rect(start.Bounds.Size).Center, owner)!.Value;
        var endCenter = end.TranslatePoint(new Rect(end.Bounds.Size).Center, owner)!.Value;
        var delta = new Vector(18, 64);
        owner.MouseMove(origin);
        owner.MouseDown(origin, MouseButton.Left);
        owner.MouseMove(origin + delta, RawInputModifiers.LeftMouseButton);
        Pump();

        using var frame = owner.CaptureRenderedFrame();
        using var png = new MemoryStream();
        frame!.Save(png, PngBitmapEncoderOptions.Default);
        png.Position = 0;
        using var rendered = SKBitmap.Decode(png);
        var left = rendered.GetPixel((int)((origin.X + delta.X) * scale), (int)((origin.Y + delta.Y) * scale));
        var right = rendered.GetPixel((int)((endCenter.X + delta.X) * scale), (int)((endCenter.Y + delta.Y) * scale));
        Check(left.Red > 235 && left.Blue > 235 && left.Green < 30 &&
              right.Green > 235 && right.Blue > 235 && right.Red < 30,
            "both ends of the dragged DataGrid row are rendered at the pointer position at 2× scaling");
    }
    finally { owner.Close(); }

    static AlderTableColumn MarkerColumn(string name, IBrush brush) => new()
    {
        Header = name,
        Width = new GridLength(145),
        CellTemplate = new FuncDataTemplate<int>((item, _) => new Border
        {
            Name = name,
            Width = 24,
            Height = 16,
            Background = item == 1 ? brush : null
        })
    };
}

void VerifyTypography()
{
    var heading = Find<AlderHeading>("TypographyHeading");
    var shortcut = Find<AlderKbdKey>("KbdModifier");
    Check(heading.Type == TypographyType.H2 && heading.Text == model.Description && heading.FontSize == 30,
        "compiled heading level, type scale, and text");
    Check(shortcut.Glyph == "⌘" && shortcut.Title == "Command", "compiled inline shortcut key and tooltip");
    model.HeadingLevel = 4;
    model.ShortcutKey = KbdKey.Shift;
    Pump();
    Check(heading.Type == TypographyType.H4 && heading.FontSize == 20 &&
          shortcut.Glyph == "⇧" && shortcut.Title == "Shift", "typography and keyboard key bindings update");

    var tightHeading = new AlderHeading { Text = "Typography\nTypography", FontSize = 36, LineHeight = 28 };
    tightHeading.Measure(Size.Infinity);
    tightHeading.Arrange(new Rect(tightHeading.DesiredSize));
    var layout = tightHeading.TextLayout;
    Check(layout.Height == 56 && layout.OverhangAfter > 0 &&
          tightHeading.Bounds.Height >= layout.Height + layout.OverhangAfter,
        "typography fits descenders while preserving the requested line height");
}

void VerifyBindings()
{
    var button = Find<AlderButton>("ActionButton");
    var icon = Part<PathIcon>(button, "LeadingIcon");
    Check(icon.IsVisible && ReferenceEquals(model.Icon, icon.Data), "compiled templated-parent icon");
    Press(button, PhysicalKey.Enter);
    Check(model.Activations == 1, "compiled command");

    var input = Find<AlderInput>("Input");
    var label = Find<Label>("FieldLabel");
    var standaloneLabel = Find<Label>("StandaloneLabel");
    Check(standaloneLabel.IsEnabled && !AlderLabel.GetIsRequired(standaloneLabel) && !AlderLabel.GetIsInvalid(standaloneLabel),
        "label without a target");
    Check(input.Text == model.Text && Part<TextBlock>(label, "RequiredMark").IsVisible, "compiled label target");
    model.Invalid = true;
    Check(AlderLabel.GetIsInvalid(label), "compiled MultiBinding updates");
    model.Text = "Updated text";
    Check(input.Text == model.Text && Find<AlderSearchField>("Search").Text == model.Text, "source notification");
    input.SetCurrentValue(TextBox.TextProperty, "Write back");
    Check(model.Text == "Write back", "two-way text");
    Check(Equals(Find<AlderInputGroup>("InputGroup").InnerLeftContent, model.Prefix), "prefix style binding");
    Check(Find<AlderTextArea>("Notes").MinLines == model.Rows, "text area style binding");
    var number = Find<AlderNumberField>("Number");
    Check(Find<AlderInputOtp>("Otp").Text == model.Code && number.Value == model.Number, "OTP and numeric data");
    number.SetCurrentValue(NumericUpDown.ValueProperty, 24m);
    Check(model.Number == 24m, "two-way numeric value");

    Press(Find<AlderToggleButton>("Toggle"), PhysicalKey.Space);
    Check(model.Checked == true && Find<AlderCheckBox>("Check").IsChecked == true, "two-way toggle");
    var radio = Find<RadioButton>("FirstRadio");
    Check(AutomationProperties.GetHelpText(radio) == model.Description, "compiled attached property");
    Press(Find<RadioButton>("SecondRadio"), PhysicalKey.Space);
    Check(model.SelectedKeys.SequenceEqual(["second"]), "two-way choice keys");
    var slider = Find<AlderRangeSlider>("RangeSlider");
    Check(slider.LowerValue == model.Position, "range slider value");
    slider.SetCurrentValue(AlderRangeSlider.LowerValueProperty, 30);
    Check(model.Position == 30, "two-way range slider value");

    Check(Find<AlderDateField>("Date").Value == model.Date && Find<AlderTimeField>("Time").Value == model.Time,
        "nullable DateOnly and TimeOnly");
    Check(Find<AlderDateRangePicker>("RangePicker").Value == model.Dates &&
          Find<AlderRangeCalendar>("RangeCalendar").Value == model.Dates, "custom date range value");
    Press(Part<ToggleButton>(Find<AlderDisclosure>("Disclosure"), "PART_Trigger"), PhysicalKey.Space);
    Check(model.Expanded, "two-way disclosure state");
}

void VerifyFeedback()
{
    var alert = Find<AlderAlert>("Alert");
    var alertPeer = ControlAutomationPeer.CreatePeerForElement(alert);
    Check(Part<TextBlock>(alert, "AlertTitle").Text == model.Description &&
          alertPeer.GetName() == model.Description && AutomationProperties.GetLiveSetting(alert) == AutomationLiveSetting.Assertive,
        "alert uses compiled content and exposes its status to automation");
    var alertSurface = Part<Control>(alert, "AlertSurface");
    var alertOutline = Part<Border>(alert, "AlertOutline");
    Check(!alert.ClipToBounds && !alertSurface.ClipToBounds &&
          alertSurface.GetValue(Border.BoxShadowProperty).Equals(alert.BoxShadow) &&
          alertSurface.GetValue(Border.BorderThicknessProperty) == alertOutline.BorderThickness &&
          alertOutline.BackgroundSizing == BackgroundSizing.OuterBorderEdge,
        "alert preserves elevation and aligns the surface with an asymmetric outline");

    model.Position = 50;
    var meter = Find<AlderMeter>("Meter");
    var progress = Find<AlderProgressBar>("ProgressBar");
    var circle = Find<AlderProgressCircle>("ProgressCircle");
    model.Expanded = false;
    Pump();
    Check(meter.Fraction == .5 && progress.Fraction == .5 && circle.Fraction == .5 &&
          Part<TextBlock>(meter, "RangeOutput").Text == meter.ValueText &&
          Part<TextBlock>(progress, "RangeOutput").IsVisible,
        "feedback templates follow the compiled range value");
    var progressPeer = ControlAutomationPeer.CreatePeerForElement(progress);
    Check(progressPeer.GetProvider<IRangeValueProvider>() is { IsReadOnly: true, Value: 50 },
        "determinate feedback exposes a read-only automation value");
    model.Expanded = true;
    Pump();
    Check(progress.IsIndeterminate && circle.IsIndeterminate && !Part<TextBlock>(progress, "RangeOutput").IsVisible &&
          !Part<TextBlock>(circle, "CircleOutput").IsVisible && progressPeer.GetProvider<IRangeValueProvider>() is null,
        "indeterminate feedback suppresses a stale determinate value");
    var spinner = Find<AlderSpinner>("Spinner");
    Check(spinner.Color == FeedbackColor.Accent &&
          Part<PathIcon>(spinner, "SpinnerGlyph") is { Width: 40, OpacityMask: DrawingBrush } &&
          !Part<Control>(Find<AlderSkeleton>("Skeleton"), "Shimmer").IsVisible,
        "feedback templates apply the spinner mask, size and reduced motion");
}

void VerifyLayout()
{
    var surface = Find<AlderSurface>("Surface");
    var card = Find<AlderCard>("Card");
    var separator = Find<AlderSeparator>("Separator");
    var decorative = Find<AlderSeparator>("DecorativeSeparator");
    Check(card.HasHeader && Part<TextBlock>(card, "CardTitle").Text == model.Description &&
          Equals(Part<ContentPresenter>(card, "CardFooter").Content, model.Text), "compiled card slots");
    Check(AlderSurface.GetContextVariant(surface) == SurfaceVariant.Secondary &&
          AlderSurface.GetContextVariant(separator) == SurfaceVariant.Tertiary &&
          AlderSurface.GetContextVariant(decorative) == SurfaceVariant.Default, "nearest surface context");
    card.Variant = SurfaceVariant.Transparent;
    Pump();
    Check(AlderSurface.GetContextVariant(separator) == SurfaceVariant.Transparent && card.BoxShadow.Count == 0,
        "surface context and elevation update together");
    Check(ControlAutomationPeer.CreatePeerForElement(separator).GetAutomationControlType() == AutomationControlType.Separator &&
          AutomationProperties.GetAccessibilityView(decorative) == AccessibilityView.Raw,
        "semantic and decorative separators");
    Check(separator.Bounds.Height == 1 && decorative.Bounds.Width == 1 && decorative.Bounds.Height == 16,
        "horizontal and vertical separator sizing");
    card.Height = 160;
    Pump();
    var slots = Part<Panel>(card, "CardSlots");
    var header = Part<StackPanel>(card, "CardHeader");
    var body = slots.Children[1];
    var footer = Part<ContentPresenter>(card, "CardFooter");
    Check(Math.Abs(footer.Bounds.Bottom - slots.Bounds.Height) < 1 &&
          Math.Abs(body.Bounds.Top - header.Bounds.Bottom - 8) < 1 &&
          Math.Abs(footer.Bounds.Top - body.Bounds.Bottom - 8) < 1,
        "a fixed-height card fills the space above its footer with the configured slot spacing");
}

void VerifyCollections()
{
    var disclosures = Find<AlderDisclosureGroup>("Disclosures");
    var accordion = Find<AlderAccordion>("Accordion");
    var breadcrumbs = Find<AlderBreadcrumbs>("Breadcrumbs");
    Check(disclosures.GetRealizedContainers().OfType<AlderDisclosure>().First().Header is "First", "compiled item container header");
    Check(accordion.GetRealizedContainers().OfType<AlderAccordionItem>().First().Content is "First body", "compiled item container content");
    var first = breadcrumbs.GetRealizedContainers().OfType<AlderBreadcrumbItem>().First();
    Check(first.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "First"), "compiled item template");
    var secondDisclosure = disclosures.GetRealizedContainers().OfType<AlderDisclosure>().Last();
    Press(Part<ToggleButton>(secondDisclosure, "PART_Trigger"), PhysicalKey.Space);
    Check(model.ExpandedKeys?.SequenceEqual(["second"]) == true, "two-way expanded keys");
    model.Items.Add(new("third", "Third", "Third body"));
    Pump();
    Check(disclosures.GetRealizedContainers().Count() == 3 && accordion.GetRealizedContainers().Count() == 3,
        "observable item source adds containers");
    var current = breadcrumbs.GetRealizedContainers().OfType<AlderBreadcrumbItem>().Last();
    Check(current.IsCurrent && current.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Third"), "current path updates");
    model.Items.RemoveAt(2);
    Pump();
    Check(breadcrumbs.GetRealizedContainers().OfType<AlderBreadcrumbItem>().Last().IsCurrent, "current path after removal");
}

void VerifyPickers()
{
    var group = Find<AlderAvatarGroup>("AvatarGroup");
    var displayed = group.GetVisualDescendants().OfType<AlderAvatar>().ToArray();
    int originalOverflow = model.Items.Count - 1;
    Check(group.OverflowCount == originalOverflow && displayed.Length == 2 && displayed[0].Fallback is "First" &&
          displayed.All(x => x.Bounds.Width == 32), "compiled avatar data and inherited group size");
    model.Items.Add(new("extra", "Extra", "Extra portrait"));
    Pump();
    Check(group.OverflowCount == originalOverflow + 1 &&
          Equals(group.GetVisualDescendants().OfType<AlderAvatar>().Last().Fallback, $"+{originalOverflow + 1}"),
        "avatar overflow updates after an observable collection change");
    model.Items.RemoveAt(model.Items.Count - 1);
    Pump();

    var select = Find<AlderSelect>("PickerSelect");
    var trigger = Part<Button>(select, "PART_Trigger");
    Press(trigger, PhysicalKey.Enter);
    Check(select.IsDropDownOpen && !select.ClipToBounds, "native Select opens and preserves its focus outline");
    Press(trigger, PhysicalKey.ArrowDown);
    Press(trigger, PhysicalKey.ArrowDown);
    Press(trigger, PhysicalKey.Enter);
    Check(!select.IsDropDownOpen && ReferenceEquals(model.PickerChoice, model.PickerChoices[2]),
        "compiled selection skips disabled rows");

    var autocomplete = Find<AlderAutocomplete>("PickerAutocomplete");
    Press(Part<Button>(autocomplete, "PART_Trigger"), PhysicalKey.Enter);
    var search = window.GetVisualDescendants().OfType<AlderSearchField>().Single(x => x.IsFocused);
    search.SetCurrentValue(TextBox.TextProperty, "br");
    Press(search, PhysicalKey.ArrowDown);
    Press(search, PhysicalKey.Enter);
    Check(autocomplete.IsDropDownOpen && model.PickerSelections.Count == 1 &&
          ReferenceEquals(model.PickerSelections[0], model.PickerChoices[2]), "compiled multiple selection after filtering");
    Press(search, PhysicalKey.Escape);
    Check(!autocomplete.IsDropDownOpen && Part<WrapPanel>(autocomplete, "PART_SelectionTags").Children.Count == 1,
        "escape dismisses the autocomplete and preserves its selected tag");

    var combo = Find<AlderComboBox>("PickerCombo");
    var editor = Part<AlderInput>(combo, "PART_Editor");
    Check(editor.Focus(NavigationMethod.Tab), "the ComboBox editor accepts keyboard focus");
    Pump();
    Check(editor.IsFocused && combo.IsDropDownOpen, "combo opens without moving focus off the editor");
    editor.SetCurrentValue(TextBox.TextProperty, "al");
    Press(editor, PhysicalKey.ArrowDown);
    Press(editor, PhysicalKey.Enter);
    Check(!combo.IsDropDownOpen && model.PickerText == "Alpha", "compiled ComboBox text and commit");
}

void VerifyPopups()
{
    var menu = Find<AlderButton>("MenuButton");
    Press(menu, PhysicalKey.Enter);
    Check(menu.Flyout?.IsOpen == true, "menu flyout");
    Press(window.GetVisualDescendants().OfType<MenuItem>().Single(), PhysicalKey.Enter);
    Check(model.Activations == 2, "compiled command inside flyout");

    var datePicker = Find<AlderDatePicker>("DatePicker");
    var calendarTrigger = Part<Button>(datePicker, "PART_CalendarTrigger");
    Press(calendarTrigger, PhysicalKey.Enter);
    Check(window.GetVisualDescendants().OfType<AlderCalendar>().Any(calendar =>
        calendar != Find<AlderCalendar>("Calendar") && calendar.Value == model.Date), "date picker renders popup calendar");
    window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
    window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
}

void VerifyNavigation()
{
    var link = Find<AlderLink>("Link");
    Check(ControlAutomationPeer.CreatePeerForElement(link).GetAutomationControlType() == AutomationControlType.Hyperlink,
        "native link accessibility");
    Press(link, PhysicalKey.Enter);
    Check(model.Activations == 3 && Part<Viewbox>(link, "LinkIconBox").IsVisible, "compiled link command and icon");
    Check(Part<Border>(link, "UnderlineMark").Opacity > 0, "persistent underline");

    var pagination = Find<AlderPagination>("Pagination");
    var pages = Part<Panel>(pagination, "PART_Pages");
    Check(pages.Children.OfType<TextBlock>().Count() == 2 && pages.Children.OfType<AlderButton>().Count() == 5,
        "bounded page window with ellipses");
    Press(Part<Button>(pagination, "PART_Next"), PhysicalKey.Enter);
    Check(model.Page == 6, "two-way compiled pagination");
    pagination.IsNextDisabled = true;
    Check(!Part<Button>(pagination, "PART_Next").IsEnabled, "independent next disabled state");
    pagination.IsNextDisabled = false;
    var curatedPages = new System.Collections.ObjectModel.ObservableCollection<AlderPaginationItem>
        { new(1), new(6, IsEnabled: false), new(null), new(20) };
    model.PageItems = curatedPages;
    Check(pages.Children.OfType<AlderButton>().Single(button => Equals(button.Tag, 6)).IsEnabled == false,
        "compiled curated page data and disabled number");
    curatedPages.Insert(1, new(4, "Four"));
    Check(pages.Children.OfType<AlderButton>().Any(button => Equals(button.Content, "Four")), "observable page collection");
    model.Page = 20;
    Check(!Part<Button>(pagination, "PART_Next").IsEnabled, "last page disables next");
    pagination.PageCount = 0;
    Check(pagination.CurrentPage == 0 && pages.Children.Count == 0 &&
          !Part<Button>(pagination, "PART_Previous").IsEnabled, "empty pagination");

    var tabs = Find<AlderTabs>("Tabs");
    AlderMotion.SetReduced(tabs, false);
    var first = (TabItem)tabs.ContainerFromIndex(0)!;
    var second = (TabItem)tabs.ContainerFromIndex(1)!;
    Check(Math.Abs(first.Bounds.Width - second.Bounds.Width) < 1 &&
          Math.Abs(first.Bounds.Width + second.Bounds.Width - (tabs.Bounds.Width - 8)) < 2, "tab headers fill their strip");
    model.TabAlignment = AlderTabAlignment.Start;
    Check(first.HorizontalContentAlignment == Avalonia.Layout.HorizontalAlignment.Left, "compiled tab header alignment");
    Press(first, PhysicalKey.ArrowRight);
    Check(model.SelectedTab == 1 && Part<ContentPresenter>(tabs, "PART_SelectedContentHost")
        .GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Second body"),
        "compiled tab data templates and two-way keyboard selection");
    Check(Part<Border>(tabs, "Indicator").Bounds.Width > 0, "tab selection indicator");
    tabs.FlowDirection = FlowDirection.RightToLeft;
    model.Items.Add(new("third", "Third", "Third body"));
    Pump();
    Press((TabItem)tabs.ContainerFromIndex(1)!, PhysicalKey.ArrowLeft);
    Check(model.SelectedTab == 2, "RTL keyboard navigation after adding an item");
}

void VerifyMorphing()
{
    var tabs = Find<AlderTabs>("Morphing");
    var surface = Part<Control>(tabs, "PART_MorphingSurface");
    Check(surface.IsVisible && surface.Bounds.Height >= 100, "morphing tabs drawable panel");
    var viewport = Part<Border>(tabs, "TabContentViewport");
    Check(viewport.ClipToBounds && viewport.CornerRadius == new CornerRadius(28) &&
          viewport.Bounds.Left == 16 && viewport.Bounds.Right == tabs.Bounds.Width - 16 &&
          viewport.Bounds.Bottom == tabs.Bounds.Height, "rounded morphing content viewport meets the shell bottom");
    AlderMotion.SetReduced(tabs, false);
    var first = (TabItem)tabs.ContainerFromIndex(0)!;
    var second = (TabItem)tabs.ContainerFromIndex(1)!;
    var selected = model.MorphItems[1];
    var dragFace = Part<Border>(second, "MorphingDragFace");
    var start = second.TranslatePoint(new Point(second.Bounds.Width / 2 - 16, second.Bounds.Height / 2), window)!.Value;
    window.MouseMove(start);
    Pump();
    Check(second.IsPointerOver && !Part<Border>(second, "FocusRing").IsVisible, "morphing hover has no focus outline");
    window.MouseDown(start, MouseButton.Left);
    Check(ReferenceEquals(model.SelectedMorphTab, model.MorphItems[0]), "morphing selection waits for click release");
    window.MouseUp(start, MouseButton.Left);
    Pump();
    var outgoing = Part<ContentPresenter>(tabs, "PART_SelectedContentHost");
    var incoming = Part<ContentPresenter>(tabs, "PART_SelectedContentHost2");
    Check(ReferenceEquals(selected, model.SelectedMorphTab) && second.IsSelected && ReferenceEquals(incoming.Content, selected),
        "pointer click updates compiled morphing selection and content");
    Check(!Part<Border>(second, "FocusRing").IsVisible, "pointer click has no focus outline");
    Check(outgoing.IsVisible && !outgoing.IsHitTestVisible && incoming.IsVisible && incoming.Opacity < 1 &&
          incoming.RenderTransform is TranslateTransform { Y: > 0 } && incoming.Effect is BlurEffect { Radius: > 0 },
        "morphing click overlaps the old content with the entering fade, lift and blur");
    var settling = System.Diagnostics.Stopwatch.StartNew();
    while (outgoing.IsVisible && settling.Elapsed < TimeSpan.FromSeconds(2))
    {
        Thread.Sleep(16);
        Pump();
    }
    Check(!outgoing.IsVisible && incoming.Opacity == 1 && incoming.RenderTransform is null && incoming.Effect is null,
        "morphing click clears outgoing content and temporary effects");

    var destination = first.TranslatePoint(new Point(first.Bounds.Width / 2 - 16, first.Bounds.Height / 2), window)!.Value;
    var end = start + (destination - start) * .73;
    window.MouseMove(start);
    window.MouseDown(start, MouseButton.Left);
    for (int step = 1; step <= 4; step++)
    {
        window.MouseMove(start + (end - start) * (step / 4.0), RawInputModifiers.LeftMouseButton);
        Pump();
    }
    Check(dragFace.IsVisible && !dragFace.IsHitTestVisible && ReferenceEquals(dragFace.Background, tabs.IndicatorBrush),
        "selected drag face covers overlapping headers with the selected surface brush");
    window.MouseUp(end, MouseButton.Left);
    Check(ReferenceEquals(selected, model.MorphItems[1]), "reorder waits for the tabs to settle");
    settling.Restart();
    while (!ReferenceEquals(selected, model.MorphItems[0]) && settling.Elapsed < TimeSpan.FromSeconds(2))
    {
        Thread.Sleep(16);
        Pump();
    }
    Check(ReferenceEquals(selected, model.MorphItems[0]) && ReferenceEquals(selected, model.SelectedMorphTab),
        "pointer reorder reaches the first slot and keeps selection");

    var moved = (TabItem)tabs.ContainerFromIndex(0)!;
    Check(!Part<Border>(moved, "MorphingDragFace").IsVisible, "selected drag face is removed after settling");
    Check(!Part<Border>(moved, "FocusRing").IsVisible, "pointer reorder has no focus outline");
    Press((TabItem)tabs.ContainerFromIndex(1)!, PhysicalKey.ArrowLeft);
    Check(Part<Border>(moved, "FocusRing").IsVisible, "keyboard navigation shows the focus outline");
    window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Alt);
    window.KeyReleaseQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Alt);
    Pump();
    Check(ReferenceEquals(selected, model.MorphItems[1]) && ReferenceEquals(selected, model.SelectedMorphTab),
        "compiled reorder command preserves selection");

    var close = Part<Button>((TabItem)tabs.ContainerFromIndex(1)!, "PART_TabClose");
    Check(close.Bounds.Size == new Size(24, 24) && close.CornerRadius == new CornerRadius(12) &&
          close.Padding == default && Part<Grid>(close, "TabCloseIcon").Bounds.Size == new Size(14, 14),
        "morphing close-button dimensions");
    Press(close, PhysicalKey.Enter);
    settling.Restart();
    while (model.MorphItems.Count > 2 && settling.Elapsed < TimeSpan.FromSeconds(2))
    {
        // The exit delay uses a DispatcherTimer, which needs the headless event loop.
        using var tick = new CancellationTokenSource(16);
        Dispatcher.UIThread.MainLoop(tick.Token);
        Pump();
    }
    Check(model.MorphItems.Count == 2 && !model.MorphItems.Contains(selected), "compiled close command");

    AlderMotion.SetReduced(tabs, true);
    var before = model.SelectedMorphTab;
    model.SelectedMorphTab = model.MorphItems.Single(item => !ReferenceEquals(item, before));
    Pump();
    Check(outgoing.IsVisible && incoming.IsVisible &&
          outgoing.RenderTransform is null && incoming.RenderTransform is null &&
          outgoing.Effect is null && incoming.Effect is null, "reduced morphing swaps content with opacity only");
    model.SelectedMorphTab = before;
    Pump();
    settling.Restart();
    while (outgoing.IsVisible == incoming.IsVisible && settling.Elapsed < TimeSpan.FromSeconds(2))
    {
        Thread.Sleep(16);
        Pump();
    }
    var visible = outgoing.IsVisible ? outgoing : incoming;
    var hidden = outgoing.IsVisible ? incoming : outgoing;
    Check(visible.IsVisible && ReferenceEquals(visible.Content, before) && visible.Opacity == 1 &&
          !hidden.IsVisible && hidden.Content is null && hidden.Effect is null,
        "rapid morphing reversal leaves only the latest content");
}

void VerifyExpandableTabs()
{
    var first = new TabItem
    {
        Header = "Overview",
        Content = model.MorphItems[0],
        ContentTemplate = new FuncDataTemplate<SmokeItem>((item, _) => new TextBlock { Text = item?.Label })
    };
    var second = new TabItem
    {
        Header = "Help",
        Content = "Help panel",
        ContentTemplate = new FuncDataTemplate<string>((label, _) => new TextBlock { Text = label })
    };
    var tabs = new AlderExpandableTabs { Items = { first, second }, ExpandedMinWidth = 248, ExpandedMinHeight = 166 };
    tabs.Bind(AlderExpandableTabs.SelectedIndexProperty, CompiledBinding.Create(
        (SmokeModel value) => value.SelectedExpandableTab, mode: BindingMode.TwoWay));
    var stage = new Grid { Background = Brushes.Transparent, Children = { tabs } };
    var host = new Window { Width = 430, Height = 250, DataContext = model, Content = stage };
    host.Show();
    try
    {
        Pump();
        var closedSize = tabs.Bounds.Size;
        var bottom = tabs.TranslatePoint(new Point(0, closedSize.Height), host)!.Value.Y;
        Check(tabs.SelectedIndex == -1 && closedSize == new Size(86, 54), "expandable tabs start as a compact bar");

        var body = Part<TransitioningContentControl>(tabs, "PART_ExpandableContent");
        model.SelectedExpandableTab = 0;
        tabs.UpdateLayout();
        Check(tabs.SelectedIndex == 0 && body.Content is null, "first expand reserves the shell before creating the panel");
        model.SelectedExpandableTab = -1;
        Pump();
        Check(body.Content is null, "closing before the first frame cancels the deferred panel");

        ClickHeader(first);
        Check(model.SelectedExpandableTab == 0 && first.IsSelected, "mouse opens expandable tabs through compiled selection");
        var openSize = new Size(248, 166);
        var settle = System.Diagnostics.Stopwatch.StartNew();
        while (tabs.Bounds.Size != openSize && settle.Elapsed < TimeSpan.FromSeconds(2))
        {
            Thread.Sleep(16);
            Pump();
        }
        Check(tabs.Bounds.Size == openSize && tabs.TranslatePoint(new Point(0, openSize.Height), host)!.Value.Y == bottom &&
              first.Bounds.Width > second.Bounds.Width, "expandable tab and shell animate around a fixed bottom edge");
        var firstLabel = Part<ContentPresenter>(first, "PART_HeaderPresenter");
        var secondLabel = Part<ContentPresenter>(second, "PART_HeaderPresenter");
        Check(firstLabel.Opacity == 1 && firstLabel.Clip is null &&
              secondLabel.Opacity == 0 && secondLabel.Clip is RectangleGeometry { Rect.Width: 0 },
            "expandable labels reveal only within their own pill");

        ClickHeader(second);
        Check(model.SelectedExpandableTab == 1 && tabs.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Help panel"),
            "expandable tabs switch content with an item-specific template");
        ClickHeader(second);
        Check(model.SelectedExpandableTab == -1, "clicking the selected tab closes it");

        AlderMotion.SetReduced(tabs, true);
        first.Focus(NavigationMethod.Tab);
        host.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        host.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
        Check(model.SelectedExpandableTab == 0 && tabs.Bounds.Size == openSize && firstLabel.Opacity == 1 &&
              firstLabel.Clip is null && firstLabel.Effect is null, "reduced motion expands immediately from the keyboard");
        host.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        host.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump();
        Check(model.SelectedExpandableTab == -1 && tabs.Bounds.Size == closedSize && first.IsFocused,
            "Escape collapses expandable tabs and restores focus");

        ClickHeader(second);
        host.MouseDown(new Point(10, 10), MouseButton.Left);
        host.MouseUp(new Point(10, 10), MouseButton.Left);
        Pump();
        Check(model.SelectedExpandableTab == -1, "outside press dismisses expandable tabs");
    }
    finally { host.Close(); }

    void ClickHeader(TabItem tab)
    {
        var point = tab.TranslatePoint(new Point(tab.Bounds.Width / 2, tab.Bounds.Height / 2), host)!.Value;
        host.MouseMove(point);
        host.MouseDown(point, MouseButton.Left);
        host.MouseUp(point, MouseButton.Left);
        Pump();
    }
}

void VerifyDialogs()
{
    var trigger = new AlderButton { Content = "Open dialog", HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center };
    var host = new DialogHost { HostId = "smoke", Content = trigger };
    var overlayOwner = new Window { Content = host, Width = 800, Height = 600 };
    AlderMotion.SetReduced(overlayOwner, true);
    overlayOwner.Show();
    try
    {
        Pump();
        Check(trigger.Focus(), "focus on the dialog opener");
        var modal = Dialog.ShowModalAsync(model, new DialogOptions
        {
            Title = "Edit", ContentTemplate = (IDataTemplate)window.Resources["DialogSmokeForm"]!,
            Buttons = DialogButtons.OKCancel
        }, owner: overlayOwner, hostId: "smoke");
        Pump();
        var input = Part<AlderInput>(host, "DialogInput");
        Check(input.Text == model.Text && input.IsFocused, "compiled dialog template and initial field focus");
        input.SetCurrentValue(TextBox.TextProperty, "Dialog edit");
        Check(model.Text == "Dialog edit", "compiled dialog template writes to its own model");
        Check(!trigger.Focus() && input.IsFocused, "the modal traps focus");
        PressOn(overlayOwner, PhysicalKey.Tab);
        PressOn(overlayOwner, PhysicalKey.Tab);
        PressOn(overlayOwner, PhysicalKey.Tab);
        PressOn(overlayOwner, PhysicalKey.Tab);
        Check(overlayOwner.FocusManager?.GetFocusedElement() is Visual focus &&
              Part<Border>(host, "AlderDialogSurface").IsVisualAncestorOf(focus), "Tab stays inside the modal");
        input.Focus();

        var alert = MessageBox.ShowAsync("Confirm this operation?", new MessageBoxOptions
        {
            Title = "Confirm", Status = MessageBoxStatus.Warning, Buttons = DialogButtons.YesNo
        }, owner: input);
        Pump();
        var no = Part<AlderButton>(host, "AlderDialogNo");
        Check(no.IsFocused, "message box starts on its safe action");
        PressOn(overlayOwner, PhysicalKey.Escape);
        Click(overlayOwner, new Point(6, 6));
        Check(!alert.IsCompleted && !modal.IsCompleted, "message box blocks Escape and outside dismissal");
        PressOn(overlayOwner, PhysicalKey.Space);
        Check(alert.IsCompletedSuccessfully && alert.Result == DialogResult.No && input.IsFocused,
            "message box result and focus restoration to the parent dialog");
        Check(Dialog.Close(input, DialogResult.OK) && !Dialog.Close(input), "custom dialog close completes exactly once");
        Check(modal.IsCompletedSuccessfully && modal.Result == DialogResult.OK && trigger.IsFocused,
            "modal returns the result and restores original focus");

        var rightToLeft = new Border { FlowDirection = FlowDirection.RightToLeft };
        foreach (var placement in Enum.GetValues<DrawerPlacement>())
        {
            var drawer = Drawer.ShowAsync(rightToLeft, new DrawerOptions
            {
                Title = "Drawer", Placement = placement, Extent = 200,
                Backdrop = placement == DrawerPlacement.Right ? DialogBackdrop.Blur : DialogBackdrop.Transparent
            }, owner: trigger);
            Pump();
            var surface = Part<Border>(host, "AlderDialogSurface");
            var origin = surface.TranslatePoint(default, host)!.Value;
            bool atEdge = placement switch
            {
                DrawerPlacement.Top => Math.Abs(origin.Y) < 1,
                DrawerPlacement.Bottom => Math.Abs(origin.Y + surface.Bounds.Height - host.Bounds.Height) < 1,
                DrawerPlacement.Left => Math.Abs(origin.X) < 1,
                _ => Math.Abs(origin.X + surface.Bounds.Width - host.Bounds.Width) < 1
            };
            Check(atEdge && surface.Bounds.Width > 0 && surface.Bounds.Height > 0, $"drawer {placement} placement");
            if (placement is DrawerPlacement.Bottom or DrawerPlacement.Right)
            {
                var grip = placement == DrawerPlacement.Bottom
                    ? (Control)Part<Border>(host, "AlderDrawerHandle")
                    : surface.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "Drawer");
                var start = grip.TranslatePoint(new Rect(grip.Bounds.Size).Center, overlayOwner)!.Value;
                var end = start + (placement == DrawerPlacement.Bottom ? new Vector(0, 100) : new Vector(100, 0));
                overlayOwner.MouseMove(start);
                overlayOwner.MouseDown(start, MouseButton.Left);
                overlayOwner.MouseMove(end);
                overlayOwner.MouseUp(end, MouseButton.Left);
                Pump();
            }
            else
            {
                Click(overlayOwner, new Point(host.Bounds.Width / 2, host.Bounds.Height / 2));
            }
            Check(drawer.IsCompletedSuccessfully && drawer.Result == DialogResult.Cancel, $"drawer {placement} dismissal");
        }

        var standalone = Dialog.ShowWindowAsync(overlayOwner, "This is a separate window.", new DialogOptions
        {
            Title = "Standalone", Buttons = DialogButtons.OK
        });
        Pump();
        var childWindow = overlayOwner.OwnedWindows.Single();
        Check(childWindow != overlayOwner && childWindow.IsVisible, "native dialog uses an owned top-level window");
        var action = Part<AlderButton>(childWindow, "AlderDialogOK");
        Check(action.Focus(), "native dialog action focus");
        PressOn(childWindow, PhysicalKey.Space);
        Check(standalone.IsCompletedSuccessfully && standalone.Result == DialogResult.OK &&
              overlayOwner.OwnedWindows.Count == 0, "native window returns typed result");

        var editor = new SmokeDialogContext();
        var editModal = Dialog.ShowModalAsync<SmokeDialogView>(editor, owner: overlayOwner, hostId: "smoke");
        Pump();
        var modalView = host.GetVisualDescendants().OfType<SmokeDialogView>().Single();
        Check(ReferenceEquals(modalView.DataContext, editor) && editor.IsConnected,
            "typed modal creates the editor view and assigns its context");
        Task.Run(() => editor.Close(DialogResult.OK)).GetAwaiter().GetResult();
        editor.Close(DialogResult.Cancel);
        Pump();
        Check(editModal.IsCompletedSuccessfully && editModal.Result == DialogResult.OK && !editor.IsConnected,
            "modal context accepts one result from asynchronous work and detaches");

        var editWindow = Dialog.ShowWindowAsync<SmokeDialogView>(editor, owner: overlayOwner);
        Pump();
        var nativeView = overlayOwner.OwnedWindows.Single().GetVisualDescendants().OfType<SmokeDialogView>().Single();
        Check(nativeView != modalView && ReferenceEquals(nativeView.DataContext, editor) && editor.IsConnected,
            "native dialog creates a fresh editor view and assigns its context");
        editor.Close(DialogResult.Cancel);
        Pump();
        Check(editWindow.IsCompletedSuccessfully && editWindow.Result == DialogResult.Cancel && !editor.IsConnected,
            "native context closes its own window and detaches");

        Check(trigger.Focus(), "focus on the animated drawer opener");
        AlderMotion.SetReduced(overlayOwner, false);
        var dismiss = new AlderButton { Content = "Done" };
        var animated = Drawer.ShowAsync(dismiss, new DrawerOptions { Extent = 160 }, owner: trigger);
        Pump();
        Check(Dialog.Close(dismiss, DialogResult.Yes) && !Dialog.Close(dismiss), "drawer exit accepts one result");
        Check(!animated.IsCompleted && !trigger.Focus(), "drawer remains modal during its exit");
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
            Dispatcher.UIThread.MainLoop(timeout.Token);
        Check(animated.IsCompletedSuccessfully && animated.Result == DialogResult.Yes, "drawer exit completes with the requested result");
        Check(trigger.IsFocused, "drawer exit restores opener focus");

        var unfinished = Dialog.ShowModalAsync<SmokeDialogView>(editor, owner: trigger);
        Pump();
        overlayOwner.Close();
        Check(unfinished.IsCompletedSuccessfully && unfinished.Result == DialogResult.None && !editor.IsConnected,
            "closing the owner resolves a pending dialog and detaches its context");
    }
    finally { if (overlayOwner.IsVisible) overlayOwner.Close(); }

    static void PressOn(Window target, PhysicalKey key)
    {
        target.KeyPressQwerty(key, RawInputModifiers.None);
        target.KeyReleaseQwerty(key, RawInputModifiers.None);
        Pump();
    }

    static void Click(Window target, Point location)
    {
        target.MouseMove(location);
        target.MouseDown(location, MouseButton.Left);
        target.MouseUp(location, MouseButton.Left);
        Pump();
    }
}

void VerifyOverlays()
{
    var action = new AlderButton { Content = "Popover action" };
    var trigger = new AlderButton { Content = "Open popover" };
    var tipTarget = new AlderButton { Content = "Tooltip trigger" };
    var host = new DialogHost
    {
        Content = new StackPanel { Margin = new Thickness(36), Spacing = 12, Children = { trigger, tipTarget } }
    };
    var owner = new Window { Width = 720, Height = 560, Content = host };
    AlderMotion.SetReduced(owner, true);
    owner.Show();
    try
    {
        Pump();
        var flyout = new AlderPopover { Content = action, ShowArrow = true, Heading = "Options" };
        trigger.Flyout = flyout;
        flyout.ShowAt(trigger);
        Pump();
        Check(flyout.IsOpen && action.IsAttachedToVisualTree() && action.IsFocused,
            "popover attaches and focuses its interactive content");
        Key(PhysicalKey.Escape);
        Check(!flyout.IsOpen && trigger.IsFocused, "popover Escape returns focus to its trigger");
        flyout.ShowAt(trigger);
        Pump();
        owner.MouseDown(new Point(20, 350), MouseButton.Left);
        owner.MouseUp(new Point(20, 350), MouseButton.Left);
        Pump();
        Check(!flyout.IsOpen, "popover closes from an outside click");

        AlderToolTip.SetTip(tipTarget, "Details");
        AlderToolTip.SetShowArrow(tipTarget, true);
        Check(tipTarget.Focus(NavigationMethod.Tab), "focus tooltip trigger");
        Pump();
        var tip = Part<Control>(owner, "AlderToolTipSurface");
        Check(tip.IsVisible && tipTarget.IsFocused && !tip.IsHitTestVisible, "tooltip follows keyboard focus without stealing it");
        Key(PhysicalKey.Escape);
        Check(!tip.IsAttachedToVisualTree(), "tooltip dismisses with Escape");
        AlderToolTip.SetTip(tipTarget, null);

        AlderToolTip.SetTip(trigger, "Immediate tooltip");
        AlderToolTip.SetShowDelay(trigger, 0);
        AlderToolTip.SetTip(tipTarget, "Warm tooltip");
        AlderToolTip.SetShowDelay(tipTarget, 1500);
        owner.MouseMove(trigger.TranslatePoint(new Rect(trigger.Bounds.Size).Center, owner)!.Value);
        Pump();
        var firstTip = Part<Control>(owner, "AlderToolTipSurface");
        owner.MouseMove(tipTarget.TranslatePoint(new Rect(tipTarget.Bounds.Size).Center, owner)!.Value);
        Pump();
        var warmTip = Part<Control>(owner, "AlderToolTipSurface");
        Check(warmTip != firstTip && !firstTip.IsAttachedToVisualTree() &&
              warmTip.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Warm tooltip"),
            "hover switches tooltips without restarting the configured delay");
        Key(PhysicalKey.Escape);
        Check(!warmTip.IsAttachedToVisualTree(), "Escape closes a hovered tooltip");
        AlderToolTip.SetTip(trigger, null);
        AlderToolTip.SetTip(tipTarget, null);

        var sheet = Sheet.ShowAsync("Recent activity", new SheetOptions
        {
            Title = "Activity", IsDetached = true, Extent = 400, SnapPoints = [.4, .7],
            Buttons = DialogButtons.OKCancel
        }, owner: trigger);
        Pump();
        var surface = Part<Border>(host, "AlderDialogSurface");
        var handle = Part<Control>(host, "AlderSheetHandle");
        Check(Math.Abs(surface.Bounds.Height - 160) < 1 && Math.Abs(surface.Bounds.X - 8) < 1,
            "detached sheet opens at its first stop");
        Check(handle.Focus(NavigationMethod.Tab), "focus the sheet handle");
        Key(PhysicalKey.ArrowUp);
        Check(Math.Abs(surface.Bounds.Height - 280) < 1, "sheet advances one snap point");
        Key(PhysicalKey.ArrowDown);
        Check(Math.Abs(surface.Bounds.Height - 160) < 1, "sheet returns to the lower snap point");
        Key(PhysicalKey.Escape);
        Check(sheet.IsCompletedSuccessfully && sheet.Result == DialogResult.Cancel, "sheet returns dialog result");

        var article = new Border { Height = 920 };
        var outerScroll = Dialog.ShowModalAsync(article, new DialogOptions
        {
            Title = "Long article", ScrollMode = DialogScrollMode.Outside, BackdropBrush = Brushes.Purple
        }, owner: trigger);
        Pump();
        var dialogScroll = Part<ScrollViewer>(host, "AlderDialogOutsideScroll");
        Check(dialogScroll.Extent.Height > dialogScroll.Viewport.Height &&
              dialogScroll.Offset.Y == 0 &&
              Part<Border>(host, "AlderDialogBackdrop").Background == Brushes.Purple,
            "whole dialog opens at the top with its custom backdrop");
        Check(Dialog.Close(article) && outerScroll.IsCompletedSuccessfully && outerScroll.Result == DialogResult.Cancel,
            "closing an outside-scrolling dialog returns the result");

        var fadingSheet = Sheet.ShowAsync("Sheet with an absolute stop", new SheetOptions
        {
            Title = "Fading sheet", Extent = 400, SnapPoints = [180],
            FadeBackdropAfterLastSnap = true, DragFromHandleOnly = true
        }, owner: trigger);
        Pump();
        var fadedPanel = Part<Border>(host, "AlderDialogSurface");
        var paint = (Border)Part<Border>(host, "AlderDialogBackdrop").Child!;
        var sheetBodyScroll = Part<ScrollViewer>(host, "AlderSheetBodyScroll");
        Check(Math.Abs(fadedPanel.Bounds.Height - 180) < 1 && paint.Opacity == 0 &&
              sheetBodyScroll.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled,
            "sheet uses an absolute stop with a clear scrim");
        Check(Part<Control>(host, "AlderSheetHandle").Focus(NavigationMethod.Tab), "focus the absolute snap sheet handle");
        Key(PhysicalKey.ArrowUp);
        Check(Math.Abs(fadedPanel.Bounds.Height - 400) < 1 && paint.Opacity == 1 &&
              sheetBodyScroll.VerticalScrollBarVisibility == ScrollBarVisibility.Auto,
            "sheet fades the scrim at its final expanded extent");
        Key(PhysicalKey.Escape);
        Check(fadingSheet.IsCompletedSuccessfully, "fading sheet closes");

        var toasts = Toast.Register(owner, "smoke");
        Check(ReferenceEquals(toasts, Toast.Register(owner, "smoke")), "toast registration is idempotent per window and ID");
        toasts.MaxVisible = 1;
        var first = Toast.Show(new ToastOptions("Working")
        {
            Variant = ToastVariant.Loading, Duration = Timeout.InfiniteTimeSpan
        }, owner, "smoke");
        Pump();
        Check(toasts.GetVisualDescendants().OfType<Alder.Avalonia.Controls.ButtonSpinner>().Any(),
            "toast loading indicator");
        Check(first.TryUpdate(new ToastOptions("Complete") { Duration = TimeSpan.FromMilliseconds(70) }),
            "toast updates in place");
        Pump();
        Check(toasts.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Complete"),
            "updated toast renders");
        using (var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(450)))
            Dispatcher.UIThread.MainLoop(timeout.Token);
        Pump();
        Check(first.Completion is { IsCompletedSuccessfully: true, Result: ToastCloseReason.Expired },
            "toast expiration completes its handle");
        var oldest = toasts.Show(new ToastOptions("Oldest") { Duration = Timeout.InfiniteTimeSpan });
        var newest = toasts.Show(new ToastOptions("Newest") { Duration = Timeout.InfiniteTimeSpan });
        Pump();
        Check(oldest.Completion is { IsCompletedSuccessfully: true, Result: ToastCloseReason.Evicted } &&
              toasts.GetVisualDescendants().OfType<ToastCard>().Count() == 1 &&
              toasts.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Newest"),
            "a full toast stack displays the newest notification immediately");
        toasts.Unregister();
        Check(newest.Completion is { IsCompletedSuccessfully: true, Result: ToastCloseReason.HostRemoved },
            "unregistering a toast host disposes its active messages");
    }
    finally { owner.Close(); }

    void Key(PhysicalKey key)
    {
        owner.KeyPressQwerty(key, RawInputModifiers.None);
        owner.KeyReleaseQwerty(key, RawInputModifiers.None);
        Pump();
    }
}

T Find<T>(string name) where T : Control => window.FindControl<T>(name)
    ?? throw new InvalidOperationException($"Missing control {name}.");

static T Part<T>(Control control, string name) where T : Control =>
    control.GetVisualDescendants().OfType<T>().Single(part => part.Name == name);

void Press(Control control, PhysicalKey key)
{
    Check(control.Focus(NavigationMethod.Tab), $"focus on {control.Name ?? control.GetType().Name}");
    window.KeyPressQwerty(key, RawInputModifiers.None);
    window.KeyReleaseQwerty(key, RawInputModifiers.None);
    Pump();
}

static void Pump()
{
    Dispatcher.UIThread.RunJobs();
    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    Dispatcher.UIThread.RunJobs();
}

static void Check(bool condition, string behavior)
{
    if (!condition) throw new InvalidOperationException($"NativeAOT smoke failed to verify {behavior}.");
}

public sealed class SmokeApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new AlderTheme());
    }
}

internal sealed class SmokeDialogContext : IDialogContext
{
    public event EventHandler<DialogResult>? RequestClose;

    public bool IsConnected => RequestClose is not null;
    public void Close(DialogResult result) => RequestClose?.Invoke(this, result);
}

internal sealed class SmokeDialogView : UserControl
{
    public SmokeDialogView() => Content = new TextBlock { Text = "Project editor" };
}

internal sealed class BindingLog : ILogSink
{
    public List<string> Warnings { get; } = [];

    public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning && area == LogArea.Binding;

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
        Log(level, area, source, messageTemplate, []);

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        if (IsEnabled(level, area))
            Warnings.Add($"{source}: {messageTemplate} ({string.Join(", ", propertyValues)})");
    }
}
