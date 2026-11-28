# Alder.Avalonia

[English](NuGet.md) · **简体中文**

Alder.Avalonia 是面向桌面应用的 Avalonia 原生控件库，包含表单、颜色编辑、导航布局、数据视图和 AI 会话组件。目标框架为 .NET 11，依赖 Avalonia 12.2.0。

## 安装

```sh
dotnet package add Alder.Avalonia --version 2.0.1-preview
```

这是预发布版本。新增控件与修复见发布说明。[查看预览版发布说明](releases/2.0.1-preview.zh-CN.md)

加载 `AlderTheme`，桌面窗口使用 `AlderWindow`。如果应用同时使用 Avalonia 自带的窗口或控件，请先加载它们的宿主主题，再加载 `AlderTheme`。

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:alderThemes="using:Alder.Avalonia.Themes">
  <Application.Styles>
    <alderThemes:AlderTheme />
  </Application.Styles>
</Application>
```

在视图中声明 `xmlns:alder="using:Alder.Avalonia.Controls"`，即可使用 `<alder:AlderButton Variant="Primary" Content="Save" />` 等控件。

## 从 1.x 升级

大多数组件的 `Icon` 属性现在接受 `object?`。应用可以传入 Avalonia 控件、图像源或现有的 `Geometry` 资源。请重新编译依赖 1.x 的应用和库。C# 代码需要读取几何图标时，可使用 `component.Icon is Geometry geometry`。窗口装饰继续使用内置几何图标。[2.0.0 发布与迁移说明](releases/2.0.0.zh-CN.md)

## 阿里巴巴普惠体

在 `AppBuilder` 上调用 `WithAlderPuHuiTiFont()` 后，应用默认使用内置的阿里巴巴普惠体 3.0，包含 Regular、Medium、SemiBold 和 Bold 四个字重。显式指定字体族的控件优先使用所指定的字体。省略此调用时，Avalonia 按宿主设置选取字体。

已有的 `FontManagerOptions` 可直接传给扩展方法。其 `DefaultFamilyName` 会在普惠体字形缺失或字体不可用时充当回退。引入 `Avalonia.Fonts.Inter` 的应用可以将 Inter 设置为回退字体。

```csharp
using Avalonia;
using Avalonia.Media;

AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .WithInterFont()
    .WithAlderPuHuiTiFont(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" });
```

- 浏览[组件目录](COMPONENTS.zh-CN.md)。
- 在[桌面 Gallery](https://github.com/LuoVerse/Alder.Avalonia/releases/tag/v2.0.1-preview)中试用交互示例并查看 AXAML 和 C# 源码。
- 本库采用 [MIT 许可](../LICENSE)。包内 `licenses/` 目录包含随包分发的第三方许可声明。
