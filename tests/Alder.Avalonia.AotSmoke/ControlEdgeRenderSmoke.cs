using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;

namespace Alder.Avalonia.AotSmoke;

internal static class ControlEdgeRenderSmoke
{
    public static void Run()
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            foreach (double scale in new[] { 1d, 1.25d, 2d })
            {
                VerifyRoundedWindow(theme, scale);
                VerifyColorArea(theme, scale);
                VerifyColorSlider(theme, scale);
                VerifyEmojiGlyph(theme, scale);
            }

            foreach (double scale in new[] { 1d, 2d })
            {
                VerifyIndicators(theme, scale);
                VerifyFileTreeArrow(theme, scale);
                VerifyCells(theme, scale);
                VerifyInlineSelectScrollBar(theme, scale);
            }
        }
        VerifyRowHover();
    }

    private static void VerifyRoundedWindow(ThemeVariant theme, double scale)
    {
        var background = theme == ThemeVariant.Light ? Brushes.AntiqueWhite : Brushes.DarkSlateGray;
        var cover = new Border { Background = background, IsVisible = false };
        var window = new AlderWindow
        {
            Width = 120, Height = 100, CornerRadius = new CornerRadius(18), Content = cover,
            RequestedThemeVariant = theme, Background = background, ShowTitleBar = false,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
            TransparencyBackgroundFallback = Brushes.Transparent
        };
        window.Show();
        window.SetRenderScaling(scale);
        try
        {
            using var presented = Capture(window);
            var frame = window.GetVisualDescendants().OfType<Control>().Single(x => x.Name == "PART_WindowFrame");
            using var backgroundOnly = CaptureVisual(frame, scale);
            cover.IsVisible = true;
            window.UpdateLayout();
            using var withContent = CaptureVisual(frame, scale);
            int blended = 0;
            int extent = (int)(18 * scale);
            for (int y = 0; y < extent; y++)
            for (int x = 0; x < extent; x++)
            {
                var a = backgroundOnly.GetPixel(x, y);
                var b = withContent.GetPixel(x, y);
                if (a.Alpha is > 5 and < 250) blended++;
                if (Math.Abs(a.Alpha - b.Alpha) > 1 || Difference(a, b) > 2)
                    throw new InvalidOperationException($"Window content changed corner coverage at ({x},{y}) ({theme.Key}, {scale}×): {a} vs {b}.");
            }

            if (blended == 0 || withContent.GetPixel(0, 0).Alpha != 0)
                throw new InvalidOperationException($"Window lost its antialiased transparent corner ({theme.Key}, {scale}×), {blended} blended pixels, mask {frame.OpacityMask}, corner {withContent.GetPixel(0, 0)}, center {withContent.GetPixel(50, 50)}.");
        }
        finally { window.Close(); }
    }

    private static void VerifyColorArea(ThemeVariant theme, double scale)
    {
        var area = new AlderColorArea
        {
            Width = 128, Height = 96, Value = AlderColorValue.FromHsb(211, .6, .5),
            CornerRadius = new CornerRadius(16), BorderBrush = null, ShowDots = false
        };
        var preview = new Border { Child = area, Padding = new Thickness(24) };
        var window = Show(preview, theme, scale);
        try
        {
            using var frame = Capture(window);
            var bottomLeft = area.TranslatePoint(new Point(0, area.Bounds.Height), window)!.Value;
            for (int dy = 1; dy <= (int)(5 * scale); dy++)
            for (int dx = 0; dx <= (int)(12 * scale); dx++)
            {
                var pixel = frame.GetPixel((int)(bottomLeft.X * scale) + dx, (int)(bottomLeft.Y * scale) - dy);
                // The dark end is nearly neutral even where the antialiasing blends with the background.
                if (pixel.Blue - pixel.Red > 12)
                    throw new InvalidOperationException($"Color Area leaked its blue base along the dark edge ({theme.Key}, {scale}×): {pixel}.");
            }
        }
        finally { window.Close(); }
    }

    private static void VerifyColorSlider(ThemeVariant theme, double scale)
    {
        var slider = new AlderColorSlider
        {
            ColorValue = AlderColorValue.FromHsb(211, .6, .5), Channel = ColorChannel.Hue,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        var window = Show(slider, theme, scale);
        try
        {
            using var hue = Capture(window);
            var rail = slider.GetVisualDescendants().OfType<Decorator>().Single(x => x.Name == "Rail");
            var origin = rail.TranslatePoint(default, window)!.Value;
            var checkerHost = rail.Child as Panel;
            var brush = checkerHost?.Background ?? (rail as Border)?.Background;
            if (checkerHost is not null) checkerHost.Background = null;
            else ((Border)rail).Background = null;
            using var hueWithoutChecker = Capture(window);
            if (CompareRailEdge(hue, hueWithoutChecker) > 2)
                throw new InvalidOperationException($"Opaque Hue revealed the checkerboard at the rounded edge ({theme.Key}, {scale}×).");

            slider.Channel = ColorChannel.Alpha;
            using var alphaWithoutChecker = Capture(window);
            if (checkerHost is not null) checkerHost.Background = brush;
            else ((Border)rail).Background = brush;
            using var alpha = Capture(window);
            if (!Enumerable.Range(14, 20).Any(x =>
                Difference(Pixel(alpha, origin + new Vector(x, rail.Bounds.Height / 2), scale),
                    Pixel(alphaWithoutChecker, origin + new Vector(x, rail.Bounds.Height / 2), scale)) >= 4))
                throw new InvalidOperationException($"Alpha lost its checkerboard ({theme.Key}, {scale}×).");

            int CompareRailEdge(SKBitmap a, SKBitmap b)
            {
                int diff = 0;
                for (int y = 0; y < (int)(rail.Bounds.Height * scale); y++)
                for (int x = 0; x < (int)(12 * scale); x++)
                {
                    int left = (int)(origin.X * scale) + x;
                    int top = (int)(origin.Y * scale) + y;
                    diff = Math.Max(diff, Difference(a.GetPixel(left, top), b.GetPixel(left, top)));
                }
                return diff;
            }
        }
        finally { window.Close(); }
    }

    private static void VerifyEmojiGlyph(ThemeVariant theme, double scale)
    {
        var picker = new AlderEmojiPicker
        {
            IsInline = true, ShowRecent = false,
            Categories = [new("faces", "Faces", "😀", [new("😀", "Grinning face")])]
        };
        var window = Show(picker, theme, scale);
        try
        {
            using var ready = Capture(window);
            var grid = picker.GetVisualDescendants().OfType<ListBox>().Single(x => x.Name == "PART_Grid");
            var glyph = grid.GetVisualDescendants().OfType<AlderTextBlock>().Single(x => x.Text == "😀");
            if (glyph.ClipToBounds)
                throw new InvalidOperationException("The Emoji Picker clips the glyph to its text line box.");

            // Deliberately shorten the text line to exercise painted glyph overflow with any system emoji font.
            glyph.LineHeight = 15;
            using var fullGlyph = Capture(window);
            glyph.ClipToBounds = true;
            using var clippedGlyph = Capture(window);
            var origin = glyph.TranslatePoint(default, window)!.Value;
            int left = Math.Max(0, (int)(origin.X * scale));
            int right = Math.Min(fullGlyph.Width, (int)((origin.X + glyph.Bounds.Width) * scale));
            int top = Math.Max(0, (int)((origin.Y - 10) * scale));
            int bottom = Math.Min(fullGlyph.Height, (int)((origin.Y + glyph.Bounds.Height + 10) * scale));
            int changedPixels = 0;
            for (int y = top; y < bottom; y++)
            for (int x = left; x < right; x++)
                if (Difference(fullGlyph.GetPixel(x, y), clippedGlyph.GetPixel(x, y)) > 8) changedPixels++;

            if (changedPixels == 0)
                throw new InvalidOperationException($"Emoji line-box overflow was clipped by an ancestor ({theme.Key}, {scale}×).");
        }
        finally { window.Close(); }
    }

    private static void VerifyRowHover()
    {
        var tree = new AlderFileTree
        {
            ItemsSource = new[] { new AlderFileTreeNode("item", "Item") },
            Width = 280
        };
        var primary = CreateGrid(TableVariant.Primary);
        var secondary = CreateGrid(TableVariant.Secondary);
        var contents = new StackPanel { Margin = new Thickness(16), Spacing = 16, Children = { primary, secondary, tree } };
        var window = Show(contents, ThemeVariant.Dark, 1);
        AlderMotion.SetReduced(window, false);
        try
        {
            using var ready = Capture(window);
            var rows = new[] { GridRow(primary), GridRow(secondary),
                tree.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "PART_Row") };
            foreach (var row in rows)
            {
                var point = row.TranslatePoint(new Point(row.Bounds.Width - 24, row.Bounds.Height / 2), window)!.Value;
                using var initial = Capture(window);
                var idle = Pixel(initial, point, 1);
                window.MouseMove(point);
                var entering = Sample(point);
                using var hover = Capture(window);
                var target = Pixel(hover, point, 1);
                window.MouseMove(new Point(2, 2));
                var leaving = Sample(point);
                if (entering.Concat(leaving).Any(pixel => pixel.Red > Math.Max(idle.Red, target.Red) + 3))
                    throw new InvalidOperationException($"Hover animation overexposed a dark row ({row.GetType().Name}, idle {idle}, target {target}).");
            }
        }
        finally { window.Close(); }

        static AlderDataGrid CreateGrid(TableVariant variant)
        {
            var grid = new AlderDataGrid { ItemsSource = new[] { "Item" }, Variant = variant, RowHeight = 36 };
            grid.Columns.Add(new AlderTableColumn { Header = "Name", CellTemplate = new FuncDataTemplate<string>((item, _) => new TextBlock { Text = item }) });
            return grid;
        }

        static Control GridRow(AlderDataGrid grid) => grid.GetVisualDescendants().OfType<Control>().Single(x => x.Classes.Contains("data-grid-row"));

        List<SKColor> Sample(Point point)
        {
            var samples = new List<SKColor>();
            using var done = new CancellationTokenSource(TimeSpan.FromMilliseconds(600));
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += (_, _) =>
            {
                using var frame = Capture(window);
                samples.Add(Pixel(frame, point, 1));
                if (samples.Count >= 10) done.Cancel();
            };
            timer.Start();
            try { Dispatcher.UIThread.MainLoop(done.Token); }
            finally { timer.Stop(); }
            if (samples.Count < 3) throw new InvalidOperationException("No intermediate hover frames were captured.");
            return samples;
        }
    }

    private static void VerifyIndicators(ThemeVariant theme, double scale)
    {
        var item = new AlderAccordionItem { Header = "Account settings", Content = "Account details" };
        var disclosure = new AlderDisclosure { Header = "Show details", Content = "Details" };
        var accordion = new AlderAccordion { Items = { item } };
        var window = Show(new StackPanel { Spacing = 16, Margin = new Thickness(16), Children = { accordion, disclosure } }, theme, scale);
        try
        {
            using var collapsed = Capture(window);
            foreach (var control in new AlderDisclosure[] { item, disclosure })
            {
                var icon = control.GetVisualDescendants().OfType<PathIcon>().Single(x => x.Name == "PART_Indicator");
                var center = icon.TranslatePoint(new Rect(icon.Bounds.Size).Center, window)!.Value;
                VerifyChevronInk(collapsed, center, scale, expanded: false, theme);
                control.IsExpanded = true;
            }

            using var expanded = Capture(window);
            foreach (var icon in window.GetVisualDescendants().OfType<PathIcon>().Where(x => x.Name == "PART_Indicator"))
            {
                var center = icon.TranslatePoint(new Rect(icon.Bounds.Size).Center, window)!.Value;
                VerifyChevronInk(expanded, center, scale, expanded: true, theme);
            }
        }
        finally { window.Close(); }
    }

    private static void VerifyChevronInk(SKBitmap frame, Point center, double scale, bool expanded, ThemeVariant theme)
    {
        var backdrop = Pixel(frame, center + new Vector(0, -6), scale);
        double armY = expanded ? 2 : -2;
        var left = Pixel(frame, center + new Vector(-4, armY), scale);
        var right = Pixel(frame, center + new Vector(4, armY), scale);
        if (Difference(left, backdrop) < 24 || Difference(right, backdrop) < 24)
            throw new InvalidOperationException($"Chevron arms were clipped or offset ({theme.Key}, {scale}×, expanded={expanded}).");
    }

    private static void VerifyFileTreeArrow(ThemeVariant theme, double scale)
    {
        var folder = new AlderFileTreeNode("src", "src") { IsFolder = true };
        folder.Children.Add(new AlderFileTreeNode("app", "App.axaml"));
        var tree = new AlderFileTree
        {
            ItemsSource = new[] { folder }, Width = 230, HorizontalAlignment = HorizontalAlignment.Center
        };
        var window = Show(tree, theme, scale);
        try
        {
            using var collapsed = Capture(window);
            var indicator = tree.GetVisualDescendants().OfType<PathIcon>()
                .Single(x => x.Name == "Indicator" && x.IsEffectivelyVisible);
            var center = indicator.TranslatePoint(new Rect(indicator.Bounds.Size).Center, window)!.Value;
            var background = Pixel(collapsed, center + new Vector(-5, 0), scale);
            var upper = Pixel(collapsed, center + new Vector(-2, -4), scale);
            var lower = Pixel(collapsed, center + new Vector(-2, 4), scale);
            if (Difference(upper, background) < 24 || Difference(lower, background) < 24)
                throw new InvalidOperationException($"File Tree cropped a collapsed chevron arm ({theme.Key}, {scale}×).");

            window.MouseMove(center);
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            window.MouseMove(new Point(2, 2));
            using var expanded = Capture(window);
            if (!folder.IsExpanded) throw new InvalidOperationException("File Tree did not expand from the chevron.");
            VerifyChevronInk(expanded, center, scale, expanded: false, theme);
        }
        finally { window.Close(); }
    }

    private static void VerifyCells(ThemeVariant theme, double scale)
    {
        var select = new AlderCellSelect { Label = "Theme", Items = { "Default", "Dark" }, Width = 252 };
        var color = new AlderCellColorPicker { Label = "Accent", Value = AlderColorValue.Parse("#3B82F6"), Width = 252 };
        var preview = new StackPanel { Spacing = 20, Margin = new Thickness(20), Children = { select, color } };
        var window = Show(preview, theme, scale);
        try
        {
            using var rest = Capture(window);
            var button = select.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "PART_Trigger");
            var swatchButton = color.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "PART_Trigger");
            foreach (var trigger in new[] { button, swatchButton })
            {
                var top = trigger.TranslatePoint(new Point(trigger.Bounds.Width / 2, -3), window)!.Value;
                if (IsAccent(Pixel(rest, top, scale)))
                    throw new InvalidOperationException($"The unfocused cell rendered a blue outline ({theme.Key}, {scale}×).");

                trigger.Focus(NavigationMethod.Tab);
                using var focused = Capture(window);
                if (!IsAccent(Pixel(focused, top, scale)))
                    throw new InvalidOperationException($"The cell lost its keyboard focus ring ({theme.Key}, {scale}×).");
            }
        }
        finally { window.Close(); }
    }

    private static void VerifyInlineSelectScrollBar(ThemeVariant theme, double scale)
    {
        var select = new AlderInlineSelect
        {
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            DropDownWidth = 140, MaxDropDownHeight = 132, SelectedIndex = 0,
            ItemsSource = Enumerable.Range(1, 16).Select(i => $"Option {i}").ToArray()
        };
        var window = Show(select, theme, scale);
        try
        {
            select.IsDropDownOpen = true;
            using var frame = Capture(window);
            var surface = window.GetVisualDescendants().OfType<Panel>().Single(x => x.Name == "AlderPopoverSurface");
            var scroll = surface.GetVisualDescendants().OfType<ScrollViewer>().Single(x => x.Name == "PART_PickerScroll");
            var origin = surface.TranslatePoint(default, window)!.Value;
            var leftCorner = Pixel(frame, origin + new Vector(5, 4), scale);
            var rightCorner = Pixel(frame, origin + new Vector(surface.Bounds.Width - 5, 4), scale);
            if (scroll.Extent.Height <= scroll.Viewport.Height || Difference(leftCorner, rightCorner) > 9)
                throw new InvalidOperationException($"Inline Select painted its scrollbar through the rounded corner ({theme.Key}, {scale}×).");

            scroll.Offset = new Vector(0, scroll.Extent.Height);
            using var end = Capture(window);
            var bottomY = surface.Bounds.Height - 5;
            var bottomLeft = Pixel(end, origin + new Vector(5, bottomY), scale);
            var bottomRight = Pixel(end, origin + new Vector(surface.Bounds.Width - 5, bottomY), scale);
            if (scroll.Offset.Y <= 0 || Difference(bottomLeft, bottomRight) > 9)
                throw new InvalidOperationException($"Inline Select overflowed the lower corner or could not be scrolled ({theme.Key}, {scale}×).");
        }
        finally { window.Close(); }
    }

    private static Window Show(Control content, ThemeVariant theme, double scale)
    {
        var window = new Window
        {
            Width = 390, Height = 410, Content = content, RequestedThemeVariant = theme,
            Background = theme == ThemeVariant.Light ? Brushes.WhiteSmoke : new SolidColorBrush(Color.Parse("#060607"))
        };
        AlderMotion.SetReduced(window, true);
        window.Show();
        window.SetRenderScaling(scale);
        return window;
    }

    private static SKBitmap CaptureVisual(Control control, double scale)
    {
        var size = PixelSize.FromSize(control.Bounds.Size, scale);
        using var bitmap = new RenderTargetBitmap(size, new Vector(96 * scale, 96 * scale));
        bitmap.Render(control);
        using var png = new MemoryStream();
        bitmap.Save(png, PngBitmapEncoderOptions.Default);
        return SKBitmap.Decode(png.ToArray());
    }

    private static SKBitmap Capture(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        using var image = window.CaptureRenderedFrame()!;
        using var png = new MemoryStream();
        image.Save(png, PngBitmapEncoderOptions.Default);
        return SKBitmap.Decode(png.ToArray());
    }

    private static SKColor Pixel(SKBitmap frame, Point p, double scale) =>
        frame.GetPixel((int)(p.X * scale), (int)(p.Y * scale));

    private static int Difference(SKColor a, SKColor b) =>
        Math.Max(Math.Abs(a.Red - b.Red), Math.Max(Math.Abs(a.Green - b.Green), Math.Abs(a.Blue - b.Blue)));

    private static bool IsAccent(SKColor pixel) => pixel.Blue > 160 && pixel.Blue - pixel.Red > 90 && pixel.Green - pixel.Red > 35;
}
