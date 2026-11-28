# Alder.Avalonia

Alder.Avalonia is a native Avalonia control library for desktop applications. It includes form and color editors, navigation and application layouts, data views, and AI conversation components. It targets .NET 11 and uses Avalonia 12.2.0.

## Install

```sh
dotnet package add Alder.Avalonia --version 2.0.1-preview
```

This is a prerelease. See the [preview release notes](https://github.com/LuoVerse/Alder.Avalonia/releases/tag/v2.0.1-preview) for new controls and fixes.

Load `AlderTheme` and use `AlderWindow` on desktop. If your application also uses built-in Avalonia windows or controls, load a host theme for them before `AlderTheme`.

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:alderThemes="using:Alder.Avalonia.Themes">
  <Application.Styles>
    <alderThemes:AlderTheme />
  </Application.Styles>
</Application>
```

In a view, declare `xmlns:alder="using:Alder.Avalonia.Controls"` and add controls such as `<alder:AlderButton Variant="Primary" Content="Save" />`.

## Upgrading from 1.x

Most component `Icon` properties now accept `object?` so applications can supply Avalonia controls, image sources, or the existing `Geometry` resources. Rebuild applications and libraries compiled against 1.x. If your C# code reads an icon as `Geometry`, use `component.Icon is Geometry geometry` to handle the new value types. Window chrome still uses built-in geometry icons.

[Read the 2.0.0 release and migration notes](https://github.com/LuoVerse/Alder.Avalonia/releases/tag/v2.0.0).

## Alibaba PuHuiTi font

Call `WithAlderPuHuiTiFont()` on your `AppBuilder` to make the bundled Alibaba PuHuiTi 3.0 the default font. It includes Regular, Medium, SemiBold, and Bold. Controls with an explicit font family use that family first. Apps that omit the call use Avalonia's normal font selection.

Pass existing `FontManagerOptions` to the extension in place of registering them separately. The supplied `DefaultFamilyName` becomes a fallback for unavailable faces and missing glyphs. For example, an app using `Avalonia.Fonts.Inter` can choose Inter as the fallback after PuHuiTi.

```csharp
using Avalonia;
using Avalonia.Media;

AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .WithInterFont()
    .WithAlderPuHuiTiFont(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" });
```

- Browse the [component documentation](https://github.com/LuoVerse/Alder.Avalonia/blob/v2.0.1-preview/docs/COMPONENTS.md).
- Try the [desktop Gallery](https://github.com/LuoVerse/Alder.Avalonia/releases/tag/v2.0.1-preview) for interactive examples and their AXAML and C# source.
- Read the [MIT license](https://github.com/LuoVerse/Alder.Avalonia/blob/v2.0.1-preview/LICENSE). Bundled third-party notices are included in the package's `licenses/` directory.
