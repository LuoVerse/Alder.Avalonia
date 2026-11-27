# Rich Text Editor

`AlderRichTextEditor.Value` 是可读写的 JSON 字符串。第一次打开时给它初始文档，编辑后保存最新的 `Value`，下次打开时把保存的字符串赋回同一属性。编辑器通过双向绑定发布正文和格式的变更。`PlainText` 则用于搜索、摘要和纯文本导出。

先在应用中加载 [AlderTheme](../../README.md#在-avalonia-应用中使用)。完整的工具栏、扩展命令、只读和字数统计用法见 [Gallery 视图](../../samples/Alder.Avalonia.Gallery/Views/RichTextEditorExample.axaml) 和 [Gallery ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/RichTextEditorViewModel.cs)。

## 构建一份富文本文档

根节点使用 `type: "doc"`，`content` 按顺序放段落、标题、列表等块。行内格式放在各个 `text` 节点的 `marks` 中。下面的 C# 构建一个二级标题、带粗体和链接的段落，以及两项有序列表。应用提供的动态文本可直接赋给 `JsonObject`，序列化时会处理引号和换行的转义。

```csharp
using System.Text.Json.Nodes;

public static class NoteDocument
{
    public static string Create(string title, string firstTask, string secondTask)
    {
        var document = new JsonObject
        {
            ["type"] = "doc",
            ["content"] = new JsonArray(
                new JsonObject
                {
                    ["type"] = "heading",
                    ["attrs"] = new JsonObject { ["level"] = 2 },
                    ["content"] = new JsonArray(Text(title))
                },
                Paragraph(
                    Text("状态", new JsonObject { ["type"] = "bold" }),
                    Text("：待确认。查看"),
                    Text("项目主页", new JsonObject
                    {
                        ["type"] = "link",
                        ["attrs"] = new JsonObject { ["href"] = "https://example.com/" }
                    })),
                new JsonObject
                {
                    ["type"] = "orderedList",
                    ["attrs"] = new JsonObject { ["start"] = 1 },
                    ["content"] = new JsonArray(
                        ListItem(firstTask), ListItem(secondTask))
                })
        };

        return document.ToJsonString();
    }

    private static JsonObject Text(string value, JsonObject? mark = null)
    {
        var node = new JsonObject { ["type"] = "text", ["text"] = value };
        if (mark is not null)
            node["marks"] = new JsonArray(mark);
        return node;
    }

    private static JsonObject Paragraph(params JsonNode[] runs) => new()
    {
        ["type"] = "paragraph",
        ["content"] = new JsonArray(runs)
    };

    private static JsonObject ListItem(string value) => new()
    {
        ["type"] = "listItem",
        ["content"] = new JsonArray(Paragraph(Text(value)))
    };
}
```

| 内容 | JSON 节点 | 写法 |
| --- | --- | --- |
| 空文档 | `doc` | `{"type":"doc","content":[{"type":"paragraph"}]}` |
| 段落和标题 | `paragraph`、`heading` | `content` 放行内节点。标题用 `attrs.level` 指定 1、2 或 3 |
| 引用 | `blockquote` | `content` 可放一个或多个段落 |
| 无序／有序列表 | `bulletList`、`orderedList` | `content` 放 `listItem`，每项再放 `paragraph` 等块。有序列表可设置 `attrs.start` |
| 代码块 | `codeBlock` | `content` 放 `text` 节点，`text` 字符串可包含换行 |
| 分割线 | `horizontalRule` | 可直接作为块放入文档 |
| 行内换行 | `hardBreak` | 可与 `text` 一起放在段落的 `content` 中 |
| 行内样式 | `bold`、`italic`、`underline`、`strike`、`code`、`link` | 每种样式是 `marks` 中带 `type` 的对象，可组合。链接用 `attrs.href` 存地址 |

保存后的 JSON 可能合并相邻且格式相同的文本节点。需要比较文档内容时，应比较解析后的语义，避免依赖缩进、键顺序或文本节点的拆分位置。导入 HTML、Markdown 或其他编辑器的 JSON 时，宿主需要先映射到上表支持的节点和标记。图片、表格等文档节点当前不受此编辑器支持。[文档读写实现](../../src/Alder.Avalonia/Controls/Forms/RichText/RichDocument.cs)

## 绑定、保存与重新打开

下面把文件路径交给 ViewModel。保存时写入完整 JSON，打开时先读到局部变量，再更新绑定属性。给每条记录分配稳定的路径或数据库主键。数据库中可以直接把 JSON 字符串存入文本列。

```csharp
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MyApp;

public sealed class NoteViewModel(string filePath, string initialDocument) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string DocumentJson
    {
        get => field;
        set
        {
            if (field == value)
                return;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DocumentJson)));
        }
    } = initialDocument;

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        string snapshot = DocumentJson;
        string fullPath = Path.GetFullPath(filePath);
        string directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, $".{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(temporaryPath, snapshot, cancellationToken);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        string savedDocument = await File.ReadAllTextAsync(filePath, cancellationToken);
        DocumentJson = savedDocument;
    }
}
```

在视图上声明 `x:DataType`，并把 `DataContext` 设为上面的 `NoteViewModel` 实例。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="using:MyApp"
             xmlns:alder="using:Alder.Avalonia.Controls"
             x:DataType="local:NoteViewModel" x:CompileBindings="True">
  <alder:AlderRichTextEditor Value="{Binding DocumentJson, Mode=TwoWay}"
                             Placeholder="输入记录…" />
</UserControl>
```

在 UI 线程创建视图时使用下面的代码。把保存按钮或应用的保存命令接到 `await note.SaveAsync()`。再次启动时，这段初始化代码会从同一个路径读取文档。也可以在视图已打开时调用 `OpenAsync()` 重新载入。

```csharp
using System;
using System.IO;

var path = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "MyApp", "notes", "weekly.json");

var note = new MyApp.NoteViewModel(path,
    NoteDocument.Create("每周记录", "汇总进度", "确认待办"));
if (File.Exists(path))
    await note.OpenAsync();

// 把 note 设为视图的 DataContext。保存按钮调用 await note.SaveAsync()。
```

- 初次打开时使用初稿。重新加载会替换整份文档及其撤销历史，覆盖当前编辑前可按应用的交互规则提示保存。
- 文件 I/O 异常应交给宿主的错误提示处理。加载外部或旧版本数据时，先检查并迁移文档。无效 JSON 或不受支持的节点可能在给 `Value` 赋值时抛出异常。做导入功能时，可先在 UI 线程尝试创建 `new AlderRichTextEditor { Value = json }`，解析成功后再替换当前绑定值。
- `Value` 记录正文结构与格式。光标、选区、撤销栈和宿主的文档标题、业务 ID 等元数据按需单独管理。
- 自动保存可在 ViewModel 属性变更后防抖并串行写入。初始化和载入文档时，应明确区分新编辑和已保存的快照。

## 在当前文档中插入或格式化

在视图模板已应用后调用这些编辑方法。下面将第一行设为粗体，再在文末插入分割线。`Select` 使用 `PlainText` 的 UTF-16 字符串偏移。样式命令也可用 `IsActive` 查询对应按钮状态。

```csharp
using Alder.Avalonia.Controls;

static void FormatAndInsert(AlderRichTextEditor editor)
{
    int lineEnd = editor.PlainText.IndexOf('\n');
    int firstLineLength = lineEnd >= 0 ? lineEnd : editor.PlainText.Length;
    if (firstLineLength > 0)
    {
        editor.Select(0, firstLineLength);
        if (!editor.IsActive(RichTextCommand.Bold))
            editor.Execute(RichTextCommand.Bold);
    }

    editor.Select(editor.PlainText.Length, 0);
    editor.InsertBlocks("""{"type":"doc","content":[{"type":"horizontalRule"}]}""");
}
```

- `InsertText(text)` 用文本替换选区。`InsertBlocks(json)` 接受带 `doc` 根节点的片段，作为一次可撤销操作插入。
- 选中文本后用 `SetLink(url)` 设置链接，用 `SetLink(null)` 清除链接。
- 用 `IsReadOnly` 展示可阅读的文档，用 `MaxLength` 约束编辑输入。导入文件的大小与业务字数限制由宿主单独校验。
- `ToolbarCommands` 可选择工具栏命令，空集合可移除内置工具按钮。用 `AdditionalToolbarContent` 放入应用自己的按钮。启用 `ShowSlashMenu` 或 `ShowFloatingMenu` 后，可通过 `Suggestions` 或 `FloatingCommands` 注册 `RichTextSuggestion`，回调收到当前编辑器，可调用插入方法。
- 接入语法诊断、悬浮提示或补全时，`ConnectLanguageServerAsync` 同步的是文档的纯文本投影。关闭文档时异步释放返回的会话，持久化仍读取 `Value`。[协议与编辑流程示例](../../tests/Alder.Avalonia.AotSmoke/EditorProtocolSmoke.cs)
