---
version: alpha
name: Alder.Avalonia
description: Avalonia 原生桌面控件与交互式组件 Gallery 的设计系统。
colors:
  accent: "#0485F7"
  accent-hover: "#3592F9"
  on-accent: "#FCFCFC"
  ink-light: "#18181B"
  ink-dark: "#FCFCFC"
  muted-light: "#71717A"
  muted-dark: "#9F9FA9"
  surface-light: "#FFFFFF"
  surface-dark: "#18181B"
  surface-secondary-light: "#EFEFF0"
  surface-secondary-dark: "#232325"
  neutral-light: "#EBEBEC"
  neutral-dark: "#27272A"
  border-light: "#DDDEE0"
  border-dark: "#28282C"
  separator-light: "#E4E4E7"
  separator-dark: "#212124"
  gallery-preview-light: "#F5F5F5"
  gallery-preview-dark: "#060607"
rounded:
  field: "12px"
  preview: "14px"
  sidebar-row: "16px"
  gallery-example: "20px"
  select-popover: "22px"
  card: "24px"
spacing:
  attached-toolbar-inset: "4px"
  gallery-preview-gap: "8px"
  gallery-preview-padding-x: "24px"
  gallery-preview-padding-y: "20px"
---

## Overview

Alder 是一套原生桌面控件。基础控件、布局壳、复杂数据组件和 AI 会话组件统一由 `Alder.Avalonia` 提供。Gallery 用真实控件展示状态和组合方式。Web 参考用于校对视觉与交互，应用本身使用 C#、AXAML 和 Avalonia 的输入、布局及渲染。

### 新组件参考基线

**Alder 的外观、交互和动效以 HeroUI 和 HeroUI Pro 的 React Web 组件为设计参考。** 先定位对应的组件文档和可操作预览，再确定要移植的变体和场景。[HeroUI React Web](https://heroui.com/en/docs/react/components) · [HeroUI Pro React Web](https://heroui.pro/docs/react/components)

- **有对应的 Web 组件时**，记录文档和预览链接，在实现前实际操作目标状态。核对尺寸、间距、图标、边框、圆角和阴影。对相关交互检查悬停、按压、释放、焦点、开关或拖拽，并核对过渡时长、缓动和减少动效下的状态。Gallery 使用同一组场景验收。
- **没有对应的 Web 组件时**，选取 Alder 中用途和交互最接近的控件，沿用它的语义资源、状态和动效。提交说明注明参考了哪个控件，以及新增行为的依据。任务额外指定的参考应用于其明确要求的场景。

本文的尺寸以 Avalonia DIP 为准。上方 `px` 是设计文档交换格式。运行时数值以 [AlderTheme.axaml](src/Alder.Avalonia/Themes/AlderTheme.axaml)、各组件主题以及 [Gallery App.axaml](samples/Alder.Avalonia.Gallery/App.axaml) 为准。

## Colors

| 角色 | Avalonia 资源 | 应用 |
| --- | --- | --- |
| 强调 | `AlderAccent`、`AlderAccentHover`、`AlderOnAccent` | 主要操作、强调选中态、适用的键盘焦点 |
| 文本 | `AlderInk`、`AlderMuted` | 标题与正文、辅助文案与次要图标 |
| 表面 | `AlderSurface`、`AlderSurfaceSecondary`、`AlderNeutral` | 卡片和浮层表面、次级层与交互填充 |
| 轮廓 | `AlderBorder`、`AlderSeparator` | 容器描边与内容分隔线 |
| Gallery | `GalleryPreviewSurface`、`GalleryCodeSurface` | 预览底色与示例卡／代码区底色 |

模板通过 `DynamicResource` 解析颜色和阴影。用 Light 和 Dark 主题字典校对同一状态。语义色、悬停填充、禁用态和细分隔线各自取自相应模板，组件有专用资源时从该资源入手。

## Typography

Gallery 通过 `WithInterFont()` 加载 Inter。控件模板使用可继承字体，正文和操作标签常用 14 DIP，辅助文字常用 12 DIP。多行内容以实际 `LineHeight` 和换行行为校对，单行表格、选择项和菜单在有限宽度下可截断。对照时固定字体、语言和缩放倍率。

## Layout

控件组合通过内容槽、集合与数据模板实现。Gallery 的一般示例卡内，预览与外壳相隔 8 DIP，内容默认内边距为横向 24 DIP、纵向 20 DIP。需要全幅布局或紧凑预览的示例按各自容器明确设置边距。

Sidebar 和 AppLayout 用实际可用宽度决定布局状态。图标栏保留导航、品牌图标以及启用后的搜索入口。窄幅校对还应包含嵌套滚动、标题缩略、弹层翻转和窗口边缘。

## Elevation & Depth

字段、卡片和设置单元格使用较短的外阴影。浮层和附着工具栏使用范围更大的阴影，并在深色主题中保留细内沿。深色外阴影以柔和的中性近层衔接接近黑色的背景，较宽的黑色层在较亮的深色表面上提供落差。共享卡片只在外层绘制表面，分隔线与子项状态单独控制。

把阴影、键盘环和拖动预览留在能容纳外溢的层。圆角内容裁剪放在最小的内容层，必要时用上层描边覆盖内沿。比较截图时取样背景、描边内外及阴影尾部。

## Shapes

普通字段使用 12 DIP 圆角，Gallery 预览面为 14 DIP，Sidebar 导航行常用 16 DIP。示例卡外壳为 20 DIP，默认 Select 弹层为 22 DIP，默认 AlderCard 为 24 DIP。组内项目、无框布局、圆形图标和细分隔线各有独立形状。尺寸变化通过所属组件主题实现。

## Components

- 普通动作使用 AlderButton。按压反馈复用 AlderPressableFeedback，连续拖拽跟随指针，收尾的回弹或吸附使用组件自身逻辑。
- 按钮在键盘焦点下复用所属组件的悬停反馈或专用焦点样式。内容区的 Tab 顺序跳过窗口标题栏的系统按钮。
- 表单在休息、悬停、编辑与校验状态间保持表面结构。Cell 的选中值与键盘焦点分别呈现。选择触发器、选项行与滑块在自身的圆角表面或手柄内提示键盘焦点。文本编辑和校验保留各自的状态轮廓。
- 弹层由 Alder 的菜单、选择器、对话框或 Popover 承载。校对首次打开、内容滚动、关闭后的焦点、圆角内沿与阴影外沿。
- 短背景或透明度过渡常见 150 ms，部分布局、滑块定位和展开过渡常见 200 ms。准确时长与曲线以组件为准。交互验证包含有效的 `AlderMotion.Reduced` 状态。
- 同时维护 [组件目录](docs/COMPONENTS.md) 和 Gallery 中与预览对应的 AXAML、C# 源码。

## Do's and Don'ts

- 从所属主题、命名部件和当前示例着手。修改共享模板前查看受影响的控件，优先复用已有语义资源和行为。
- 保持鼠标、键盘、可访问名称和 MVVM 状态一致。图标按钮设置可访问名称，主题切换保留所处页面和已有状态。
- 对裁剪回归检查模板和祖先直到窗口或 Overlay，记录失效像素及修复后的取样。视觉对照记录主题、宽度、DPI 和操作阶段。
- 对可删除或隐藏数据的示例提供 Reset。新可见文字同步到 Gallery 的英文与简体中文目录。
