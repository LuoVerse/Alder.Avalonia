using Avalonia;
using Avalonia.Media;
using ReactiveUI.Avalonia;

namespace Alder.Avalonia.Gallery;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        CreateAppBuilder(args.Contains("--system-cjk", StringComparer.Ordinal))
            .StartWithClassicDesktopLifetime(args);

    // The Avalonia designer calls this method without starting the desktop lifetime.
    public static AppBuilder BuildAvaloniaApp() =>
        CreateAppBuilder(useSystemCjk: false);

    private static AppBuilder CreateAppBuilder(bool useSystemCjk)
    {
        var fontOptions = new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" };
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI(_ => { });

        return useSystemCjk ? builder.With(fontOptions) : builder.WithAlderPuHuiTiFont(fontOptions);
    }
}
