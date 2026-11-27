using Alder.Avalonia.Controls;
using Alder.Avalonia.Themes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.AotSmoke;

internal static class PublicControlSmoke
{
    public static void Run(ThemeVariant theme, BindingLog bindingLog)
    {
        if (Application.Current?.Styles is not { Count: 1 } styles || styles[0] is not AlderTheme)
            throw new InvalidOperationException("The public control smoke host must load only AlderTheme.");

        var failures = new List<Exception>();
        foreach (var (type, create) in PublicControlFactories.All)
            Verify(type.Name, () => VerifyControl(create(), theme));
        Verify(nameof(ToastHost), () => VerifyToastHost(theme));

        if (failures.Count > 0)
            throw new AggregateException($"Public control smoke failed in {theme}.", failures);

        Console.WriteLine($"{theme.Key}: {PublicControlFactories.All.Count + 1} public controls and flyouts rendered without binding warnings.");

        void Verify(string name, Action exercise)
        {
            bindingLog.Warnings.Clear();
            try
            {
                exercise();
                if (bindingLog.Warnings.Count > 0)
                    throw new InvalidOperationException(string.Join(Environment.NewLine, bindingLog.Warnings));
            }
            catch (Exception error)
            {
                failures.Add(new InvalidOperationException($"{theme.Key} / {name}: {error.Message}", error));
            }
        }
    }

    private static void VerifyControl(AvaloniaObject instance, ThemeVariant theme)
    {
        switch (instance)
        {
            case Window window:
                window.RequestedThemeVariant = theme;
                window.Width = 520;
                window.Height = 360;
                WithWindow(window, () => VerifyFrame(window));
                break;
            case Control child:
                var host = CreateWindow(theme, child);
                WithWindow(host, () =>
                {
                    VerifyFrame(host);
                    // These parts paint through Render and have no control template.
                    if (child is TemplatedControl { Template: null } and not (AlderColorSwatch or AlderMentionHighlights))
                        throw new InvalidOperationException("The control did not get an Alder template.");
                });
                break;
            case FlyoutBase flyout:
                var anchor = new AlderButton { Content = "Open", HorizontalAlignment = HorizontalAlignment.Center };
                var flyoutHost = CreateWindow(theme, anchor);
                WithWindow(flyoutHost, () =>
                {
                    flyout.ShowAt(anchor);
                    if (!flyout.IsOpen) throw new InvalidOperationException("The flyout did not open.");
                    VerifyFrame(flyoutHost);
                    flyout.Hide();
                    Pump(flyoutHost);
                    if (flyout.IsOpen) throw new InvalidOperationException("The flyout did not close.");
                });
                break;
            default:
                throw new InvalidOperationException($"Unexpected type: {instance.GetType().Name}.");
        }
    }

    private static void VerifyToastHost(ThemeVariant theme)
    {
        var window = CreateWindow(theme);
        WithWindow(window, () =>
        {
            const string title = "Alder smoke toast";
            var host = Toast.Register(window, "Smoke");
            host.Show(new ToastOptions(title));
            VerifyFrame(window);
            if (!host.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == title))
                throw new InvalidOperationException("The toast host did not display the toast.");
        });
    }

    private static AlderWindow CreateWindow(ThemeVariant theme, Control? content = null) => new()
    {
        Width = 520, Height = 360, ShowTitleBar = false, RequestedThemeVariant = theme, Content = content
    };

    private static void WithWindow(Window window, Action exercise)
    {
        AlderMotion.SetReduced(window, true);
        window.Show();
        try
        {
            Pump(window);
            exercise();
        }
        finally { window.Close(); }
    }

    private static void VerifyFrame(Window window)
    {
        Pump(window);
        using var frame = window.CaptureRenderedFrame();
        if (frame is null || frame.PixelSize.Width <= 0 || frame.PixelSize.Height <= 0)
            throw new InvalidOperationException("The host did not render a Skia frame.");
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
