# Alder.Avalonia

**English** · [简体中文](README.zh-CN.md)

Alder.Avalonia is a native desktop control library for Avalonia. It includes form controls, color editors, navigation and layout, data displays, charts, and AI conversation components. The Gallery brings together interactive previews, AXAML, and C# examples.

The visuals and interactions draw on HeroUI Web and HeroUI Pro Web. The controls use Avalonia templates, properties, commands, and input events. [HeroUI Web](https://heroui.com/en/docs/react/components) · [HeroUI Pro Web](https://heroui.pro/docs/react/components)

## Online preview

Try the Gallery controls, search examples, switch languages and themes, and inspect the AXAML and C# source in your browser. The first visit downloads the WebAssembly runtime. Examples that open separate system windows run in the desktop Gallery. [Open the online Gallery](https://luoverse.github.io/Alder.Avalonia/)

The solution lists `Alder.Avalonia.Gallery.Browser`. Build and run this project separately with the .NET 11 SDK and the `wasm-tools` workload. A regular solution build only requires the .NET 11 SDK.

```sh
cd samples/Alder.Avalonia.Gallery.Browser
dotnet workload install wasm-tools
dotnet run
```

GitHub Actions builds and deploys the browser Gallery to GitHub Pages when changes to the browser project or control library reach `main`. [Deployment runs](https://github.com/LuoVerse/Alder.Avalonia/actions/workflows/gallery-pages.yml)

## Run the desktop Gallery

Download a desktop build from GitHub Releases or run it from source with the .NET 11 SDK. The solution targets `net11.0` and uses Avalonia 12.2.0. [GitHub Releases](https://github.com/LuoVerse/Alder.Avalonia/releases)

```sh
dotnet run --project samples/Alder.Avalonia.Gallery
```

The Gallery opens on Home. Use the sidebar to search components. The window supports Light and Dark themes and English and Simplified Chinese. Each example card has Preview, AXAML, and C# tabs.

In Data Views → Workflow Editor, edit approval steps, countersignatures, and conditional branches. Use the preview controls to run a simulation or import and export JSON.

## Use in an Avalonia app

The current stable version is 2.0.0. Install 2.0.1-preview to try the latest controls and fixes. [Stable package](https://www.nuget.org/packages/Alder.Avalonia/2.0.0) · [Preview package](https://www.nuget.org/packages/Alder.Avalonia/2.0.1-preview)

```sh
dotnet package add Alder.Avalonia --version 2.0.1-preview
```

Load `AlderTheme` in `App.axaml` and use `AlderWindow` for desktop windows. If your app also uses built-in Avalonia windows or controls, load their host theme before `AlderTheme`.

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:alderThemes="using:Alder.Avalonia.Themes">
  <Application.Styles>
    <alderThemes:AlderTheme />
  </Application.Styles>
</Application>
```

The control namespace is `Alder.Avalonia.Controls`. Buttons, lists, and inputs support Avalonia commands, bindings, and templates.

```xml
<StackPanel xmlns="https://github.com/avaloniaui"
            xmlns:alder="using:Alder.Avalonia.Controls"
            Spacing="12">
  <alder:AlderButton Variant="Primary" Content="Save" />
  <alder:AlderSegment SelectedKey="overview">
    <alder:AlderSegmentItem Key="overview" Content="Overview" />
    <alder:AlderSegmentItem Key="activity" Content="Activity" />
  </alder:AlderSegment>
</StackPanel>
```

## Project structure and verification

| Path | Purpose |
| --- | --- |
| [`src/Alder.Avalonia`](src/Alder.Avalonia) | Controls, themes, assets, and the native text engine |
| [`samples/Alder.Avalonia.Gallery`](samples/Alder.Avalonia.Gallery) | Gallery, bilingual resources, and complete examples |
| [`samples/Alder.Avalonia.Gallery.Browser`](samples/Alder.Avalonia.Gallery.Browser) | WebAssembly entry point for the Gallery |
| [`tests/Alder.Avalonia.Tests`](tests/Alder.Avalonia.Tests) | Headless interaction, binding, and state regression tests |
| [`tests/Alder.Avalonia.AotSmoke`](tests/Alder.Avalonia.AotSmoke) | Skia pixel checks, binding diagnostics, and NativeAOT smoke tests |

```sh
dotnet build Alder.Avalonia.slnx -c Release
dotnet test tests/Alder.Avalonia.Tests -c Release
dotnet run --project tests/Alder.Avalonia.AotSmoke -c Release
```

The default host for interaction regressions and Skia smoke tests loads only `AlderTheme`. A separate test host checks compatibility with Fluent2.

For NativeAOT on macOS arm64, publish for the host RID and run the resulting executable.

```sh
dotnet publish tests/Alder.Avalonia.AotSmoke -c Release -r osx-arm64 -p:PublishAot=true
./tests/Alder.Avalonia.AotSmoke/bin/Release/net11.0/osx-arm64/publish/Alder.Avalonia.AotSmoke
```

Append `-p:AuditWholeLibrary=true` to the publish command for a library-wide trimming audit. This roots the `Alder.Avalonia` assembly so diagnostics also cover code outside the smoke test's direct references. A regular publish tests the controls, themes, and interactions retained after trimming.

The NativeAOT smoke workflow runs normally trimmed executables on Windows x64 and Linux x64. The host loads only `AlderTheme`. It renders the public controls and flyouts with `--all-controls Light` and `--all-controls Dark`, then runs interaction, pixel, and binding regressions. [NativeAOT smoke workflow](.github/workflows/nativeaot-smoke.yml)

Write builds, screenshot probes, and local benchmark results outside the checkout with `--artifacts-path` or the tool's own output options.

## Documentation

- [2.0.1-preview release notes](docs/releases/2.0.1-preview.md)
- [2.0.0 release and migration notes](docs/releases/2.0.0.md)
- [NuGet installation and setup](docs/NuGet.md)
- [Design system](DESIGN.md)
- [Component catalog and usage contracts](docs/COMPONENTS.md)
- [Integration guides](docs/COMPONENTS.md#integration-guides)
- [Rich Text Editor construction, saving, and reloading](docs/guides/rich-text-editor.md)

## License

The project uses the MIT license. Third-party assets and code retain their own licenses. The Chinese fallback font in the browser Gallery is based on Noto Sans SC and uses the SIL Open Font License 1.1. [MIT license](LICENSE) · [Asset notices](src/Alder.Avalonia/Assets) · [Noto Sans SC license](samples/Alder.Avalonia.Gallery.Browser/wwwroot/licenses/NotoSansSC-OFL.txt)
