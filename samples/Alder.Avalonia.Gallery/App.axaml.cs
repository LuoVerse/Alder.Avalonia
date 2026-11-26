using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Alder.Avalonia.Gallery.Views;
using Alder.Avalonia.Gallery.Localization;
using Alder.Avalonia.Gallery.ViewModels;

namespace Alder.Avalonia.Gallery;

public partial class App : Application
{
    internal GalleryLocalization Localization { get; } = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        if (OperatingSystem.IsWindows())
        {
            // Native popups have separate render roots and do not inherit the window's text options.
            Popup.IsOpenProperty.Changed.AddClassHandler<Popup>((popup, change) =>
            {
                if (change.GetNewValue<bool>() && TopLevel.GetTopLevel(popup.Child) is PopupRoot root)
                    TextOptions.SetTextRenderingMode(root, TextRenderingMode.Antialias);
            });
        }
        Localization.Apply(GalleryLocalization.DefaultLanguage, Resources);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = CreateDemoWindow();
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            var gallery = new GalleryView();
            var scope = new ThemeVariantScope
            {
                DataContext = gallery.DataContext,
                Child = new DialogHost { Content = gallery }
            };
            scope.Bind(ThemeVariantScope.RequestedThemeVariantProperty,
                CompiledBinding.Create((GalleryWindowViewModel model) => model.WindowTheme));
            singleView.MainView = scope;
        }
        base.OnFrameworkInitializationCompleted();
    }

    public static Window CreateDemoWindow()
    {
        var window = new GalleryWindow();
        if (OperatingSystem.IsWindows())
            TextOptions.SetTextRenderingMode(window, TextRenderingMode.Antialias);
        return window;
    }

    internal static TopLevel? GalleryTopLevel => Current?.ApplicationLifetime switch
    {
        IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow,
        ISingleViewApplicationLifetime singleView => TopLevel.GetTopLevel(singleView.MainView),
        _ => null
    };
}
