# Alder.Avalonia 设计与 HeroUI 上游对照

> 历史移植记录，基线日期为 2026-11-23。本文保留当时的上游清单、阶段建议和取样结果。当前设计、编译依赖和已实现的 Gallery 示例分别见 [DESIGN.md](../DESIGN.md)、[README.md](../README.md) 和 [组件目录](COMPONENTS.md)。

Alder.Avalonia 是独立的 Avalonia 原生控件库。本文保留最初的 HeroUI React 和 Pro 设计基线，供实现与验收参考。

**Alder.Avalonia 在同一个控件库中实现原生桌面控件，参考 HeroUI React 3.2.8 与 HeroUI Pro React 1.0.0-beta.11 的公开文档。** Button、CheckBox、TextBox 等应保留 Avalonia 的命令、编辑、绑定和自动化行为，用主题还原外观。Select、Sheet、分段日期输入等则需要明确的交互模型。第一轮用同一个编辑对话框串起 Button、TextField、Select 和 Modal，验证输入、选择、焦点和换肤；通过后再批量扩展组件。

设计基线为 2026-11-23。读者是实现控件、制作主题和编写验收用例的开发者。第 1 节记录上游事实，第 2～7 节给出目标设计和验收条件，第 8 节记录本次实际验证。第 4 节覆盖 73 个开源组件文档条目和 65 个 Pro 组件文档条目。Pro 清单可从[图表与其余七个家族](#pro-react-组件对照)进入；两个目录合计 138 项，实际控件类型数取决于可复用的行为与模板。

## 1. 移植基线与证据

| 项目 | 已核对的事实 | 对本项目的影响 |
| --- | --- | --- |
| HeroUI 产品范围 | 官网区分 Web 与 Native，用户给出的六篇技术文档均属于 React。版本 3.2.8 的 React 包含 73 个组件文档、16 篇指南 | 同一套 Alder 组件按控件家族覆盖相关 Web 场景。全页模板仅用于选取集成场景。React Native、Pro v2 另有目录 |
| HeroUI Pro 版本与范围 | 2026-11-23 公开发布页标示 React 1.0.0-beta.11；当日 Web 组件目录列出八类、65 个组件、474 个示例变体。Sheet 已在此版迁入开源包 3.2.7 | 使用公开目录、各组件页和发行说明作基线。Sheet 只计入开源表；移植接口以本库的 Avalonia 契约为准。组件目录 · beta.11 发行说明 |
| 行为 | React 层组合 React Aria Components 等原语。Button 在原语上叠加分组上下文和外观；日期依赖国际化日期模型，OTP 有专门的编辑实现 | 每项要对照输入、状态和无障碍语义。单靠截图无法验收 |
| 外观 | 同版本 styles 包提供变体定义、组件 CSS、默认主题及派生颜色。DESIGN.md 明确以主题 CSS 为视觉依据 | 参数默认值查变体源码，尺寸与动效查组件 CSS，明暗色和派生公式查 variables.css。三者分别取证 |
| 目标工程 | 工作目录原为空目录。Avalonia 12.1.6 包含 net8.0 和 net10.0 资产 | 本设计建议首个库和 Gallery 采用 net10.0。首轮目标为 Windows、macOS、Linux 桌面；11.x、移动端和 WASM 留待消费者需求与平台验证后定案 |

上游入口与可复查版本见 HeroUI 首页、React 3.2.8 发布包、Styles 3.2.8 发布包、Button 实现（`button/button.tsx`）、默认主题源码及 [Avalonia 12.1.6](https://www.nuget.org/packages/Avalonia/12.1.6)。

### 原生实现边界

开源与 Pro 目录都作为视觉和交互参照。以控件外形、状态变化、输入行为和可访问语义定义验收，用 C#、AXAML、Avalonia 属性系统、布局和动画实现。运行、构建和测试均走 .NET 工具链。React 示例和 CSS 中的数值只作为对照材料。图表、地图及富文本后续选型也遵循原生 .NET 边界。

### 查阅上游设计与主题

对照具体版本的组件文档、样式和主题变量，记录语义色、留白、圆角、字体继承和交互状态。全局令牌、单组件覆盖和具名主题分别通过 Avalonia 的主题字典、`ControlTheme` 和 Light/Dark 资源实现。验收覆盖 hover、pressed、focus 和 disabled 状态。

## 2. 库结构与公开 API

`Alder.Avalonia` 统一承载所有控件、默认主题和必要的图形资源。组件按功能家族组织，通过同一套主题资源和命名空间对外提供。地图和富文本后端由可选适配器装配，普通设置页无需加载。Gallery 是开发和验收程序。CSS 取值、颜色转换和 Web 参照图生成放在开发工具内，使用方只需引入 NuGet 和主题入口。暂不引入额外 UI 框架。示意目录如下。

```text
src/Alder.Avalonia/
  Controls/             各控件的行为、属性、事件与必要的自动化 Peer
  Themes/AlderTheme.axaml  对外资源入口
  Themes/Tokens/        Light、Dark、尺寸、排版、动效与阴影
  Themes/Controls/      每个家族的 ControlTheme 与部件样式
  Overlays/             每个 TopLevel 独立的弹层与通知宿主
samples/Alder.Avalonia.Gallery/  可重现的场景、主题、密度及键盘操作
tests/                  行为、资源加载、截图和平台验收
```

**主题的边界。** 宿主继续提供其基础主题。库以具名资源为内置控件提供选择性主题，例如 `AlderButtonTheme`；新增语义或组合行为时才提供控件类型，例如 `AlderTextField`。这些新类型以自己的样式键查找默认模板。库内部用到的编辑器、滚动条、列表项和弹层按钮须指定合适的主题，避免嵌套后混入宿主外观。应用可以在窗口或子树覆盖 Alder 资源。样式作者使用公开属性和文档化的模板部件。[Avalonia 控件主题](https://docs.avaloniaui.net/docs/styling/control-themes) · [模板化控件](https://docs.avaloniaui.net/docs/custom-controls/templated-controls)

| 上游概念 | Avalonia 公开契约（拟定） |
| --- | --- |
| variant、size | 具名主题配合强类型属性。原生 Button 可单独应用具名主题。第一批实现中，AlderButton 从 Button 派生，在控件自身公开 Variant、Size、IsPending，以便在原生 OnClick 入口处理等待态。每个家族只公开上游支持的取值 |
| children、slot、render props | 单内容用 Content/ContentTemplate，多区块用 Header、Content、Footer 或显式插槽。集合提供 ItemsSource、ItemTemplate、容器主题；模板读取容器状态 |
| controlled、default、onChange | 单一可绑定属性同时支持本地值和 MVVM。输入、选择、打开状态支持双向绑定；控件内部用 SetCurrentValue 保留绑定，只有实际变化才发事件。提交、取消和实时编辑分开定义 |
| group、context | 分组仅向所属子项提供默认配置，显式子项设置优先。ButtonGroup 只影响直接按钮，嵌套工具栏里的按钮不串组。Surface 层级可用可继承的附加属性，局部主题作用域另行处理 |
| data-*、ARIA、onPress | 使用内置伪类、属性选择器、Click/Command 和 AutomationPeer。自定义伪类只表达控件已计算的状态，例如 :pending；不要让画刷反向决定状态 |
| className、fullWidth、ref | 分别用 Classes/Theme、布局对齐、具名实例或模板部件。模板不设置 DataContext，业务绑定保持宿主上下文 |

**消费端草案。** 下例展示一个拟建 API 的使用方式。主题入口将合并公开资源；TextField 持有内部编辑器并转发 Text，调用者可以用绑定错误驱动 FieldError。Button Pending 由异步命令所在的 ViewModel 提供单一真值。部件较少的控件无需为了统一前缀再包装一层。

```xml
<!-- App.axaml，放入 Application.Styles，基础主题之后 -->
<StyleInclude xmlns="https://github.com/avaloniaui"
              Source="avares://Alder.Avalonia/Themes/AlderTheme.axaml" />
```

```xml
<!-- 视图内，假定 DataContext 提供 Name、SaveCommand 和 IsSaving -->
<StackPanel xmlns="https://github.com/avaloniaui"
            xmlns:alder="using:Alder.Avalonia.Controls" Spacing="12">
  <alder:AlderTextField Label="项目名称" Description="用于在列表中识别项目"
                      Text="{Binding Name, Mode=TwoWay}" />
  <alder:AlderButton Content="保存" Variant="Primary" IsPending="{Binding IsSaving}"
                   Command="{Binding SaveCommand}" />
</StackPanel>
```

## 3. 视觉与主题契约

**资源分三层。** 基础值保存上游表达式与单位，语义资源描述用途，控件资源只处理组件特有的映射。例如 Primary Button 的背景引用 `AlderAccentBrush`，悬停引用 `AlderAccentHoverBrush`；浮层引用 `AlderOverlayBrush`。浅色下 Surface 和 Overlay 即便同为白色也保留两个键，深色描边、阴影及应用覆盖各自独立。默认变量 · [Avalonia 主题字典](https://docs.avaloniaui.net/docs/how-to/styling-controls-how-to)

| 视觉角色 | 3.2.8 默认主题取值或规则 | Avalonia 设计 |
| --- | --- | --- |
| 页面、正文 | Light 背景 OKLCH(0.9702 0 0)，正文 eclipse；Dark 背景 OKLCH(0.12 0.005 285.823)，正文 snow | Background 与 Foreground 成对取值，分别为正文、弱化文字和反色区域设键 |
| Surface、Field、Overlay | Light 默认填充白色。Dark 默认填充 eclipse；浮层有亮色内缘，表面和字段的常规阴影透明 | 独立填充、前景和阴影。浮层在深色背景上以轮廓维持层级，检查阴影是否被宿主裁剪 |
| Accent、状态与焦点 | Accent 为 OKLCH(0.6204 0.195 253.83)，Focus 默认引用 Accent。Warning、Danger 在两种模式的值不同 | Accent、Success、Warning、Danger 均与相应 Foreground 一起导出；Focus 可独立覆盖。错误态同时给文字或图标 |
| 间距、边框、圆角 | 以默认根字号 16px 换算，基础间距 4、基础圆角 8、字段圆角 12；通用边宽 1，字段默认边宽 0；焦点环偏移 2；禁用透明度 0.5 | 以 DIP 建立 Double、Thickness、CornerRadius 资源。按角色分别配置可见轮廓和实际命中区域 |
| 字体 | HeroUI 继承应用字体。默认按钮中号采用 text-sm 与 medium 字重 | 字体族继承宿主，默认以 14 DIP、Medium 作中号按钮对照。中文、英文、数字混排分别量基线；支持字体替换和文本放大 |
| 动效 | Button 普通按压缩放 0.97，变换 250ms、背景过渡 100ms；Input 背景和边框过渡 150ms。Tooltip 默认打开/关闭延迟 1500/500ms | 分别给交互反馈和提示延时设资源。减少动态效果时即时完成状态切换，进度与加载态保留静态标签 |

**颜色转换决定换肤是否可靠。** 构建时展开基础变量、别名、明暗模式和派生配方，并按源 CSS 声明的空间运算。例：浅色 accent-soft 是 accent 15% 与透明色混合，深色是 12%；accent-hover 用 OKLab 混合，outline 按钮的悬停另用 sRGB。应按 CSS 规则处理权重归一与透明度，转换至 sRGB 后导出 Avalonia 的 #AARRGGBB。用同版本浏览器计算色在浅、深两种底色上对照合成结果。设计稿、文档和 CSS 冲突时，以锁定的组件 CSS 与默认主题为依据。

**自定义主题交付单位。** 内置默认主题可预生成全部资源。品牌主题输入品牌色、前景与形状后，由同一转换步骤重新生成完整的明暗字典，连同 Focus、Hover、Soft 一起交付。运行时换肤通过 ThemeDictionaries 和 DynamicResource 生效。手工只覆盖 AccentBrush 会留下旧的派生画刷，单键覆盖 API 应明确这一效果。弹层开启时切主题也要测试，特别是来自局部 ThemeVariantScope 的触发器。

**Button 的参考规格。** 默认 Primary、Medium，另有 Secondary、Tertiary、Outline、Ghost、Danger、DangerSoft。桌面对照采用上游 md 断点样式，Small/Medium/Large 高度分别为 32/36/40 DIP，较窄布局上游为 36/40/44。桌面提供显式密度选择，默认使用桌面组，窗口变窄时保持用户选择。中号水平内边距 16、内容间距 8、圆角 24。实现以最小高度容纳文本放大；图标按钮平时宽高相等并提供操作名称。验收同时检查单个按钮、相邻分组边角和高 DPI 焦点环。Button 变体定义（`button/button.styles.ts`） · 版本化 Button CSS（`button.css`）

## 4. 开源与 Pro 组件对照

开源部分以发布包内的独立文档为单位，每项只列一次。每个上游名称链接到对应版本的完整示例、部件和属性表。**A** 表示基础控件、表单和首个端到端场景，**B** 表示复用这些基础的组装控件，**C** 表示需要专门模型或平台验证的高级控件。阶段表示依赖顺序。每行最后一列给出最容易在移植中丢失的一项行为。

### 按钮、选择开关与工具栏

| 上游组件 | 阶段 | Avalonia 实现建议 | 验收重点 |
| --- | --- | --- | --- |
| Button（`button.md`） | A | Button 具名主题和附加属性 | 保留 Click、Command、CanExecute；Pending 期间阻止重复执行并保持焦点 |
| ButtonGroup（`button-group`） | B | 原生分组容器、外轮廓与可选分隔线 | 横纵方向和可见项变化时更新首尾圆角。支持全宽等分和按钮级禁用覆盖 |
| CloseButton（`close-button.md`） | A | Button 的图标主题 | 无文字时提供可本地化的“关闭”名称，支持键盘触发 |
| ToggleButton（`toggle-button`） | A | AlderToggleButton 和原生 ToggleButton 主题 | IsChecked 可双向绑定，沿用原生切换的自动化语义 |
| ToggleButtonGroup（`toggle-button-group.md`） | B | ItemsControl 与共享选择模型 | 支持单选、多选、禁用项，切换时只发一次选择变化 |
| Checkbox（`checkbox`） | A | AlderCheckBox 和原生 CheckBox 主题 | 三态、外置标签、只读与错误提示。半选态可通过原生自动化接口查询 |
| CheckboxGroup（`checkbox-group.md`） | B | 分组容器与可绑定值集合 | 组级必填错误与单项禁用独立，移除选项后校正选择 |
| RadioGroup（`radio-group.md`） | B | 分组容器和 AlderRadioButton 子项 | 相邻组互不影响，方向键移动并选中，跳过禁用项 |
| Switch（`switch.md`） | A | AlderSwitch，含标签与说明插槽 | 开关名称稳定，状态可由键盘和读屏读取 |
| Slider（`slider.md`） | C | 单值使用 AlderSlider，范围模式共享 Track 和两个 AlderThumb | 每柄可独立聚焦和步进，区分上下界，校验范围、刻度和垂直方向 |
| Toolbar（`toolbar.md`） | B | 工具栏容器与组内方向键导航 | Tab 进出一组，横纵方向键不抢占嵌入编辑器的输入 |

#### 本批实现契约

业务程序集位于 `src/Alder.Avalonia`，只引用 Avalonia。Gallery 单向引用该程序集，代码预览与着色仍属于 Gallery。在应用基础主题之后加入 `<themes:AlderTheme />`，其中 themes 对应 `using:Alder.Avalonia.Themes`。原生控件通过具名主题选择外观，专用类型通过默认主题工作。

| 家族 | 消费接口与视觉 | 输入与反馈 |
| --- | --- | --- |
| 按钮与关闭按钮 | AlderButton 支持七种 Variant、三种 Size；原生 Button 可用 AlderButtonTheme 或 24 DIP 的 AlderCloseButtonTheme | 单按钮按下时缩放，松开复原。IsPending 阻止 Click/Command，保留原位置和焦点 |
| 按钮组 | AlderButtonGroup 的 Size、Variant 提供组级默认值。FullWidth 在横向平分可用宽度，在纵向填满宽度。Joined 控制拼接形状，ShowSeparators 控制分隔线，AlderButtonGroup.ShowSeparator 可覆盖当前按钮前的分隔线 | IsDisabled 为直接按钮提供禁用默认值，局部 IsEnabled 可覆盖。每个按钮保留自己的焦点和命令。组内按压用色调反馈以保持接缝稳定 |
| 切换按钮 | AlderToggleButton 支持 Default、Ghost、三种 Size、Icon、IsIconOnly 和 FullWidth。现有 ToggleButton 可显式使用 AlderToggleButtonTheme | IsChecked 提供初始选中状态和双向绑定。Space、Enter 与指针激活沿用原生 ToggleButton。选中底色过渡 100ms，减少动效时直接切换 |
| 复选框 | AlderCheckBox 支持 Primary、Secondary、说明、错误提示和自定义指示器。CheckedIcon 为空时使用默认描边动画，勾选延迟 15 ms 后绘制 150 ms，取消时用 200 ms 收回 | IsChecked 为可双向绑定的可空布尔值，null 表示半选。默认激活半选项会选中，启用 IsThreeState 时按原生顺序循环。IsReadOnly 阻止用户修改，应用通过 IsRequired 和 IsInvalid 表达校验状态 |
| 单行输入 | AlderInput 基于原生 TextBox，支持 Primary、Secondary、FullWidth、文本、邮件和密码。数字输入使用 NumericUpDown 与 AlderNumericInputTheme | Text 默认双向绑定，原生编辑器处理选区、剪贴板、撤销和输入法。IsInvalid 控制错误外观，校验规则由应用提供。焦点圈在 150 ms 内同时改变宽度和透明度，支持减少动效 |
| 输入组 | AlderInputGroup 继承 AlderInput，Prefix 和 Suffix 接收文本、图标或可操作控件。AcceptsReturn 启用多行编辑，前后缀与首行对齐 | 编辑器获得焦点时整个输入组显示焦点圈。后缀按钮有独立的 Tab 焦点。输入组共享 AlderInput 的外观、校验状态和减少动效设置 |
| 验证码输入 | AlderInputOtp 使用一个原生编辑器呈现分格。MaxLength 默认 6，GroupSize 默认 3，设为 0 可取消分组。Pattern 可限制编辑内容，PasteTransformer 可清理粘贴格式 | Text 双向绑定，值从未填满变为填满时调用 CompletedCommand。当前格显示焦点圈，字符以 250 ms 淡入并上移。开启减少动效时立即到达目标状态 |
| 数字字段 | AlderNumberField 继承 NumericUpDown。默认左右加减，VerticalStepper 将上下箭头置于右侧。FormatString 和 NumberFormat 控制货币、百分比与单位格式，InnerRightContent 可放置独立后缀 | Value 为可空 decimal。百分比的 `0.5` 显示为 `50%`。原生编辑器处理本地化解析、键盘和重复步进。只读时仍可聚焦与复制 |
| 搜索字段 | AlderSearchField 继承 AlderInput，共用主题、焦点圈和两个附加内容槽。内置搜索与清除图标，允许替换 | Enter 将当前字符串传给 SubmitCommand，输入法组字期间不提交。Escape 与清除按钮清空文本并聚焦编辑器。空值和只读状态隐藏清除按钮 |
| 多行文本 | AlderTextArea 继承 AlderInput。Rows 控制初始行数，ResizeMode 控制调整方向。支持 Primary、Secondary 和 FullWidth | 原生 TextBox 处理换行、选区、IME、撤销和滚动。可用指针拖动右下角调整视口，受最小和最大尺寸约束 |
| 日期与时间字段 | AlderDateField 使用 DateOnly?，AlderTimeField 使用 TimeOnly?。Culture 决定字段顺序和本地历法。TimeField 支持 12/24 小时、分钟和秒 | 左右键切换分段，上下键步进，支持直接键入、粘贴和范围检查。无效日期保留已提交值，应用可读取 HasInvalidInput |
| 日期与区间日历 | AlderCalendar 和 AlderRangeCalendar 共用本地化网格，支持日、周、月、多月、年份网格和日期指示点 | DisplayDate、FocusedDate 和选中值独立。AlderDateRange 储存有序的闭区间，RangeCalendar 首次点选只建立草稿，可限制不可用日和区间终点 |
| 日期选择器 | AlderDatePicker 和 AlderDateRangePicker 使用分段字段与原生日历弹层。弹层打开与关闭分别使用 150 ms 和 100 ms 的合成动画 | 键入与日历选择更新同一个 Value。完成区间选择才提交，Escape 关闭并放弃弹层草稿，焦点回到触发按钮 |
| 字段标签 | 在原生 Label 上显式使用 AlderLabelTheme，通过 Target 关联字段。主题读取字段的必填、禁用和错误状态 | 点击标签与访问键沿用原生 Label。字段通过 AutomationProperties.LabeledBy 获取可访问名称。AlderLabel.IsRequired 和 AlderLabel.IsInvalid 可覆盖呈现状态，必填语义由字段的 AutomationProperties.IsRequiredForForm 提供 |
| 选择 | ToggleButton、CheckBox、RadioButton 对应 AlderToggleButtonTheme、AlderCheckBoxTheme、AlderRadioButtonTheme。三类容器分别是 AlderToggleButtonGroup、AlderCheckboxGroup、AlderRadioGroup；Radio 与 Checkbox 的 card 类用于 Pro 卡片式选择 | 直接子项的 `AlderChoiceGroup.Key` 是稳定标识。SelectedKeys 是双向绑定的选择快照，宿主更新时替换集合。checkbox 支持三态；必选组清空会更新 IsInvalid。切换单选只发一次组值变化 |
| 单选组 | AlderRadioGroup 支持横向、纵向和 Secondary。AlderRadioButton.Description 提供选项说明，Indicator 可替换选中指示器 | 方向键跳过禁用选项，只读组允许移动焦点并保留当前值。标签关联整个组，外部校验可设置 HasValidationError |
| 开关 | AlderSwitch 默认使用 AlderSwitchTheme。中号轨道 40 × 20 DIP、白色滑块 22 × 16 DIP；small、large 样式类切换尺寸。复杂描述可用 ContentTemplate 或组合设置行 | 保留 ToggleSwitch 的指针拖动、Space 和 IsChecked 双向绑定。轨道变色 250ms，滑块位置过渡 300ms；开启减少动效时直接到达目标状态 |
| 滑块 | AlderSlider 默认使用 AlderSliderTheme；AlderRangeSlider 的 LowerValue、UpperValue 分别双向绑定，Minimum、Maximum、Step 和 Orientation 控制量程 | 两端分别是能独立聚焦和报告数值的 AlderSlider。点轨道选最近端点，拖动捕获指针，键盘按步长调整；端点不交叉。Gallery 将两个绑定值显示在轨道旁 |
| 工具栏 | AlderToolbar 是有 ToolBar 自动化角色的横纵向容器。可组合普通按钮、独立切换按钮和输入控件 | Tab 把工具栏作为一组进出，沿排列方向的方向键移动焦点，跳过禁用项。文本编辑器保留光标键；横向 RTL 按视觉方向移动 |

**两项取舍。** 组内首尾形状、范围滑块的指针命中和焦点策略由控件负责，业务视图只提供键、值和命令。减少动效通过可继承的 `AlderMotion.Reduced` 设置到窗口或示例根节点，宿主可将系统偏好映射到该属性。品牌主题通过 Light/Dark 语义画刷切换。

**复现。** 安装 .NET 11 SDK。当前方案使用 Avalonia 12.2.0，组件库、Gallery、交互测试和 NativeAOT 烟测均面向 `net11.0`。用 `dotnet run --project samples/Alder.Avalonia.Gallery` 打开示例。Button 页面包含 Button、ButtonGroup。Input 页面包含 Input、InputGroup、InputOTP、NumberField、SearchField、TextArea。Selection 页面分别展示 Selection 和 RadioGroup。Date and Time 页面展示 Calendar、DateField、DatePicker、DateRangePicker、RangeCalendar 和 TimeField。窗口宽度低于 720 DIP 时改用顶部横向导航。标题栏的月亮／太阳按钮切换整个窗口的明暗主题。内容区可选择减少动效。卡片右上角独立切换当前示例的预览、AXAML、C#。在 Rider 打开根目录的 `Alder.Avalonia.slnx`，选择 `Alder.Avalonia.Gallery` 运行。用 `dotnet test Alder.Avalonia.slnx -c Release` 验证原生输入与状态回归。截图比较以锁定的字体、缩放和输入状态为前提。三个桌面后端的真实辅助技术及逐帧动效仍需分别验收。

Gallery 的 `Views/` 保存窗口和示例 AXAML，`ViewModels/` 保存示例状态与命令。ViewModel 使用 `ReactiveUI.Avalonia 12.1.6` 和 .NET 10 的 `field` 属性写法。预览卡按类型的完整名称读取嵌入资源，并显示相应的 AXAML 和 ViewModel C#。

日期与时间组件使用 `.NET` 的本地历法。Gallery 的预约示例在宿主中组合日期和时间。需要命名时区或夏令时校验时，由宿主用 `TimeZoneInfo` 处理。

启用 `AlderTheme` 后，可直接引用 `AlderIconPlus`、`AlderIconChevronDown`、`AlderIconTrash` 等常用图形。标准 16 DIP 图标可使用 `AlderIcon16Theme`，控件的 `Icon` 属性也能直接引用同一资源。

```xml
<PathIcon Theme="{StaticResource AlderIcon16Theme}" Data="{StaticResource AlderIconPlus}" />
```

Gallery 使用英文示例和状态文案。预览区与代码区分别取官网演示的 sRGB 底色。浅色模式为 `#F5F5F5` 和 `#EDEDEE`，深色模式为 `#060607` 和 `#17171A`。代码区的文本、行号和 AlderCodeBlock 的语法颜色随示例主题切换。官网演示（`button`）

#### ButtonGroup 的演示

Gallery 展示七种外观、三种尺寸、横纵排列、文字与图标混排、纯图标等分、禁用覆盖、隐藏分隔线及自定义样式。拆分按钮左侧执行命令，右侧通过 `AlderMenuFlyout` 打开原生菜单。菜单以靠近触发按钮的边缘为原点，在合成器上执行 150 ms 的进入动效和 100 ms 的退出动效。菜单开始退出时停止接收点击，开启 Reduce motion 时直接显示或关闭。HeroUI ButtonGroup（`button-group`）

#### Toolbar 和 Slider 的演示

Toolbar 展示横向、纵向、附着、混合按钮组和自定义样式。附着样式通过 `AlderAttachedToolbarSurfaceTheme` 为外层 Border 提供 4 DIP 内边距、圆角和明暗阴影。Tab 进入工具栏后，方向键可跨过分隔线进入下一组。示例的格式按钮操作同一段文本，复制、剪切、撤销、重做和对齐按钮提供可操作的反馈。HeroUI Toolbar（`toolbar`）

Toolbar 的纯图标按钮在桌面端为 36 × 36 DIP。Gravity UI 的图标按原始 16 × 16 坐标绘制。Undo 和 Redo 使用 Inter Medium 14 DIP，按钮组内分隔线长 18 DIP，组间分隔线使用独立的明暗资源。自定义样式的选中背景在悬停时保持强调色。实测横向、纵向、附着和自定义示例尺寸与 Chrome 相同，文字按钮组的总宽度有小于 1 DIP 的布局取整差异。[Gravity UI Icons](https://gravity-ui.com/libraries/icons) · HeroUI Toolbar（`toolbar`）

单值和范围 Slider 使用同一套绘制轨道。轨道厚 20 DIP，滑块外层为 28 × 20 DIP，白色内芯为 24 × 16 DIP。纵向使用转置尺寸。拖动时缩放内芯，轨道填充保持连接。Gallery 展示水平、步进与刻度、范围、纵向和禁用状态。HeroUI Slider（`slider`）

#### 组件配色取样

Primary 的主题资源使用 sRGB `#0485F7`。在带显示器 ICC 的并排截图中，Chrome 将它转换为 `#3A83EF`，修正前的 macOS Gallery 则将原始 sRGB 数值直接送到显示器。Gallery 现在将原生窗口和 Metal 渲染层标记为 sRGB，由系统按当前显示器配置转换。在同一台 Mac 上复测，Gallery 和 Chrome 的显示截图均为 `#3A83EF`。复选框组选项与单选卡片的选中背景按官网公开示例使用 10% 的 accent。ToggleButton 保留官网 accent-soft 的主题差异，Light 为 15% 并在悬停时升到 20%，Dark 为 12% 并在悬停时升到 16%。关闭按钮使用 muted 前景，Light 为 `#71717A`，Dark 为 `#9F9FA9`。开关打开后悬停时，轨道从 `#0485F7` 过渡到 `#3592F9`。复选框和单选框的默认边缘采用字段阴影，校验错误仍用危险色描边。CheckboxGroup（`checkbox-group`） · RadioGroup（`radio-group`） · ToggleButton（`toggle-button`） · CloseButton（`close-button`） · Switch（`switch`）

#### Button 的桌面反馈与性能

在 Chrome 桌面视口实际操作按钮后，测得小、中、大按钮高度分别为 32、36、40 CSS 像素，按住后缩放分别为 0.98、0.97、0.96。缩放过渡 250 ms，曲线为 cubic-bezier(0.25, 0.1, 0.25, 1)。悬停颜色过渡 100 ms，曲线为 cubic-bezier(0, 0, 0.2, 1)。点击上传后进入等待态，按住状态示例时其状态标记变化，涟漪只出现在专门的组合示例。HeroUI Button 官方演示（`button`）

原生模板按相同数字使用 DIP。按压缩放施加在内表面，提示色使用独立图层的透明度。两者都交给 Avalonia 合成器插值，重复使用同一合成器的动画定义。控件本身保留 Button 的键盘、焦点和命令行为。主要、次要、普通和危险按钮的悬停画刷按官网 sRGB 渲染值设置。轻量按钮悬停时铺满中性色，描边按钮使用 60% 的中性色覆盖，Danger Soft 从 15% 过渡到 20% 的危险色覆盖。减少动效时即时显示目标颜色，并保持原尺寸。

| 示例 | 原生使用方式 |
| --- | --- |
| 图标、全宽 | AlderButton.Icon 接收 Geometry 并渲染 16 DIP 的前置图标。纯图标使用 IsIconOnly 和 AutomationProperties.Name，全宽按钮使用 FullWidth |
| 异步操作 | Gallery 的 Upload File 点击后显示 Uploading... 约两秒，随后还原。IsPending 将透明度降为 0.5，使用旋转指示器替换图标槽，保留焦点并忽略后续激活。旋转周期为 750 ms，由合成器执行，减少动效时显示静态指示器 |
| 自定义反馈 | 通过 :pressed 更换内容，通过画刷和 CornerRadius 自定义外观。IsRippleEnabled 在指针位置扩散，键盘从中心触发 |
| 社交按钮 | Gallery 用 GitHub 登录外观演示全宽、图标和本地命令反馈 |

macOS arm64 的 Release 桌面窗口中放置 36 个按钮，每 7 次帧回调同时翻转按压和悬停样式，预热 60 帧后采集 240 个 UI 帧回调间隔。基线中位数 16.66 ms、P95 17.62 ms，改动后分别为 16.71 ms 和 17.14 ms。同一段采样的 UI 线程分配从 17,724,768 字节降至 3,820,648 字节。单机单轮结果主要支持 UI 线程分配下降。GPU 呈现时序、点击到显示的延迟、等待指示器与涟漪负载、Windows/Linux 后端需另行测量。

### 字段与表单

| 上游组件 | 阶段 | Avalonia 实现建议 | 验收重点 |
| --- | --- | --- | --- |
| Label（`label.md`） | A | Label 及字段关联 | 点击标签将焦点交给实际编辑器，生成单一可访问名称 |
| Description（`description.md`） | A | 说明文字与 HelpText 关联 | 长说明换行，焦点进字段能获得说明 |
| ErrorMessage（`error-message.md`） | A | 可复用的错误文字呈现器 | 能独立显示业务错误，颜色之外有文字信息 |
| FieldError（`field-error.md`） | A | 绑定 DataValidationErrors 的字段错误呈现器 | 清错后公告同步消失，多条错误顺序稳定 |
| Fieldset（`fieldset.md`） | B | 带标题和组内容的容器 | 组名可访问，组级禁用作用于内部交互项 |
| Form（`form.md`） | A | 表单容器、验证汇总与提交命令 | 提交定位首个无效字段，异步提交期间抑制重复操作 |
| Input（`input`） | A | AlderInput、AlderInputTheme 和 AlderNumericInputTheme | 原生插入点、选区、撤销、IME 和粘贴。数字输入使用原生范围与步进 |
| InputGroup（`input-group.md`） | A | 前缀、编辑器、后缀的组合外框 | 共享一圈焦点反馈；可操作后缀有独立名称和焦点 |
| InputOTP（`input-otp.md`） | C | 单一真实编辑器加分格呈现器 | 整串粘贴、范围删除、回填、完成事件只触发一次 |
| NumberField（`number-field.md`） | B | NumericUpDown 主题与字段壳 | 允许负号等中间文本，按 CultureInfo 提交，处理步长和边界 |
| SearchField（`search-field.md`） | B | 带清除命令的单行字段 | 清除后焦点回编辑器，空值时清除按钮退出焦点序列 |
| TextArea（`text-area.md`） | A | `AlderTextArea` 多行编辑与缩放手柄 | Enter 换行，长文滚动，表单默认提交不抢占换行 |
| TextField（`text-field.md`） | A | 标签、编辑器、说明、错误的模板化控件 | Text、只读、必填、错误与内部编辑器同源，换模板后绑定仍在 |

### 日期与时间

| 上游组件 | 阶段 | Avalonia 实现建议 | 验收重点 |
| --- | --- | --- | --- |
| Calendar（`calendar.md`） | C | `AlderCalendar` 月、周、日视图与日期网格 | 可用日期、键盘焦点日与选中日独立，支持多月和多选对照 |
| DateField（`date-field.md`） | C | 按区域设置排序的分段编辑器 | 月/日/年分段输入、未完成值与闰日分别处理 |
| DatePicker（`date-picker.md`） | C | DateField、Calendar 与锚定弹层 | 键入或点选走同一提交路径，取消后恢复旧值 |
| DateRangePicker（`date-range-picker.md`） | C | 两个分段字段与范围日历 | 开始/结束共享一次提交，半填区间保持为草稿 |
| RangeCalendar（`range-calendar.md`） | C | 共享日期网格与区间选择模型 | 跨月范围、反向点选、不可用日及端点均有反馈 |
| TimeField（`time-field.md`） | C | 时间分段编辑器 | 同值切换 12/24 小时显示，检查秒、午前午后与空值 |

### 导航

| 上游组件 | 阶段 | Avalonia 实现建议 | 验收重点 |
| --- | --- | --- | --- |
| Accordion（`accordion.md`） | B | ItemsControl、展开项及标题按钮 | 容器统一处理单开/多开，折叠区子控件退出焦点序列 |
| Breadcrumbs（`breadcrumbs.md`） | B | 路径集合、链接与当前项模板 | 当前项可辨识，路径过长时仍能键盘访问每一级 |
| Disclosure（`disclosure.md`） | B | Expander 主题 | 展开状态可绑定；带焦点内容被折叠时返回标题 |
| DisclosureGroup（`disclosure-group.md`） | B | 多个 Disclosure 的共享展开模型 | 受控展开项与单项切换一致，可指定多展开 |
| Link（`link.md`） | A | HyperlinkButton 主题或绑定导航命令 | 内部导航交给宿主，外部链接有可读名称和键盘入口 |
| Pagination（`pagination.md`） | B | 页码模型与上一页、下一页按钮 | 首尾禁用、跳页省略、总页数缩小后修正当前页 |
| Tabs（`tabs.md`） | B | TabControl 与 TabItem 主题 | 选中页、焦点、横纵布局与溢出可见性分别验证 |

### 弹层与通知

桌面主窗体可以使用 `AlderWindow`（继承 Avalonia 的 `Window`）。加载 `AlderTheme` 后，每个窗口都会提供独立的默认 `DialogHost` 和 `ToastHost`。窗口打开后，可直接调用 `Dialog.ShowModalAsync(..., owner: this)`、`Drawer.ShowAsync`、`Sheet.ShowAsync`、`MessageBox.ShowAsync` 或 `Toast.Show`。同一窗口还可以通过 `HostId` 添加命名弹层区域，或用 `Toast.Register(window, "jobs")` 添加独立队列。

使用自定义标题栏的窗口可以设置 `ShowTitleBar="False"`，并将内容里的可拖动区域标为 `WindowDecorationProperties.ElementRole="TitleBar"`。系统按钮保留在窗口模板中。系统负责最大化和还原窗口，标题栏图标提供简短的状态反馈。设定 `AlderMotion.Reduced="True"` 或 `AnimateWindowState="False"` 可关闭图标动效。

| 上游组件 | 阶段 | Avalonia 实现建议 | 验收重点 |
| --- | --- | --- | --- |
| AlertDialog（`alert-dialog.md`） | B | 共用模态宿主并提供标题、说明与明确的操作 | 危险操作默认聚焦安全按钮；Escape 遵守关闭策略 |
| Drawer（`drawer.md`） | B | 贴边的模态面板 | 限制面板内焦点，滚动内容时标题和操作仍可到达 |
| Modal（`modal.md`） | A | TopLevel 内的对话框宿主 | 阻止背景输入，支持嵌套、关闭取消、焦点返回和窗口关闭 |
| Popover（`popover.md`） | A | 锚定 Popup 与触发器 | 靠屏幕边缘翻转，滚动、切主题、移除锚点时正确更新 |
| Sheet（`sheet.md`） | C | 支持拖动和停靠点的模态面板 | 支持部分展开、嵌套、仅手柄拖动，以及滚动与拖动的交接 |
| Toast（`toast.md`） | B | 每窗口通知队列和非模态呈现器 | 不抢焦点；悬停或聚焦时暂停超时，操作后只关闭一次 |
| Tooltip（`tooltip.md`） | B | ToolTip 主题与时序配置 | 指针和键盘均可触发，Escape 关闭，提示文字不成为唯一标签 |

### 集合与选择器

| 上游组件 | 阶段 | Avalonia 实现建议 | 验收重点 |
| --- | --- | --- | --- |
| Dropdown（`dropdown.md`） | B | MenuFlyout 或菜单原语与可选项、分组及子菜单 | 命令激活、选中标记、子菜单左右键与焦点返回各自独立 |
| ListBox（`list-box.md`） | A | ListBox 和 ListBoxItem 主题 | 焦点游标、单选/多选、禁用、组标题与虚拟化分开处理 |
| TagGroup（`tag-group.md`） | B | 可聚焦的标签集合与删除命令 | 删除中间项后焦点到相邻标签，空组仍可退出 |
| Autocomplete（`autocomplete.md`） | B | 选择触发器、弹层内 SearchField 和可过滤的 ListBox | 搜索文本与已选键独立；多选、异步查询、分组与空态可组合 |
| ComboBox（`combo-box.md`） | C | 常驻可编辑字段与建议列表 | 自由文本、选中项、多选和 IME 提交各有明确定义 |
| Select（`select.md`） | A | 单选触发器、Popover 与 ListBox；多选复用选择模型 | 关闭状态呈现选值，支持清空；移动高亮与提交分别测试 |

### 反馈、布局与显示

| 上游组件 | 阶段 | Avalonia 实现建议 | 验收重点 |
| --- | --- | --- | --- |
| Alert（`alert.md`） | A | 含状态图标、标题、说明的内联容器 | 内容进入阅读顺序，状态含义有文字，并控制公告频率 |
| Meter（`meter.md`） | B | 只读的范围值指示器与专用语义 | 报告数值和量程，例如容量使用率，保留量度用途 |
| ProgressBar（`progress-bar.md`） | A | ProgressBar 主题 | 确定进度可读出范围和数值；未知进度有独立状态 |
| ProgressCircle（`progress-circle.md`） | B | 弧形绘制与同一进度模型 | 0%、100% 和不确定状态可区分，读屏获得相同进度 |
| Skeleton（`skeleton.md`） | B | 占位呈现器 | 保留布局尺寸并从语义树隐藏装饰节点，减弱动画后仍有占位 |
| Spinner（`spinner.md`） | A | 旋转指示器与加载上下文 | 单独使用时提供忙碌名称，嵌入按钮时避免重复朗读 |
| Card（`card.md`） | A | Header、Title、Description、Content、Footer 插槽 | 省略区块时收起间距，嵌套表单不会出现双重内边距 |
| Separator（`separator.md`） | A | Separator 主题 | 横纵方向及高 DPI 细线稳定；纯装饰分隔线跳过朗读 |
| Surface（`surface.md`） | A | 传递表面层级的 ContentControl | 支持 Default、Secondary、Tertiary、Transparent 及局部覆盖 |
| Avatar（`avatar.md`） | B | 图像、占位和文本回退的模板 | 加载失败或地址变化时正确回退，迟到的旧图不覆盖新图 |
| AvatarGroup（`avatar-group.md`） | B | 头像集合与叠放、溢出计数模板 | 检查重叠顺序、最大可见数和额外成员的可访问说明 |
| Kbd（`kbd.md`） | B | 快捷键组合的文本模板 | 按平台显示修饰键，视觉符号对应可读名称 |
| Typography（`typography.md`） | A | TextBlock 样式族与行内文字部件 | 区分标题层级、正文、代码，检查中文回退与多行行高 |
| Badge（`badge.md`） | B | 锚点与角标双部件模板 | 数量、圆点、四角和 RTL；计数变化不遮住触发器命中区 |
| Chip（`chip.md`） | A | 状态标签与内容插槽 | 文本、图标与颜色共同传意；需要可选可删行为时使用 TagGroup |
| Table（`table.md`） | C | 列定义、可回收行容器与表头模型 | 覆盖排序、选择、调列宽、展开、分页/加载更多和虚拟化 |
| ScrollShadow（`scroll-shadow.md`） | B | ScrollViewer 外围的渐隐遮罩 | 只有仍可滚动的一端显示，遮罩不拦截滚轮或点击 |

### 颜色

| 上游组件 | 阶段 | Avalonia 实现建议 | 验收重点 |
| --- | --- | --- | --- |
| ColorArea（`color-area.md`） | C | 二维通道面板和可聚焦游标 | 指针定位与键盘细调共享颜色模型，提供非拖动输入途径 |
| ColorField（`color-field.md`） | C | 可编辑颜色字符串与解析/格式化器 | 允许暂时无效的输入，提交时报告错误并保留可修改文本 |
| ColorPicker（`color-picker.md`） | C | 共享颜色值、触发器、弹层和通道控件 | 任一子控件改变后其他视图同步，取消可回到已提交色 |
| ColorSlider（`color-slider.md`） | C | 单通道 Slider 与渐变轨道 | 色相、透明度等通道的范围、文本值和键盘步长正确 |
| ColorSwatch（`color-swatch.md`） | B | 颜色样本呈现器 | 透明色以棋盘底区分，另有可访问的颜色名称 |
| ColorSwatchPicker（`color-swatch-picker.md`） | C | 可选择的色样集合 | 单选语义、方向键导航、选中标记与颜色值绑定一致 |

### 独立文档以外的导出

实施清单还须跟踪组合部件。根导出的 Menu、MenuItem、MenuSection 归 Dropdown；ListBoxItem、ListBoxSection、Header、EmptyState 归集合；Radio、Tag、SwitchGroup 归各自家族；CalendarYearPicker 归 Calendar。DateInputGroup、ColorInputGroup 的输入区、前后缀等按公开组合路径映射。头像溢出数、表头调宽柄、日期段等应能在模板中替换。React Aria 再导出的 Provider、Hook 和类型按它们提供的能力映射，例如区域设置、选择、过滤和虚拟化。后续每发布一个家族，应把该版本文档的 API Reference 和实际导出逐项标为“已支持／有平台差异／待实现”。版本化组件导出（`index.d.ts`） · React Aria 再导出（`rac/index.d.ts`）

### Pro React 组件对照

以下名称、链接和示例数来自 2026-11-23 的官方 Web 目录。示例数是验收参考，例如 Agenda 的目录卡片计 1 个示例，其组件页仍包含多个日历视图、手势和时间设置。每行的 Avalonia 方案及验收动作是本项目的设计建议。Q1、Q2、Q3 分别对应第 7 节的桌面基础组合、共享数据与消息模型、专用引擎与平台交互。实施时按页建变体记录，同一控件可以覆盖多个示例。目录快照来源 · 官方机器可读索引

#### 图表（8 项）

| Pro 组件 | 上游示例数 | 阶段 | Avalonia 实现建议 | 代表性验收用例 |
| --- | ---: | --- | --- | --- |
| Area Chart（`area-chart`） | 6 | Q2 | 共用直角坐标系，面积填充支持堆叠及迷你图 | 在含缺失值的两条序列间切换是否连线，填充与提示值均与源数据一致 |
| Bar Chart（`bar-chart`） | 8 | Q2 | 共用类别轴、量轴和分组/堆叠布局，横向时交换轴 | 混合正负值时以零线为界，切换方向后命中同一类别 |
| Chart Tooltip（`chart-tooltip`） | 7 | Q2 | 从图表的活动数据点生成指示器、标签和值；弹层复用 Popover | 用键盘和指针命中同一点得到相同值，靠边翻转不遮住当前点 |
| Composed Chart（`composed-chart`） | 5 | Q2 | 不同系列共用绘图区，柱、线和面积各自绘制并参与命中 | 一类目上多系列共存，隐藏其中一层后图例、提示和范围同步 |
| Line Chart（`line-chart`） | 10 | Q2 | 多系列折线与 sparkline 共用坐标变换和裁剪 | 相邻数据点密集时提示对准原始值，无数据段按配置断开 |
| Pie Chart（`pie-chart`） | 7 | Q2 | 按占比生成扇区，嵌套环分别布局标签及命中区域 | 总值为零显示空态，内环与外环在同一角度命中各自数据 |
| Radar Chart（`radar-chart`） | 5 | Q2 | 极坐标轴按维度布局，多系列共享尺度 | 键盘逐维读出维度、系列和值；窄窗口标签可辨认 |
| Radial Chart（`radial-chart`） | 5 | Q2 | 由量程和起止角绘制弧段，中心文字单独排版 | 零值、满值及不完整圆环的进度与朗读值一致 |

#### 数据展示（18 项）

| Pro 组件 | 上游示例数 | 阶段 | Avalonia 实现建议 | 代表性验收用例 |
| --- | ---: | --- | --- | --- |
| Agenda（`agenda`） | 1 | Q3 | 按日、周、月布局事件，时区、可见范围、时间格式独立于数据源 | 同一重叠事件在三种视图可定位；拖动跨日、取消、已过期和已拒绝状态均可复现 |
| Action Bar（`action-bar`） | 2 | Q1 | 在窗口浮层呈现所选项和批量命令，复用 Toolbar | 清空选择后收起并归还焦点，批量命令不会因浮层关闭执行两次 |
| Carousel（`carousel`） | 6 | Q1 | 选择模型协调主视图、缩略图、圆点与前后按钮 | 点击缩略图、键盘翻页、缩减集合后均定位到有效项 |
| Data Grid（`data-grid`） | 14 | Q3 | 扩展开源 Table 的行列键、选择与加载模型，增加固定列、拖动重排和粘性表头 | 横向滚动时固定列对齐；虚拟化开关、页尾加载和重排后保持选中行键 |
| Empty State（`empty-state`） | 8 | Q1 | 复用基础 EmptyState 插槽，增加图标、标题、说明与可选操作的组合模板 | 空列表出现操作按钮；结果到达后恢复内容和合理焦点 |
| File Tree（`file-tree`） | 11 | Q3 | 树节点以稳定键标识，展开、选择、复选与拖放分别建模 | 移动已选子树后选择跟随节点；拒绝拖入自身后代，取消后层级不变 |
| Floating TOC（`floating-toc`） | 10 | Q1 | 将文档标题键与 ScrollViewer 锚点关联，滚动更新活动项 | 点击目录后重排正文或放大字体，当前标题及滚动定位仍一致 |
| Holo Card（`holo-card`） | 8 | Q3 | 在 Card 上叠加指针坐标驱动的倾斜、纹理和高光效果，静态绘制作为降级 | 指针离开恢复静态；关闭动效、软件渲染或无指针时文字和操作仍可用 |
| Hover Card（`hover-card`） | 6 | Q1 | 锚定预览复用 Popover，管理触发器与内容间的延迟关闭 | 从锚点移入预览可继续阅读，键盘可打开，Escape 归还焦点 |
| Kanban（`kanban`） | 4 | Q3 | 列与卡用稳定键，拖动预览独立于已提交的排序，提供键盘移动命令 | 跨到空列、取消拖动、宿主拒绝移动时，顺序、焦点和公告一致 |
| Item Card（`item-card`） | 12 | Q1 | 以 Header、说明、图标和末端控件组成可模板化行 | 末端 Switch/Select 获得输入时不会同时触发行命令 |
| Item Card Group（`item-card-group`） | 12 | Q1 | 分组 ItemsControl 提供列表/网格布局、标题和分隔符 | 变更布局、删除末项后分隔符与阅读顺序正确 |
| KPI（`kpi`） | 9 | Q2 | 组合 Number Value、Trend Chip 和可选迷你图，各自使用同一数据快照 | 数据刷新时数值、趋势和曲线同期，缺失值显示明确 |
| KPI Group（`kpi-group`） | 3 | Q2 | 按可用宽度组织 KPI 卡片，复用度量、间距和对齐资源 | 横排切竖排时单位、基线与阅读顺序保持匹配 |
| List View（`list-view`） | 5 | Q1 | 复用选择模型和可回收容器，定义行激活与行内操作命令 | 键盘浏览时能进入行内操作，回收后名称和选中态正确 |
| Map（`map`） | 10 | Q3 | 独立地图适配层负责相机、样式、标记、弹窗、路线与聚合；宿主提供数据源 | 缩放后标记命中正确；离线、无可用图源、卸载时有明确反馈并释放渲染资源 |
| Timeline（`timeline`） | 8 | Q1 | 时间节点按稳定键呈现，模板区分标记、连线、时间和正文 | 多行、首末节点和动态插入时连线连续，时间仍可读 |
| Widget（`widget`） | 8 | Q1 | 以 Surface 组合仪表板外壳和内容区，图表与表格可分别换模板 | 切暗色后壳与内容层级可辨，嵌套表格只产生一个主滚动区 |

#### AI 交互（14 项）

| Pro 组件 | 上游示例数 | 阶段 | Avalonia 实现建议 | 代表性验收用例 |
| --- | ---: | --- | --- | --- |
| Chain Of Thought（`chain-of-thought`） | 4 | Q2 | 折叠时间线消费宿主提供的可展示步骤、状态和摘要 | 追加进度时保留展开状态；只渲染宿主明确传入的事件 |
| Chat Attachment（`chat-attachment`） | 3 | Q2 | 附件描述符承载文件名、状态、预览和移除命令，复用文件选择入口 | 预览失败仍能识别或移除附件，上传取消后迟到结果不恢复条目 |
| Chat Conversation（`chat-conversation`） | 3 | Q2 | 虚拟化消息视口以消息键保留滚动锚点，接近底部时自动跟随 | 上翻阅读期间流式追加不抢滚动，回到底部才恢复跟随 |
| Chat List View（`chat-list-view`） | 2 | Q2 | 将会话键、当前项和行内命令映射到 List View | 按最近活动重排后，选中会话仍正确，删除动作不会打开会话 |
| Chat Loader（`chat-loader`） | 1 | Q2 | 组合 Skeleton 与输入中占位，忙碌状态只公告一次 | 首个消息片段到达即替换占位，减少动态效果时状态仍可读 |
| Chat Message（`chat-message`） | 3 | Q2 | 消息角色、内容块与状态分别模板化，用户与助手共享选择和复制行为 | 长消息、混合附件及流式追加时不覆盖文本选区 |
| Chat Message Actions（`chat-message-actions`） | 3 | Q2 | 按消息键绑定复制、重试等宿主命令，使用 Toolbar 组织焦点 | 列表回收后操作仍作用于正确消息，禁用动作无法重复触发 |
| Chat Source（`chat-source`） | 5 | Q2 | 来源键关联行内引用、来源列表与导航命令 | 重复来源保持关联，键盘可从引用打开列表再回到正文 |
| Chat Tool（`chat-tool`） | 6 | Q2 | 工具卡呈现输入、输出、待批准、成功或失败，由宿主绑定操作 | 重复更新同一调用只更新一张卡，批准按钮只提交一次 |
| Code Block（`code-block`） | 1 | Q2 | 可选择的等宽文本视图，语言标记与高亮层独立 | 复制保留原始缩进与换行，未知语言退为纯文本 |
| Markdown（`markdown`） | 3 | Q2 | 按块解析和渲染，已完成块缓存；链接导航交给宿主 | 围栏被流式片段截断后仍能继续解析，完成块及滚动锚点保持稳定 |
| Prompt Input（`prompt-input`） | 9 | Q2 | 多行编辑器组合附件、工具栏、行内标记和队列；发送、停止由宿主命令驱动 | 中文候选确认不发送；排队、取消、重试后草稿和附件归属正确 |
| Prompt Suggestion（`prompt-suggestion`） | 2 | Q2 | 以可模板化按钮把建议交给填入或发送命令 | 键盘选中建议后按配置填入或发送；已有草稿按宿主策略保留 |
| Text Shimmer（`text-shimmer`） | 1 | Q2 | 用渐变蒙版表现忙碌文字；减少动态效果时显示静态前景 | 状态可由读屏读取，不逐帧重复公告 |

#### 反馈（5 项）

| Pro 组件 | 上游示例数 | 阶段 | Avalonia 实现建议 | 代表性验收用例 |
| --- | ---: | --- | --- | --- |
| Emoji Reaction Button（`emoji-reaction-button`） | 4 | Q1 | ToggleButton 组合表情和计数；提交态与乐观显示可区分 | 快速连点和服务端拒绝后，计数与本人是否已响应一致 |
| Number Value（`number-value`） | 8 | Q1 | 数值与显示格式分开，使用 CultureInfo 处理货币、百分比、紧凑表示 | 切换区域格式、负值和零值时，显示与可访问数值一致 |
| Pressable Feedback（`pressable-feedback`） | 17 | Q1 | 在现有按钮上添加波纹、高亮或长按进度；原控件保留命令入口 | 按住后移出、丢失捕获与键盘取消均可撤销确认，动作最多执行一次 |
| Rating（`rating`） | 12 | Q1 | 区分可操作评分与只读小数值；命中、选择和显示独立 | 只读半星正确朗读，键盘设置评分时尊重步长和禁用状态 |
| Trend Chip（`trend-chip`） | 6 | Q1 | 在 Chip 中分别设置数值方向、业务好坏、图标和后缀 | 成本下降、收入下降能有不同语义；单色状态下仍可分辨方向 |

#### 布局（1 项）

| Pro 组件 | 上游示例数 | 阶段 | Avalonia 实现建议 | 代表性验收用例 |
| --- | ---: | --- | --- | --- |
| Resizable（`resizable`） | 7 | Q1 | Grid 与可聚焦分隔柄统一处理像素/比例尺寸、最小宽度及折叠 | 拖动和方向键都受约束，窗口缩小再放大可恢复有效布局 |

#### 表单（11 项）

| Pro 组件 | 上游示例数 | 阶段 | Avalonia 实现建议 | 代表性验收用例 |
| --- | ---: | --- | --- | --- |
| Cell Color Picker（`cell-color-picker`） | 6 | Q2 | 设置行串接 ColorPicker、色样及自定义值输入 | 修改预设或自定义颜色后行内预览同步，取消恢复旧值 |
| Cell Select（`cell-select`） | 7 | Q1 | 设置行标签与 Select 触发器关联，可双向绑定选值 | 点击行或键盘打开只有一个选择弹层，提交后读回新值 |
| Cell Slider（`cell-slider`） | 7 | Q2 | 设置行组合 Slider、当前值和步进配置 | 拖动和方向键更新同一值，格式化数值与滑块状态一致 |
| Cell Switch（`cell-switch`） | 7 | Q1 | 说明与 Switch 共享可访问名称及命中策略 | 点标签或开关仅切换一次，禁用时仍能阅读说明 |
| Checkbox Button Group（`checkbox-button-group`） | 11 | Q1 | 复用 CheckboxGroup 的值集合，换用卡片选项模板 | 点击卡片任意有效区域只切换对应键，图标与说明不增加额外 Tab 停靠 |
| Drop Zone（`drop-zone`） | 9 | Q1 | 使用 Avalonia 拖放与 StorageProvider，统一验证类型、数量、大小并呈现文件列表 | 系统拖入和键盘选文件执行同一规则，混合有效/无效文件逐项反馈 |
| Inline Select（`inline-select`） | 5 | Q1 | 将紧凑 Select 锚点与行内文字基线对齐，沿用弹层选择模型 | 长选项或放大字体后仍可打开，提交后焦点返回原位置 |
| Native Select（`native-select`） | 12 | Q1 | 桌面以 Avalonia ComboBox 单选模式承接 HTML select，沿用平台输入与校验语义 | 键盘搜索、禁用项、标签和错误可读；平台弹层差异进入记录 |
| Number Stepper（`number-stepper`） | 13 | Q1 | 复用 NumberField 的值、边界和格式，步进按钮分别暴露命令 | 小数步长、长按连续步进和手输到边界时不会越界 |
| Radio Button Group（`radio-button-group`） | 12 | Q1 | 复用 RadioGroup 模型，以图文卡片呈现互斥项 | 方向键跳过禁用项；视觉选中和读屏报告始终只有一项 |
| Rich Text Editor（`rich-text-editor`） | 8 | Q3 | 独立文档模型、命令栈和选区；工具栏、浮动菜单与字符计数订阅同一文档 | 中文输入、跨段撤销、链接编辑及支持节点的 JSON 往返均有用例 |

#### 导航（7 项）

| Pro 组件 | 上游示例数 | 阶段 | Avalonia 实现建议 | 代表性验收用例 |
| --- | ---: | --- | --- | --- |
| AppLayout（`app-layout`） | 14 | Q1 | 在 Grid 中组织侧栏、导航、主内容和可选辅助栏，由窗口宽度决定姿态 | 切换窄宽布局后选中路由、内容滚动位置及焦点可恢复 |
| Command（`command`） | 9 | Q1 | `AlderCommand` 已在 `AlderTheme` 下提供内嵌和 `ShowAsync` 弹窗，复用 DialogHost、SearchField 和虚拟化列表，支持模糊匹配、分组和子页面 | 快捷键打开后输入中文、进入子组并返回，执行命令后焦点归还 |
| Context Menu（`context-menu`） | 7 | Q1 | 复用菜单原语，统一右击、触摸长按和键盘菜单入口 | 多级菜单方向键可进退，命令绑定到打开菜单时的目标项 |
| Navbar（`navbar`） | 7 | Q1 | 导航栏连接宿主路由和滚动位置，紧凑布局提供菜单入口 | 深层页面回退后活动项正确，键盘焦点在栏内时避免滚动导致失焦 |
| Segment（`segment`） | 11 | Q1 | 复用单选模型与可变宽指示器；支持紧凑控件独立于 Tabs 使用 | 长短标签、RTL 和集合重排后，指示器仍跟随选中键 |
| Sidebar（`sidebar`） | 16 | Q1 | 同一导航树提供展开、窄轨和 Sheet 三种呈现，保留展开与当前项 | 收窄再展开保持导航位置，Sheet 关闭后焦点回到菜单按钮 |
| Stepper（`stepper`） | 19 | Q1 | 将当前步、完成状态与可导航性分别建模，支持水平/垂直布局 | 异步校验失败留在当前步，已完成与当前状态均可朗读 |

#### 弹层（1 项）

| Pro 组件 | 上游示例数 | 阶段 | Avalonia 实现建议 | 代表性验收用例 |
| --- | ---: | --- | --- | --- |
| Emoji Picker（`emoji-picker`） | 4 | Q2 | 共享搜索、分类、肤色、最近使用模型与虚拟化网格；宿主决定历史存储 | 搜索后用键盘选取含修饰符的完整字素，焦点归还且最近记录不重复 |

## 5. 复杂控件的实现契约

### 5.1 文本、错误与命令

TextField 默认模板建议包含标签、一个真实 TextBox、说明、错误四部分。外部 Text 是可空字符串双向属性；内部编辑器绑定到模板父级，使用显式 TwoWay 模板父级绑定。替换模板时解除旧部件事件。文字选择、复制粘贴、字符组合、撤销栈由编辑器持有。读屏只应遇到一个可编辑节点，Label 指向它，字段名和错误信息也在这个节点上可读。InputGroup 中的“显示密码”一类按钮保留本身的语义和 Tab 顺序。

Form 接收宿主的 SubmitCommand，字段参与同一份验证结果。ViewModel 的 INotifyDataErrorInfo 通过启用验证的绑定进入控件外层，在一处汇总并向内层展示。IsRequired 提供语义和提示；调用者能自定义必填规则和错误文本。提交无效时展开必要容器并焦点定位；验证中保留用户输入。正在编辑中文拼音或多行文本时，不把 Enter 当整表提交。异步验证用请求代数忽略过期结果。Pending Button 同时处理鼠标、键盘和自动化激活，重复执行在命令入口拦截；操作完成后恢复原有可用性。[Avalonia 验证与辅助技术](https://docs.avaloniaui.net/docs/app-development/accessibility)

### 5.2 集合、过滤与选择

Select 适合从已知选项中挑选；弹层打开前后，触发器都显示已提交值。Autocomplete 先打开选择面板，再在面板内输入过滤词，适合在长列表里找多个标签。ComboBox 的文本始终在主字段里，适合编辑收件人，也可配置接受自定义文本。三者共享项模板、禁用项、分组、空态、选择模型与异步加载；分别实现编辑和焦点策略。Select（`select.md`） · Autocomplete（`autocomplete.md`） · ComboBox（`combo-box.md`）

| 操作 | 建议的可观察结果 |
| --- | --- |
| 打开 | Select 定位到已选项或首个可选项。Autocomplete 聚焦面板内搜索框。ComboBox 保留主编辑器的插入点 |
| 输入、键盘浏览 | 分别维护查询字符串、活动键和已选键。上下移动仅改变活动项；Home/End 在文本编辑与列表浏览模式分别处理。组合输入完成后触发过滤 |
| Enter、Space、Escape | Enter 提交活动项；单选提交后关闭，多选可继续。列表模式可用 Space 切换；编辑器中的空格照常输入。Escape 关闭本层并丢弃未提交候选，已提交多选项保留 |
| 远端过滤 | 调用方提供异步数据和稳定键。取消旧请求，以序号拒绝迟到结果。加载、空集合和错误分开显示；过滤暂时隐藏选项时保留已选键及显示文本 |
| 重排、回收、删除 | 用稳定键比较，容器回收时重置伪类、订阅和自动化名称。区分“当前页无此项”和“数据源确认删除”。删除后按策略清除选择并通知一次 |

### 5.3 弹层、焦点与生命周期

每个 TopLevel 持有自己的宿主、活动弹层栈、通知队列和前次焦点引用。Popover/菜单按锚点定位，记录打开方的主题作用域；Modal/Drawer/Sheet 在窗口内容层呈现并隔离背景输入。只有确需独立标题栏或系统模态边界时才使用有 owner 的 Window。对话框有可访问名称。进入模态层时同时限制 Tab 顺序、指针命中和后台自动化激活。

一次打开依次完成“登记打开者 → 创建内容 → 布局与定位 → 设置初始焦点”；一次关闭依次完成“判断策略 → 发可取消的关闭请求 → 停止输入 → 播放退出效果 → 清理订阅与宿主 → 恢复焦点”。Escape 只送给栈顶。已关闭的 Popup 不吞掉下一次 Escape。打开者已卸载时回到父层合理的焦点锚点。宿主窗口关闭时强制清理全部层，取消计时器和待执行回调。快速重开应使旧退出回调失效，减少动态效果时也执行同样的清理路径。

**Sheet 的额外工作量来自拖动与滚动冲突。** 内容仍可沿手势方向滚动时先交给滚动区；到边界且允许手势时才转移到面板。手柄模式只允许从手柄开始拖动。捕获丢失、窗口失焦、缩放与嵌套面板关闭均须落回合法停靠点。为键盘和读屏提供“展开、收起、关闭”操作。Drawer 可先共用模态宿主完成四边进出；Sheet 单独验收停靠点和嵌套。Sheet（`sheet.md`） · Drawer（`drawer.md`）

### 5.4 日期、颜色与表格的值模型

| 家族 | 建议的第一版模型 | 升级到该家族完整对标所需的证据 |
| --- | --- | --- |
| 日期时间 | 日期用 DateOnly?，时间用 TimeOnly?；范围草稿允许仅有一端，提交值成对且有序。Calendar 维护独立的“可见月、焦点日、选中值”。CultureInfo 控制字段顺序、月份名称及首日 | 上游区分纯日期、无时区本地日期时间、带命名时区日期时间。后者要保留时区标识，DateTimeOffset 只保留偏移；补测夏令时缺失/重复时刻、非公历、区间内禁用日、多月、日/周模式。按实际支持的历法和类型公布差异 |
| 颜色 | 编辑中保存浮点通道、当前色彩空间、透明度和未提交文本；提交时同时提供 Avalonia.Media.Color 适配。灰色时保留上次色相，使用户重新提高饱和度后仍回到预期颜色 | 验证 RGB、HSL、HSB 表示互转、通道步长、Alpha 与无效输入。拖动更新界面，释放时提交；多次转换不反复经过 8 位通道。主题 OKLCH 令牌转换和用户选择颜色分别维护 |
| Table | 先定义列键、行键、排序描述、单/多选、单元格模板及加载请求。排序由宿主选择本地或远端执行。原型对比可用的 Avalonia 表格实现与回收型行列表的能力和依赖成本 | 实测列宽拖动、分组表头需求、展开行高度、键盘跨格、屏幕阅读器行列关系、滚动期间刷新、分页与增量加载。回收列表视觉成本低，但要自行补齐网格语义；采用现成表格前核对目标版本、许可和可替换模板 |

日期上游的 ZonedDateTime 示例、表格加载与虚拟化示例都随版本发布。首版日期子集可以单独预览；只有公布并通过对应差异清单后，才能对外声明该家族达到目标范围。DateField（`date-field.md`） · ColorField（`color-field.md`） · Table（`table.md`）

### 5.5 Pro 组件共享的行为与数据模型

| 设计决策 | 建议的契约及理由 |
| --- | --- |
| Table、Data Grid、File Tree、Kanban | 基础 Table 与 Pro Data Grid 共用行列键、选择与列宽；Pro 负责固定列、可控重排及粘性表头。在树和看板中，一次移动请求携带项目键、源容器、目标容器与相邻键，宿主一次性接受或拒绝。键盘移动与拖放走同一路径。开启排序或过滤时，移动含义由宿主明确；无法定义新顺序时禁用重排 |
| Agenda 与基础 Calendar | Calendar 提供日期导航、区域设置与键盘网格。Agenda 独立持有事件键、起止时间、事件时区、全天标记与响应状态。全天用本地日期区间，定时事件用带时区的时刻；分栏重叠事件、跨午夜和夏令时切换单测。拖动仅生成变更请求，宿主确认后提交 |
| 图表、KPI、Widget | 先定义系列键、类别或时间轴、数值与缺失值、格式化器及活动数据点。几何绘制、键盘命中和自动化数据摘要共享同一变换。首个原型同时绘制带空值的面积图和正负混合柱图，再依据大数据量与多后端测试决定自绘或图表依赖。主题提供图区前景、网格、序列色、选中态与非颜色标识 |
| 对话与流式内容 | 消息以会话键、消息键、内容块键和序号更新，组件只消费宿主传入的文本、附件、工具事件及可展示步骤。插入新会话、取消响应或重试时忽略旧流的迟到片段。Markdown 追加、代码块复制、聊天滚动各自持有局部状态。发送、停止、导航、附件上传和批准均由应用命令执行 |
| Rich Text Editor 与 Map | 富文本先约定支持的 JSON 节点、标记、选择及单次撤销边界，以共享样本验证导入、编辑与导出；遇到未知节点可保留或提示，避免静默丢内容。地图独立验证相机、坐标投影、样式、热力/聚合、图层拾取及授权署名。优先评估原生绘制与可维护的后端；选择原生后端前先量化 IME、焦点、自动化树、图层叠放与打包成本 |
| 与基础层重叠的语义 | Pro Empty State 复用集合空态；Cell Select 等设置行复用原字段的选值与验证；Rich Text Editor 中的链接弹层和 Sidebar 的窄屏 Sheet 走基础弹层栈。主题资源只新增图表序列、看板拖放、富文本选区等专有角色 |

对照重点可直接查看 Data Grid（`data-grid`）、Agenda（`agenda`）、Prompt Input（`prompt-input`）、Rich Text Editor（`rich-text-editor`） 和 Map（`map`）。

## 6. 验收标准

验收时每个家族保留一份对应版本的 Web 参考场景、一个可复现的 Gallery 场景、一份差异记录。Web 和原生使用相同的标签、数据、模式、逻辑尺寸、字体条件和静止动画状态。文本使用各平台字体基线；几何、颜色、行为与语义分别比较。

| 检查 | 建议通过条件 |
| --- | --- |
| 视觉 | 明暗两套截图包含默认、悬停、按下、键盘焦点、禁用，以及适用的选中、错误、只读、等待状态。100% 和 150% 缩放下，代表控件边界与基线几何偏差目标 ≤1 DIP；默认主题不透明纯色的 sRGB 通道偏差目标 ≤1/255。阴影、半透明叠加、文本抗锯齿分区域复核 |
| 可达性 | 正文对比目标 ≥4.5∶1，大字和有意义的控件轮廓目标 ≥3∶1；源主题如有偏差，记录配对、实测值及高对比替代。Tab/Shift+Tab、方向键、Enter、Space、Escape 有完整路径。错误、忙碌、选中不用颜色单独传意 |
| 语义树与真机 | 为按钮、输入、选择、范围和展开行为核对名称、角色、值、状态和可用操作。Windows 用 Narrator/NVDA，macOS 用 VoiceOver，Linux 用带 AT-SPI 的桌面和 Orca。记录实际后端；读屏口述、IME 候选窗和跨屏 Popup 需真机检查 |
| 本地化 | zh-CN、en-US 和一种 RTL 语言，覆盖长标签、中文拼音输入、粘贴、组合字符、日期段序、数字小数点、首尾圆角镜像和方向键。把文本放大至 200%，检查字段和错误提示可读、可达 |
| 行为与生命周期 | 对双向绑定、命令不可用、撤销、取消、焦点恢复及异步乱序做 Headless 测试。同一弹层连续开关 100 次并移除宿主，确认无残留计时器、订阅或活动窗口引用 |
| 性能 | 固定设备、后端、刷新率和 Release 配置。用一万行集合连续滚动，记录首屏耗时、P95 帧时长、分配量和已实现容器数；将屏外数据翻倍时容器数应近似不变。若以 60 Hz 为目标，帧预算 16.7ms；先测一轮再确定各平台发布门槛 |
| 分发 | 在干净的消费项目只引用打包产物并加载主题；检查资源 URI、默认模板、换肤和局部覆盖。分别核对 React、Styles、字体、图标等素材来源；复用 Apache-2.0 内容时随包处理 LICENSE、适用 NOTICE 和修改标注 |

**Pro 的三组端到端验收。** 每组都要记录公开示例、对应的本地 Gallery 场景、支持的变体以及平台差异。目录中的 474 个示例逐个归入“已覆盖／平台等价／待实现”，同一个原生实现可以覆盖多个组合。

| 场景 | 操作路径 | 通过条件 |
| --- | --- | --- |
| 可编辑工作台 | 用 Command 打开项目，Data Grid 选中多行后调用 Action Bar，再在 Kanban 中移动一项 | 排序、异步更新和布局切换后选中键一致。被拒绝的跨列移动可回退，鼠标和键盘得到同一结果 |
| 数据仪表板 | 在 Resizable 中并排放置 KPI、Chart Tooltip、图表、Agenda，缩窄侧栏并切换明暗色 | 图例与数值可用键盘及读屏读取；坐标、提示、时区和颜色同步；拖动分隔柄时绘图区不出现持续重复布局 |
| 流式会话 | 使用 Prompt Input 插入附件、确认中文候选、发送、取消，再打开含代码和引用的上一条消息 | 组合输入不会误发送；迟到片段不会写入新请求；用户上翻、选字或复制时，流式更新不抢焦点与滚动位置 |

Headless 可验证输入事件、状态和资源解析；真实窗口负责系统辅助技术、弹层边界和输入法的终验。[Avalonia Headless](https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform) · [Avalonia 辅助功能](https://docs.avaloniaui.net/docs/app-development/accessibility) · [Linux 平台说明](https://docs.avaloniaui.net/docs/platform-specific-guides/linux) · 上游许可证

## 7. 实施顺序与发布门槛

| 里程碑 | 开发内容 | 可以进入下一阶段的条件 |
| --- | --- | --- |
| M0 参考与骨架 | 固定两个发布包和对照场景，准备默认明暗资源、Gallery、Headless 测试入口与消费端示例 | 新的消费项目可加载主题；切换主题、窗口级覆盖和原生可访问角色通过验证 |
| M1 首个业务场景 | “编辑项目”对话框包含名称、所属分类、保存和取消。使用 Button、TextField、Select、ListBox、Popover 与 Modal；随后补全 A 类基础组件 | 光用键盘完成打开、输入中文、选择分类、错误修正、提交、焦点返回。对话框中切深色时，已打开的选项面板也同步；并排打开两个窗口互不干扰 |
| M2 组合与常用交互 | 完成 B 类家族，包括分组、导航、Autocomplete、Dropdown、Toast、Drawer；为 Surface、表单和集合提供组合样例 | 验证异步乱序、子菜单回退、多选、窄窗口、长文本、读屏路径。A/B 每一行均有示例和验收记录 |
| M3 高级组件 | C 类按专长拆开：编辑与日期、颜色、表格、拖动与范围。先交付各家族最小纵向场景，再扩展额外模式 | 对照第 4 节和上游部件表逐项签收；把支持的历法、输入模式、Table 规模与桌面后端写进发布说明 |
| Q1 桌面工作台与设置 | M1 和所需 B 类基础就绪后，实现第 4 节 Q1 条目。先用 AppLayout、Sidebar、Command 串起一个可键盘操作的设置页 | 验证宽窄布局切换、窗口级快捷键、局部主题和双向绑定，分组选择与表单行为复用基础层 |
| Q2 图表、指标与对话 | 为第 4 节 Q2 条目建立共享数据模型，交付可调宽的指标面板与可取消的流式聊天。Cell 颜色与滑块控件等待相应基础输入就绪 | 换肤、键盘数据导航、缺失数据、流式更新、复制和取消通过；每项均关联上游示例 |
| Q3 专用控件 | 对第 4 节 Q3 条目分别做可运行原型。Data Grid 依赖基础 Table，Agenda 依赖日期能力；地图与富文本可提前独立评估后端 | 完成第 5.5 节的数据往返、重排与平台输入验证。三桌面报告说明后端、性能基线和已知差异 |
| 稳定版 | 固定公开资源键、模板部件、属性、事件、NuGet 版本和上游基线 | 三桌面验收记录齐备；消费端能升级并保留原主题覆盖。所有已知差异有复现方式与版本归属 |

开源和 Pro 分别锁定版本，跨层迁移的组件应在两份目录中核销，例如本次的 Sheet。

上游升级按版本做差分：先比组件目录和根导出，再比公开签名、默认变体、主题表达式和状态 CSS。新增项进矩阵；默认值变化先更新对照场景，说明差异后再改模板。这样一次小版本升级也能追溯到具体按钮、字段或弹层。

## 8. 本次验证与尚待实测的部分

- **覆盖检查。** 以 `@heroui/react@3.2.8/docs/components/*.md` 为集合，比对矩阵的 73 个名称和对应文件名，检查无遗漏和重复。复合部件另与组件导出索引核对。此项验证证明覆盖目录，具体属性和行为仍按家族验收。
- **Pro 覆盖检查。** 比对官方 Web 组件目录与官方 llms 索引，逐项读取 65 个公开组件页并核对标题。矩阵按八类列出全部条目，卡片变体数合计 474。核对范围为公开文档；逐个示例的视觉与交互等价性在实现期验证。
- **原生可行性小试。** 在仓库外建立 net10.0 临时工程，使用 Avalonia.Headless 12.1.6 编译 AXAML 并打开测试窗口。为原生 Button 指定具名 ControlTheme，通过 DynamicResource 绑定测试画刷；程序断言 Light/Dark 两种颜色分别为 #112233、#CCDDEE，切换通过，自动化角色仍为 Button。这两个颜色只用于确认资源变化。
- **版本指纹。** 本次读取的 npm 压缩包 SHA-256（整包文件）为 React `2fedc30b9ac090e9a50e1c338ff2c479cdd32807a2902886902f0c825662298f`、Styles `9bd8ea778ca561ac9ae91943df8bec848008ddc8ef7a2ac39a790ae0d3ba4029`。
- **交付边界。** 仓库已有原生按钮、选择、开关、滑块及工具栏的首批实现和 Gallery。当前接口见第 4 节，其他家族的控件接口仍属设计草案。品牌换肤生成、弹层栈、截图一致性、三平台读屏、复杂日期和表格性能尚待实现与实测。下一步用 M1 的编辑对话框验证基础层，随后用 Q1 的 AppLayout 与 Command 打开同一对话框，检验控件能否共用焦点、弹层和主题。
