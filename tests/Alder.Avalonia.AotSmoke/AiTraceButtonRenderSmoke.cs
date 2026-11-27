using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;

namespace Alder.Avalonia.AotSmoke;

/// <summary>Checks AI disclosure, composer focus and model picker visuals as rendered pixels.</summary>
internal static class AiTraceButtonRenderSmoke
{
    public static void Run()
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (double scale in new[] { 1d, 2d })
            {
                Verify(theme, scale);
                VerifyPromptEditor(theme, scale);
                VerifyModelPopup(theme, scale);
            }
    }

    private static void Verify(ThemeVariant theme, double scale)
    {
        var trace = new AlderChainOfThought { Header = "Checks finished", Content = "No errors", IsExpanded = true, Status = AiRunStatus.Completed };
        var viewport = new AlderScrollViewer { Content = new StackPanel { Margin = new Thickness(12), Children = { trace } } };
        var window = new AlderTestWindow { Width = scale == 1 ? 480 : 390, Height = 120, Content = viewport, RequestedThemeVariant = theme };
        AlderMotion.SetReduced(window, true);
        window.Show();
        try
        {
            if (!window.TryFindResource("AlderIcon16Theme", out var iconTheme))
                throw new InvalidOperationException("The Gallery icon theme was unavailable.");
            window.Styles.Add(new Style(s => s.OfType<AlderToggleButton>().Descendant().OfType<PathIcon>())
            {
                Setters = { new Setter(StyledElement.ThemeProperty, iconTheme) }
            });
            window.SetRenderScaling(scale);
            using var initial = Capture(window);
            var trigger = trace.GetVisualDescendants().OfType<ToggleButton>().Single();
            var indicator = trigger.GetVisualDescendants().OfType<PathIcon>().Single(icon => icon.Name == "PART_Indicator");
            var center = indicator.TranslatePoint(new Rect(indicator.Bounds.Size).Center, window)!.Value;
            CheckArms(initial, center, scale, theme, expanded: true);

            var origin = trigger.TranslatePoint(default, window)!.Value;
            var pointer = trigger.TranslatePoint(new Rect(trigger.Bounds.Size).Center, window)!.Value;
            var edge = origin + new Vector(1.5, trigger.Bounds.Height / 2);
            var corner = origin + new Vector(1.5, 1.5);
            window.MouseMove(pointer);
            using var hover = Capture(window);
            if (Pixel(hover, edge, scale) == Pixel(initial, edge, scale) ||
                Pixel(hover, corner, scale) != Pixel(initial, corner, scale))
                throw new InvalidOperationException($"The trace hover fill or rounded corner did not render ({theme.Key}/{scale}x).");

            window.MouseDown(pointer, MouseButton.Left);
            using var held = Capture(window);
            if (Pixel(held, edge, scale) != Pixel(hover, edge, scale))
                throw new InvalidOperationException($"The trace surface moved while pressed ({theme.Key}/{scale}x).");

            window.MouseUp(pointer, MouseButton.Left);
            using var collapsed = Capture(window);
            if (trace.IsExpanded) throw new InvalidOperationException("The disclosure did not collapse on release.");
            CheckArms(collapsed, center, scale, theme, expanded: false);

            window.MouseMove(default);
            viewport.Focusable = true;
            viewport.Focus();
            trigger.Focus(NavigationMethod.Tab);
            using var focused = Capture(window);
            var focusEdge = origin + new Vector(-1, trigger.Bounds.Height / 2);
            if (Pixel(focused, edge, scale) != Pixel(hover, edge, scale) ||
                Pixel(focused, focusEdge, scale) != Pixel(initial, focusEdge, scale))
                throw new InvalidOperationException($"The trace keyboard focus did not use the hover fill within the button ({theme.Key}/{scale}x).");
        }
        finally { window.Close(); }
    }

    private static void VerifyPromptEditor(ThemeVariant theme, double scale)
    {
        var prompt = new AlderPromptInput { Width = 320, Placeholder = "Ask about your files" };
        var window = new AlderTestWindow { Width = 390, Height = 150, Content = prompt, RequestedThemeVariant = theme };
        AlderMotion.SetReduced(window, true);
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            using var beforeFocus = Capture(window);
            var editor = prompt.GetVisualDescendants().OfType<TextBox>().Single();
            var origin = editor.TranslatePoint(default, window)!.Value;
            var size = editor.Bounds.Size;
            editor.Focus(NavigationMethod.Tab);
            using var focused = Capture(window);
            if (!editor.IsFocused) throw new InvalidOperationException("The AI prompt editor did not receive keyboard focus.");

            // Only new blue pixels count. The native caret and text selection may legitimately change with focus.
            for (int y = Math.Max(0, (int)((origin.Y - 3) * scale)); y < Math.Min(focused.Height, (origin.Y + size.Height + 3) * scale); y++)
            {
                for (int x = Math.Max(0, (int)((origin.X - 3) * scale)); x < Math.Min(focused.Width, (origin.X + size.Width + 3) * scale); x++)
                {
                    var pixel = focused.GetPixel(x, y);
                    if (pixel.Blue > pixel.Red + 70 && pixel.Blue > pixel.Green + 25 && pixel != beforeFocus.GetPixel(x, y))
                        throw new InvalidOperationException($"The AI prompt rendered a blue focus edge ({theme.Key}/{scale}x) at {x},{y}.");
                }
            }
            window.KeyTextInput("hello");
            using var typed = Capture(window);
            if (prompt.Text != "hello") throw new InvalidOperationException("The AI prompt editor stopped accepting text.");
        }
        finally { window.Close(); }
    }

    private static void VerifyModelPopup(ThemeVariant theme, double scale)
    {
        var fast = new AlderPickerOption { Text = "Fast model", Image = Icon(Brushes.OrangeRed) };
        var reasoning = new AlderPickerOption { Text = "Reasoning model", ShortText = "Reasoning", Image = Icon(Brushes.DodgerBlue) };
        var model = new AlderModelSelector
        {
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left,
            ItemsSource = new[] { fast, reasoning }, SelectedModel = fast,
            Efforts = new[] { "Low", "High" }, SelectedEffort = "High"
        };
        var window = new AlderTestWindow
        {
            Width = 390, Height = 600, RequestedThemeVariant = theme,
            Content = new Border { Padding = new Thickness(8, 96, 8, 0), Child = model },
            Background = theme == ThemeVariant.Light ? Brushes.WhiteSmoke : new SolidColorBrush(Color.Parse("#0A0A0B"))
        };
        AlderMotion.SetReduced(window, true);
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            using var before = Capture(window);
            var picker = model.GetVisualDescendants().OfType<AlderSelect>().Single();
            var trigger = picker.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_Trigger");
            var origin = trigger.TranslatePoint(default, window)!.Value;
            var center = origin + new Vector(trigger.Bounds.Width / 2, trigger.Bounds.Height / 2);
            var top = new Point(center.X, origin.Y + 2);
            var corner = origin + new Vector(2, 2);
            if (Pixel(before, top, scale) != Pixel(before, origin + new Vector(0, -3), scale))
                throw new InvalidOperationException("The model trigger painted a background at rest.");
            window.MouseMove(center);
            using var hovered = Capture(window);
            if (Pixel(hovered, top, scale) == Pixel(before, top, scale) ||
                Pixel(hovered, corner, scale) != Pixel(before, corner, scale) || trigger.CornerRadius.TopLeft < 18)
                throw new InvalidOperationException($"The model trigger did not paint a hover pill ({theme.Key}/{scale}x).");

            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            window.MouseMove(new Point(2, 2));
            using var after = Capture(window);
            if (!picker.IsDropDownOpen || Pixel(after, top, scale) != Pixel(hovered, top, scale))
                throw new InvalidOperationException("The model trigger did not keep its fill while the popup was open.");
            var footer = (Control)picker.DropDownFooter!;
            var surface = footer.GetVisualAncestors().OfType<Panel>().Single(panel => panel.Name == "AlderPopoverSurface");
            var left = surface.TranslatePoint(new Point(0, surface.Bounds.Height / 2), window)!.Value;
            if (left.X < 15 || left.X + surface.Bounds.Width > window.ClientSize.Width - 15)
                throw new InvalidOperationException($"The model popup lost its shadow gutter ({theme.Key}/{scale}x).");
            var innerEdge = left + new Vector(3, 0);
            if (Pixel(after, innerEdge, scale) == Pixel(before, innerEdge, scale))
                throw new InvalidOperationException($"The model popup surface did not paint up to the edge ({theme.Key}/{scale}x).");
            var shadow = left + new Vector(-3, 0);
            if (theme == ThemeVariant.Light && Pixel(after, shadow, scale).Red >= Pixel(before, shadow, scale).Red)
                throw new InvalidOperationException("The light model popup shadow was clipped.");

            var row = surface.GetVisualDescendants().OfType<TextBlock>()
                .Single(text => text.IsEffectivelyVisible && text.Text == reasoning.Text);
            var choice = row.TranslatePoint(new Rect(row.Bounds.Size).Center, window)!.Value;
            window.MouseMove(choice);
            window.MouseDown(choice, MouseButton.Left);
            window.MouseUp(choice, MouseButton.Left);
            using var selected = Capture(window);
            var image = trigger.GetVisualDescendants().OfType<Image>().Single(icon => icon.Name == "ModelTriggerIcon");
            var imageCenter = image.TranslatePoint(new Rect(image.Bounds.Size).Center, window)!.Value;
            if (picker.IsDropDownOpen || !ReferenceEquals(model.SelectedModel, reasoning) ||
                !ReferenceEquals(image.Source, reasoning.Image) || !image.IsEffectivelyVisible ||
                Pixel(selected, imageCenter, scale).Blue < 200)
                throw new InvalidOperationException($"The selected model icon did not render in the trigger ({theme.Key}/{scale}x).");
        }
        finally { window.Close(); }

        static DrawingImage Icon(IBrush brush) => new()
        {
            Drawing = new GeometryDrawing { Geometry = new RectangleGeometry(new Rect(0, 0, 12, 12)), Brush = brush }
        };
    }

    private static void CheckArms(SKBitmap bitmap, Point center, double scale, ThemeVariant theme, bool expanded)
    {
        // Sample both arms outside the center joint. A correctly sized icon preserves both arms after rotation.
        var surface = Pixel(bitmap, center + new Vector(0, -6), scale);
        var left = 0;
        var right = 0;
        for (int y = (int)((center.Y - 5) * scale); y < (center.Y + 5) * scale; y++)
        {
            for (int x = (int)((center.X - 6) * scale); x < (center.X + 6) * scale; x++)
            {
                if (Math.Abs(bitmap.GetPixel(x, y).Red - surface.Red) < 45) continue;
                double dx = (x + .5) / scale - center.X;
                if (dx < -1.5) left++;
                if (dx > 1.5) right++;
            }
        }
        if (left == 0 || right == 0 || Math.Min(left, right) * 2 < Math.Max(left, right))
            throw new InvalidOperationException($"Chevron clipping in {theme.Key}/{scale}x, expanded={expanded}, visible pixels left/right={left}/{right}.");
    }

    private static SKColor Pixel(SKBitmap bitmap, Point point, double scale) =>
        bitmap.GetPixel((int)(point.X * scale), (int)(point.Y * scale));

    private static SKBitmap Capture(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame()!;
        using var png = new MemoryStream();
        frame.Save(png, PngBitmapEncoderOptions.Default);
        png.Position = 0;
        return SKBitmap.Decode(png);
    }
}
