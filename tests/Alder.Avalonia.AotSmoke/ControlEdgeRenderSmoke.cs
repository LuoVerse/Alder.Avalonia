using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
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
            foreach (double scale in new[] { 1d, 2d })
            {
                VerifyIndicators(theme, scale);
                VerifyFileTreeArrow(theme, scale);
                VerifyCells(theme, scale);
                VerifyInlineSelectScrollBar(theme, scale);
            }
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
