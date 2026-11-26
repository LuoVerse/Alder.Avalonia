using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

static class ProgramRunner
{
    [STAThread]
    public static void Main()
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Windows required");
        Program.Build();
        static void Pump(Window window)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
        }

        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var window = App.CreateDemoWindow();
            var anchor = new AlderButton { Content = "想去的州 / State" };
            var picker = new AlderEmojiPicker { IsInline = true };
            var label = new AlderTextBlock { Text = "佛罗里达州 ABC 123", FontSize = 12 };
            var popup = new Popup
            {
                PlacementTarget = anchor,
                ShouldUseOverlayLayer = false,
                Child = label
            };
            window.Content = new StackPanel { Children = { anchor, picker, popup } };
            window.RequestedThemeVariant = theme;
            window.Show();
            try
            {
                Pump(window);
                if (TextOptions.GetTextRenderingMode(window) != TextRenderingMode.Antialias)
                    throw new InvalidOperationException("Window did not opt out of subpixel rendering");
                popup.IsOpen = true;
                Pump(window);
                if (TopLevel.GetTopLevel(label) is not PopupRoot popupRoot ||
                    TextOptions.GetTextRenderingMode(popupRoot) != TextRenderingMode.Antialias)
                    throw new InvalidOperationException("Native popup did not opt out of subpixel rendering");
                var strokes = new FormattedText(label.Text, System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),
                    FlowDirection.LeftToRight, new Typeface(label.FontFamily), label.FontSize, Brushes.White);
                if (strokes.Width <= 0) throw new InvalidOperationException("Missing mixed script glyphs");
                var glyphs = picker.GetVisualDescendants().OfType<AlderTextBlock>()
                    .Where(x => x.FontSize >= 20 && x.Text?.Length > 0).ToArray();
                if (glyphs.Length < 3 || glyphs.Any(x => x.ClipToBounds))
                    throw new InvalidOperationException("Emoji grid glyphs still clipped");
                Console.WriteLine($"{theme.Key}, {window.RenderScaling}x: native Win32 window and popup use grayscale text, {glyphs.Length} unclipped emoji glyphs.");
                popup.IsOpen = false;
            }
            finally { window.Close(); }
        }
    }
}

static class Program
{
    public static void Build() =>
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().SetupWithoutStarting();
}
