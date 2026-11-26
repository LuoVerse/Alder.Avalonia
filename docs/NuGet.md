# Alder.Avalonia

Alder.Avalonia is a native Avalonia control library for desktop applications. It includes form and color editors, navigation and application layouts, data views, and AI conversation components. It targets .NET 11 and uses Avalonia 12.2.0.

## Install

```sh
dotnet package add Alder.Avalonia --version 1.0.0-preview.1
```

Load `AlderTheme` after your application's base theme. This example uses the `Avalonia.Themes.Fluent` package.

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:alderThemes="using:Alder.Avalonia.Themes">
  <Application.Styles>
    <FluentTheme />
    <alderThemes:AlderTheme />
  </Application.Styles>
</Application>
```

In a view, declare `xmlns:alder="using:Alder.Avalonia.Controls"` and add controls such as `<alder:AlderButton Variant="Primary" Content="Save" />`.

- Browse the [component documentation](https://github.com/LuoVerse/Alder.Avalonia/blob/v1.0.0-preview.1/docs/COMPONENTS.md).
- Try the [desktop Gallery](https://github.com/LuoVerse/Alder.Avalonia/releases/tag/v1.0.0-preview.1) for interactive examples and their AXAML and C# source.
- Read the [MIT license](https://github.com/LuoVerse/Alder.Avalonia/blob/v1.0.0-preview.1/LICENSE). Bundled third-party notices are included in the package's `licenses/` directory.
