using System.Collections.ObjectModel;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;

namespace Alder.Avalonia.AotSmoke;

internal static class CompositionUpgradeSmoke
{
    public static void Run()
    {
        VerifyLoader(ThemeVariant.Light, 1);
        VerifyLoader(ThemeVariant.Dark, 2);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        foreach (double scale in new[] { 1d, 1.5d, 2d })
            VerifyCompactLoadingRings(theme, scale);
        VerifyActionBar(ThemeVariant.Light, 1, 480);
        VerifyActionBar(ThemeVariant.Dark, 2, 390);
        VerifyUserMenuChevron();
        VerifyImageViewSwitch(ThemeVariant.Light, 1);
        VerifyImageViewSwitch(ThemeVariant.Dark, 2);
        VerifyImageViewTransitionOptions();
        VerifyRipple(ThemeVariant.Light, 1);
        VerifyRipple(ThemeVariant.Dark, 2);
        VerifyDataGridLanding(ThemeVariant.Light, 1);
        VerifyDataGridLanding(ThemeVariant.Dark, 2);
    }

    private static void VerifyUserMenuChevron()
    {
        const double scale = 2;
        var menu = new AlderUserMenu
        {
            Title = "Account", Width = 168, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, Items = { new MenuItem { Header = "Profile" } }
        };
        var window = new AlderTestWindow { Width = 380, Height = 300, Content = menu, RequestedThemeVariant = ThemeVariant.Dark };
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            using (Capture(window)) { }
            var icon = menu.GetVisualDescendants().OfType<AlderPathIcon>().Single(part => part.Name == "TriggerChevronIcon");
            var pivot = icon.TranslatePoint(new Rect(icon.Bounds.Size).Center, window)!.Value;
            var ink = ((ISolidColorBrush)icon.Foreground!).Color;
            Check(ReadDirection().Y > 1, "the closed UserMenu chevron points down");

            menu.Open();
            Check(HasRotatedFrame(), "the UserMenu renders an intermediate rotation on opening");
            Wait(170);
            Check(ReadDirection().Y < -1, "the open UserMenu chevron points up");

            menu.Close();
            Check(HasRotatedFrame(), "the UserMenu renders an intermediate rotation on dismissal");
            Wait(170);
            Check(ReadDirection().Y > 1, "the dismissed UserMenu chevron points down");

            menu.Open();
            Check(HasRotatedFrame(), "the UserMenu starts rotating on a repeated opening");
            menu.Close();
            Wait(170);
            Check(ReadDirection().Y > 1, "interrupting the opening returns the chevron to the closed direction");

            menu.Open();
            Check(HasRotatedFrame(), "the UserMenu starts rotating before reduced motion is enabled");
            AlderMotion.SetReduced(menu, true);
            Check(ReadDirection().Y < -1, "enabling reduced motion applies the open direction immediately");
            menu.Close();
            Check(ReadDirection().Y > 1, "reduced motion applies the close direction immediately");

            bool HasRotatedFrame()
            {
                for (int i = 0; i < 8; i++)
                {
                    if (Math.Abs(ReadDirection().X) > 1.2) return true;
                    Wait(9);
                }
                return false;
            }

            Vector ReadDirection()
            {
                using var frame = Capture(window);
                var points = new List<Point>();
                for (int y = (int)((pivot.Y - 8) * scale); y <= (pivot.Y + 8) * scale; y++)
                for (int x = (int)((pivot.X - 11) * scale); x <= (pivot.X + 8) * scale; x++)
                {
                    var pixel = frame.GetPixel(x, y);
                    if (Math.Abs(pixel.Red - ink.R) + Math.Abs(pixel.Green - ink.G) + Math.Abs(pixel.Blue - ink.B) <= 50)
                        points.Add(new Point(x / scale, y / scale));
                }

                Check(points.Count > 10, "the UserMenu chevron remains visible across its rotation");
                var xCenter = (points.Max(p => p.X) + points.Min(p => p.X)) / 2;
                var yCenter = (points.Max(p => p.Y) + points.Min(p => p.Y)) / 2;
                var dx = points.Where(p => Math.Abs(p.Y - yCenter) <= 1.5).Average(p => p.X) -
                         points.Where(p => Math.Abs(p.Y - yCenter) >= 2).Average(p => p.X);
                var dy = points.Where(p => Math.Abs(p.X - xCenter) <= 1.5).Average(p => p.Y) -
                         points.Where(p => Math.Abs(p.X - xCenter) >= 2).Average(p => p.Y);
                return new Vector(dx, dy);
            }
        }
        finally { window.Close(); }

        static void Wait(int milliseconds)
        {
            using var timeout = new CancellationTokenSource(milliseconds);
            Dispatcher.UIThread.MainLoop(timeout.Token);
        }
    }

    private static void VerifyImageViewSwitch(ThemeVariant theme, double scale)
    {
        var view = new AlderImageView
        {
            Items = { ImageViewPhoto(Brushes.Red), ImageViewPhoto(Brushes.Blue), ImageViewPhoto(Brushes.Lime) },
            ViewportHeight = 200
        };
        var window = new AlderTestWindow { Width = 420, Height = 380, Content = view, RequestedThemeVariant = theme };
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            using (Capture(window)) { }
            var viewport = view.GetVisualDescendants().OfType<AlderImageViewport>().Single();
            var center = viewport.TranslatePoint(new Rect(viewport.Bounds.Size).Center, window)!.Value;
            var next = view.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_Next");
            var click = next.TranslatePoint(new Rect(next.Bounds.Size).Center, window)!.Value;
            Check(ReadCenter() == SKColors.Red, "the first image is visible before switching");
            window.MouseDown(click, MouseButton.Left);
            window.MouseUp(click, MouseButton.Left);
            Check(view.SelectedIndex == 1, "the next action selects the second image immediately");

            bool blended = false;
            for (int frame = 0; frame < 8 && !blended; frame++)
            {
                var pixel = ReadCenter();
                blended = pixel.Red > pixel.Green + 25 && pixel.Blue > pixel.Green + 25;
                using var tick = new CancellationTokenSource(14);
                Dispatcher.UIThread.MainLoop(tick.Token);
            }
            Check(blended, $"the {theme} Image View renders both images during the switch at {scale}×");
            using (var settle = new CancellationTokenSource(225)) Dispatcher.UIThread.MainLoop(settle.Token);
            Check(ReadCenter() == SKColors.Blue, "image switch settles on the second image");

            view.SelectedIndex = 2;
            view.SelectedIndex = 0;
            using (var settle = new CancellationTokenSource(225)) Dispatcher.UIThread.MainLoop(settle.Token);
            Check(ReadCenter() == SKColors.Red, "interrupted switches settle on the latest image");

            view.SelectedIndex = 1;
            AlderMotion.SetReduced(view, true);
            Check(ReadCenter() == SKColors.Blue, "enabling reduced motion finishes the image switch immediately");
            view.SelectedIndex = 2;
            Check(ReadCenter() == SKColors.Lime, "reduced motion shows the selected image immediately");

            SKColor ReadCenter()
            {
                using var bitmap = Capture(window);
                return bitmap.GetPixel((int)(center.X * scale), (int)(center.Y * scale));
            }
        }
        finally { window.Close(); }
    }

    private static void VerifyImageViewTransitionOptions()
    {
        var view = new AlderImageView
        {
            Items = { ImageViewPhoto(Brushes.Red), ImageViewPhoto(Brushes.Blue) },
            ImageTransition = AlderImageViewTransition.None, ViewportHeight = 200
        };
        var window = new AlderTestWindow { Width = 420, Height = 380, Content = view, RequestedThemeVariant = ThemeVariant.Dark };
        window.Show();
        try
        {
            const double scale = 2;
            window.SetRenderScaling(scale);
            using (Capture(window)) { }
            var viewport = view.GetVisualDescendants().OfType<AlderImageViewport>().Single();
            var region = new Rect(viewport.TranslatePoint(default, window)!.Value, viewport.Bounds.Size);
            var fitted = Measure(blue: false);
            view.SelectedIndex = 1;
            Check(Measure(blue: true) == fitted, "None switches immediately to the fitted image");

            foreach (var (mode, index) in new[]
            {
                (AlderImageViewTransition.Slide, 1),
                (AlderImageViewTransition.Slide, 0),
                (AlderImageViewTransition.Zoom, 1),
                (AlderImageViewTransition.Lift, 1)
            })
            {
                view.ImageTransition = AlderImageViewTransition.None;
                view.SelectedIndex = 1 - index;
                AlderMotion.SetReduced(view, false);
                Check(Measure(blue: index == 0) == fitted, "each transition starts with a fitted image");
                view.ImageTransition = mode;
                view.SelectedIndex = index;
                bool inMotion = false;
                for (int frame = 0; frame < 8 && !inMotion; frame++)
                {
                    var bounds = Measure(blue: index == 1);
                    inMotion = bounds.Width > 0 && (mode switch
                    {
                        AlderImageViewTransition.Slide => (bounds.Center.X - fitted.Center.X) * (index == 1 ? 1 : -1) > 6,
                        AlderImageViewTransition.Zoom => bounds.Width < fitted.Width - 6 && Math.Abs(bounds.Center.X - fitted.Center.X) <= 1,
                        AlderImageViewTransition.Lift => bounds.Center.Y > fitted.Center.Y + 5,
                        _ => false
                    });
                    if (inMotion) break;
                    using var tick = new CancellationTokenSource(12);
                    Dispatcher.UIThread.MainLoop(tick.Token);
                }
                Check(inMotion, $"{mode} renders its distinct intermediate geometry toward index {index}");

                if (mode == AlderImageViewTransition.Zoom)
                    view.ImageTransition = AlderImageViewTransition.None;
                else
                    AlderMotion.SetReduced(view, true);
                Check(Measure(blue: index == 1) == fitted, $"{mode} settles immediately when its motion is canceled");
                Check(viewport.GetVisualDescendants().OfType<Image>().Count(image => image.Source is not null) == 1,
                    "the outgoing image is released");
            }

            Rect Measure(bool blue)
            {
                using var bitmap = Capture(window);
                var centerX = (int)(region.Center.X * scale);
                var centerY = (int)(region.Center.Y * scale);
                var left = Enumerable.Range((int)(region.Left * scale), (int)(region.Width * scale))
                    .Where(x => IsMarker(bitmap.GetPixel(x, centerY))).ToArray();
                var top = Enumerable.Range((int)(region.Top * scale), (int)(region.Height * scale))
                    .Where(y => IsMarker(bitmap.GetPixel(centerX, y))).ToArray();
                return left.Length == 0 || top.Length == 0 ? default :
                    new Rect(left[0] / scale, top[0] / scale, (left[^1] - left[0] + 1) / scale, (top[^1] - top[0] + 1) / scale);

                bool IsMarker(SKColor pixel) => (blue ? pixel.Blue : pixel.Red) > pixel.Green + 32;
            }
        }
        finally { window.Close(); }
    }

    private static AlderImageViewItem ImageViewPhoto(IBrush brush) => new()
    {
        Source = new DrawingImage
        {
            Drawing = new GeometryDrawing
            {
                Brush = brush,
                Geometry = new RectangleGeometry(new Rect(0, 0, 280, 160))
            }
        }
    };

    private static void VerifyDataGridLanding(ThemeVariant theme, double scale)
    {
        var items = new ObservableCollection<int>(Enumerable.Range(1, 50));
        var grid = new AlderDataGrid { ItemsSource = items, CanReorderRows = true, RowHeight = 40 };
        grid.ReorderCommand = new CommitMove(items);
        grid.Columns.Add(new AlderTableColumn { Header = "Task", Width = new GridLength(320),
            CellTemplate = new FuncDataTemplate<int>((value, _) => new Border
            {
                Width = 24, Height = 12, HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center, Background = value == 1 ? Brushes.Magenta : Brushes.Gray
            }) });
        var window = new AlderTestWindow { Width = 420, Height = 270, Content = grid, RequestedThemeVariant = theme };
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            using var initial = Capture(window);
            var view = grid.GetVisualDescendants().OfType<TableView>().Single();
            var row = view.ContainerFromIndex(0)!;
            var grip = row.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Reorder row");
            var start = grip.TranslatePoint(new Rect(grip.Bounds.Size).Center, window)!.Value;
            var destination = view.ContainerFromIndex(3)!;
            var landing = destination.TranslatePoint(new Point(start.X + 120, 29), window)!.Value;
            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(landing, RawInputModifiers.LeftMouseButton);
            using var dragging = Capture(window);
            var dragCenter = Marker(dragging);
            var initialCenter = Marker(initial);
            Check(Math.Abs(dragCenter.X - initialCenter.X - (landing.X - start.X)) <= 2,
                $"the DataGrid snapshot follows the pointer at {scale}×");

            window.MouseUp(landing, MouseButton.Left);
            // MouseUp has already rendered the release. Another capture tick advances the easing
            // beyond the first frame on slower runners.
            using var releaseFrame = window.GetLastRenderedFrame()!;
            using var released = DecodeFrame(releaseFrame);
            var releaseCenter = Marker(released);
            Check(items[3] == 1 &&
                window.GetVisualDescendants().Any(visual => visual is Border { Name: "DataGridDragPreview" }) &&
                releaseCenter.X > initialCenter.X + (dragCenter.X - initialCenter.X) / 2 && releaseCenter.X <= dragCenter.X + 1,
                $"the DataGrid release starts at the dragged position at {scale}× " +
                $"(source {initialCenter.X:F1}, drag {dragCenter.X:F1}, release {releaseCenter.X:F1})");

            using (var end = new CancellationTokenSource(260)) Dispatcher.UIThread.MainLoop(end.Token);
            using var settled = Capture(window);
            var settledCenter = Marker(settled);
            Check(items[3] == 1 && Math.Abs(settledCenter.X - initialCenter.X) < 1 &&
                Math.Abs(settledCenter.Y - initialCenter.Y - 3 * grid.RowHeight) < 1 &&
                !window.GetVisualDescendants().Any(visual => visual is Border { Name: "DataGridDragPreview" }),
                $"the DataGrid snapshot hands off to the destination row at {scale}×");
        }
        finally { window.Close(); }

        Point Marker(SKBitmap image)
        {
            int count = 0, xTotal = 0, yTotal = 0;
            for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
            {
                var color = image.GetPixel(x, y);
                if (color.Red < 210 || color.Blue < 210 || color.Green > 95) continue;
                count++;
                xTotal += x;
                yTotal += y;
            }
            Check(count > 0, "a frame contains the DataGrid row marker");
            return new Point(xTotal / (count * scale), yTotal / (count * scale));
        }
    }

    private sealed class CommitMove(ObservableCollection<int> items) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => parameter is DataGridReorderRequest;
        public void Execute(object? parameter) => items.Move(0, 3);
    }

    private static void VerifyRipple(ThemeVariant theme, double scale)
    {
        var button = new AlderButton
        {
            Width = 144, Height = 44, HorizontalAlignment = HorizontalAlignment.Center, Variant = ButtonVariant.Ghost,
            VerticalAlignment = VerticalAlignment.Center, CornerRadius = new CornerRadius(22), Background = Brushes.Gray,
            Feedback = new AlderPressableFeedback { FeedbackBrush = Brushes.Magenta, HoverOpacity = 0, PressedOpacity = .3 }
        };
        var window = new AlderTestWindow
        {
            Width = 214, Height = 124, Content = button, RequestedThemeVariant = theme,
            Background = theme == ThemeVariant.Dark ? Brushes.Black : Brushes.White
        };
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            using var resting = Capture(window);
            var corner = button.TranslatePoint(default, window)!.Value;
            var left = corner + new Vector(29, 16);
            var right = corner + new Vector(112, 16);
            window.MouseDown(left, MouseButton.Left);
            var clip = button.GetVisualDescendants().OfType<Border>().Single(part => part.Name == "RippleClip");
            var ink = clip.Child!;
            Check(ElementComposition.GetElementVisual(ink) is { Scale.X: not 1 }, "ripple growth uses the render layer");
            using (Capture(window)) { }
            AdvanceDispatcher(45);
            using var leftPress = Capture(window);
            Check(InkColor(leftPress, left + new Vector(12, 12)) - InkColor(leftPress, right) > 15,
                $"the first ripple grows from the left pointer position ({theme}, {scale}×)");
            window.MouseUp(left, MouseButton.Left);
            window.MouseDown(right, MouseButton.Left);
            using (Capture(window)) { }
            AdvanceDispatcher(45);
            using var rightPress = Capture(window);
            Check(InkColor(rightPress, right + new Vector(12, 12)) - InkColor(rightPress, left) > 15,
                $"a rapid second press moves the ripple origin to the right ({theme}, {scale}×)");
            AdvanceDispatcher(225);
            using var held = Capture(window);
            Check(InkColor(held, left) > 22 && InkColor(held, right) > 22, "a held ripple fills the button");
            Check(Read(held, corner + new Vector(2, 2)) == Read(resting, corner + new Vector(2, 2)),
                "the ripple stays inside the rounded corners");

            var outside = corner - new Vector(8, 12);
            window.MouseMove(outside);
            window.MouseUp(outside, MouseButton.Left);
            using (Capture(window)) { }
            AdvanceDispatcher(105);
            using var fading = Capture(window);
            Check(InkColor(fading, right) > 3 && InkColor(fading, right) < InkColor(held, right),
                "dragging out and releasing fades the ripple");
            AdvanceDispatcher(310);
            using var settled = Capture(window);
            Check(clip.Child is null && Math.Abs(InkColor(settled, right)) < 3, "the released ripple leaves no residual ink");

            int InkColor(SKBitmap image, Point p) => Read(image, p).Red - Read(image, p).Green;
            SKColor Read(SKBitmap image, Point p) => image.GetPixel((int)(p.X * scale), (int)(p.Y * scale));

            static void AdvanceDispatcher(int milliseconds)
            {
                using var end = new CancellationTokenSource(milliseconds);
                Dispatcher.UIThread.MainLoop(end.Token);
            }
        }
        finally { window.Close(); }
    }

    private static void VerifyLoader(ThemeVariant theme, double scale)
    {
        var loader = new CountedLoader { Variant = AiLoaderVariant.Ring, Width = 34, Height = 34 };
        var scope = new Border { Child = loader };
        var window = new AlderTestWindow { Width = 120, Height = 80, Content = scope, RequestedThemeVariant = theme };
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            foreach (var variant in new[] { AiLoaderVariant.Ring, AiLoaderVariant.Dots, AiLoaderVariant.Pulse, AiLoaderVariant.Scan })
            {
                loader.Variant = variant;
                using var first = Capture(window);
                var motion = loader.GetVisualDescendants().OfType<LoaderMotionLayer>().Single();
                Check(motion.IsComposed, $"{variant} uses its composition layer");
                int initialRenders = loader.RenderCalls;
                bool pixelsMoved = false;
                for (int i = 0; i < 6 && !pixelsMoved; i++)
                {
                    Thread.Sleep(35);
                    using var next = Capture(window);
                    pixelsMoved = !first.Bytes.AsSpan().SequenceEqual(next.Bytes);
                }
                Check(pixelsMoved && loader.RenderCalls == initialRenders, $"{variant} animates without re-recording the loader");
            }

            var layer = loader.GetVisualDescendants().OfType<LoaderMotionLayer>().Single();
            scope.IsVisible = false;
            Pump(window);
            Check(!layer.IsComposed, "a hidden loader ancestor stops its animations");
            scope.IsVisible = true;
            Pump(window);
            Check(layer.IsComposed, "a visible loader resumes its animations");

            var foreground = loader.Foreground;
            loader.Foreground = new LinearGradientBrush
            {
                GradientStops = new GradientStops { new(Colors.Red, 0), new(Colors.Blue, 1) }
            };
            using (Capture(window)) { }
            int fallbackRenders = loader.RenderCalls;
            Thread.Sleep(40);
            using (Capture(window)) { }
            Check(!layer.IsComposed && loader.RenderCalls > fallbackRenders, "a custom gradient keeps the original loader drawing and animation");
            loader.Foreground = foreground;
            Pump(window);
            Check(layer.IsComposed, "restoring the themed brush resumes the composition animation");

            AlderMotion.SetReduced(window, true);
            using var reduced = Capture(window);
            Thread.Sleep(70);
            using var later = Capture(window);
            Check(!layer.IsComposed && reduced.Bytes.AsSpan().SequenceEqual(later.Bytes),
                $"reduced motion uses the static loader drawing in {theme} at {scale}×");

            AlderMotion.SetReduced(window, false);
            Pump(window);
            Check(layer.IsComposed, "restoring motion rebuilds the loader's composition layer");
        }
        finally { window.Close(); }
    }

    private static void VerifyCompactLoadingRings(ThemeVariant theme, double scale)
    {
        var step = new AlderChainOfThoughtStep { Header = "Reading configuration", Status = AiRunStatus.Running };
        var prompt = new AlderPromptInput { IsSubmitting = true };
        var composer = new AlderPromptInput
        {
            Theme = (ControlTheme)Application.Current!.FindResource("AlderModelComposerTheme")!,
            IsSubmitting = true
        };
        var compact = new AlderLoader { Width = 12, Height = 16, Variant = AiLoaderVariant.Ring };
        var window = new AlderTestWindow
        {
            Width = 350, Height = 360, RequestedThemeVariant = theme,
            Content = new StackPanel { Margin = new Thickness(16), Spacing = 12, Children = { step, prompt, composer, compact } }
        };
        AlderMotion.SetReduced(window, true);
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            using var still = Capture(window);
            var rings = new[] { FindRing(step, "ActiveRing"), FindRing(prompt, "PendingGlyph"), FindRing(composer, "PendingGlyph"), compact };
            var disc = step.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "ActiveDisc");
            var discOffset = Center(rings[0]) - Center(disc);
            Check(Math.Abs(discOffset.X) < .01 && Math.Abs(discOffset.Y) < .01, "the active step ring slot is centered on its disc");
            foreach (var ring in rings) VerifyTrack(still, ring);

            AlderMotion.SetReduced(window, false);
            using var animated = Capture(window);
            foreach (var ring in rings)
            {
                var layer = ring.GetVisualDescendants().OfType<LoaderMotionLayer>().Single();
                Check(layer.IsComposed, $"{ring.Name} uses composition at {theme.Key}, {scale}×");
                VerifyTrack(animated, ring);
            }
        }
        finally { window.Close(); }

        static AlderLoader FindRing(Control host, string name) =>
            host.GetVisualDescendants().OfType<AlderLoader>().Single(loader => loader.Name == name);

        Point Center(Visual visual) => visual.TranslatePoint(new Rect(visual.Bounds.Size).Center, window)!.Value;

        void VerifyTrack(SKBitmap frame, AlderLoader ring)
        {
            var center = Center(ring);
            var background = Pixel(center);
            double radius = Math.Min(ring.Bounds.Width, ring.Bounds.Height) * 10 / 24;
            foreach (var axis in new[] { new Vector(-1, 0), new Vector(1, 0), new Vector(0, -1), new Vector(0, 1) })
            {
                int contrast = 0;
                for (double offset = -1; offset <= 1; offset += .5)
                {
                    var pixel = Pixel(center + axis * (radius + offset));
                    contrast = Math.Max(contrast, Math.Max(Math.Abs(pixel.Red - background.Red),
                        Math.Max(Math.Abs(pixel.Green - background.Green), Math.Abs(pixel.Blue - background.Blue))));
                }
                Check(contrast >= 8, $"the {ring.Name ?? "standalone"} ring track is off-center toward {axis} at {theme.Key}, {scale}×");
            }

            SKColor Pixel(Point point) => frame.GetPixel((int)(point.X * scale), (int)(point.Y * scale));
        }
    }

    private static void VerifyActionBar(ThemeVariant theme, double scale, int width)
    {
        var marker = new Border { Width = 44, Height = 12, Background = Brushes.Magenta };
        var bar = new AlderActionBar { Content = marker, IsOpen = true };
        var window = new AlderTestWindow
        {
            Width = width, Height = 150, Content = new Grid { Children = { bar } }, RequestedThemeVariant = theme,
            Background = theme == ThemeVariant.Dark ? Brushes.Black : Brushes.White
        };
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            using var opened = Capture(window);
            double originalCenter = BarCenter(opened);
            Check(originalCenter > 0, "the open Action Bar paints its content");

            bar.IsOpen = false;
            using (Capture(window)) { }
            Thread.Sleep(75);
            using var exiting = Capture(window);
            double exitingCenter = BarCenter(exiting);
            Check(exitingCenter > originalCenter + scale && exitingCenter < originalCenter + 13 * scale,
                "the exiting Action Bar moves down with its content");

            bar.IsOpen = true;
            using var reversing = Capture(window);
            double reversingCenter = BarCenter(reversing);
            Check(reversingCenter > 0 && Math.Abs(reversingCenter - exitingCenter) <= 7 * scale,
                "reopening the Action Bar continues from its visible position");

            Thread.Sleep(235);
            using var reopened = Capture(window);
            Check(Math.Abs(BarCenter(reopened) - originalCenter) < scale, "the reopened bar returns to its original position");

            bar.IsOpen = false;
            AlderMotion.SetReduced(window, true);
            using var closed = Capture(window);
            Check(BarCenter(closed) < 0, "reduced motion settles the closing Action Bar immediately");
        }
        finally { window.Close(); }

        static double BarCenter(SKBitmap image)
        {
            var first = -1;
            var last = -1;
            for (var y = 0; y < image.Height; y++)
            {
                var pixel = image.GetPixel(image.Width / 2, y);
                if (pixel.Red - pixel.Green < 90 || pixel.Blue - pixel.Green < 90) continue;
                if (first < 0) first = y;
                last = y;
            }
            return (first + last) / 2d;
        }
    }

    private static void Check(bool result, string message)
    {
        if (!result) throw new InvalidOperationException(message);
    }

    private static SKBitmap Capture(Window window)
    {
        Pump(window);
        using var frame = window.CaptureRenderedFrame()!;
        return DecodeFrame(frame);
    }

    private static SKBitmap DecodeFrame(Bitmap frame)
    {
        using var stream = new MemoryStream();
        frame.Save(stream, PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        return SKBitmap.Decode(stream);
    }

    private static void Pump(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class CountedLoader : AlderLoader
    {
        public int RenderCalls { get; private set; }
        protected override Type StyleKeyOverride => typeof(AlderLoader);

        public override void Render(DrawingContext context)
        {
            RenderCalls++;
            base.Render(context);
        }
    }
}
