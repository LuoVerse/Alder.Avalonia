using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Alder.Avalonia.Gallery.Views;
using Alder.Avalonia.Gallery.Localization;

namespace Alder.Avalonia.Gallery;

public partial class App : Application
{
    internal GalleryLocalization Localization { get; } = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Localization.Apply(GalleryLocalization.DefaultLanguage, Resources);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = CreateDemoWindow();
        base.OnFrameworkInitializationCompleted();
    }

    public static Window CreateDemoWindow() => new GalleryWindow();
}
