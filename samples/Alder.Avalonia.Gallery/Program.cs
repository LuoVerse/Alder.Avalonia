using Avalonia;
using ReactiveUI.Avalonia;

namespace Alder.Avalonia.Gallery;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // The Avalonia designer calls this method without starting the desktop lifetime.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI(_ => { });
}
