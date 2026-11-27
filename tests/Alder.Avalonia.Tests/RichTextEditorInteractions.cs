using System.Text.Json;
using Alder.Avalonia.Controls;
using Alder.Editor;
using Alder.Editor.Lsp;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class RichTextEditorInteractions
{
    [Fact]
    public void EditorBufferAndLanguageSessionShipWithTheControl()
    {
        var library = typeof(AlderRichTextEditor).Assembly;
        Assert.Same(library, typeof(EditorBuffer).Assembly);
        Assert.Same(library, typeof(EditorLanguageSession).Assembly);
        Assert.Same(library, typeof(CompletionResponse).Assembly);
    }

    [Fact]
    public Task LanguageServerReceivesIncrementalChangesAndRoutesTypedResponses() => AotSmoke.EditorProtocolSmoke.Run();

    [Fact]
    public void PieceTreeMatchesStringAfterMultilineEdits()
    {
        var random = new Random(2050);
        var rope = new EditorBuffer();
        var expected = string.Concat(Enumerable.Repeat("abc你好\n😀\n", 10000));
        rope.Replace(0, 0, expected);
        for (int i = 0; i < 1200; i++)
        {
            int at = random.Next(expected.Length + 1);
            int length = random.Next(Math.Min(20, expected.Length - at) + 1);
            var insert = i % 3 == 0 ? "\nhello\n" : "😀";
            rope.Replace(at, length, insert);
            expected = expected.Remove(at, length).Insert(at, insert);
            if (i % 23 != 0) continue;
            Assert.Equal(expected, rope.Text);
            Assert.Equal(expected.AsSpan(0, at).Count('\n'), rope.Row(at));
            Assert.Equal(expected.LastIndexOf('\n', Math.Max(0, at - 1)) + 1, rope.Start(rope.Row(at)));
            Assert.Equal(insert, rope.Slice(at, insert.Length));
        }
    }

    [Fact]
    public void ProtocolUnionsAcceptAlternativeWireShapes()
    {
        var json = ProtocolJson.Default;
        var plain = JsonSerializer.Deserialize("""[{"label":"a","documentation":"tip","textEdit":{"newText":"x","insert":{"start":{"line":0,"character":0},"end":{"line":0,"character":0}},"replace":{"start":{"line":0,"character":0},"end":{"line":0,"character":1}}}}]""", json.CompletionResponse);
        Assert.True(plain is Completion[] { Length: 1 } items && items[0].TextEdit is { } edit && edit is InsertReplace);
        var hover = JsonSerializer.Deserialize("""{"contents":{"value":"**word**","kind":"markdown"}}""", json.Hover);
        Assert.True(hover!.Contents is Markup { Kind: "markdown" });
        var hints = JsonSerializer.Deserialize("""[{"position":{"line":0,"character":1},"label":"note"}]""", json.InlayHintArray);
        Assert.True(hints![0].Label is string label && label == "note");
        var report = JsonSerializer.Deserialize("""{"uri":"file:///a","diagnostics":[{"range":{"start":{"line":0,"character":0},"end":{"line":0,"character":1}},"message":"typo","code":"spell"}]}""", json.PublishedDiagnostics);
        Assert.True(report!.Diagnostics[0].Code is { } code && code is string value && value == "spell");
    }

    [AvaloniaFact]
    public void SlashCommandAndTypingMarksUseTheActualInputSelection()
    {
        var editor = new AlderRichTextEditor { ShowSlashMenu = true, ShowFloatingMenu = true,
            Suggestions = [new("Timestamp", target => target.InsertText("today"))] };
        var host = new AlderTestWindow { Width = 480, Height = 240, Content = editor };
        host.Show();
        try
        {
            editor.FocusContent();
            Dispatcher.UIThread.RunJobs();
            host.KeyTextInput("/");
            Dispatcher.UIThread.RunJobs();
            host.KeyTextInput("time");
            Dispatcher.UIThread.RunJobs();
            host.UpdateLayout();
            var popup = editor.GetVisualDescendants().OfType<Popup>().Single();
            Assert.True(popup.IsOpen);
            var option = Assert.Single(popup.Child!.GetVisualDescendants().OfType<Button>());
            Assert.NotNull(TopLevel.GetTopLevel(option));
            Assert.Equal("Timestamp", AutomationProperties.GetName(option));
            host.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Assert.Equal("today", editor.PlainText);
            editor.Execute(RichTextCommand.Italic);
            host.KeyTextInput("!");
            Assert.Contains("\"italic\"", editor.Value);
            editor.Select(0, 5);
            Dispatcher.UIThread.RunJobs();
            Assert.False(editor.IsActive(RichTextCommand.Italic));
            editor.SetLink("https://example.org");
            Assert.Contains("https://example.org", editor.Value);
        }
        finally { host.Close(); }
    }

    [AvaloniaFact]
    public async Task LinkFormPreservesSelectionAndUsesAnimatedLightDismiss()
    {
        var editor = new AlderRichTextEditor { ShowBubbleMenu = false, ToolbarCommands = [RichTextCommand.Link] };
        var outside = new AlderButton { Content = "Outside" };
        var host = new AlderTestWindow { Width = 500, Height = 450, Content = new StackPanel
            { Margin = new Thickness(24, 60), Spacing = 24, Children = { editor, outside } } };
        host.Show();
        try
        {
            editor.InsertText("Add a link");
            editor.Select(6, 4);
            var trigger = editor.GetVisualDescendants().OfType<Button>().Single(b => b.Tag is RichTextCommand.Link);
            Click(trigger);
            var surface = Surface();
            var url = surface.GetVisualDescendants().OfType<AlderInput>().Single();
            Assert.Equal(InputVariant.Secondary, url.Variant);
            Assert.Equal("https://example.com", url.PlaceholderText);
            Assert.False(url.IsFocused);
            Assert.All(VisibleActions(surface), button => Assert.False(button.IsEnabled));
            host.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Assert.True(url.IsFocused);
            host.KeyTextInput("https://example.org");
            host.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            using var json = JsonDocument.Parse(editor.Value);
            var linkedText = json.RootElement.GetProperty("content")[0].GetProperty("content")[1];
            Assert.Equal("link", linkedText.GetProperty("text").GetString());
            Assert.Contains("https://example.org", linkedText.ToString());
            await Task.Delay(160);
            Assert.False(surface.IsAttachedToVisualTree());

            editor.Select(7, 0);
            Click(trigger);
            surface = Surface();
            var remove = VisibleActions(surface).First();
            Assert.True(remove.IsEnabled);
            await Task.Delay(160);
            Click(outside);
            Assert.False(surface.IsHitTestVisible);
            Assert.True(surface.IsAttachedToVisualTree());
            await Task.Delay(160);
            Assert.False(surface.IsAttachedToVisualTree());
            Assert.True(outside.IsFocused);

            AlderMotion.SetReduced(editor, true);
            Click(trigger);
            surface = Surface();
            Click(VisibleActions(surface).First());
            Assert.DoesNotContain("https://example.org", editor.Value);
            Assert.False(surface.IsAttachedToVisualTree());
            Assert.Equal("Add a link", editor.PlainText);
        }
        finally { host.Close(); }

        Control Surface() => host.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "AlderPopoverSurface");
        static IEnumerable<AlderButton> VisibleActions(Control surface) =>
            surface.GetVisualDescendants().OfType<AlderButton>().Where(button => button.IsEffectivelyVisible);
        void Click(Control control)
        {
            var point = control.TranslatePoint(new Rect(control.Bounds.Size).Center, host)!.Value;
            host.MouseDown(point, MouseButton.Left);
            host.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            host.UpdateLayout();
        }
    }

    [AvaloniaFact]
    public void StructuredCommandsReplaceTheSelectionAndUndoAsOneEdit()
    {
        var editor = new AlderRichTextEditor();
        var host = new AlderTestWindow { Width = 500, Height = 360, Content = editor };
        host.Show();
        try
        {
            editor.InsertText("before REPLACE after");
            editor.Select(0, 6);
            editor.Execute(RichTextCommand.Bold);
            const string fragment = """{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Preflight checklist"}]},{"type":"bulletList","content":[{"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"Confirm owner"}]}]}]}]}""";
            var original = editor.Value;
            editor.Select(7, 7);
            editor.InsertBlocks(fragment);
            Assert.Equal("before \nPreflight checklist\nConfirm owner\n after", editor.PlainText);
            using var result = JsonDocument.Parse(editor.Value);
            var blocks = result.RootElement.GetProperty("content");
            Assert.Equal("bold", blocks[0].GetProperty("content")[0].GetProperty("marks")[0].GetProperty("type").GetString());
            Assert.Equal("bulletList", blocks[2].GetProperty("type").GetString());
            var inserted = editor.Value;
            editor.Execute(RichTextCommand.Undo);
            Assert.Equal(original, editor.Value);
            editor.Execute(RichTextCommand.Redo);
            Assert.Equal(inserted, editor.Value);
            editor.MaxLength = editor.CharacterCount;
            editor.InsertBlocks(fragment);
            Assert.Equal(inserted, editor.Value);
            editor.MaxLength = 0;
            editor.IsReadOnly = true;
            editor.InsertBlocks(fragment);
            Assert.Equal(inserted, editor.Value);
        }
        finally { host.Close(); }
    }

    [AvaloniaFact]
    public void DividerSlashCommandLeavesAnEditableParagraphAndRoundTrips()
    {
        const string divider = """{"type":"doc","content":[{"type":"horizontalRule"}]}""";
        var editor = new AlderRichTextEditor { ShowSlashMenu = true,
            Suggestions = [new("Divider", target => target.InsertBlocks(divider)) { Keywords = "separator hr" }] };
        var host = new AlderTestWindow { Width = 500, Height = 320, Content = editor };
        host.Show();
        try
        {
            editor.FocusContent();
            host.KeyTextInput("/hr");
            Dispatcher.UIThread.RunJobs();
            host.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            host.KeyTextInput("next");
            Assert.Equal("\nnext", editor.PlainText);
            Assert.Contains("horizontalRule", editor.Value);
            var restored = new AlderRichTextEditor { Value = editor.Value };
            Assert.Equal(editor.PlainText, restored.PlainText);
        }
        finally { host.Close(); }
    }

    [AvaloniaFact]
    public void LongWrappedTextKeepsLineMetricsAfterAnEditShiftsLaterParagraphs()
    {
        var editor = new AlderRichTextEditor();
        var host = new AlderTestWindow { Width = 420, Height = 300, Content = editor };
        host.Show();
        try
        {
            host.UpdateLayout();
            var paragraphs = Enumerable.Range(0, 120).Select(i =>
                $"Line {i:D3}. Ordinary Latin text and ligatures like office must wrap consistently.");
            editor.InsertText(string.Join('\n', paragraphs));
            var before = Snapshot();
            Assert.True(before.Length > 120);
            editor.Select(0, 0);
            editor.InsertText("First paragraph\n");
            var after = Snapshot();
            Assert.Equal(before.Select(line => (line.First + 16, line.Length, line.Width, line.Height)), after.Skip(1));
            editor.Execute(RichTextCommand.Undo);
            Assert.Equal(before, Snapshot());
        }
        finally { host.Close(); }

        (int First, int Length, double Width, double Height)[] Snapshot()
        {
            Dispatcher.UIThread.RunJobs();
            host.UpdateLayout();
            var presenter = editor.GetVisualDescendants().OfType<RichTextPresenter>().Single();
            return presenter.TextLayout.TextLines.Select(line =>
                (line.FirstTextSourceIndex, line.Length, line.Width, line.Height)).ToArray();
        }
    }

    [AvaloniaFact]
    public void ActualInputFormatsRangesAndSupportsUndoLimitAndReadonly()
    {
        var editor = new AlderRichTextEditor();
        var host = new AlderTestWindow { Width = 590, Height = 280, Content = editor };
        host.Show();
        try
        {
            editor.FocusContent();
            host.KeyTextInput("A😀A");
            Assert.Equal(3, editor.CharacterCount);
            editor.Select(1, 2);
            editor.Execute(RichTextCommand.Bold);
            Assert.True(editor.IsActive(RichTextCommand.Bold));
            Assert.Contains("\"bold\"", editor.Value);
            editor.Execute(RichTextCommand.Undo);
            Assert.DoesNotContain("\"bold\"", editor.Value);
            editor.Execute(RichTextCommand.Redo);
            Assert.Contains("\"bold\"", editor.Value);
            editor.Select(4, 0);
            editor.InsertText("\nsecond");
            Assert.Equal("A😀A\nsecond", editor.PlainText);
            editor.Select(5, 6);
            editor.Execute(RichTextCommand.OrderedList);
            Assert.Contains("\"orderedList\"", editor.Value);
            editor.SetLink("https://example.org");
            Assert.Contains("https://example.org", editor.Value);
            var backup = editor.Value;
            editor.Execute(RichTextCommand.ClearContent);
            Assert.Equal("", editor.PlainText);
            editor.Value = backup;
            Assert.Equal("A😀A\nsecond", editor.PlainText);
            editor.MaxLength = editor.CharacterCount;
            editor.Select(editor.PlainText.Length, 0);
            host.KeyTextInput("X");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("A😀A\nsecond", editor.PlainText);
            editor.IsReadOnly = true;
            editor.Execute(RichTextCommand.ClearContent);
            Assert.Equal(backup, editor.Value);
        }
        finally { host.Close(); }
    }
}
