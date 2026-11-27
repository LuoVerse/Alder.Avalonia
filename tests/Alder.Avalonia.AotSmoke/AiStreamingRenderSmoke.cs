using System.Collections.Immutable;
using System.Diagnostics;
using Alder.Avalonia.Controls;
using Alder.NativeMarkdown.Core.Highlighting;
using Alder.NativeMarkdown.Core.Streaming;
using Alder.NativeMarkdown.UI;
using Alder.NativeMarkdown.UI.Extensions;
using Alder.NativeMarkdown.UI.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;

namespace Alder.Avalonia.AotSmoke;

/// <summary>Samples the same visible pixels before and during asynchronous syntax colouring.</summary>
internal static class AiStreamingRenderSmoke
{
    public static void Run()
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            foreach (bool fenced in new[] { false, true })
                Verify(theme, fenced);
            VerifyCodeRunRendering(theme);
            VerifyCodeStripTransition(theme);
        }
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (double scale in new[] { 1d, 2d })
            {
                CheckSurfaces(theme, scale);
                CheckChatList(theme, scale);
                CheckCitationPreview(theme, scale);
                CheckSplitMarkdownMarkers(theme, scale);
            }
        VerifyMotion();
        VerifyChatLoading();
        MeasureBursts(false);
        MeasureBursts(true);
    }

    private static void VerifyCodeStripTransition(ThemeVariant theme)
    {
        const int scale = 2;
        const string line = "const answer = 42;";
        var shortCode = string.Join('\n', Enumerable.Repeat(line, 16));
        var view = new AlderCodeBlock { Code = shortCode, Language = "text" };
        var window = new AlderTestWindow { Width = 390, Height = 240, Content = view, RequestedThemeVariant = theme };
        AlderMotion.SetReduced(window, true);
        window.Show();
        window.SetRenderScaling(scale);
        try
        {
            view.ApplyLatest();
            Pump(window);
            var code = view.GetVisualDescendants().OfType<CodeTextView>().Single();
            var origin = code.TranslatePoint(default, window)!.Value;
            using var initial = Capture(window);
            CheckStripCount(0);

            view.Code += "\n" + line;
            Pump(window);
            CheckStripCount(2);
            using (var split = Capture(window))
                CheckPrefix(split);

            view.Code = shortCode;
            Pump(window);
            CheckStripCount(0);
            using var shortAgain = Capture(window);
            CheckPrefix(shortAgain);

            void CheckStripCount(int expected)
            {
                if (code.StripCount != expected || code.TranslatePoint(default, window) != origin)
                    throw new InvalidOperationException($"The code view did not preserve its origin when switching to {expected} strips.");
            }

            void CheckPrefix(SKBitmap current)
            {
                var x = (int)(origin.X * scale);
                var y = (int)(origin.Y * scale);
                for (var row = y; row < y + 64; row++)
                    for (var column = x; column < x + 320; column++)
                        if (initial.GetPixel(column, row) != current.GetPixel(column, row))
                            throw new InvalidOperationException($"Code pixels shifted when switching strips in {theme.Key}.");
            }
        }
        finally { window.Close(); }
    }

    private static void VerifyCodeRunRendering(ThemeVariant theme)
    {
        const string source = "\tconst value = { size: 42, label: \"你好 e\u0301 👩‍💻\" };";
        var lexer = new GatedHighlighter { Hold = true };
        var view = new AlderCodeBlock { Code = source, Language = "ts" };
        MarkdownServices.SetServices(view, new MarkdownServices { Highlighter = lexer });
        var window = new AlderTestWindow { Width = 240, Height = 120, Content = view, RequestedThemeVariant = theme };
        AlderMotion.SetReduced(window, true);
        window.Show();
        window.SetRenderScaling(2);
        try
        {
            view.ApplyLatest();
            var block = view.GetVisualDescendants().OfType<CodeBlockView>().Single();
            var code = view.GetVisualDescendants().OfType<CodeTextView>().Single();
            block.Wrap = true;
            Pump(window);
            var originalBounds = code.Bounds;
            var origin = code.TranslatePoint(default, window)!.Value;
            using var plain = Capture(window);
            lexer.Release.Set();
            var timeout = Stopwatch.StartNew();
            while (code.State is { Tokens.Count: 0 } && timeout.Elapsed < TimeSpan.FromSeconds(5))
            {
                Pump(window);
                Thread.Sleep(2);
            }
            if (code.State is not { Tokens.Count: > 0 } state)
                throw new InvalidOperationException("The wrapped code did not receive syntax colors.");
            Pump(window);
            using var colored = Capture(window);
            if (code.Bounds != originalBounds || code.VisibleLineText(0) != source)
                throw new InvalidOperationException($"Syntax coloring changed wrapped code bounds or source in {theme.Key}.");

            var left = (int)(origin.X * 2);
            var top = (int)(origin.Y * 2);
            var right = Math.Min(plain.Width, left + (int)(code.Bounds.Width * 2));
            var bottom = Math.Min(plain.Height, top + (int)(code.Bounds.Height * 2));
            var renderedRoles = new HashSet<uint>();
            for (var y = top; y < bottom; y++)
                for (var x = left; x < right; x++)
                    if (plain.GetPixel(x, y) != colored.GetPixel(x, y))
                        renderedRoles.Add((uint)colored.GetPixel(x, y));
            var paintedRoles = state.Tokens[0].Select(token => token.Foreground).Distinct().Count(renderedRoles.Contains);
            if (paintedRoles < 3)
                throw new InvalidOperationException($"Expected visibly distinct syntax colors in {theme.Key}, painted {paintedRoles}.");
        }
        finally
        {
            lexer.Release.Set();
            window.Close();
        }
    }

    private static void Verify(ThemeVariant theme, bool fenced)
    {
        const string first = "const answer = 4";
        var lexer = new GatedHighlighter();
        var document = new StreamingDocument();
        var literal = new AlderCodeBlock { Code = first, Language = "ts" };
        var markdown = new AlderMarkdown { Document = document };
        var view = fenced ? (Alder.NativeMarkdown.UI.MarkdownView)markdown : literal;
        MarkdownServices.SetServices(view, new MarkdownServices { Highlighter = lexer });
        document.Append("# Stable heading\n\nCompleted paragraph.\n\n```ts\n" + first);
        document.WhenIdle().GetAwaiter().GetResult();
        var window = new AlderTestWindow { Width = 500, Height = 300, Content = view, RequestedThemeVariant = theme };
        AlderMotion.SetReduced(window, true);
        window.Show();
        try
        {
            view.ApplyLatest();
            Pump(window);
            var code = view.GetVisualDescendants().OfType<CodeTextView>().Single();
            var state = code.State!;
            var timeout = Stopwatch.StartNew();
            while (state.Tokens.Count == 0 && timeout.Elapsed < TimeSpan.FromSeconds(5))
            {
                Pump(window);
                Thread.Sleep(2);
            }
            if (state.Tokens.Count == 0) throw new InvalidOperationException("Initial highlighting did not arrive.");
            var pos = code.TranslatePoint(default, window)!.Value;
            var start = new PixelPoint((int)Math.Ceiling(pos.X), (int)Math.Ceiling(pos.Y));
            using var before = Capture(window);
            lexer.Hold = true;
            try
            {
                if (fenced)
                {
                    document.Append("2;");
                    document.WhenIdle().GetAwaiter().GetResult();
                }
                else literal.Code += "2;";
                view.ApplyLatest();
                Pump(window);
                using var pending = Capture(window);
                CheckPrefix(pending);
            }
            finally { lexer.Release.Set(); }
            if (fenced)
            {
                document.Append("\n```");
                document.Complete();
                document.WhenIdle().GetAwaiter().GetResult();
                view.ApplyLatest();
            }
            timeout.Restart();
            while (!state.TokensCode.StartsWith(first + "2;", StringComparison.Ordinal) && timeout.Elapsed < TimeSpan.FromSeconds(5))
            {
                Pump(window);
                Thread.Sleep(2);
            }
            if (!state.TokensCode.StartsWith(first + "2;", StringComparison.Ordinal))
                throw new InvalidOperationException("Final highlighting did not arrive.");
            Pump(window);
            using var settled = Capture(window);
            CheckPrefix(settled);

            void CheckPrefix(SKBitmap frame)
            {
                for (int y = fenced ? 0 : start.Y; y < start.Y + 18; y++)
                    for (int x = y < start.Y ? 0 : start.X; x < (y < start.Y ? 490 : start.X + 34); x++)
                        if (before.GetPixel(x, y) != frame.GetPixel(x, y))
                            throw new InvalidOperationException($"Stable pixels changed during streaming ({theme.Key}, fenced={fenced}, {x},{y}).");
            }

        }
        finally { window.Close(); }
    }

    private static void CheckSplitMarkdownMarkers(ThemeVariant theme, double scale)
    {
        var document = new StreamingDocument();
        var markdown = new AlderMarkdown { Document = document };
        markdown.Resources[MarkdownResourceKeys.Typography] = MarkdownTypography.Chat;
        // The separate highlighting check covers asynchronous colour updates.
        MarkdownServices.SetServices(markdown, new MarkdownServices());
        var window = new AlderTestWindow { Width = 218, Height = 420, Content = markdown, RequestedThemeVariant = theme };
        AlderMotion.SetReduced(window, true);
        window.Show();
        window.SetRenderScaling(scale);
        try
        {
            Append("9. Keep this completed line in place.");
            SameFrames("ordered marker", "\n1", "0");
            Append(". A parent with wrapping text near the right edge.");
            SameFrames("nested marker", "\n    -", " ");
            Append("Nested item");
            SameFrames("opening fence", "\n\n`", "`", "`t", "s");
            Append("\nconst content = \"" + new string('x', 70) + "\";");
            SameFrames("closing fence", "\n`", "`", "`", "\n");
        }
        finally { window.Close(); }

        void Append(string chunk)
        {
            document.Append(chunk);
            document.WhenIdle().GetAwaiter().GetResult();
            markdown.ApplyLatest();
            Pump(window);
        }

        void SameFrames(string marker, params string[] chunks)
        {
            using var before = Capture(window);
            foreach (var chunk in chunks)
            {
                Append(chunk);
                using var current = Capture(window);
                if (Different(before, current, 0, before.Height))
                    throw new InvalidOperationException($"A split {marker} changed the rendered Markdown ({theme.Key}, {scale}x).");
            }
        }
    }

    private static void VerifyMotion()
    {
        var loader = new AlderLoader { Variant = AiLoaderVariant.Dots, Width = 44, Height = 28 };
        var shimmer = new AlderTextShimmer { Text = "Generating response…", FontSize = 20 };
        var window = new AlderTestWindow { Width = 260, Height = 100, Content = new StackPanel { Children = { loader, shimmer } } };
        window.Show();
        try
        {
            AlderMotion.SetReduced(window, false);
            bool changedLoader = false, changedShimmer = false;
            using var first = Capture(window);
            for (int i = 0; i < 24 && !(changedLoader && changedShimmer); i++)
            {
                Thread.Sleep(35);
                Pump(window);
                using var next = Capture(window);
                changedLoader |= Different(first, next, 0, 28);
                changedShimmer |= Different(first, next, 28, 58);
            }
            if (!changedLoader || !changedShimmer) throw new InvalidOperationException($"AI motion did not advance: loader={changedLoader}, shimmer={changedShimmer}.");
            AlderMotion.SetReduced(window, true);
            Pump(window);
            using var rest = Capture(window);
            Thread.Sleep(50);
            Pump(window);
            using var later = Capture(window);
            if (Different(rest, later, 0, 58)) throw new InvalidOperationException("Reduced motion continued changing pixels.");
        }
        finally { window.Close(); }

    }

    private static bool Different(SKBitmap a, SKBitmap b, int top, int bottom)
    {
        for (int y = top; y < bottom; y++)
            for (int x = 0; x < a.Width; x++)
                if (a.GetPixel(x, y) != b.GetPixel(x, y)) return true;
        return false;
    }

    private static void VerifyChatLoading()
    {
        var dots = new AlderChatLoader { Variant = AiLoaderVariant.Dots, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left };
        var pulse = new AlderChatLoader { Variant = AiLoaderVariant.Pulse, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left };
        var ring = new AlderChatLoader { Variant = AiLoaderVariant.Ring, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left };
        var skeleton = new AlderChatSkeleton();
        var window = new AlderTestWindow { Width = 300, Height = 180, Content = new StackPanel { Margin = new Thickness(12), Spacing = 12,
            Children = { dots, pulse, ring, skeleton } } };
        window.Show();
        try
        {
            Pump(window);
            if (dots.Bounds.Size != new Size(26, 6) || pulse.Bounds.Size != new Size(8, 8) ||
                ring.Bounds.Size != new Size(24, 24) || skeleton.Bounds.Height != 52)
                throw new InvalidOperationException("Incorrect chat loading indicator sizes.");

            using var first = Capture(window);
            var indicators = new[] { dots, pulse, ring };
            var advanced = new bool[indicators.Length];
            for (int i = 0; i < 18 && !advanced.All(changed => changed); i++)
            {
                Thread.Sleep(35);
                Pump(window);
                using var next = Capture(window);
                if (Changed(skeleton, first, next)) throw new InvalidOperationException("The chat skeleton should remain still.");
                for (int j = 0; j < indicators.Length; j++)
                    advanced[j] |= Changed(indicators[j], first, next);
            }
            if (!advanced.All(changed => changed))
                throw new InvalidOperationException($"Chat loader animations did not advance: {string.Join(", ", advanced)}.");

            AlderMotion.SetReduced(window, true);
            Pump(window);
            using var rest = Capture(window);
            Thread.Sleep(90);
            Pump(window);
            using var later = Capture(window);
            if (Different(rest, later, 0, rest.Height)) throw new InvalidOperationException("Chat loading still animated with reduced motion.");

            bool Changed(Control part, SKBitmap a, SKBitmap b)
            {
                double top = part.TranslatePoint(default, window)!.Value.Y;
                return Different(a, b, (int)top, (int)(top + part.Bounds.Height));
            }
        }
        finally { window.Close(); }
    }

    private static void CheckCitationPreview(ThemeVariant theme, double scale)
    {
        var hover = new AlderHoverCard
        {
            Trigger = new AlderChatSource { Content = "Documentation" },
            Content = new Border { Width = 288, Height = 46 },
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right
        };
        var window = new AlderTestWindow { Width = 390, Height = 220, Content = new Border { Padding = new Thickness(26), Child = hover },
            Background = theme == ThemeVariant.Light ? Brushes.WhiteSmoke : Brushes.Black, RequestedThemeVariant = theme };
        AlderMotion.SetReduced(window, true);
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            if (!window.TryFindResource("AlderChatSourcePreviewTheme", out var resource) || resource is not ControlTheme surfaceTheme)
                throw new InvalidOperationException("Citation preview theme is unavailable.");
            hover.Theme = surfaceTheme;
            Pump(window);
            using var before = Capture(window);
            hover.IsOpen = true;
            Pump(window);
            using var after = Capture(window);
            var surface = window.GetVisualDescendants().OfType<Panel>().Single(panel => panel.Name == "AlderHoverCardSurface");
            var edge = surface.TranslatePoint(new Point(0, surface.Bounds.Height / 2), window)!.Value;
            if (surface.Bounds.Width != 320 || edge.X < 12 || edge.X + surface.Bounds.Width > window.ClientSize.Width - 12)
                throw new InvalidOperationException($"The citation preview lost its edge clearance ({theme.Key}/{scale}x).");
            int x = (int)((edge.X - 3) * scale), y = (int)(edge.Y * scale);
            if (theme == ThemeVariant.Light && after.GetPixel(x, y).Red >= before.GetPixel(x, y).Red)
                throw new InvalidOperationException($"The citation overlay shadow was clipped ({scale}x).");
        }
        finally { window.Close(); }
    }

    private static void CheckChatList(ThemeVariant theme, double scale)
    {
        var first = new AlderListViewItem { Title = "First thread", Description = "Latest reply", Action = "2h" };
        var last = new AlderListViewItem { Title = "Last thread", Description = "Another reply", Action = "Yesterday" };
        var chat = new AlderChatListView { SelectionEnabled = false, Items = { first, last } };
        var window = new AlderTestWindow { Width = 390, Height = 170, RequestedThemeVariant = theme,
            Background = theme == ThemeVariant.Light ? Brushes.WhiteSmoke : Brushes.Black,
            Content = new Border { Padding = new Thickness(11), Child = chat } };
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            Pump(window);
            using var rest = Capture(window);
            var origin = first.TranslatePoint(default, window)!.Value;
            var finalOrigin = last.TranslatePoint(default, window)!.Value;
            var edge = origin + new Vector(3, 4);
            var divider = origin + new Vector(3, first.Bounds.Height - .5);
            var bottom = finalOrigin + new Vector(3, last.Bounds.Height - .5);
            if (At(rest, divider) == At(rest, edge) || At(rest, bottom) != At(rest, edge))
                throw new InvalidOperationException($"Chat list did not paint only the interior divider ({theme.Key}/{scale}x).");

            window.MouseMove(edge);
            Pump(window);
            using var over = Capture(window);
            if (At(over, edge) == At(rest, edge) || At(over, bottom) != At(rest, bottom))
                throw new InvalidOperationException($"Chat row hover changed pixels outside the hovered row ({theme.Key}/{scale}x).");

            SKColor At(SKBitmap bitmap, Point point) => bitmap.GetPixel((int)(point.X * scale), (int)(point.Y * scale));
        }
        finally { window.Close(); }
    }

    // Exterior pixels distinguish a real drop shadow from an in-bounds border with similar contrast.
    private static void CheckSurfaces(ThemeVariant theme, double scale)
    {
        var approval = new AlderApprovalCard { Header = "Question?", Content = "Answer" };
        var message = new AlderChatMessage { IsCard = true, Content = "Assistant response" };
        var stack = new StackPanel { Spacing = 22, Margin = new Thickness(20), Children = { approval, message } };
        var window = new AlderTestWindow { Width = scale == 1 ? 540 : 390, Height = 330, Content = stack, RequestedThemeVariant = theme,
            Background = theme == ThemeVariant.Light ? global::Avalonia.Media.Brushes.WhiteSmoke : global::Avalonia.Media.Brushes.Black };
        AlderMotion.SetReduced(window, true);
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            Pump(window);
            var surfaces = stack.GetVisualDescendants().OfType<Border>().Where(b => b.Name is "ApprovalSurface" or "MessageCard").ToArray();
            if (surfaces.Length != 2) throw new InvalidOperationException("AI card surface missing.");
            using var painted = Capture(window);
            foreach (var surface in surfaces) surface.BoxShadow = default;
            Pump(window);
            using var flat = Capture(window);
            foreach (var surface in surfaces)
            {
                var bottom = surface.TranslatePoint(new Point(surface.Bounds.Width / 2, surface.Bounds.Height), window)!.Value;
                int x = (int)(bottom.X * scale), y = (int)Math.Ceiling(bottom.Y * scale);
                int difference = 0;
                for (int dy = 0; dy < 4 * scale; dy++)
                    difference = Math.Max(difference, flat.GetPixel(x, y + dy).Red - painted.GetPixel(x, y + dy).Red);
                if ((theme == ThemeVariant.Light) != (difference > 0))
                    throw new InvalidOperationException($"Unexpected exterior shadow on {surface.Name}, {theme.Key}, {scale}x, RGB difference {difference}.");
            }
        }
        finally { window.Close(); }
    }

    // Include document publication, UI layout and a real Skia frame. Keep image encoding out of the timing.
    private static void MeasureBursts(bool fenced)
    {
        string source = string.Join('\n', Enumerable.Range(0, 400).Select(i => $"const value{i} = {i};"));
        var document = new StreamingDocument();
        var literal = new AlderCodeBlock { Code = source, Language = "ts" };
        var markdown = new AlderMarkdown { Document = document };
        var view = fenced ? (Alder.NativeMarkdown.UI.MarkdownView)markdown : literal;
        if (fenced) document.Append("# Heading\n\n```ts\n" + source);
        document.WhenIdle().GetAwaiter().GetResult();
        var scroll = new AlderScrollViewer { Content = view };
        var window = new AlderTestWindow { Width = 600, Height = 360, Content = scroll };
        AlderMotion.SetReduced(window, true);
        window.Show();
        try
        {
            List<double> times = [];
            var tail = new System.Text.StringBuilder();
            for (int frame = 0; frame < 80; frame++)
            {
                long start = Stopwatch.GetTimestamp();
                for (int token = 0; token < 8; token++)
                {
                    string delta = token == 0 ? $"\nconst item{frame}" : token == 1 ? " = " : "7";
                    tail.Append(delta);
                    if (fenced) document.Append(delta);
                    else literal.Code += delta;
                }
                document.WhenIdle().GetAwaiter().GetResult();
                view.ApplyLatest();
                scroll.ScrollToEnd();
                Pump(window);
                using var image = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Missing burst frame.");
                double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                if (frame >= 8) times.Add(elapsed);
            }
            if (fenced)
            {
                document.Append("\n```");
                document.Complete();
                document.WhenIdle().GetAwaiter().GetResult();
                view.ApplyLatest();
                var block = (Alder.NativeMarkdown.Core.Model.CodeBlock)document.Latest[document.Latest.Count - 1];
                if (block.Code.TrimEnd('\n') != source + tail) throw new InvalidOperationException("Burst output changed on completion.");
            }
            else if (literal.Code != source + tail) throw new InvalidOperationException("Standalone burst output changed.");
            times.Sort();
            Console.WriteLine($"AI stream, {(fenced ? "Markdown fence" : "standalone code")}, 400 initial lines, 8 appends/frame, {times.Count} measured Skia frames: P50 {times[times.Count / 2]:F2} ms, P95 {times[(int)Math.Ceiling(times.Count * .95) - 1]:F2} ms, max {times[^1]:F2} ms.");
        }
        finally { window.Close(); }
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static SKBitmap Capture(Window window)
    {
        using var frame = window.CaptureRenderedFrame()!;
        using var png = new MemoryStream();
        frame.Save(png, PngBitmapEncoderOptions.Default);
        png.Position = 0;
        return SKBitmap.Decode(png);
    }

    private sealed class GatedHighlighter : ISyntaxHighlighter
    {
        private readonly AlderCodeHighlighter inner = new();
        public volatile bool Hold;
        public readonly ManualResetEventSlim Release = new();
        public bool SupportsLanguage(string? language) => inner.SupportsLanguage(language);
        public (uint Foreground, uint Background) GetThemeColors(HighlightThemeKind theme) => inner.GetThemeColors(theme);
        public IHighlightSession CreateSession(string language, HighlightThemeKind theme)
        {
            if (Dispatcher.UIThread.CheckAccess())
                throw new InvalidOperationException("Syntax session initialization ran on the UI thread.");
            return new Session(this, inner.CreateSession(language, theme));
        }

        private sealed class Session(GatedHighlighter owner, IHighlightSession inner) : IHighlightSession
        {
            public IReadOnlyList<ImmutableArray<HighlightToken>> Update(string code)
            {
                if (owner.Hold) owner.Release.Wait(TimeSpan.FromSeconds(5));
                return inner.Update(code);
            }

            public void Dispose() => inner.Dispose();
        }
    }
}
