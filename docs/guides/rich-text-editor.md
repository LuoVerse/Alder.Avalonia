# Rich Text Editor

**English** · [简体中文](rich-text-editor.zh-CN.md)

`AlderRichTextEditor.Value` is a read/write JSON string. Supply an initial document when the editor opens, save the latest `Value` after editing, and assign the saved string to the same property when reopening. The editor publishes content and formatting changes through two-way binding. Use `PlainText` for search, summaries, and plain-text export.

Load `AlderTheme` in your app first. The Gallery demonstrates the full toolbar, custom commands, read-only mode, and character counts. [Theme setup](../../README.md#use-in-an-avalonia-app) · [Gallery view](../../samples/Alder.Avalonia.Gallery/Views/RichTextEditorExample.axaml) · [Gallery ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/RichTextEditorViewModel.cs)

## Building a rich-text document

Use `type: "doc"` for the root node. Put blocks such as paragraphs, headings, and lists in `content` in display order. Inline formatting goes in each `text` node's `marks`. This C# example builds a level-two heading, a paragraph with bold text and a link, and an ordered list with two items. Assign dynamic text directly to a `JsonObject` so serialization handles quotes and line breaks.

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
                    Text("Status", new JsonObject { ["type"] = "bold" }),
                    Text(": Pending confirmation. See the "),
                    Text("project homepage", new JsonObject
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

| Content | JSON nodes | Structure |
| --- | --- | --- |
| Empty document | `doc` | `{"type":"doc","content":[{"type":"paragraph"}]}` |
| Paragraphs and headings | `paragraph`, `heading` | Put inline nodes in `content`. Set `attrs.level` to 1, 2, or 3 for a heading. |
| Quote | `blockquote` | Put one or more paragraphs in `content`. |
| Unordered and ordered lists | `bulletList`, `orderedList` | Put `listItem` nodes in `content`, with blocks such as `paragraph` inside each item. Ordered lists can set `attrs.start`. |
| Code block | `codeBlock` | Put `text` nodes in `content`. The `text` string can include line breaks. |
| Divider | `horizontalRule` | Insert as a document block. |
| Inline line break | `hardBreak` | Place alongside `text` in a paragraph's `content`. |
| Inline formatting | `bold`, `italic`, `underline`, `strike`, `code`, `link` | Each mark is a `marks` object with a `type`. Marks can be combined. Set `attrs.href` for a link. |

Saved JSON may merge adjacent text nodes that have identical formatting. Compare the parsed document structure and content, since indentation, key order, and text node boundaries can change. When importing HTML, Markdown, or another editor's JSON, map it to the nodes and marks above. Importers need to convert unsupported elements such as images and tables before loading them. [Document reader and writer](../../src/Alder.Avalonia/Controls/Forms/RichText/RichDocument.cs)

## Binding, saving, and reopening

Pass the file path to the ViewModel. Save the entire JSON string. On open, read into a local variable before updating the bound property. Give each record a stable file path or database key. In a database, store the JSON string directly in a text column.

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

Declare `x:DataType` on the view and assign the `NoteViewModel` instance above as its `DataContext`.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="using:MyApp"
             xmlns:alder="using:Alder.Avalonia.Controls"
             x:DataType="local:NoteViewModel" x:CompileBindings="True">
  <alder:AlderRichTextEditor Value="{Binding DocumentJson, Mode=TwoWay}"
                             Placeholder="Write a note…" />
</UserControl>
```

Use this code when creating the view on the UI thread. Wire the Save button or application command to `await note.SaveAsync()`. On the next launch, this initialization reads the document from the same path. You can also call `OpenAsync()` to reload an already open view.

```csharp
using System;
using System.IO;

var path = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "MyApp", "notes", "weekly.json");

var note = new MyApp.NoteViewModel(path,
    NoteDocument.Create("Weekly notes", "Summarize progress", "Review action items"));
if (File.Exists(path))
    await note.OpenAsync();

// Set note as the view's DataContext. Call await note.SaveAsync() from the Save button.
```

- The initial draft is used on first open. Reloading replaces the entire document and its undo history. Follow the application's save prompt behavior before replacing current edits.
- Surface file I/O errors through the host's error UI. Validate and migrate external or older documents before loading. Assigning invalid JSON or an unsupported node to `Value` can throw. For an import flow, try creating `new AlderRichTextEditor { Value = json }` on the UI thread and update the bound value after parsing succeeds.
- `Value` stores document structure and formatting. Manage cursor position, selection, undo history, and host metadata such as the title and application ID separately as needed.
- For autosave, debounce ViewModel property changes and serialize writes. During initialization and reload, distinguish fresh edits from saved snapshots.

## Inserting and formatting in the current document

Call the editing methods after the view template has been applied. This example bolds the first line and inserts a divider at the end. `Select` uses UTF-16 string offsets into `PlainText`. Use `IsActive` to query the button state for a formatting command.

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

- `InsertText(text)` replaces the selection with text. `InsertBlocks(json)` accepts a fragment with a `doc` root and inserts it as one undoable operation.
- Select text and call `SetLink(url)` to set a link. Call `SetLink(null)` to clear it.
- Set `IsReadOnly` to show a read-only document and `MaxLength` to constrain editing input. Validate imported file sizes and application word limits separately in the host.
- Use `ToolbarCommands` to choose toolbar commands. An empty collection hides the built-in buttons. Use `AdditionalToolbarContent` for custom buttons. With `ShowSlashMenu` or `ShowFloatingMenu` enabled, register `RichTextSuggestion` items through `Suggestions` or `FloatingCommands`. Callbacks receive the current editor and can invoke insertion methods.
- To connect diagnostics, hovers, or completions, use `ConnectLanguageServerAsync` to synchronize the plain-text projection. Dispose the returned session asynchronously when the document closes and use `Value` for persistence. [Protocol and editing example](../../tests/Alder.Avalonia.AotSmoke/EditorProtocolSmoke.cs)
