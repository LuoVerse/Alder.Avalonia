# Alder.Avalonia

Alder.Avalonia 是一套 Avalonia 原生桌面控件库。它提供基础表单、颜色编辑、导航布局、数据展示、图表和 AI 会话组件，并在同一个 Gallery 中展示可操作的预览、AXAML 和 C# 示例。

界面和交互参考 [HeroUI Web](https://heroui.com/en/docs/react/components) 与 [HeroUI Pro Web](https://heroui.pro/docs/react/components)。控件通过 Avalonia 的模板、属性系统、命令和输入事件运行。

## 在线预览

在浏览器中试用 Gallery 控件、搜索示例、切换语言和主题，并查看 AXAML 和 C# 示例。首次打开会下载 WebAssembly 运行时，独立系统窗口示例在桌面版运行。[打开 Alder Gallery 在线预览](https://luoverse.github.io/Alder.Avalonia/)

`Alder.Avalonia.Gallery.Browser` 在解决方案中可见。浏览器版需要 .NET 11 SDK 和 `wasm-tools` 工作负载，按项目单独构建和运行。常规解决方案构建只需要 .NET 11 SDK。

```sh
cd samples/Alder.Avalonia.Gallery.Browser
dotnet workload install wasm-tools
dotnet run
```

浏览器项目和控件库的改动合并到 `main` 后，由 GitHub Actions 构建并部署到 GitHub Pages。[部署记录](https://github.com/LuoVerse/Alder.Avalonia/actions/workflows/gallery-pages.yml)

## 运行桌面 Gallery

[GitHub Releases](https://github.com/LuoVerse/Alder.Avalonia/releases) 提供可直接运行的 Gallery 桌面程序。若从源码运行，需要 .NET 11 SDK。解决方案目标为 `net11.0`，使用 Avalonia 12.2.0。

```sh
dotnet run --project samples/Alder.Avalonia.Gallery
```

Gallery 默认打开 Home。侧栏可以搜索组件，窗口支持浅色和深色切换，以及英文和简体中文。示例卡包含 Preview、AXAML 和 C# 标签。

在 Gallery 的「实用工具 → 工作流编辑器」中，可以编辑审批、会签与条件分支，并通过预览页的按钮试跑流程或导入导出 JSON。

## 在 Avalonia 应用中使用

从 [NuGet.org](https://www.nuget.org/packages/Alder.Avalonia/2.0.0) 安装 `Alder.Avalonia`。

```sh
dotnet package add Alder.Avalonia --version 2.0.0
```

在 `App.axaml` 中加载 `AlderTheme`，桌面窗口使用 `AlderWindow`。如果应用也使用原生 Avalonia 窗口或控件，可以为它们添加宿主主题，并在宿主主题之后加载 `AlderTheme`。

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:alderThemes="using:Alder.Avalonia.Themes">
  <Application.Styles>
    <alderThemes:AlderTheme />
  </Application.Styles>
</Application>
```

控件命名空间是 `Alder.Avalonia.Controls`。按钮、列表与输入控件支持 Avalonia 的命令、绑定和模板组合。

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

## 项目结构与验证

| 路径 | 用途 |
| --- | --- |
| [`src/Alder.Avalonia`](src/Alder.Avalonia) | 控件、主题、资产和原生文本引擎 |
| [`samples/Alder.Avalonia.Gallery`](samples/Alder.Avalonia.Gallery) | Gallery、双语资源及完整示例 |
| [`samples/Alder.Avalonia.Gallery.Browser`](samples/Alder.Avalonia.Gallery.Browser) | Gallery 的 WebAssembly 入口 |
| [`tests/Alder.Avalonia.Tests`](tests/Alder.Avalonia.Tests) | Headless 交互、绑定和状态回归 |
| [`tests/Alder.Avalonia.AotSmoke`](tests/Alder.Avalonia.AotSmoke) | Skia 像素验收、绑定诊断和 NativeAOT 烟测 |

```sh
dotnet build Alder.Avalonia.slnx -c Release
dotnet test tests/Alder.Avalonia.Tests -c Release
dotnet run --project tests/Alder.Avalonia.AotSmoke -c Release
```

交互回归和 Skia 烟测的默认宿主只加载 `AlderTheme`。Fluent2 共存由独立测试宿主验证。

在 macOS arm64 上验证 NativeAOT 时，使用匹配宿主的 RID 并运行生成的可执行文件。

```sh
dotnet publish tests/Alder.Avalonia.AotSmoke -c Release -r osx-arm64 -p:PublishAot=true
./tests/Alder.Avalonia.AotSmoke/bin/Release/net11.0/osx-arm64/publish/Alder.Avalonia.AotSmoke
```

全库裁剪诊断使用同一条发布命令并追加 `-p:AuditWholeLibrary=true`。该选项将 `Alder.Avalonia` 程序集设为裁剪根，检查烟测未直接使用的代码。常规发布用于测试裁剪后实际保留的控件、主题和交互行为。

[NativeAOT smoke](.github/workflows/nativeaot-smoke.yml) 在 Windows x64 和 Linux x64 上运行常规裁剪后的原生可执行文件。烟测只加载 `AlderTheme`，通过 `--all-controls Light` 和 `--all-controls Dark` 逐个实例化并渲染公开控件和弹层，随后运行交互、像素和绑定回归。

构建、截图探针和本地压测结果可以通过 `--artifacts-path` 或工具自身的输出选项写到工作区外。

## 文档

- [2.0.0 发布与升级说明](docs/releases/2.0.0.md)
- [设计系统](DESIGN.md)
- [组件目录与使用契约](docs/COMPONENTS.md)
- [组件接入指南](docs/COMPONENTS.md#接入指南)
- [Rich Text Editor：构建、保存与重新加载](docs/guides/rich-text-editor.md)

## 许可

本项目采用 [MIT 许可](LICENSE)。第三方资产和代码保留各自的许可，见[资产目录](src/Alder.Avalonia/Assets)中的许可文件。
浏览器版的中文回退字体基于 Noto Sans SC，采用 [SIL Open Font License 1.1](samples/Alder.Avalonia.Gallery.Browser/wwwroot/licenses/NotoSansSC-OFL.txt)。
