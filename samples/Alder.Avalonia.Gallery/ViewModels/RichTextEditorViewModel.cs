using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class RichTextEditorViewModel : GalleryViewModel
{
    public RichTextEditorViewModel()
    {
        controlledValue = Guide;
        LoadGuide = ReactiveCommand.Create(() => { ControlledValue = Guide; });
        LoadNotes = ReactiveCommand.Create(() => { ControlledValue = Notes; });
        InsertDate = ReactiveCommand.Create<AlderRichTextEditor>(Date);
        InsertChecklist = ReactiveCommand.Create<AlderRichTextEditor>(Checklist);
    }

    public ICommand LoadGuide { get; }
    public ICommand LoadNotes { get; }
    public ICommand InsertDate { get; }
    public ICommand InsertChecklist { get; }
    public IReadOnlyList<RichTextCommand> CompactToolbar { get; } =
        [RichTextCommand.Bold, RichTextCommand.Italic, RichTextCommand.BulletList, RichTextCommand.Link];
    public IReadOnlyList<RichTextCommand> EmptyToolbar { get; } = [];
    public IReadOnlyList<RichTextSuggestion> Suggestions =>
    [
        new(Text("RichDivider"), editor => editor.InsertBlocks(Doc(new JsonObject { ["type"] = "horizontalRule" })))
            { Description = Text("RichDividerHint"), IconResourceKey = "AlderIconMinus", Keywords = "divider separator hr" },
        new(Text("RichAction"), editor => editor.InsertBlocks(Doc(Quote("RichActionText"))))
            { Description = Text("RichActionHint"), IconResourceKey = "AlderRichIconChecklist", Keywords = "action callout" }
    ];
    public IReadOnlyList<RichTextSuggestion> FloatingCommands =>
        [new(Text("RichAddHeading"), editor => editor.InsertBlocks(Doc(new JsonObject { ["type"] = "heading",
            ["attrs"] = new JsonObject { ["level"] = 2 } }))) { IconResourceKey = "AlderRichIconAddHeading" }];

    private void Date(AlderRichTextEditor editor) =>
        editor.InsertText(DateTime.Today.ToString(DisplayCulture.TwoLetterISOLanguageName == "zh" ? "yyyy年M月d日" : "MMM d, yyyy", DisplayCulture));

    private void Checklist(AlderRichTextEditor editor) =>
        editor.InsertBlocks(Doc(Paragraph("RichPreflight"), List("RichOwner")));

    private string controlledValue;
    public string ControlledValue
    {
        get => controlledValue;
        set
        {
            if (controlledValue == value) return;
            this.RaiseAndSetIfChanged(ref controlledValue, value);
            this.RaisePropertyChanged(nameof(JsonPreview));
        }
    }
    private static readonly JsonSerializerOptions PreviewFormat = new()
        { WriteIndented = true, Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };
    public string JsonPreview => JsonNode.Parse(ControlledValue)!.ToJsonString(PreviewFormat);
    public string Guide => Doc(Heading("RichControlled"), Inline("RichGuideBody", ("Value", "code")),
        Items("bulletList", Paragraph("RichGuideHint"), Inline("RichGuideCount", (Text("RichCharacter"), "bold"), (Text("RichWord"), "italic"))),
        Quote("RichGuideQuote"), Blank());
    public string Notes => Doc(Heading("RichNotes"), Inline("RichNotesBody", ("JSON", "code")),
        Items("orderedList", Paragraph("RichReload"), Paragraph("RichEdit"), Paragraph("RichConfirm")), Blank());
    public string Features => Doc(Heading("RichFeatures"), Quote("RichIntro"), SelectionHint(), Heading("RichTry"),
        List("RichToolsHint", "RichLinkHint", "RichJsonHint"), Block("codeBlock", "editor.Execute(RichTextCommand.Heading2);"), Blank());
    public string Limited => Doc(Heading("RichLimitTitle"), Paragraph("RichLimitBody"), Quote("RichShortNotes"));
    public string Disabled => Doc(Heading("RichDisabled"), Paragraph("RichDisabledBody"));
    public string ReadOnly => Doc(Heading("RichReadOnly"), Paragraph("RichReadOnlyBody"));
    public string Custom => Doc(Heading("RichCustom"), Paragraph("RichCustomBody"), List("RichCompactHint", "RichLinkHint"));
    public string Extensions => Doc(Heading("RichExtensible"), Inline("RichSlashHint",
        ("/", "code"), ("h", "code"), ("list", "code"), ("quote", "code"), ("action", "code")), Quote("RichCommandHint"),
        Heading("RichTryIt", 3), List("RichDateHint", "RichChecklistHint", "RichFloatingHint"), Blank());

    private static string Doc(params JsonNode[] blocks) =>
        new JsonObject { ["type"] = "doc", ["content"] = new JsonArray(blocks) }.ToJsonString();
    private static JsonObject Blank() => new() { ["type"] = "paragraph" };
    private static JsonObject Block(string type, string text) => new()
    {
        ["type"] = type, ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text })
    };
    private JsonObject SelectionHint() => Inline("RichSelectHint", (Text("RichSelectionPhrase"), "bold"));

    private JsonObject Inline(string key, params (string Text, string Mark)[] spans)
    {
        var content = new JsonArray();
        foreach (string part in Regex.Split(Text(key), @"(\{[0-9]+\})"))
        {
            if (part.Length == 0) continue;
            if (part[0] == '{' && int.TryParse(part.AsSpan(1, part.Length - 2), out int i))
            {
                var (text, mark) = spans[i];
                content.Add((JsonNode)new JsonObject { ["type"] = "text", ["text"] = text,
                    ["marks"] = new JsonArray(new JsonObject { ["type"] = mark }) });
            }
            else content.Add((JsonNode)new JsonObject { ["type"] = "text", ["text"] = part });
        }
        return new JsonObject { ["type"] = "paragraph", ["content"] = content };
    }

    private JsonObject Paragraph(string key) => Block("paragraph", Text(key));
    private JsonObject Heading(string key, int level = 2)
    {
        var block = Block("heading", Text(key));
        block["attrs"] = new JsonObject { ["level"] = level };
        return block;
    }
    private JsonObject Quote(string key) => new() { ["type"] = "blockquote", ["content"] = new JsonArray(Paragraph(key)) };
    private JsonObject List(params string[] keys) => Items("bulletList", keys.Select(Paragraph).ToArray());
    private static JsonObject Items(string type, params JsonObject[] paragraphs)
    {
        var list = new JsonObject { ["type"] = type, ["content"] = new JsonArray(paragraphs.Select(p => (JsonNode)new JsonObject
            { ["type"] = "listItem", ["content"] = new JsonArray(p) }).ToArray()) };
        if (type == "orderedList") list["attrs"] = new JsonObject { ["start"] = 1 };
        return list;
    }
}
