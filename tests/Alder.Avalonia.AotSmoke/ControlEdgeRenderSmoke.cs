using System.Diagnostics;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
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
                VerifySalashWindowFrame(theme, scale);
                VerifyColorArea(theme, scale);
                VerifyColorSlider(theme, scale);
                VerifyEmojiGlyph(theme, scale);
                VerifyChartTooltipShadow(theme, scale);
                VerifyPromptStarterShadow(theme, scale);
            }

            foreach (double scale in new[] { 1d, 2d })
            {
                VerifyIndicators(theme, scale);
                VerifyChatSourcesChevron(theme, scale);
                VerifyInputLoadingIndicator(theme, scale);
                VerifyButtonFocus(theme, scale);
                VerifyPopoverFocusReturn(theme, scale);
                VerifySegmentFocus(theme, scale);
                VerifyFieldActionFocus(theme, scale);
                VerifyFileTreeArrow(theme, scale);
                VerifyDenseLineChart(theme, scale);
                VerifyChipGeometry(theme, scale);
                VerifyCells(theme, scale);
                VerifyPickerChartAndMarkdownFocus(theme, scale);
                VerifyInlineSelectScrollBar(theme, scale);
                VerifyAgendaCorners(theme, scale);
                VerifyBloomMenuEdges(theme, scale);
                VerifyApprovalDismissIcon(theme, scale);
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

    private static void VerifyBloomMenuEdges(ThemeVariant theme, double scale)
    {
        var menu = new AlderBloomMenu { Foreground = new SolidColorBrush(Color.Parse("#129B38")) };
        var itemInk = new SolidColorBrush(Color.Parse("#D52B69"));
        var glyph = Geometry.Parse("M0 0 H16 V16 H0 Z");
        foreach (var label in new[] { "Doc", "Board", "Table", "Folder", "Reminder", "Link" })
            menu.Items.Add(new AlderBloomMenuItem { Content = label, Icon = glyph, Foreground = itemInk });

        var preview = new Border { Child = menu, Height = 300, CornerRadius = new CornerRadius(14), ClipToBounds = true };
        var window = Show(new ScrollViewer { Content = preview }, theme, scale);
        window.Width = 280;
        try
        {
            using var before = Capture(window);
            AlderMotion.SetReduced(window, false);
            menu.IsOpen = true;
            var surface = window.GetVisualDescendants().OfType<AlderBloomMenuSurface>().Single();
            VerifyBloomMenuHandoff(window, menu, scale);
            surface.FinishMotion();
            using var after = Capture(window);
            var chrome = surface.GetVisualDescendants().OfType<Border>().Single(part => part.Name == "PART_Chrome");
            var bounds = new Rect(chrome.TranslatePoint(default, window)!.Value, chrome.Bounds.Size);
            var shadowSample = new Point(bounds.Center.X, bounds.Bottom + 5);
            var offset = new Vector(0, -28);
            var edge = Pixel(after, new Point(bounds.Left + .25, bounds.Center.Y) + offset, scale);
            var fill = Pixel(after, new Point(bounds.Left + 4, bounds.Center.Y) + offset, scale);

            if (Difference(edge, fill) < 6)
                throw new InvalidOperationException($"Bloom Menu lost its panel edge in {theme} at {scale}×.");
            var near = Difference(Pixel(after, shadowSample, scale), Pixel(before, shadowSample, scale));
            var tail = shadowSample + new Vector(0, 17);
            var far = Difference(Pixel(after, tail, scale), Pixel(before, tail, scale));
            if (near < 4 || far >= near)
                throw new InvalidOperationException($"Bloom Menu shadow was clipped in {theme} at {scale}× (near {near}, tail {far}).");

            menu.IsOpen = false;
            VerifyBloomMenuHandoff(window, menu, scale);
            surface.FinishMotion();
        }
        finally { window.Close(); }
    }

    private static void VerifyChartTooltipShadow(ThemeVariant theme, double scale)
    {
        var tooltip = new AlderChartTooltip
        {
            Label = "Week 1",
            ItemsSource = [new("Revenue", 42)],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        VerifyExteriorShadow(tooltip, "Chart Tooltip", theme, scale);
    }

    private static void VerifyPromptStarterShadow(ThemeVariant theme, double scale)
    {
        var prompt = new AlderPromptInput
        {
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { "starter-composer" }
        };
        VerifyExteriorShadow(prompt, "Prompt Input", theme, scale);
    }

    private static void VerifyExteriorShadow(Control control, string name, ThemeVariant theme, double scale)
    {
        var preview = new Border { Child = control, Padding = new Thickness(24), CornerRadius = new CornerRadius(14), ClipToBounds = true };
        var window = Show(preview, theme, scale);
        window.Width = 280;
        try
        {
            using var frame = Capture(window);
            var bounds = new Rect(control.TranslatePoint(default, window)!.Value, control.Bounds.Size);
            SKColor Read(double offset) => Pixel(frame, new Point(bounds.Center.X, bounds.Bottom + offset), scale);
            var backdrop = Read(24);
            int near = Difference(Read(2), backdrop);
            int tail = Difference(Read(7), backdrop);
            if (near < 5 || near <= tail)
                throw new InvalidOperationException($"{name} needs a visible shadow outside its bounds in {theme} at {scale}× (near {near}, tail {tail}).");
        }
        finally { window.Close(); }
    }

    private static void VerifyBloomMenuHandoff(Window window, AlderBloomMenu menu, double scale)
    {
        var origin = menu.TranslatePoint(default, window)!.Value;
        var anchorBounds = new Rect(origin, menu.Bounds.Size);
        var animationBounds = anchorBounds.Inflate(new Thickness(32, 100));
        var time = Stopwatch.StartNew();
        do
        {
            using var frame = Capture(window);
            bool hasCaption = false, hasItem = false;
            for (int y = (int)(animationBounds.Top * scale); y < (int)(animationBounds.Bottom * scale); y++)
            for (int x = (int)(animationBounds.Left * scale); x < (int)(animationBounds.Right * scale); x++)
            {
                var pixel = frame.GetPixel(x, y);
                if (pixel.Green > pixel.Red + 18 && pixel.Green > pixel.Blue + 15)
                {
                    hasCaption = true;
                    if (y / scale < anchorBounds.Center.Y - 9 || y / scale > anchorBounds.Center.Y + 9)
                        throw new InvalidOperationException($"Bloom Menu caption jumped vertically at {scale}×.");
                }
                hasItem |= pixel.Red > pixel.Green + 16 && pixel.Red > pixel.Blue + 11;
            }

            if (!hasCaption && !hasItem)
                throw new InvalidOperationException($"Bloom Menu showed an empty transition frame at {time.ElapsedMilliseconds} ms ({scale}×).");

            Thread.Sleep(13);
        } while (time.ElapsedMilliseconds < 310);
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

    private static void VerifySalashWindowFrame(ThemeVariant theme, double scale)
    {
        var window = new RenderSalashWindow { Width = 416, Height = 444, RequestedThemeVariant = theme, Background = Brushes.Gray };
        window.Show();
        window.SetRenderScaling(scale);
        try
        {
            using var presented = Capture(window);
            var chrome = window.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_WindowFrame");
            using var frame = CaptureVisual(chrome, scale);
            var outline = window.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_WindowOutline");
            var brush = (ISolidColorBrush)outline.BorderBrush!;
            var edge = presented.GetPixel(presented.Width / 2, 0);
            var center = frame.GetPixel(frame.Width / 2, frame.Height / 2);
            if (frame.GetPixel(0, 0).Alpha != 0 || frame.GetPixel(frame.Width - 1, frame.Height - 1).Alpha != 0 ||
                center != SKColors.Gray || Difference(edge, new SKColor(brush.Color.R, brush.Color.G, brush.Color.B)) > 4)
                throw new InvalidOperationException($"Login window corner or themed border was lost ({theme.Key}, {scale}×).");
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private sealed class RenderSalashWindow : AlderSalashWindow
    {
        protected override Task<Window?> CreateNextWindow() => Task.FromResult<Window?>(null);
    }

    private static void VerifyPopoverFocusReturn(ThemeVariant theme, double scale)
    {
        var popover = new AlderPopover
        {
            Content = new StackPanel
            {
                Children = { new AlderButton { Content = "First" }, new AlderButton { Content = "Last" } }
            }
        };
        var trigger = new AlderButton { Content = "Open", Flyout = popover, HorizontalAlignment = HorizontalAlignment.Center };
        var window = Show(new StackPanel { Margin = new Thickness(32), Children = { trigger } }, theme, scale);
        try
        {
            trigger.Focus(NavigationMethod.Tab);
            using var initial = Capture(window);
            Press(PhysicalKey.Enter);
            Press(PhysicalKey.Tab, RawInputModifiers.Shift);
            if (!popover.IsOpen || window.FocusManager?.GetFocusedElement() is not AlderButton { Content: "Last" })
                throw new InvalidOperationException("Shift+Tab focused the popover chrome instead of its last action.");
            Press(PhysicalKey.Escape);
            using var returned = Capture(window);
            if (popover.IsOpen || !trigger.IsFocused || !trigger.Classes.Contains(":focus-visible"))
                throw new InvalidOperationException("The popover did not return keyboard focus to its trigger.");

            var origin = trigger.TranslatePoint(default, window)!.Value;
            for (int y = -4; y < trigger.Bounds.Height + 4; y++)
            for (int x = -4; x < trigger.Bounds.Width + 4; x++)
            {
                var point = origin + new Vector(x, y);
                if (Difference(Pixel(initial, point, scale), Pixel(returned, point, scale)) > 2)
                    throw new InvalidOperationException($"The trigger changed appearance after the popover closed ({theme.Key}, {scale}×).");
            }
        }
        finally { window.Close(); }

        void Press(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            window.KeyPressQwerty(key, modifiers);
            window.KeyReleaseQwerty(key, modifiers);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
        }
    }

    private static void VerifyButtonFocus(ThemeVariant theme, double scale)
    {
        var enter = new AlderButton { Content = "Sign in", Width = 160, Height = 44 };
        var start = new AlderButton { Content = "Previous", Variant = ButtonVariant.Ghost };
        var clip = new Border { Child = enter, ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Left };
        var picker = new AlderColorPicker();
        var window = Show(new StackPanel { Margin = new Thickness(24), Spacing = 12, Children = { start, clip, picker } }, theme, scale);
        try
        {
            start.Focus();
            using var rest = Capture(window);
            Tab(window);
            using var focused = Capture(window);
            CheckFocusFill(enter, rest, focused, "AlderAccentHover");

            Tab(window);
            using var pickerFocused = Capture(window);
            var trigger = picker.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_Trigger");
            CheckFocusFill(trigger, focused, pickerFocused, "AlderNeutral");
        }
        finally { window.Close(); }

        void CheckFocusFill(Button button, SKBitmap before, SKBitmap after, string key)
        {
            var center = button.Bounds.Width / 2;
            var inside = button.TranslatePoint(new Point(center, 2), window)!.Value;
            var outside = button.TranslatePoint(new Point(center, -1), window)!.Value;
            button.TryFindResource(key, theme, out var value);
            var color = ((ISolidColorBrush)value!).Color;
            var fill = Pixel(after, inside, scale);
            if (!button.IsFocused || fill == Pixel(before, inside, scale) ||
                Difference(fill, new SKColor(color.R, color.G, color.B)) > 4 ||
                Pixel(after, outside, scale) != Pixel(before, outside, scale))
                throw new InvalidOperationException($"{button.Name ?? button.Content} did not show its focus fill inside the button ({theme.Key}, {scale}×).");
        }
    }

    private static void VerifySegmentFocus(ThemeVariant theme, double scale)
    {
        var light = new AlderSegmentItem { Key = "light", Content = "Light" };
        var dark = new AlderSegmentItem { Key = "dark", Content = "Dark" };
        var segment = new AlderSegment { Size = AlderSegmentSize.Small, SelectedKey = "light", Items = { light, dark } };
        var start = new AlderButton { Content = "Previous" };
        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 12, Children = { start, segment } };
        var window = Show(panel, theme, scale);
        try
        {
            start.Focus();
            using var rest = Capture(window);
            Tab(window);
            using var focused = Capture(window);
            var inside = light.TranslatePoint(new Point(light.Bounds.Width / 2, 4), window)!.Value;
            var corner = light.TranslatePoint(new Point(0, 0), window)!.Value;
            var outside = light.TranslatePoint(new Point(light.Bounds.Width / 2, -2), window)!.Value;
            if (!light.IsFocused || !Equals(segment.SelectedKey, "light") ||
                Pixel(focused, inside, scale) == Pixel(rest, inside, scale) ||
                Pixel(focused, corner, scale) != Pixel(rest, corner, scale) ||
                Pixel(focused, outside, scale) != Pixel(rest, outside, scale))
                throw new InvalidOperationException($"The selected segment did not show a borderless focus fill ({theme.Key}, {scale}×).");

            start.Focus();
            window.MouseMove(inside);
            using var hover = Capture(window);
            if (Pixel(hover, inside, scale) != Pixel(focused, inside, scale))
                throw new InvalidOperationException($"Segment keyboard focus differed from pointer hover ({theme.Key}, {scale}×).");
        }
        finally { window.Close(); }
    }

    private static void VerifyFieldActionFocus(ThemeVariant theme, double scale)
    {
        var field = new AlderSearchField { Text = "Find a project" };
        var window = Show(new Border { Child = field, Padding = new Thickness(24) }, theme, scale);
        try
        {
            field.Focus(NavigationMethod.Tab);
            using var editing = Capture(window);
            Tab(window);
            using var acting = Capture(window);
            var action = field.GetVisualDescendants().OfType<Button>().Single(button => button.IsFocused);
            var point = action.TranslatePoint(new Point(action.Bounds.Width / 2, 2), window)!.Value;
            action.TryFindResource("AlderNeutralHover", theme, out var brush);
            var color = ((ISolidColorBrush)brush!).Color;
            if (!editing.Pixels.Any(IsAccent) || acting.Pixels.Any(IsAccent) ||
                Difference(Pixel(acting, point, scale), new SKColor(color.R, color.G, color.B)) > 5)
                throw new InvalidOperationException($"Search suffix did not use its hover fill or retained the editor outline ({theme.Key}, {scale}×).");
        }
        finally { window.Close(); }
    }

    private static void Tab(Window window)
    {
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.None);
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

    private static void VerifyChatSourcesChevron(ThemeVariant theme, double scale)
    {
        var group = new AlderDisclosure
        {
            Theme = (ControlTheme)Application.Current!.FindResource("AlderChatSourcesTheme")!,
            Header = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 8,
                Children =
                {
                    new Panel { Width = 44, Height = 20 },
                    new AlderTextBlock { Text = "3 sources", FontSize = 12, LineHeight = 16, VerticalAlignment = VerticalAlignment.Center }
                }
            },
            Content = new AlderChatSource { Content = "Avalonia Docs" }
        };
        var preview = new Border { Padding = new Thickness(22, 24), MaxWidth = 248, Child = group };
        preview.Styles.Add(new Style(x => x.OfType<AlderToggleButton>().Descendant().OfType<PathIcon>())
        {
            Setters = { new Setter(StyledElement.ThemeProperty, Application.Current!.FindResource("AlderIcon16Theme")) }
        });
        var window = Show(preview, theme, scale);
        try
        {
            using var collapsed = Capture(window);
            var icon = group.GetVisualDescendants().OfType<PathIcon>().Single(x => x.Name == "PART_Indicator");
            var center = icon.TranslatePoint(new Rect(icon.Bounds.Size).Center, window)!.Value;
            VerifyChevronInk(collapsed, center, scale, expanded: false, theme);

            window.MouseMove(center);
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            using var expanded = Capture(window);
            if (!group.IsExpanded)
                throw new InvalidOperationException("The Chat Source group did not expand when its chevron was clicked.");
            VerifyChevronInk(expanded, center, scale, expanded: true, theme);
        }
        finally { window.Close(); }
    }

    private static void VerifyInputLoadingIndicator(ThemeVariant theme, double scale)
    {
        var app = Application.Current!;
        var spinner = new AlderSpinner { Size = SpinnerSize.Small };
        var input = new AlderInputGroup { Width = 160, Text = "Sending...", IsReadOnly = true, Suffix = spinner };
        var preview = new WrapPanel { Children = { input } };
        preview.Styles.Add(new Style(x => x.OfType<PathIcon>())
        {
            Setters = { new Setter(StyledElement.ThemeProperty, app.FindResource("AlderIcon16Theme")) }
        });
        var window = Show(preview, theme, scale);
        try
        {
            using var frame = Capture(window);
            var center = spinner.TranslatePoint(new Rect(spinner.Bounds.Size).Center, window)!.Value;
            var background = Pixel(frame, center, scale);
            foreach (var offset in new[] { new Vector(-3, -5), new Vector(-5.5, 0), new Vector(0, 5), new Vector(5, 2) })
            {
                var ink = Pixel(frame, center + offset, scale);
                if (ink.Blue - ink.Red - (background.Blue - background.Red) < 25)
                    throw new InvalidOperationException($"The Input loading arc is clipped at {offset} ({theme.Key}, {scale}×).");
            }
        }
        finally { window.Close(); }
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

    private static void VerifyChipGeometry(ThemeVariant theme, double scale)
    {
        var geometry = Geometry.Parse("M10 10H26V20H10Z");
        var chip = new AlderChip { Content = "Ready", Icon = geometry, TrailingIcon = geometry, Foreground = Brushes.Magenta };
        var window = Show(chip, theme, scale);
        try
        {
            foreach (var size in new[] { AlderSize.Small, AlderSize.Large })
            {
                chip.Size = size;
                using var bitmap = Capture(window);
                var slots = chip.GetVisualDescendants().OfType<AlderChipIcon>().Where(slot => slot.IsVisible).ToArray();
                if (slots.Length != 2) throw new InvalidOperationException("Chip did not retain both icon slots.");
                foreach (var slot in slots)
                {
                    var point = slot.TranslatePoint(new Rect(slot.Bounds.Size).Center, window)!.Value;
                    var color = Pixel(bitmap, point, scale);
                    if (slot.Bounds.Width != chip.IconSize || color.Red < 220 || color.Blue < 220 || color.Green > 35)
                        throw new InvalidOperationException($"Chip did not center the vector at {size} ({theme.Key}, {scale}×).");
                }
            }
        }
        finally { window.Close(); }
    }

    private static void VerifyDenseLineChart(ThemeVariant theme, double scale)
    {
        const int count = 10001;
        var values = Enumerable.Repeat<double?>(25, count).ToArray();
        values[count / 2] = 80;
        Array.Clear(values, 7000, 1000);
        var chart = new AlderLineChart
        {
            Width = 220, Height = 100, Padding = default, IsAnimationEnabled = false,
            ShowXAxis = false, ShowYAxis = false, ShowGrid = false, ShowTooltip = false,
            ValueAxisTicks = [0, 100],
            Categories = Enumerable.Range(0, count).Select(i => i.ToString()).ToArray(),
            Series = [new("Sample", values, Brushes.Magenta)]
        };
        var window = Show(chart, theme, scale);
        try
        {
            var origin = chart.TranslatePoint(default, window)!.Value;
            using var rest = Capture(window);
            if (!HasInk(rest, 110, 20) || !HasInk(rest, 77, 75) || HasInk(rest, 165, 75))
                throw new InvalidOperationException($"Dense line lost its peak, baseline, or gap ({theme.Key}, {scale}×).");

            window.MouseMove(origin + new Vector(77, 50));
            using var hover = Capture(window);
            if (!HasInk(hover, 77, 72) || !HasInk(hover, 110, 20))
                throw new InvalidOperationException($"Hover did not paint the active point over the retained line ({theme.Key}, {scale}×).");

            var replacement = (double?[])values.Clone();
            replacement[count / 2] = 50;
            chart.Series = [new("Sample", replacement, Brushes.Magenta)];
            using var updated = Capture(window);
            if (HasInk(updated, 110, 20) || !HasInk(updated, 110, 50) || HasInk(updated, 77, 72))
                throw new InvalidOperationException($"Replacing the series left stale line or hover pixels ({theme.Key}, {scale}×).");

            bool HasInk(SKBitmap frame, double x, double y)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var pixel = Pixel(frame, origin + new Vector(x + dx / scale, y), scale);
                    if (pixel.Red > pixel.Green + 55 && pixel.Blue > pixel.Green + 55)
                        return true;
                }
                return false;
            }
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
            var button = select.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "PART_Trigger");
            var swatchButton = color.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "PART_Trigger");
            using var rest = Capture(window);
            foreach (var trigger in new[] { button, swatchButton })
            {
                trigger.Focus(NavigationMethod.Tab);
                using var focused = Capture(window);
                CheckFocusInside(trigger, window, rest, focused, theme, scale);
            }
        }
        finally { window.Close(); }
    }

    private static void VerifyPickerChartAndMarkdownFocus(ThemeVariant theme, double scale)
    {
        var select = new AlderNativeSelect { Items = { "Default", "Dark" }, SelectedIndex = 0 };
        var chart = new AlderAreaChart { Width = 220, Height = 120 };
        var markdown = new AlderMarkdown { Markdown = "Selectable text", Width = 220, MinHeight = 40 };
        var code = new AlderCodeBlock { Code = "42", Width = 220 };
        var preview = new StackPanel { Spacing = 20, Margin = new Thickness(20), Children = { select, chart, markdown, code } };
        var window = Show(preview, theme, scale);
        try
        {
            using var rest = Capture(window);
            var trigger = select.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "PART_Trigger");
            foreach (var target in new Control[] { trigger, chart, markdown, code })
            {
                if (!target.Focus(NavigationMethod.Tab))
                    throw new InvalidOperationException($"{target.GetType().Name} could not receive keyboard focus.");

                using var focused = Capture(window);
                CheckFocusInside(target, window, rest, focused, theme, scale);
            }
        }
        finally { window.Close(); }
    }

    private static void CheckFocusInside(Control target, Window window, SKBitmap rest, SKBitmap focused, ThemeVariant theme, double scale)
    {
        var middleX = target.Bounds.Width / 2;
        var samplesOutside = new[] { new Point(middleX, -3), new Point(1, -1), new Point(-1, target.Bounds.Height / 2) };
        var inside = target.TranslatePoint(new Point(middleX, 5), window)!.Value;
        var edge = target.TranslatePoint(new Point(middleX, 0.5), window)!.Value;
        if (Difference(Pixel(rest, edge, scale), Pixel(focused, edge, scale)) > 64)
            throw new InvalidOperationException($"{target.GetType().Name} painted a high-contrast focus edge ({theme.Key}, {scale}×).");
        foreach (var sample in samplesOutside)
        {
            var point = target.TranslatePoint(sample, window)!.Value;
            if (Difference(Pixel(rest, point, scale), Pixel(focused, point, scale)) > 2)
                throw new InvalidOperationException($"{target.GetType().Name} painted a focus outline outside the surface ({theme.Key}, {scale}×).");
        }

        if (Difference(Pixel(rest, inside, scale), Pixel(focused, inside, scale)) < 5)
            throw new InvalidOperationException($"{target.GetType().Name} lost its visible keyboard focus feedback ({theme.Key}, {scale}×).");
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
            var content = scroll.GetVisualDescendants().OfType<ScrollContentPresenter>().Single(x => x.Name == "PART_ContentPresenter");
            var bar = scroll.GetVisualDescendants().OfType<ScrollBar>().Single(x => x.Orientation == Orientation.Vertical);
            if (!bar.IsVisible || Math.Abs(content.Bounds.Width - scroll.Bounds.Width) > 0.5 || bar.Bounds.Width > 8)
                throw new InvalidOperationException($"Inline Select scrollbar layout is incorrect ({theme.Key}, {scale}×): " +
                    $"content width {content.Bounds.Width} of {scroll.Bounds.Width}, scrollbar width {bar.Bounds.Width}, visible {bar.IsVisible}.");

            var viewportTop = content.TranslatePoint(default, surface)!.Value.Y;
            if (viewportTop < 8 || surface.Bounds.Height - viewportTop - content.Bounds.Height < 8)
                throw new InvalidOperationException($"Inline Select did not inset the scrolling content from its corners ({theme.Key}, {scale}×).");

            var origin = surface.TranslatePoint(default, window)!.Value;
            var leftCorner = Pixel(frame, origin + new Vector(5, 4), scale);
            var rightCorner = Pixel(frame, origin + new Vector(surface.Bounds.Width - 5, 4), scale);
            if (scroll.Extent.Height <= scroll.Viewport.Height || Difference(leftCorner, rightCorner) > 9)
                throw new InvalidOperationException($"Inline Select painted its scrollbar through the rounded corner ({theme.Key}, {scale}×).");

            var thumb = bar.GetVisualDescendants().OfType<Thumb>().Single();
            var start = thumb.TranslatePoint(new Rect(thumb.Bounds.Size).Center, window)!.Value;
            var endOfDrag = start + new Vector(0, 36);
            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(endOfDrag);
            using var dragged = Capture(window);
            window.MouseUp(endOfDrag, MouseButton.Left);
            if (scroll.Offset.Y <= 0 || !select.IsDropDownOpen || select.SelectedIndex != 0)
                throw new InvalidOperationException($"Dragging the Inline Select scrollbar did not scroll independently of selection ({theme.Key}, {scale}×).");

            scroll.Offset = new Vector(0, scroll.Extent.Height);
            using var end = Capture(window);
            var bottomY = surface.Bounds.Height - 5;
            var bottomLeft = Pixel(end, origin + new Vector(5, bottomY), scale);
            var bottomRight = Pixel(end, origin + new Vector(surface.Bounds.Width - 5, bottomY), scale);
            if (scroll.Offset.Y <= 0 || Difference(bottomLeft, bottomRight) > 9)
                throw new InvalidOperationException($"Inline Select overflowed the lower corner or could not be scrolled ({theme.Key}, {scale}×).");

            var list = surface.GetVisualDescendants().OfType<ListBox>().Single();
            if (list.ContainerFromIndex(list.ItemCount - 1) is not Control last ||
                last.TranslatePoint(new Point(0, last.Bounds.Height), content) is not { } bottom ||
                bottom.Y > content.Bounds.Height + 0.5)
                throw new InvalidOperationException($"The last Inline Select option did not scroll fully into view ({theme.Key}, {scale}×).");
        }
        finally { window.Close(); }
    }

    private static void VerifyAgendaCorners(ThemeVariant theme, double scale)
    {
        var agenda = new AlderAgenda
        {
            Height = 380,
            Margin = new Thickness(16),
            DisplayDate = new DateOnly(2026, 1, 14),
            BorderBrush = Brushes.Magenta,
            BorderThickness = new Thickness(3)
        };
        var window = Show(new ScrollViewer { Content = agenda }, theme, scale);
        try
        {
            foreach (var view in new[] { AlderCalendarView.Day, AlderCalendarView.Week, AlderCalendarView.Month })
            {
                agenda.View = view;
                using var frame = Capture(window);
                var origin = agenda.TranslatePoint(default, window)!.Value;
                var backdrop = Pixel(frame, origin - new Vector(2, 2), scale);
                foreach (bool bottom in new[] { false, true })
                foreach (bool right in new[] { false, true })
                {
                    // The 14 DIP corner with a 3 DIP stroke crosses the diagonal at 5.25 DIP.
                    Point AtCorner(double inset) => origin + new Vector(
                        right ? agenda.Bounds.Width - inset : inset,
                        bottom ? agenda.Bounds.Height - inset : inset);
                    if (Difference(Pixel(frame, AtCorner(5.25), scale), SKColors.Magenta) > 24 ||
                        Difference(Pixel(frame, AtCorner(2), scale), backdrop) > 4)
                        throw new InvalidOperationException(
                            $"The Agenda corner outline is obscured or content overflows ({view}, {theme.Key}, {scale}×, bottom={bottom}, right={right}).");
                }
            }
        }
        finally { window.Close(); }
    }

    private static void VerifyApprovalDismissIcon(ThemeVariant theme, double scale)
    {
        var model = new SmokeModel();
        var card = new AlderApprovalCard
        {
            Width = 240, Header = "How should users sign in?", DismissCommand = model.Activate
        };
        var window = Show(new Border { Padding = new Thickness(16), Child = card }, theme, scale);
        try
        {
            using var rest = Capture(window);
            var dismiss = card.GetVisualDescendants().OfType<AlderButton>().Single(button => button.Name == "Dismiss");
            var center = dismiss.TranslatePoint(new Rect(dismiss.Bounds.Size).Center, window)!.Value;
            var restingInk = VerifyInk(rest);
            dismiss.FontSize = 24;
            using var largerFont = Capture(window);
            if (VerifyInk(largerFont) != restingInk)
                throw new InvalidOperationException($"The Approval Card dismiss icon moved when the font size changed ({theme.Key}, {scale}×).");

            window.MouseMove(center);
            using var hover = Capture(window);
            VerifyInk(hover);

            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            if (model.Activations != 1) throw new InvalidOperationException("The Approval Card dismiss button did not execute on click.");

            PixelRect VerifyInk(SKBitmap frame)
            {
                var background = Pixel(frame, center + new Vector(-8, 0), scale);
                int left = frame.Width, top = frame.Height, right = -1, bottom = -1;
                for (int y = (int)((center.Y - 7) * scale); y < (center.Y + 7) * scale; y++)
                for (int x = (int)((center.X - 7) * scale); x < (center.X + 7) * scale; x++)
                {
                    var pixelCenter = new Point((x + .5) / scale, (y + .5) / scale);
                    if (((Vector)(pixelCenter - center)).SquaredLength > 81 || Difference(frame.GetPixel(x, y), background) < 30) continue;
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }

                if ((right - left + 1) / scale < 7 || (bottom - top + 1) / scale < 7 ||
                    Math.Abs((left + right + 1) / (2 * scale) - center.X) > .75 ||
                    Math.Abs((top + bottom + 1) / (2 * scale) - center.Y) > .75)
                    throw new InvalidOperationException($"The Approval Card dismiss icon is clipped or off-center ({theme.Key}, {scale}×).");
                return new PixelRect(left, top, right - left + 1, bottom - top + 1);
            }
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
