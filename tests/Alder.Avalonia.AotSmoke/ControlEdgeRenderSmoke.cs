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
        foreach (double scale in new[] { 1d, 1.25d, 2d })
        {
            VerifyOpenColorPickerEdge(scale);
            VerifyDataGridRowBoundary(TableVariant.Primary, scale);
            VerifyDataGridRowBoundary(TableVariant.Secondary, scale);
            VerifyDataGridActionMenu(TableColumnPin.End, scale);
            VerifyDataGridActionMenu(TableColumnPin.None, scale);
        }
    }

    private static void VerifyOpenColorPickerEdge(double scale)
    {
        var picker = new AlderColorPicker
        {
            Value = AlderColorValue.Parse("#3B82F6"), ShowAlpha = false, ShowField = false
        };
        var window = Show(new Border { Padding = new Thickness(19, 12, 0, 0), Child = picker }, ThemeVariant.Dark, scale);
        try
        {
            using var initial = Capture(window);
            picker.Open();
            using var frame = Capture(window);
            var area = window.GetVisualDescendants().OfType<AlderColorArea>().Single();
            var hue = window.GetVisualDescendants().OfType<AlderColorSlider>().Single(x => x.Name == "Hue");
            var surface = area.GetVisualAncestors().OfType<Border>().First(x => x.Name == "ColorPickerSurface");
            var surfaceColor = ((ISolidColorBrush)surface.Background!).Color;
            var plane = area.TranslatePoint(default, window)!.Value;
            var rail = hue.TranslatePoint(default, window)!.Value;
            int left = (int)(rail.X * scale);
            int right = (int)((rail.X + hue.Bounds.Width) * scale) - 1;
            int top = (int)(rail.Y * scale);
            int bottom = (int)((rail.Y + hue.Bounds.Height) * scale) - 1;

            for (int y = top; y <= bottom; y++)
            for (int x = 0; x < (int)(6 * scale); x++)
            {
                VerifyCap(frame.GetPixel(left + x, y));
                VerifyCap(frame.GetPixel(right - x, y));
            }

            int planeBottom = (int)((plane.Y + area.Bounds.Height) * scale) - 1;
            for (int dy = 0; dy < (int)(6 * scale); dy++)
            for (int dx = 0; dx < (int)(16 * scale); dx++)
            {
                var pixel = frame.GetPixel((int)(plane.X * scale) + dx, planeBottom - dy);
                if (Math.Max(pixel.Red, Math.Max(pixel.Green, pixel.Blue)) > surfaceColor.B + 4)
                    throw new InvalidOperationException($"Color Picker plane showed a bright lower edge ({scale}×): {pixel} at ({dx},{dy}).");
            }

            var sample = plane + new Vector(area.Bounds.Width * .85, area.Bounds.Height * .2);
            var blue = Pixel(frame, sample, scale);
            hue.Value = 120;
            using var updated = Capture(window);
            var green = Pixel(updated, sample, scale);
            if (blue.Blue <= blue.Green || green.Green <= green.Blue || Difference(blue, green) < 48)
                throw new InvalidOperationException($"Color Picker did not repaint the plane after changing hue ({scale}×): {blue} to {green}.");

            void VerifyCap(SKColor pixel)
            {
                if (pixel.Green > surfaceColor.G + 4 || pixel.Blue > surfaceColor.B + 4)
                    throw new InvalidOperationException($"Color Picker hue cap leaked a light edge ({scale}×): {pixel}.");
            }
        }
        finally { window.Close(); }
    }

    private static void VerifyDataGridRowBoundary(TableVariant variant, double scale)
    {
        var grid = new AlderDataGrid { ItemsSource = new[] { "First", "Second" }, RowHeight = 36, Variant = variant };
        grid.Columns.Add(new AlderTableColumn
        {
            Header = "Item",
            CellTemplate = new FuncDataTemplate<string>((item, _) => new TextBlock { Text = item })
        });
        var window = Show(new Border { Padding = new Thickness(16), Child = grid }, ThemeVariant.Dark, scale);
        AlderMotion.SetReduced(window, false);
        try
        {
            using var initial = Capture(window);
            var rows = grid.GetVisualDescendants().OfType<Control>().Where(x => x.Classes.Contains("data-grid-row")).ToArray();
            var first = rows[0].TranslatePoint(new Point(210, rows[0].Bounds.Height / 2), window)!.Value;
            var second = rows[1].TranslatePoint(new Point(210, rows[1].Bounds.Height / 2), window)!.Value;
            double boundary = (first.Y + second.Y) / 2;
            var firstRest = Pixel(initial, first, scale);
            var secondRest = Pixel(initial, second, scale);
            window.MouseMove(first);
            PumpFrames(160);
            using var start = Capture(window);
            var active = Pixel(start, first, scale);
            if (Difference(active, firstRest) <= 2) throw new InvalidOperationException("DataGrid row did not show hover feedback.");

            // Step pixel by pixel through the separator and sample both rows at every stop.
            for (int step = -4; step <= 4; step++)
            {
                window.MouseMove(new Point(first.X, boundary + (step + .5) / scale));
                PumpFrames(28);
                using var frame = Capture(window);
                bool firstHover = Difference(Pixel(frame, first, scale), firstRest) > 1;
                bool secondHover = Difference(Pixel(frame, second, scale), secondRest) > 1;
                if (firstHover != (step < 0) || secondHover != (step >= 0))
                    throw new InvalidOperationException($"DataGrid hover was unstable at the separator ({variant}, {scale}×, step {step}, rows {firstHover}/{secondHover}).");
            }
        }
        finally { window.Close(); }
    }

    private static void VerifyDataGridActionMenu(TableColumnPin pin, double scale)
    {
        var grid = new AlderDataGrid { ItemsSource = new[] { "First", "Second" }, RowHeight = 42 };
        grid.Columns.Add(new AlderTableColumn { Header = "Item", Width = new GridLength(430) });
        grid.Columns.Add(new AlderTableColumn
        {
            Pin = pin, Width = new GridLength(64),
            CellTemplate = new FuncDataTemplate<string>((_, _) => new AlderButton
            {
                Theme = (ControlTheme)Application.Current!.FindResource("AlderDataGridRowActionsButtonTheme")!,
                Flyout = new AlderDropdownFlyout { Items = { new AlderMenuItem { Header = "Edit" } } }
            })
        });

        var window = Show(new Border { Padding = new Thickness(16), Child = grid }, ThemeVariant.Dark, scale);
        AlderMotion.SetReduced(window, false);
        try
        {
            using var initial = Capture(window);
            var scroll = grid.GridScroll!;
            scroll.Offset = new Vector(scroll.ScrollBarMaximum.X, 0);
            using var scrolled = Capture(window);
            var row = grid.GetVisualDescendants().OfType<TableViewRow>().First();
            var button = row.GetVisualDescendants().OfType<AlderButton>().Single();
            var menu = (AlderDropdownFlyout)button.Flyout!;
            var point = button.TranslatePoint(new Rect(button.Bounds.Size).Center, window)!.Value;
            var rowSample = row.TranslatePoint(new Point(scroll.Offset.X + 70, 5), window)!.Value;
            var cellSample = point + new Vector(-23, 0);
            var rest = ReadColors(scrolled);

            window.MouseMove(point);
            using var hover = Capture(window);
            var highlighted = ReadColors(hover);
            if (highlighted == rest)
                throw new InvalidOperationException($"The row action did not hover ({pin}, {scale}×).");

            Click(point);
            if (!menu.IsOpen)
                throw new InvalidOperationException($"The row action menu did not open ({pin}, {scale}×).");
            for (int frame = 0; frame < 4; frame++)
            {
                PumpFrames(28);
                window.MouseMove(point + new Vector(frame % 2, 0));
                VerifyHover();
            }

            Click(point);
            VerifyHover();
            PumpFrames(160);
            if (menu.IsOpen)
                throw new InvalidOperationException($"The row action menu did not close from its trigger ({pin}, {scale}×).");
            VerifyHover();

            Click(point);
            Click(new Point(2, 2));
            PumpFrames(160);
            using var dismissed = Capture(window);
            if (menu.IsOpen || ReadColors(dismissed) != rest)
                throw new InvalidOperationException($"Dismissing the row action menu left stale hover ({pin}, {scale}×).");

            (SKColor Row, SKColor Cell) ReadColors(SKBitmap frame) =>
                (Pixel(frame, rowSample, scale), Pixel(frame, cellSample, scale));

            void VerifyHover()
            {
                using var frame = Capture(window);
                if (ReadColors(frame) != highlighted)
                    throw new InvalidOperationException($"The row background flashed while opening or closing its action menu ({pin}, {scale}×).");
            }

            void Click(Point position)
            {
                window.MouseDown(position, MouseButton.Left);
                window.MouseUp(position, MouseButton.Left);
            }
        }
        finally { window.Close(); }
    }

    private static void PumpFrames(int milliseconds)
    {
        using var done = new CancellationTokenSource(TimeSpan.FromMilliseconds(milliseconds));
        Dispatcher.UIThread.MainLoop(done.Token);
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
            var layerMask = frame.OpacityMask;
            frame.OpacityMask = null;
            using var singleSurface = CaptureVisual(frame, scale);
            frame.OpacityMask = layerMask;
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
                var single = singleSurface.GetPixel(x, y);
                if (Math.Abs(single.Alpha - a.Alpha) > 1)
                    throw new InvalidOperationException($"Window corner alpha differs from a single rounded surface at ({x},{y}): {a.Alpha} vs {single.Alpha} ({theme.Key}, {scale}×).");
                if (Math.Abs(a.Alpha - b.Alpha) > 1 || Difference(a, b) > 2)
                    throw new InvalidOperationException($"Window content changed corner coverage at ({x},{y}) ({theme.Key}, {scale}×): {a} vs {b}.");
            }

            if (blended == 0 || withContent.GetPixel(0, 0).Alpha != 0)
                throw new InvalidOperationException($"Window lost its antialiased transparent corner ({theme.Key}, {scale}×), {blended} blended pixels, mask {frame.OpacityMask}, corner {withContent.GetPixel(0, 0)}, center {withContent.GetPixel(50, 50)}.");

            window.CornerRadius = new CornerRadius(18, 12, 6, 0);
            using var asymmetric = CaptureVisual(frame, scale);
            var topLeft = asymmetric.GetPixel(0, 0).Alpha;
            var bottomLeft = asymmetric.GetPixel(0, asymmetric.Height - 1).Alpha;
            if (topLeft != 0 || bottomLeft != 255)
                throw new InvalidOperationException($"Window asymmetric corner alpha was {topLeft} and {bottomLeft} ({theme.Key}, {scale}×).");

            window.CornerRadius = default;
            using var square = CaptureVisual(frame, scale);
            if (square.GetPixel(0, 0).Alpha != 255)
                throw new InvalidOperationException($"Window did not clear its rounded clip ({theme.Key}, {scale}×).");
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
            var opaqueRail = slider.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "OpaqueRail");
            var translucentRail = slider.GetVisualDescendants().OfType<Decorator>().Single(x => x.Name == "TranslucentRail");
            if (!opaqueRail.IsVisible || translucentRail.IsVisible)
                throw new InvalidOperationException("Opaque Hue still uses a checkerboard layer.");

            slider.Channel = ColorChannel.Alpha;
            using var alpha = Capture(window);
            if (opaqueRail.IsVisible || !translucentRail.IsVisible)
                throw new InvalidOperationException("Alpha lost its checkerboard layer.");

            var origin = translucentRail.TranslatePoint(default, window)!.Value;
            ((Panel)translucentRail.Child!).Background = null;
            using var alphaWithoutChecker = Capture(window);
            if (!Enumerable.Range(14, 20).Any(x =>
                Difference(Pixel(alpha, origin + new Vector(x, translucentRail.Bounds.Height / 2), scale),
                    Pixel(alphaWithoutChecker, origin + new Vector(x, translucentRail.Bounds.Height / 2), scale)) >= 4))
                throw new InvalidOperationException($"Alpha lost its checkerboard ({theme.Key}, {scale}×).");

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
