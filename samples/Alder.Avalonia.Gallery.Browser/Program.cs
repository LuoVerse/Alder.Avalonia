using Alder.Avalonia.Gallery;
using Avalonia;
using Avalonia.Browser;
using Avalonia.Media;
using ReactiveUI.Avalonia;

await AppBuilder.Configure<App>()
    .WithInterFont()
    .With(new FontManagerOptions
    {
        DefaultFamilyName = "fonts:Inter#Inter",
        FontFallbacks =
        [
            new FontFallback
            {
                FontFamily = new FontFamily("avares://Alder.Avalonia.Gallery.Browser/Assets/Fonts#Alder Gallery CJK"),
                UnicodeRange = UnicodeRange.Parse("U+2E80-9FFF, U+F900-FAFF, U+FE10-FE1F, U+FE30-FE4F, U+FF00-FFEF")
            }
        ]
    })
    .UseReactiveUI(_ => { })
    .StartBrowserAppAsync("out", new BrowserPlatformOptions
    {
        RenderingMode = [BrowserRenderingMode.WebGL2, BrowserRenderingMode.Software2D]
    });
