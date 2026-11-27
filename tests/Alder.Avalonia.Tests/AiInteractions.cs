using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Alder.NativeMarkdown.Core.Highlighting;
using Alder.NativeMarkdown.Core.Model;
using Alder.NativeMarkdown.Core.Streaming;
using Alder.NativeMarkdown.UI;
using Alder.NativeMarkdown.UI.Rendering;
using Alder.NativeMarkdown.UI.Selection;
using Alder.NativeMarkdown.UI.Transcript;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class AiInteractions
{
    [AvaloniaFact]
    public async Task StreamingShortFenceReusesPrefixDrawListWhileTheTailGrows()
    {
        var document = new StreamingDocument();
        document.Append("```text\nCompleted line\nGrowing");
        await document.WhenIdle();
        var markdown = new AlderMarkdown { Document = document };
        var window = Show(new AlderScrollViewer { Content = markdown });
        try
        {
            markdown.ApplyLatest();
            Pump(window);
            var code = Assert.Single(markdown.GetVisualDescendants().OfType<CodeTextView>());
            var prefix = DrawListOf(code);
            var tail = Assert.Single(code.GetVisualChildren());
            var oldTail = DrawListOf(tail);
            var bounds = code.Bounds;

            document.Append(" tail");
            await document.WhenIdle();
            markdown.ApplyLatest();
            Assert.True(code.IsMeasureValid);
            Pump(window);

            Assert.Equal(bounds, code.Bounds);
            Assert.Equal("Completed line\nGrowing tail", code.Text);
            Assert.Same(tail, Assert.Single(code.GetVisualChildren()));
            Assert.Same(prefix, DrawListOf(code));
            Assert.NotSame(oldTail, DrawListOf(tail));
        }
        finally { window.Close(); }

        static object DrawListOf(Visual visual)
        {
            // Avalonia keeps the draw list on the composition visual. Checking its identity
            // distinguishes retaining the prefix from republishing an identical drawing.
            var composition = ElementComposition.GetElementVisual(visual)!;
            var drawList = composition.GetType().GetProperty("DrawList")!.GetValue(composition);
            return Assert.IsAssignableFrom<object>(drawList);
        }
    }

    [AvaloniaFact]
    public async Task MarkdownChatTypographyPreservesOrderedMarkersAndBlockSpacing()
    {
        var document = new StreamingDocument();
        document.Load("# Title\n\nText\n\n9. Here we have a short sentence which wraps to the second line.\n10. Second\n\n> Quoted text");
        await document.WhenIdle();
        var markdown = new AlderMarkdown { Document = document, Width = 190 };
        markdown.Resources[MarkdownResourceKeys.Typography] = MarkdownTypography.Chat;
        var window = Show(markdown);
        try
        {
            markdown.ApplyLatest();
            Pump(window);
            var heading = markdown.GetVisualDescendants().OfType<TextBlockView>().Single(view => view.Block is HeadingBlock);
            var paragraph = markdown.GetVisualDescendants().OfType<TextBlockView>().First(view => view.Block is ParagraphBlock);
            var list = markdown.GetVisualDescendants().OfType<ListBlockView>().Single();
            var quote = markdown.GetVisualDescendants().OfType<QuoteBlockView>().Single();
            Assert.Equal(28, heading.Bounds.Height, 1);
            Assert.Equal(8, paragraph.Bounds.Top - heading.Bounds.Bottom, 1);
            Assert.Equal(0, list.Bounds.Top - paragraph.Bounds.Bottom, 1);
            var items = list.GetVisualChildren().OfType<ListItemBlockView>().ToArray();
            Assert.Equal(new[] { "9.", "10." }, items.Select(view => view.Marker));
            Assert.Equal(8, items[0].Bounds.Left);
            Assert.Equal(4, items[1].Bounds.Top - items[0].Bounds.Bottom);
            var text = items[0].GetVisualDescendants().OfType<TextSegmentView>().Single();
            var layout = Assert.IsType<SegmentLayout>(text.Layout);
            Assert.Equal(0, text.TranslatePoint(default, items[0])!.Value.X);
            Assert.True(layout.TextLines.Count > 1);
            var style = items[0].CurrentContext!.Style;
            foreach (var item in items)
            {
                using var marker = new global::Avalonia.Media.TextFormatting.TextLayout(item.Marker, new Typeface(style.FontFamily), style.FontSize, style.Foreground);
                var itemText = item.GetVisualDescendants().OfType<TextSegmentView>().Single();
                Assert.Equal(marker.WidthIncludingTrailingWhitespace + style.FontSize / 4, itemText.Layout!.HitTestTextPosition(0).X, 1);
            }
            Assert.Equal(0, layout.HitTestTextPosition(layout.LineStart(1)).X);
            Assert.Equal(18, quote.GetVisualDescendants().OfType<TextBlockView>().Single().Bounds.Left);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(1d)]
    [InlineData(1.25)]
    [InlineData(2d)]
    public void ModelComposerVoiceAndSendHaveMatchingCircularTargets(double scaling)
    {
        var voice = new AlderToggleButton();
        var sent = new List<object?>();
        var composer = new AlderPromptInput
        {
            MaxWidth = 640,
            SendCommand = new ActionCommand(sent.Add),
            Tools = new Border { Padding = new Thickness(0, 0, 8, 0), Child = voice }
        };
        var viewport = new Border { Child = composer };
        Container.SetName(viewport, "ModelSelectorComposer");
        Container.SetSizing(viewport, ContainerSizing.Width);
        var window = Show(viewport);
        try
        {
            window.SetRenderScaling(scaling);
            window.Width = 768;
            composer.Theme = Assert.IsType<ControlTheme>(composer.FindResource("AlderModelComposerTheme"));
            voice.Theme = Assert.IsType<ControlTheme>(composer.FindResource("AlderModelComposerVoiceTheme"));
            Pump(window);
            var send = composer.GetVisualDescendants().OfType<AlderButton>().Single(part => part.Name == "Send");
            AssertTargets(36, enabled: false);

            // The composer remains 640 DIP wide while its viewport crosses the Web breakpoint.
            window.Width = 767;
            Pump(window);
            Assert.Equal(640, composer.Bounds.Width);
            AssertTargets(40, enabled: false);

            window.Width = 312;
            composer.Text = "Draft";
            Pump(window);
            AssertTargets(40, enabled: true);

            window.Width = 820;
            Pump(window);
            AssertTargets(36, enabled: true);

            void AssertTargets(double side, bool enabled)
            {
                Assert.Equal(new Size(side, side), send.Bounds.Size);
                Assert.Equal(send.Bounds.Size, voice.Bounds.Size);
                Assert.Equal(new CornerRadius(24), send.CornerRadius);
                Assert.Equal(send.CornerRadius, voice.CornerRadius);
                Assert.True(send.IsEffectivelyVisible);
                Assert.True(voice.IsEffectivelyVisible);
                Assert.Equal(enabled, send.IsEffectivelyEnabled);
                var sendOrigin = send.TranslatePoint(default, composer)!.Value;
                var voiceOrigin = voice.TranslatePoint(default, composer)!.Value;
                Assert.InRange(sendOrigin.X - voiceOrigin.X - side, 8 - 1 / scaling, 8 + 1 / scaling);
                Assert.Equal(sendOrigin.Y, voiceOrigin.Y);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ChatListUpdatesDensityAndTheFinalDividerWhenItemsChange()
    {
        var first = new AlderListViewItem { Title = "First", Description = "Preview", Action = "2h" };
        var second = new AlderListViewItem { Title = "Second" };
        var list = new AlderChatListView { Items = { first, second } };
        var window = Show(list);
        try
        {
            Assert.True(Divider(first).IsVisible);
            Assert.False(Divider(second).IsVisible);
            list.SelectedIndex = 0;
            list.Density = ChatListDensity.Compact;
            Pump(window);
            Assert.True(first.IsSelected);
            Assert.False(first.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "ChatRowPreview").IsEffectivelyVisible);
            Assert.False(first.GetVisualDescendants().OfType<Control>().Single(control => control.Name == "ChatRowMeta").IsEffectivelyVisible);

            var third = new AlderListViewItem { Title = "Third" };
            list.Items.Add(third);
            Pump(window);
            Assert.True(Divider(second).IsVisible);
            Assert.False(Divider(third).IsVisible);
            list.Items.Remove(third);
            Pump(window);
            Assert.False(Divider(second).IsVisible);
        }
        finally { window.Close(); }

        static Border Divider(AlderListViewItem item) =>
            item.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "ChatRowDivider");
    }

    [Fact]
    public async Task StreamingKeepsCompletedPrefixAcrossIncompleteFence()
    {
        var document = new StreamingDocument();
        document.Append("# Heading\n\nFirst paragraph.\n\n```cs\nint");
        await document.WhenIdle();
        var initial = document.Latest;
        Assert.True(initial.FrozenCount > 0);
        document.Append(" n = 3;");
        await document.WhenIdle();
        Assert.Same(initial[0], document.Latest[0]);
        document.Append("\n```");
        document.Complete();
        await document.WhenIdle();
        var final = document.Latest;
        Assert.True(final.IsComplete);
        Assert.Equal("int n = 3;", Assert.IsType<CodeBlock>(final[final.Count - 1]).Code.TrimEnd('\n'));
    }

    [AvaloniaFact]
    public async Task MarkdownStreamingKeepsListAndFenceLayoutStableAcrossSplitMarkers()
    {
        var document = new StreamingDocument();
        var markdown = new AlderMarkdown { Document = document, Width = 218, VerticalAlignment = VerticalAlignment.Top };
        markdown.Resources[MarkdownResourceKeys.Typography] = MarkdownTypography.Chat;
        var window = Show(markdown);
        try
        {
            const string parentText = "A parent item whose text may wrap at this width.";
            await Append("9. First item");
            var first = FindText("First item");
            var firstBounds = first.Bounds;
            var afterFirst = markdown.Bounds.Height;
            foreach (var chunk in new[] { "\n1", "0" })
            {
                await Append(chunk);
                Assert.Same(first, FindText("First item"));
                Assert.Equal(afterFirst, markdown.Bounds.Height);
            }

            await Append(". " + parentText);
            var parent = FindText(parentText);
            var parentBounds = parent.Bounds;
            var beforeChildren = markdown.Bounds.Height;
            foreach (var chunk in new[] { "\n    -", " " })
            {
                await Append(chunk);
                Assert.Same(parent, FindText(parentText));
                Assert.Equal(parentBounds, parent.Bounds);
                Assert.Equal(beforeChildren, markdown.Bounds.Height);
            }
            await Append("Nested item.\n    - Another nested item.");
            Assert.Equal(firstBounds, first.Bounds);
            Assert.Same(parent, FindText(parentText));
            Assert.Equal(parentBounds, parent.Bounds);
            var beforeFence = markdown.Bounds.Height;
            foreach (var chunk in new[] { "\n\n`", "`", "`t", "s" })
            {
                await Append(chunk);
                Assert.Equal(beforeFence, markdown.Bounds.Height);
                Assert.Empty(markdown.GetVisualDescendants().OfType<CodeBlockView>());
            }

            await Append("\nconst n = 1;");
            var code = Assert.Single(markdown.GetVisualDescendants().OfType<CodeBlockView>());
            var codeBounds = code.Bounds;
            await Append(" // " + new string('x', 90));
            Assert.Equal(codeBounds, code.Bounds);
            var codeText = code.Code;
            foreach (var chunk in new[] { "\n`", "`", "`", "\n" })
            {
                await Append(chunk);
                Assert.Same(code, Assert.Single(markdown.GetVisualDescendants().OfType<CodeBlockView>()));
                Assert.Equal(codeText, code.Code);
                Assert.Equal(codeBounds, code.Bounds);
            }
        }
        finally { window.Close(); }

        TextSegmentView FindText(string text) => markdown.GetVisualDescendants().OfType<TextSegmentView>().Single(view => view.Content.Text == text);

        async Task Append(string chunk)
        {
            document.Append(chunk);
            await document.WhenIdle();
            markdown.ApplyLatest();
            Pump(window);
        }
    }

    [Fact]
    public async Task MarkdownStreamingRestoresLiteralMarkersWhenTheyAreDisambiguated()
    {
        var document = new StreamingDocument();
        document.Append("1. First item\n2");
        await document.WhenIdle();
        Assert.Equal("First item", FirstItemText());
        document.Append(" trailing words");
        await document.WhenIdle();
        Assert.Equal("First item 2 trailing words", FirstItemText());

        document.Append("\n\n~~~~text\ncontent\n~~~");
        await document.WhenIdle();
        Assert.Equal("content", LastCode().Code);
        document.Append(" literal");
        await document.WhenIdle();
        Assert.Equal("content\n~~~ literal", LastCode().Code);
        document.Append("\n~~");
        document.Complete();
        await document.WhenIdle();
        Assert.Equal("content\n~~~ literal\n~~", LastCode().Code);

        string FirstItemText() => Assert.IsType<ParagraphBlock>(Assert.IsType<ListBlock>(document.Latest[0]).Items[0].Children[0]).Content.Text;
        CodeBlock LastCode() => Assert.IsType<CodeBlock>(document.Latest[document.Latest.Count - 1]);
    }

    [Fact]
    public void MonarchPreservesPublishedLinesAndHandlesSplitLineEndings()
    {
        var highlighter = new AlderCodeHighlighter();
        Assert.False(highlighter.SupportsLanguage("unknown"));
        using var session = highlighter.CreateSession("cs", HighlightThemeKind.Dark);
        const string prefix = "int x = 1;\r\n/* opening";
        var previous = session.Update(prefix);
        Assert.NotEmpty(previous[0]);
        var updated = session.Update(prefix + "\r\nclosing */\nreturn;");
        Assert.Equal(4, updated.Count);
        Assert.True(previous[0] == updated[0]);
        Assert.Equal(2, previous.Count);
        Assert.Equal(updated[1][0].Foreground, updated[2][0].Foreground);
        var split = session.Update("int n;\r");
        var joined = session.Update("int n;\r\n");
        Assert.Equal(2, joined.Count);
        Assert.Equal(split[0], joined[0]);
        using var json = highlighter.CreateSession("json", HighlightThemeKind.Light);
        Assert.True(json.Update("{\"size\":42,\"enabled\":true}")[0].Length >= 4);
    }

    [Fact]
    public void CodeHighlightingUsesLanguageRolesAndRetainsLinesAcrossInteriorEdits()
    {
        var highlighter = new AlderCodeHighlighter();
        Assert.True(highlighter.SupportsLanguage("{.language-json}"));
        using var light = highlighter.CreateSession("{.language-json}", HighlightThemeKind.Light);
        using var dark = highlighter.CreateSession("application/json", HighlightThemeKind.Dark);
        const string sample = "{\"size\":42,\"name\":\"A\"}";
        var lightLine = light.Update(sample)[0];
        var darkLine = dark.Update(sample)[0];
        Assert.Equal(lightLine.Select(token => (token.Start, token.Length, token.Style)),
            darkLine.Select(token => (token.Start, token.Length, token.Style)));
        Assert.NotEqual(At(lightLine, 2), At(lightLine, 8));
        Assert.NotEqual(At(lightLine, 14), At(lightLine, 19));
        Assert.NotEqual(At(lightLine, 2), At(darkLine, 2));

        using var typescript = highlighter.CreateSession("ts", HighlightThemeKind.Light);
        const string example = "function greet(name: string) { return `Hello, ${name}!`; }";
        var typed = typescript.Update(example)[0];
        Assert.Equal(0xff584cadU, At(typed, 0));
        Assert.Equal(0xff852e2cU, At(typed, example.IndexOf("greet", StringComparison.Ordinal)));
        Assert.Equal(0xff704b00U, At(typed, example.IndexOf("name", StringComparison.Ordinal)));
        Assert.Equal(0xff006292U, At(typed, example.IndexOf("string", StringComparison.Ordinal)));
        Assert.Equal(0xff006b2dU, At(typed, example.IndexOf('$')));
        Assert.Equal(0xff006b2dU, At(typed, example.IndexOf('}')));
        Assert.Equal(0xff585858U, At(typed, example.IndexOf('(')));

        using var code = highlighter.CreateSession("cs", HighlightThemeKind.Light);
        const string csharpExample = "Console.WriteLine($\"Hi {name}\");";
        var csharp = code.Update(csharpExample)[0];
        Assert.Equal(0xff006292U, At(csharp, 0));
        Assert.Equal(0xff852e2cU, At(csharp, csharpExample.IndexOf("WriteLine", StringComparison.Ordinal)));
        Assert.Equal(0xff006b2dU, At(csharp, csharpExample.IndexOf("Hi", StringComparison.Ordinal)));
        Assert.Equal(0xff585858U, At(csharp, csharpExample.IndexOf('{')));

        var before = code.Update("int first;\nint middle;\nreturn 1;\nreturn 2;");
        var after = code.Update("int first;\nint revised;\nreturn 1;\nreturn 2;");
        Assert.Equal(before[0], after[0]);
        Assert.Equal(before[2], after[2]);
        Assert.Equal(before[3], after[3]);
        var comment = code.Update("int first;\n/* revised;\nreturn 1;\n*/ return 2;");
        Assert.True(comment[2][0].Style.HasFlag(HighlightFontStyle.Italic));
        Assert.NotEqual(At(before[2], 0), At(comment[2], 0));

        using var longLine = highlighter.CreateSession("cs", HighlightThemeKind.Light);
        var oversized = longLine.Update("/*" + new string('x', 4200) + "\nreturn 2;");
        Assert.Empty(oversized[0]);
        Assert.Equal(At(before[3], 0), At(oversized[1], 0));

        static uint At(ImmutableArray<HighlightToken> line, int offset) =>
            line.Single(token => token.Start <= offset && offset < token.Start + token.Length).Foreground;
    }

    [Theory]
    [InlineData("CS", "C#")]
    [InlineData("TypeScript", "TypeScript")]
    [InlineData(".axaml", "Avalonia XAML")]
    [InlineData("{.language-json}", "json")]
    [InlineData(null, "Plain text")]
    public void CodeBlockLanguageLabelsUseReadableCasing(string? input, string label)
    {
        Assert.Equal(label, CodeBlockView.LanguageLabel(input));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CodeScrollbarDragsToBothEndsWithoutSelectingText(bool fenced)
    {
        var source = new string('x', 200) + " end";
        MarkdownView view = fenced
            ? new AlderMarkdown { Markdown = "```text\n" + source + "\n```" }
            : new AlderCodeBlock { Code = source };
        view.Width = 320;
        var window = Show(new AlderScrollViewer { Content = view });
        try
        {
            // The fenced Markdown case parses on a worker before creating the code view.
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (!view.GetVisualDescendants().OfType<CodeTextView>().Any() && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
                Pump(window);
            }
            var text = Assert.Single(view.GetVisualDescendants().OfType<CodeTextView>());
            var origin = text.TranslatePoint(default, window)!.Value;
            var trackY = origin.Y + text.Bounds.Height - 4;
            var left = new Point(origin.X + 5, trackY);
            var right = new Point(origin.X + text.Bounds.Width - 5, trackY);
            var beyondRight = right + new Vector(32, 12);
            var beyondLeft = left - new Vector(32, -12);
            var initialEnd = text.GetCharacterBounds(source.Length).X;
            Assert.True(initialEnd > text.Bounds.Width);

            Drag(left, beyondRight);
            Assert.InRange(text.GetCharacterBounds(source.Length).X, text.Bounds.Width - 2, text.Bounds.Width + 2);
            Drag(right, beyondLeft);
            Assert.Equal(initialEnd, text.GetCharacterBounds(source.Length).X, 2);

            var middle = new Point((left.X + right.X) / 2, trackY);
            window.MouseMove(middle);
            window.MouseDown(middle, MouseButton.Left);
            window.MouseUp(middle, MouseButton.Left);
            Assert.InRange(text.GetCharacterBounds(source.Length).X, text.Bounds.Width + 4, initialEnd - 4);
            Assert.True(((ISelectionHost)view).Selection.IsEmpty);

            void Drag(Point start, Point end)
            {
                window.MouseMove(start);
                window.MouseDown(start, MouseButton.Left);
                window.MouseMove(end, RawInputModifiers.LeftMouseButton);
                window.MouseUp(end, MouseButton.Left);
                Pump(window);
            }
        }
        finally { window.Close(); }
    }

    [Fact]
    public void JsxTagsRemainHighlightedWhenAnAttributeSpansChunks()
    {
        var highlighter = new AlderCodeHighlighter();
        foreach (string language in new[] { "jsx", "tsx" })
        {
            using var session = highlighter.CreateSession(language, HighlightThemeKind.Light);
            const string prefix = "<ChatMessage.Body\n  choice={";
            var first = session.Update(prefix);
            var completed = session.Update(prefix + "value}>text</ChatMessage.Body>");
            Assert.Contains(first[0], token => token.Start == 1 && token.Length == "ChatMessage.Body".Length &&
                token.Foreground == 0xff006292U);
            Assert.Equal(first[0], completed[0]);
            Assert.Contains(completed[1], token => token.Foreground == 0xff006292U);
            var openAttribute = session.Update("<Card title=\"你好\n下一行");
            Assert.Contains(openAttribute[0], token => token.Start == 6 && token.Foreground == 0xff704b00U);
            Assert.Contains(openAttribute[0], token => token.Start == 11 && token.Foreground == 0xff585858U);
            Assert.Contains(openAttribute[0], token => token.Start == 12 && token.Foreground == 0xff006b2dU);
            Assert.Equal(0xff006b2dU, Assert.Single(openAttribute[1]).Foreground);
            var closedAttribute = session.Update("<Card title=\"你好\n下一行\" />");
            Assert.Equal(openAttribute[0], closedAttribute[0]);
            Assert.Contains(closedAttribute[1], token => token.Foreground == 0xff006b2dU);

            const string nested = "<><Card>Rendered <Label>{value}</Label></Card></>; const next = true;";
            var children = session.Update(nested)[0];
            var textStart = nested.IndexOf("Rendered", StringComparison.Ordinal);
            Assert.DoesNotContain(children, token => token.Start <= textStart && textStart < token.Start + token.Length);
            Assert.Contains(children, token => token.Start == nested.IndexOf("Label", StringComparison.Ordinal) && token.Foreground == 0xff006292U);
            Assert.Contains(children, token => token.Start == nested.IndexOf("const", StringComparison.Ordinal) && token.Foreground == 0xff584cadU);
        }
    }

    [AvaloniaFact]
    public void ActivityTimelineUsesOneTriggerAndMapsInterruptedSteps()
    {
        var completed = new AlderChainOfThoughtStep { Header = "Read configuration", Status = AiRunStatus.Completed };
        var active = new AlderChainOfThoughtStep { Header = "Write changes", Status = AiRunStatus.Running };
        var pending = new AlderChainOfThoughtStep { Header = "Run tests", Status = AiRunStatus.Pending, IsVisible = true };
        var run = new AlderChainOfThought
        {
            Header = "Updating files",
            Status = AiRunStatus.Running,
            IsExpanded = true,
            Content = new AlderChainOfThoughtSteps { Children = { completed, active, pending } }
        };
        var window = Show(run);
        try
        {
            var trigger = Assert.Single(run.GetVisualDescendants().OfType<global::Avalonia.Controls.Primitives.ToggleButton>());
            var ring = active.GetVisualDescendants().OfType<AlderLoader>().Single();
            var connector = active.GetVisualDescendants().OfType<Panel>().Single(panel => panel.Name == "Connector");
            Assert.True(ring.IsEffectivelyVisible);
            Assert.True(connector.IsEffectivelyVisible);

            run.Status = AiRunStatus.Stopped;
            Pump(window);
            Assert.False(pending.IsVisible);
            Assert.False(ring.IsEffectivelyVisible);
            Assert.False(connector.IsEffectivelyVisible);
            var stop = active.GetVisualDescendants().OfType<Border>().Single(mark => mark.Name == "InterruptedMark");
            Assert.Equal(1, stop.Opacity);

            run.Status = AiRunStatus.Failed;
            Pump(window);
            Assert.False(pending.IsVisible);
            Assert.Equal(1, stop.Opacity);

            run.Status = AiRunStatus.Running;
            Pump(window);
            Assert.True(pending.IsVisible);
            Assert.True(ring.IsEffectivelyVisible);
            Assert.True(connector.IsEffectivelyVisible);

            trigger.Focus();
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Pump(window);
            Assert.False(run.IsExpanded);
            Assert.False(ring.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ReasoningStatusFlowsThroughStepListsAndCanBeOverridden()
    {
        var active = new AlderChainOfThoughtStep { Header = "Editing", Status = AiRunStatus.Running };
        var pending = new AlderChainOfThoughtStep { Header = "Verify", Status = AiRunStatus.Pending };
        var steps = new AlderChainOfThoughtSteps { Children = { active, pending } };
        var reasoning = new AlderReasoning { Status = AiRunStatus.Running, IsExpanded = true, Content = steps };
        var window = Show(reasoning);
        try
        {
            var ring = Assert.Single(active.GetVisualDescendants().OfType<AlderLoader>());
            Assert.True(ring.IsEffectivelyVisible);
            Assert.True(pending.IsEffectivelyVisible);

            reasoning.Status = AiRunStatus.Stopped;
            Pump(window);
            Assert.True(reasoning.IsExpanded);
            Assert.False(ring.IsEffectivelyVisible);
            Assert.False(pending.IsVisible);

            steps.Status = AiRunStatus.Running;
            Pump(window);
            Assert.True(ring.IsEffectivelyVisible);
            Assert.True(pending.IsEffectivelyVisible);

            steps.Status = null;
            reasoning.Status = AiRunStatus.Failed;
            Pump(window);
            Assert.False(pending.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void LiteralAndFencedCodeUseSameRendererAndKeepExactSource()
    {
        const string source = "  string a;\r\n```\nlast";
        var literal = new AlderCodeBlock { Code = source, Language = "cs" };
        var document = new StreamingDocument();
        document.LoadNow("~~~~cs\nstring b;\n~~~~");
        var markdown = new AlderMarkdown { Document = document };
        var window = Show(new StackPanel { Children = { literal, markdown } });
        try
        {
            literal.ApplyLatest();
            markdown.ApplyLatest();
            Pump(window);
            var standalone = Assert.Single(literal.GetVisualDescendants().OfType<CodeBlockView>());
            Assert.Equal(source, standalone.Code);
            Assert.Single(markdown.GetVisualDescendants().OfType<CodeBlockView>());
            var state = CodeBlockState.For(standalone.CurrentContext!, standalone.TopKey, "cs");
            Assert.Equal(3, state.LineCount);
            Assert.Equal(source.IndexOf('`'), state.LineStart(1));
            Assert.Equal("```", state.LineText(1));
            literal.Code += "\r\nnew";
            Assert.Equal("new", state.LineText(3));
            literal.Code = "replacement";
            Assert.Equal(1, state.LineCount);
            Assert.Equal("replacement", state.LineText(0));
            literal.Code += "\r";
            Assert.Equal(2, state.LineCount);
            Assert.Equal("replacement", state.LineText(0));
            literal.Code += "\nsecond";
            Assert.Equal(2, state.LineCount);
            Assert.Equal("replacement\r\n".Length, state.LineStart(1));
            Assert.Equal("second", state.LineText(1));
            var longSuffix = new string('x', 5000);
            literal.Code += "\n" + longSuffix;
            Assert.Equal(3, state.LineCount);
            Assert.Equal("replacement\r\nsecond\n".Length, state.LineStart(2));
            Assert.Equal(longSuffix, state.LineText(2));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ReasoningExpandsOnTheFirstVisibleTextAndWaitsForFocusBeforeCollapsing()
    {
        var text = new AlderTextBlock();
        var trace = new AlderReasoning
        {
            Status = AiRunStatus.Running, CollapseDelay = TimeSpan.Zero, Duration = "3s", Content = text
        };
        var outside = new AlderButton { Content = "Outside" };
        var window = Show(new StackPanel { Children = { trace, outside } });
        try
        {
            Assert.False(trace.IsExpanded);
            Assert.Equal(AiLoaderVariant.Grid, trace.LoaderVariant);
            text.Text = "  ";
            Assert.False(trace.IsExpanded);
            text.Text = "First token";
            Assert.True(trace.IsExpanded);
            Pump(window);

            Assert.True(trace.Trigger!.Focus(NavigationMethod.Tab));
            trace.Status = AiRunStatus.Completed;
            await Task.Delay(75);
            Pump(window);
            Assert.Equal("Thought for 3s", trace.ResolvedHeader);
            var label = trace.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "ReasoningStatusLabel");
            Assert.Equal("Thought for 3s", string.Concat(label.Inlines!.OfType<Run>().Select(run => run.Text)));
            Assert.True(trace.IsExpanded);
            Assert.True(outside.Focus(NavigationMethod.Tab));
            Pump(window);
            Assert.False(trace.IsExpanded);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ReasoningPreservesManualAndTerminalExpansion()
    {
        var manual = new AlderReasoning { Status = AiRunStatus.Running, Text = "Thought", CollapseDelay = TimeSpan.Zero };
        var stopped = new AlderReasoning { Status = AiRunStatus.Running, Text = "Partial", CollapseDelay = TimeSpan.Zero };
        var history = new AlderReasoning { Duration = "9s", IsExpanded = true };
        var window = Show(new StackPanel { Children = { manual, stopped, history } });
        try
        {
            Assert.Equal(AiRunStatus.Completed, history.Status);
            Assert.Equal("Thought for 9s", history.ResolvedHeader);
            ToggleWithPointer(manual);
            Assert.False(manual.IsExpanded);
            manual.Text += " continues";
            Assert.False(manual.IsExpanded);
            ToggleWithPointer(manual);
            Assert.True(manual.IsExpanded);
            manual.Status = AiRunStatus.Completed;
            stopped.Status = AiRunStatus.Stopped;
            Assert.Equal("Stopped thinking", stopped.ResolvedHeader);
            stopped.Status = AiRunStatus.Completed;
            await Task.Delay(75);
            Pump(window);
            Assert.True(manual.IsExpanded);
            Assert.True(stopped.IsExpanded);
            Assert.True(history.IsExpanded);
        }
        finally { window.Close(); }

        void ToggleWithPointer(AlderReasoning target)
        {
            var trigger = target.Trigger!;
            var p = trigger.TranslatePoint(new Rect(trigger.Bounds.Size).Center, window)!.Value;
            window.MouseMove(p);
            window.MouseDown(p, MouseButton.Left);
            window.MouseUp(p, MouseButton.Left);
            Pump(window);
        }
    }

    [AvaloniaFact]
    public async Task CodeBlockCopyUsesCompactHeaderButtonAndCopiesTheLiteralTextWithTheKeyboard()
    {
        const string source = "  const x = 1;\r\n\r\nconsole.log(x);";
        var code = new AlderCodeBlock { Code = source, Language = "typescript", Width = 312 };
        var window = Show(code);
        try
        {
            var block = Assert.Single(code.GetVisualDescendants().OfType<CodeBlockView>());
            var copy = Assert.Single(block.GetVisualDescendants().OfType<CodeCopyButton>(), button => button.Name == "PART_CopyCode");
            Assert.Equal(new Size(24, 24), copy.Bounds.Size);
            Assert.False(copy.ClipToBounds);
            Assert.Equal(new Point(block.Bounds.Width - 36, 8), copy.TranslatePoint(default, block));
            Assert.Equal("Copy code", AutomationProperties.GetName(copy));
            Assert.NotNull(copy.GetValue(CodeCopyButton.CopyIconProperty));
            Assert.Empty(block.GetVisualDescendants().OfType<IconButton>());
            Assert.True(copy.Focus(NavigationMethod.Tab));
            Pump(window);
            var tint = Assert.Single(copy.GetVisualDescendants().OfType<ButtonTint>());
            Assert.Equal(1, tint.Opacity);
            Assert.Null(copy.BorderBrush);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump(window);
            Assert.Equal(source, await window.Clipboard!.TryGetTextAsync());
            Assert.True(copy.IsCopied);
            code.Code = "replacement";
            Pump(window);
            Assert.False(copy.IsCopied);
            AlderMotion.SetReduced(window, false);
            var faceVisual = global::Avalonia.Rendering.Composition.ElementComposition.GetElementVisual(copy)!;
            Assert.Null(faceVisual.ImplicitAnimations);
            var target = copy.TranslatePoint(new Rect(copy.Bounds.Size).Center, window)!.Value;
            window.MouseMove(target);
            Pump(window);
            Assert.Same(tint, Assert.Single(copy.GetVisualDescendants().OfType<ButtonTint>()));
            var tintVisual = global::Avalonia.Rendering.Composition.ElementComposition.GetElementVisual(tint)!;
            Assert.NotNull(tintVisual.ImplicitAnimations);
            window.MouseDown(target, MouseButton.Left);
            Pump(window);
            Assert.True(copy.IsPressed);
            Assert.NotNull(faceVisual.ImplicitAnimations);
            window.MouseUp(target, MouseButton.Left);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void LoaderSizeReservesTheInfinityAspectAndCustomLabelHeight()
    {
        var loader = new AlderLoader { Variant = AiLoaderVariant.Infinity, Size = SpinnerSize.Large };
        var window = Show(loader);
        try
        {
            Assert.Equal(new Size(36, 24), loader.Bounds.Size);
            loader.Size = SpinnerSize.Small;
            Pump(window);
            Assert.Equal(new Size(24, 16), loader.Bounds.Size);
            var peer = ControlAutomationPeer.CreatePeerForElement(loader);
            Assert.Equal("Loading", peer.GetName());
            loader.Label = new AlderTextShimmer { Text = "Thinking", FontSize = 14 };
            Pump(window);
            Assert.Equal("Thinking", peer.GetName());
            Assert.Equal(20, loader.Bounds.Height);
            Assert.True(loader.Bounds.Width > 32);
            loader.Label = null;
            loader.Variant = AiLoaderVariant.Grid;
            Pump(window);
            Assert.Equal(new Size(16, 16), loader.Bounds.Size);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ChatAttachmentSwitchesFileAndMediaLayoutsAndKeepsRemovalIndependent()
    {
        var removed = new List<object?>();
        var opened = new List<object?>();
        var attachment = new AlderChatAttachment
        {
            FileName = "a-very-long-project-financial-summary-to-be-truncated.pdf",
            FileSize = 824_120,
            CommandParameter = "attachment",
            OpenCommand = new ActionCommand(opened.Add),
            RemoveCommand = new ActionCommand(removed.Add)
        };
        var window = Show(attachment);
        try
        {
            var caption = attachment.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "Caption");
            var remove = attachment.GetVisualDescendants().OfType<AlderButton>().Single(button => button.Name == "Remove");
            var name = attachment.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "FileName");
            Assert.Equal(ChatAttachmentFileKind.Pdf, attachment.FileKind);
            Assert.Equal("805 KB", attachment.DisplayDescription);
            Assert.Equal(64, attachment.Bounds.Height);
            Assert.InRange(attachment.Bounds.Width, 250, 256);
            Assert.Equal(global::Avalonia.Media.TextTrimming.CharacterEllipsis, name.TextTrimming);
            Assert.Equal(20, remove.Bounds.Width);
            Assert.False(caption.IsEffectivelyVisible);

            attachment.MimeType = "application/zip";
            attachment.Description = "Uploading";
            Assert.Equal(ChatAttachmentFileKind.Archive, attachment.FileKind);
            Assert.Equal("Uploading", attachment.DisplayDescription);
            attachment.Content = new Border { Background = global::Avalonia.Media.Brushes.Red };
            Pump(window);
            Assert.Equal(new Size(64, 64), attachment.Bounds.Size);
            Assert.Equal(0, remove.Opacity);

            var center = attachment.TranslatePoint(new Point(30, 30), window)!.Value;
            window.MouseMove(center);
            Pump(window);
            Assert.Equal(1, caption.Opacity);
            Assert.Equal(1, remove.Opacity);
            var removeCenter = remove.TranslatePoint(remove.Bounds.Center - remove.Bounds.Position, window)!.Value;
            window.MouseMove(removeCenter);
            window.MouseDown(removeCenter, MouseButton.Left);
            window.MouseUp(removeCenter, MouseButton.Left);
            Pump(window);
            Assert.Equal(new object?[] { "attachment" }, removed);
            Assert.Empty(opened);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PromptAttachmentOpensItsImageAndRestoresFocusOnDismissal()
    {
        var image = new DrawingImage
        {
            Drawing = new GeometryDrawing { Brush = Brushes.CornflowerBlue, Geometry = new RectangleGeometry(new Rect(0, 0, 160, 90)) }
        };
        var attachment = new AlderChatAttachment { FileName = "screenshot.png", Preview = image };
        var prompt = new AlderPromptInput { Attachments = attachment };
        var window = Show(prompt);
        try
        {
            var open = attachment.GetVisualDescendants().OfType<AlderButton>().Single(button => button.Name == "Open");
            Assert.True(open.IsEffectivelyVisible);
            var center = open.TranslatePoint(new Rect(open.Bounds.Size).Center, window)!.Value;
            window.MouseMove(center);
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Pump(window);
            var viewer = Assert.Single(window.GetVisualDescendants().OfType<ImageViewer>());
            Assert.True(viewer.IsFocused);

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump(window);
            Assert.Empty(window.GetVisualDescendants().OfType<ImageViewer>());
            Assert.True(open.IsFocused);

            var customOpens = 0;
            attachment.OpenCommand = new ActionCommand(_ => customOpens++);
            Pump(window);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Assert.Equal(1, customOpens);
            Assert.Empty(window.GetVisualDescendants().OfType<ImageViewer>());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task AttachmentComposerReceivesMultipleFilesAndResetsItsDropState()
    {
        var received = new List<object?>();
        var prompt = new AlderPromptInput { FileDropCommand = new ActionCommand(received.Add) };
        var window = Show(prompt);
        var directory = Directory.CreateTempSubdirectory("alder-attachment-");
        try
        {
            async Task<IStorageFile> FileHandle(string name)
            {
                var path = Path.Combine(directory.FullName, name);
                File.WriteAllText(path, "test");
                return Assert.IsAssignableFrom<IStorageFile>(await window.StorageProvider.TryGetFileFromPathAsync(path));
            }

            using var first = await FileHandle("one.pdf");
            using var second = await FileHandle("two.zip");
            using var data = new DataTransfer();
            data.Add(DataTransferItem.CreateFile(first));
            data.Add(DataTransferItem.CreateFile(second));
            var editor = prompt.GetVisualDescendants().OfType<TextBox>().Single();
            var position = new Point(5, 5);
            void Raise(global::Avalonia.Interactivity.RoutedEvent<DragEventArgs> eventType)
                => editor.RaiseEvent(new DragEventArgs(eventType, data, editor, position, KeyModifiers.None)
                    { DragEffects = DragDropEffects.Copy });

            Assert.True(DragDrop.GetAllowDrop(prompt));
            Raise(DragDrop.DragOverEvent);
            Assert.Contains(":file-over", prompt.Classes);
            Raise(DragDrop.DragLeaveEvent);
            Assert.DoesNotContain(":file-over", prompt.Classes);
            Raise(DragDrop.DragOverEvent);
            Raise(DragDrop.DropEvent);
            var files = Assert.IsAssignableFrom<IReadOnlyList<IStorageFile>>(Assert.Single(received));
            Assert.Equal(new[] { first, second }, files);
            Assert.DoesNotContain(":file-over", prompt.Classes);
        }
        finally
        {
            window.Close();
            directory.Delete(recursive: true);
        }
    }

    [AvaloniaFact]
    public void ComposerSendsOnceAndApprovalLocksAfterDecision()
    {
        var submitted = new List<object?>();
        var command = new ActionCommand(submitted.Add);
        var prompt = new AlderPromptInput { SendCommand = command, Text = "draft" };
        var card = new AlderApprovalCard
        {
            Header = "Publish?", ApproveLabel = "Send", ApproveCommand = command, CommandParameter = "approved",
            Footer = new AlderTextBlock { Text = "Progress", Height = 84 }
        };
        var window = Show(new StackPanel { Children = { prompt, card } });
        try
        {
            var editor = prompt.GetVisualDescendants().OfType<TextBox>().Single();
            editor.Focus();
            void Enter(RawInputModifiers mods)
            {
                window.KeyPressQwerty(PhysicalKey.Enter, mods);
                window.KeyReleaseQwerty(PhysicalKey.Enter, mods);
                Pump(window);
            }
            Enter(RawInputModifiers.None);
            Assert.Equal(new object?[] { "draft" }, submitted);
            Enter(RawInputModifiers.Shift);
            Assert.Single(submitted);
            prompt.IsRunning = true;
            Enter(RawInputModifiers.None);
            Assert.Single(submitted);
            prompt.IsRunning = false;
            prompt.Text = " ";
            Enter(RawInputModifiers.None);
            Assert.Single(submitted);
            var approve = card.GetVisualDescendants().OfType<AlderButton>().Single(b => b.Name == "PART_Approve");
            Assert.Equal(32, approve.Bounds.Height);
            var point = approve.TranslatePoint(new Rect(approve.Bounds.Size).Center, window)!.Value;
            for (int i = 0; i < 2; i++)
            {
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                Pump(window);
            }
            Assert.Equal(AiDecision.Approved, card.Decision);
            Assert.Equal(new object?[] { "draft", "approved" }, submitted);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TokenCompletionPrecedesSubmitAndInlineLayoutSharesOneRow()
    {
        var sent = new List<object?>();
        var prompt = new AlderPromptInput { Tokens = ["@sam", "@sara", "/review"], Variant = AiPromptVariant.Inline,
            SendCommand = new ActionCommand(sent.Add) };
        var window = Show(prompt);
        try
        {
            var editor = prompt.GetVisualDescendants().OfType<TextBox>().Single();
            editor.Focus();
            editor.Text = "Ask @s";
            editor.CaretIndex = editor.Text.Length;
            Pump(window);
            var popup = prompt.GetVisualDescendants().OfType<global::Avalonia.Controls.Primitives.Popup>()
                .Single(part => part.Name == "PART_TokenPopup");
            var choices = Assert.IsAssignableFrom<ListBox>(Assert.IsType<Border>(popup.Child).Child);
            Assert.True(choices.IsVisible);
            Assert.True(popup.IsOpen);
            Assert.NotNull(popup.PlacementRect);
            Assert.Equal(2, choices.ItemCount);
            Assert.Equal(2, Grid.GetRow(editor));
            void Key(PhysicalKey key)
            {
                window.KeyPressQwerty(key, RawInputModifiers.None);
                window.KeyReleaseQwerty(key, RawInputModifiers.None);
                Pump(window);
            }
            Key(PhysicalKey.Escape);
            Assert.False(popup.IsOpen);
            editor.Text = "Ask @sa";
            editor.CaretIndex = editor.Text.Length;
            Pump(window);
            Assert.False(popup.IsOpen);
            editor.CaretIndex = 0;
            editor.CaretIndex = editor.Text.Length;
            Pump(window);
            Assert.True(popup.IsOpen);
            Key(PhysicalKey.ArrowDown);
            Key(PhysicalKey.Enter);
            Assert.Equal("Ask @sara ", prompt.Text);
            Assert.False(choices.IsVisible);
            Assert.False(popup.IsOpen);
            Assert.Empty(sent);
            Key(PhysicalKey.Enter);
            Assert.Equal(new object?[] { "Ask @sara " }, sent);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PromptSuggestionKeepsItsLabelInPlaceWhenTheHoverArrowAppears()
    {
        var selected = new List<object?>();
        var item = new AlderPromptSuggestion
        {
            Width = 260, Content = "Summarize this meeting", Description = "Capture next steps.",
            Command = new ActionCommand(selected.Add), CommandParameter = "meeting"
        };
        var window = Show(new Border { Padding = new Thickness(20), Child = item });
        try
        {
            window.MouseMove(new Point(5, 5));
            Pump(window);
            var arrow = item.GetVisualDescendants().OfType<AlderPathIcon>().Single(icon => icon.Name == "EndArrow");
            var presenter = item.GetVisualDescendants().OfType<global::Avalonia.Controls.Presenters.ContentPresenter>()
                .Single(part => part.Name == "PART_ContentPresenter");
            var resting = presenter.Bounds;
            Assert.Equal(0, arrow.Opacity);
            window.MouseMove(item.TranslatePoint(new Point(60, 24), window)!.Value);
            Pump(window);
            Assert.Equal(1, arrow.Opacity);
            Assert.Equal(resting, presenter.Bounds);

            item.ShowEndIcon = false;
            Pump(window);
            Assert.False(arrow.IsEffectivelyVisible);
            item.ShowEndIcon = true;
            item.Appearance = PromptSuggestionAppearance.Card;
            Pump(window);
            Assert.False(arrow.IsEffectivelyVisible);
            Assert.Equal(default, item.BorderThickness);
            Assert.Equal(FontWeight.Medium, item.FontWeight);

            item.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Assert.Equal(new object?[] { "meeting" }, selected);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CompactPromptStacksOnWrapAndLocksOnlyDuringItsRun()
    {
        var stopped = new List<object?>();
        var prompt = new AlderPromptInput
        {
            Width = 380, Variant = AiPromptVariant.Compact,
            SendCommand = new ActionCommand(_ => { }), StopCommand = new ActionCommand(stopped.Add),
        };
        var window = Show(prompt);
        try
        {
            prompt.Theme = Assert.IsType<ControlTheme>(prompt.FindResource("AlderPromptComposerTheme"));
            Pump(window);
            var editor = prompt.GetVisualDescendants().OfType<TextBox>().Single();
            Assert.Equal(2, Grid.GetRow(editor));
            prompt.Text = string.Join(" ", Enumerable.Repeat("Wrap this text at the single-row width.", 5));
            Pump(window);
            Assert.Equal(1, Grid.GetRow(editor));
            prompt.Text = "Short";
            Pump(window);
            Assert.Equal(2, Grid.GetRow(editor));
            prompt.Attachments = new Border { Height = 64 };
            Pump(window);
            Assert.Equal(1, Grid.GetRow(editor));
            prompt.Attachments = null;
            prompt.IsSubmitting = true;
            Pump(window);
            var pending = prompt.GetVisualDescendants().OfType<AlderButton>().Single(button => button.Name == "Pending");
            var stop = prompt.GetVisualDescendants().OfType<AlderButton>().Single(button => button.Name == "Stop");
            var send = prompt.GetVisualDescendants().OfType<AlderButton>().Single(button => button.Name == "Send");
            Assert.True(pending.IsEffectivelyVisible);
            Assert.False(send.IsEffectivelyVisible || stop.IsEffectivelyVisible || editor.IsEffectivelyEnabled);
            prompt.IsRunning = true;
            prompt.IsSubmitting = false;
            Pump(window);
            Assert.True(stop.IsEffectivelyVisible);
            Assert.False(pending.IsEffectivelyVisible || editor.IsEffectivelyEnabled);
            prompt.LockInputOnRun = false;
            Pump(window);
            Assert.True(editor.IsEffectivelyEnabled);
            prompt.LockInputOnRun = true;
            Pump(window);
            var center = stop.TranslatePoint(new Point(stop.Bounds.Width / 2, stop.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center);
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Assert.Single(stopped);
            prompt.IsRunning = false;
            Pump(window);
            Assert.True(send.IsEffectivelyVisible && editor.IsEffectivelyEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SourceUsesACompactChipAndSwitchesBetweenLogoAndVectorIcon()
    {
        var logo = new DrawingImage();
        var source = new AlderChatSource { Content = "Documentation", Logo = logo };
        var window = Show(source);
        try
        {
            var image = source.GetVisualDescendants().OfType<Image>().Single(part => part.Name == "CitationLogo");
            var icon = source.GetVisualDescendants().OfType<AlderIconPresenter>().Single(part => part.Name == "CitationIcon");
            Assert.Equal(24, source.Bounds.Height);
            Assert.Same(logo, image.Source);
            Assert.True(image.IsEffectivelyVisible);
            Assert.False(icon.IsEffectivelyVisible);

            source.Logo = null;
            Pump(window);
            Assert.False(image.IsEffectivelyVisible);
            Assert.True(icon.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ToolSummaryBecomesExpandableWhenInputArrives()
    {
        var tool = new AlderChatTool { Header = "Used tool:", ToolName = "searchDocs", Status = AiRunStatus.Completed };
        var window = Show(tool);
        try
        {
            var trigger = tool.Trigger!;
            var indicator = tool.GetVisualDescendants().OfType<PathIcon>().Single(icon => icon.Name == "PART_Indicator");
            Assert.False(trigger.IsEnabled);
            Assert.False(indicator.IsEffectivelyVisible);
            tool.Arguments = "{}";
            Pump(window);
            Assert.True(trigger.IsEnabled);
            Assert.True(indicator.IsEffectivelyVisible);
            tool.IsExpanded = true;
            Pump(window);
            Assert.Contains(tool.GetVisualDescendants().OfType<AlderCodeBlock>(), code => code.IsEffectivelyVisible && code.Code == "{}");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ToolUsesCompactCodeForJsonAndDisplaysAnErrorOnlyOnce()
    {
        var tool = new AlderChatTool { Arguments = """{"city":"Paris"}""", Result = "Request timed out",
            Status = AiRunStatus.Failed, IsExpanded = true };
        var window = Show(tool);
        try
        {
            AlderCodeBlock[] VisibleCode() => tool.GetVisualDescendants().OfType<AlderCodeBlock>()
                .Where(code => code.IsEffectivelyVisible).ToArray();
            var payload = Assert.Single(VisibleCode());
            Assert.Equal(tool.Arguments, payload.Code);
            Assert.False(Alder.NativeMarkdown.UI.Extensions.MarkdownServices.GetServices(payload).CodeBlocks.ShowHeader);
            var error = tool.GetVisualDescendants().OfType<SelectableTextBlock>().Single(text => text.Name == "ResultText");
            Assert.True(error.IsEffectivelyVisible);
            tool.Result = """{"temperature":18}""";
            tool.Status = AiRunStatus.Completed;
            Pump(window);
            Assert.False(error.IsEffectivelyVisible);
            Assert.Equal(new[] { tool.Arguments, tool.Result }, VisibleCode().Select(code => code.Code));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ToolShowsOneStreamingLabelAndBindsPendingApprovalCommands()
    {
        const string prefix = "{\"query\":\"Alder";
        var decisions = new List<(bool Approved, object? Parameter)>();
        var tool = new AlderChatTool
        {
            Header = "Running tool:", ToolName = "searchDocs", Status = AiRunStatus.Running,
            IsExpanded = true, Arguments = prefix,
            ApproveCommand = new ActionCommand(parameter => decisions.Add((true, parameter))),
            RejectCommand = new ActionCommand(parameter => decisions.Add((false, parameter))),
            ApprovalCommandParameter = "call-1",
        };
        var summary = new AlderChatTool { Header = "Completed", Status = AiRunStatus.Completed };
        var group = new AlderChatToolGroup
        {
            Header = "Tool calls", IsExpanded = true, Content = new StackPanel { Children = { tool, summary } }
        };
        var window = Show(group);
        try
        {
            Assert.True(group.Trigger!.IsEnabled);
            Assert.False(summary.Trigger!.IsEnabled);
            var shimmer = Assert.Single(tool.GetVisualDescendants().OfType<AlderTextShimmer>());
            Assert.True(shimmer.IsEffectivelyVisible);
            Assert.Equal("Running tool: searchDocs", shimmer.Text);
            Assert.DoesNotContain(tool.GetVisualDescendants().OfType<AlderButton>(), button => button.IsEffectivelyVisible);

            var expandedHeight = tool.Bounds.Height;
            tool.Arguments = prefix + new string('x', 160);
            Pump(window);
            Assert.True(tool.IsExpanded);
            Assert.Equal(expandedHeight, tool.Bounds.Height);
            Assert.Equal(tool.Arguments, Assert.Single(tool.GetVisualDescendants().OfType<AlderCodeBlock>(),
                code => code.IsEffectivelyVisible).Code);

            tool.Status = AiRunStatus.Pending;
            Pump(window);
            Assert.False(shimmer.IsEffectivelyVisible);
            var buttons = tool.GetVisualDescendants().OfType<AlderButton>().Where(button => button.IsEffectivelyVisible).ToArray();
            Assert.Equal(2, buttons.Length);
            foreach (var button in buttons)
            {
                var point = button.TranslatePoint(new Rect(button.Bounds.Size).Center, window)!.Value;
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                Pump(window);
            }
            Assert.Equal(new[] { (false, (object?)"call-1"), (true, (object?)"call-1") }, decisions);

            tool.Status = AiRunStatus.Completed;
            Pump(window);
            Assert.All(buttons, button => Assert.False(button.IsEffectivelyVisible));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ModelAndEffortSelectionsPropagateInBothDirections()
    {
        var picker = new AlderModelSelector { ItemsSource = new[] { "A", "B" }, SelectedModel = "A",
            Efforts = new[] { "Low", "High" }, SelectedEffort = "High" };
        var window = Show(picker);
        try
        {
            var models = Assert.Single(picker.GetVisualDescendants().OfType<AlderSelect>());
            Assert.Equal("A", models.SelectedItem);
            var footer = Assert.IsType<StackPanel>(models.DropDownFooter);
            var effort = Assert.Single(footer.Children.OfType<ListBox>());
            models.IsDropDownOpen = true;
            Pump(window);
            Assert.True(footer.IsAttachedToVisualTree());
            Assert.Equal("High", effort.SelectedItem);
            var slider = Assert.Single(effort.GetVisualDescendants().OfType<Slider>());
            Assert.Equal(1d, slider.Maximum);
            slider.Value = 0;
            Assert.Equal("Low", effort.SelectedItem);
            Assert.Equal("Low", picker.SelectedEffort);
            picker.SelectedEffort = "High";
            Assert.Equal("High", effort.SelectedItem);
            models.SelectedItem = "B";
            Assert.Equal("B", picker.SelectedModel);
            picker.Efforts = null;
            Assert.False(effort.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ModelEffortTrackUpdatesItsTierAndHandlesTrackClicks()
    {
        var picker = new AlderModelSelector
        {
            ItemsSource = new[] { "A" }, SelectedModel = "A", ShowOuterSparks = false,
            Efforts = new[] { "Low", "Medium", "High", "Max", "Ultra" }, SelectedEffort = "Ultra"
        };
        var window = Show(picker);
        try
        {
            var select = Assert.Single(picker.GetVisualDescendants().OfType<AlderSelect>());
            select.IsDropDownOpen = true;
            Pump(window);
            var footer = Assert.IsType<StackPanel>(select.DropDownFooter);
            var meter = Assert.Single(footer.GetVisualDescendants().OfType<AlderModelEffortTrack>());
            var hint = Assert.Single(footer.GetVisualDescendants().OfType<AlderAccordionPanel>());
            Assert.Equal(4d, meter.Value);
            Assert.Equal(1d, meter.UltraAmount);
            Assert.Equal(1d, hint.Progress);
            Assert.False(meter.ShowOuterSparks);

            var low = meter.TranslatePoint(new Point(7, 12), window)!.Value;
            window.MouseMove(low);
            window.MouseDown(low, MouseButton.Left);
            window.MouseUp(low, MouseButton.Left);
            Pump(window);
            Assert.Equal("Low", picker.SelectedEffort);
            Assert.Equal(0d, meter.Value);
            Assert.Equal(0d, meter.AccentAmount);
            Assert.Equal(0d, hint.Progress);

            picker.SelectedEffort = "Max";
            Pump(window);
            Assert.Equal(3d, meter.Value);
            Assert.Equal(1d, meter.AccentAmount);
            Assert.Equal(0d, meter.UltraAmount);
            Assert.Equal(1d, hint.Progress);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ModelEffortDragSnapsWhileHeldAndRestoresTheTierSheenOnRelease()
    {
        var picker = new AlderModelSelector
        {
            ItemsSource = new[] { "A" }, SelectedModel = "A",
            Efforts = new[] { "Low", "Medium", "High", "Max", "Ultra" }, SelectedEffort = "Medium"
        };
        var window = Show(picker);
        try
        {
            var select = Assert.Single(picker.GetVisualDescendants().OfType<AlderSelect>());
            select.IsDropDownOpen = true;
            Pump(window);
            var footer = Assert.IsType<StackPanel>(select.DropDownFooter);
            var meter = Assert.Single(footer.GetVisualDescendants().OfType<AlderModelEffortTrack>());
            var slider = Assert.Single(footer.GetVisualDescendants().OfType<AlderModelEffortSlider>());
            Point At(double fraction, double y = 12) => meter.TranslatePoint(
                new Point(7 + fraction * (meter.Bounds.Width - 14), y), window)!.Value;
            void DragTo(double fraction, string tier, double y = 12)
            {
                window.MouseMove(At(fraction, y));
                Pump(window);
                Assert.Equal(tier, picker.SelectedEffort);
                Assert.Equal(0, meter.SheenAmount);
            }

            window.MouseMove(At(.25));
            window.MouseDown(At(.25), MouseButton.Left);
            DragTo(.38, "Medium");
            DragTo(.44, "High");
            DragTo(.66, "Max");
            DragTo(.91, "Max");
            DragTo(1, "Ultra");
            Assert.Equal(1, meter.UltraAmount);
            DragTo(.84, "Max", y: 50);
            Assert.Equal(0, meter.UltraAmount);
            window.MouseUp(At(.84, y: 50), MouseButton.Left);
            Pump(window);
            Assert.Equal("Max", picker.SelectedEffort);
            Assert.Equal(3, meter.Value);
            Assert.Equal(1, meter.SheenAmount);
            Assert.True(select.IsDropDownOpen);

            slider.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Pump(window);
            Assert.Equal("Ultra", picker.SelectedEffort);
            Assert.Equal(1, meter.UltraAmount);
            Assert.Equal(.25, meter.SheenAmount);

            using var contact = window.TouchBegin(At(1));
            window.TouchMove(contact, At(.8));
            Pump(window);
            Assert.Equal("Max", picker.SelectedEffort);
            Assert.Equal(0, meter.SheenAmount);
            window.TouchEnd(contact, At(.8));
            Pump(window);
            Assert.Equal("Max", picker.SelectedEffort);
            Assert.Equal(1, meter.SheenAmount);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ModelPopupFiltersProvidersAndAcceptsKeyboardSelection()
    {
        var claude = new AlderPickerOption { Text = "Claude Sonnet", Group = "Anthropic", Image = new DrawingImage() };
        var gpt = new AlderPickerOption { Text = "GPT Sol", Group = "OpenAI", Image = new DrawingImage() };
        var picker = new AlderModelSelector { ItemsSource = new[] { claude, gpt }, SelectedModel = claude, ShowSearch = true };
        var window = Show(picker);
        try
        {
            var select = Assert.Single(picker.GetVisualDescendants().OfType<AlderSelect>());
            var icon = Assert.Single(select.GetVisualDescendants().OfType<Image>(), image => image.Name == "ModelTriggerIcon");
            Assert.Same(claude.Image, icon.Source);
            Assert.True(icon.IsEffectivelyVisible);
            select.IsDropDownOpen = true;
            Pump(window);
            var footer = Assert.IsAssignableFrom<Control>(select.DropDownFooter);
            var surface = Assert.Single(footer.GetVisualAncestors().OfType<AnchoredSurface>());
            var search = Assert.Single(surface.GetVisualDescendants().OfType<AlderSearchField>());
            var list = Assert.Single(surface.GetVisualDescendants().OfType<PickerList>());
            Assert.True(search.IsVisible);
            search.Text = "openai";
            Pump(window);
            Assert.Collection(list.ItemsSource!.Cast<PickerEntry>(),
                entry => Assert.Equal("OpenAI", entry.Item),
                entry => Assert.Same(gpt, entry.Item));
            search.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump(window);
            Assert.Same(gpt, picker.SelectedModel);
            Assert.False(select.IsDropDownOpen);
            Assert.Same(gpt.Image, icon.Source);

            picker.SelectedModel = claude;
            var leading = new Border { Width = 16, Height = 16 };
            picker.LeadingContent = leading;
            Pump(window);
            Assert.Same(claude.Image, icon.Source);
            Assert.False(icon.IsEffectivelyVisible);
            Assert.True(leading.IsAttachedToVisualTree() && leading.IsEffectivelyVisible);
            picker.ShowIcon = false;
            Pump(window);
            Assert.False(leading.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(500, 36)]
    [InlineData(760, 32)]
    public void ConversationKeepsTheReadingPositionUntilSendingAndReturnsFocusAfterJumping(double width, double buttonSize)
    {
        var entries = new ObservableCollection<TranscriptEntry>(Enumerable.Range(0, 16)
            .Select(index => new ContentEntry(new Border { Height = 46, Child = new TextBlock { Text = $"Message {index}" } })));
        var chat = new AlderChatConversation { ItemsSource = entries, Height = 260, ShowScrollButton = true, ScrollOnUserMessage = true };
        var window = Show(chat);
        try
        {
            window.Width = width;
            Pump(window);
            var presenter = chat.GetVisualDescendants().OfType<global::Avalonia.Controls.Presenters.ScrollContentPresenter>().Single();
            var fade = Assert.IsType<LinearGradientBrush>(presenter.OpacityMask);
            Assert.Equal(32, fade.GradientStops[1].Offset * presenter.Bounds.Height, 1);
            var jump = chat.GetVisualDescendants().OfType<AlderButton>().Single(button => button.Name == "PART_ScrollButton");
            Assert.False(jump.IsEffectivelyEnabled);
            chat.Offset = new Vector(0, 120);
            Pump(window);
            var readingPosition = chat.Offset.Y;
            entries.Add(new ContentEntry(new Border { Height = 90 }));
            Pump(window);
            Assert.Equal(readingPosition, chat.Offset.Y, 1);
            Assert.True(jump.IsEffectivelyEnabled);
            Assert.Equal(buttonSize, jump.Bounds.Width);
            Assert.Equal(chat.Bounds.Width / 2, jump.TranslatePoint(new Point(buttonSize / 2, buttonSize / 2), chat)!.Value.X, 1);

            Assert.True(jump.Focus());
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump(window);
            Assert.True(chat.IsFocused);
            Assert.False(jump.IsEffectivelyEnabled);
            Assert.Equal(chat.ScrollBarMaximum.Y, chat.Offset.Y, 1);

            chat.Offset = default;
            Pump(window);
            entries.Add(new ContentEntry(new AlderChatMessage { IsUser = true, Content = "Follow this message" }));
            Pump(window);
            Assert.True(chat.IsPinnedToBottom);
            Assert.Equal(chat.ScrollBarMaximum.Y, chat.Offset.Y, 1);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CompactMessageActionsBrightenOnHoverAndFocusWithoutMoving()
    {
        var actions = new AlderChatMessageActions
        {
            CopyCommand = new ActionCommand(_ => { }),
            LikeCommand = new ActionCommand(_ => { }),
        };
        var message = new AlderChatMessage { Content = "Response", Avatar = "AI", Actions = actions };
        var preview = new Border { Width = 360, Child = message };
        Container.SetName(preview, "ChatConversationViewport");
        Container.SetSizing(preview, ContainerSizing.Width);
        var outside = new AlderButton { Content = "Continue" };
        var window = Show(new StackPanel { Children = { preview, outside } });
        try
        {
            var slot = message.GetVisualDescendants().OfType<Control>().Single(part => part.Name == "ActionSlot");
            var copy = actions.GetVisualDescendants().OfType<AlderButton>().Single(button => button.Name == "Copy");
            var restingBounds = slot.Bounds;
            Assert.Equal(new Size(36, 36), copy.Bounds.Size);
            Assert.Equal(0.5, slot.Opacity);

            window.MouseMove(copy.TranslatePoint(new Point(18, 18), window)!.Value);
            Pump(window);
            Assert.Equal(1, slot.Opacity);
            Assert.Equal(restingBounds, slot.Bounds);

            window.MouseMove(outside.TranslatePoint(new Point(10, 10), window)!.Value);
            copy.Focus(NavigationMethod.Tab);
            Pump(window);
            Assert.Equal(1, slot.Opacity);
            outside.Focus();
            Pump(window);
            Assert.Equal(0.5, slot.Opacity);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MessageCardContainsItsActionsAndKeepsAnExplicitBorder()
    {
        var message = new AlderChatMessage
        {
            Content = "Response",
            IsCard = true,
            BorderBrush = Brushes.Crimson,
            BorderThickness = new Thickness(2),
            Actions = new AlderChatMessageActions { CopyCommand = new ActionCommand(_ => { }) },
        };
        var window = Show(message);
        try
        {
            var card = message.GetVisualDescendants().OfType<Border>().Single(part => part.Name == "MessageCard");
            var actions = message.GetVisualDescendants().OfType<AlderChatMessageActions>().Single();
            Assert.Same(Brushes.Crimson, card.BorderBrush);
            Assert.Equal(new Thickness(2), card.BorderThickness);
            Assert.Equal(new CornerRadius(16), card.CornerRadius);
            Assert.False(card.ClipToBounds);
            var actionsStart = actions.TranslatePoint(default, card)!.Value;
            Assert.True(actionsStart.X >= 20);
            Assert.True(actionsStart.Y + actions.Bounds.Height <= card.Bounds.Height - 20);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MessageActionsShareTheirParameterAndUseAdjacentIconButtons()
    {
        var invoked = new List<object?>();
        var actions = new AlderChatMessageActions
        {
            CopyCommand = new ActionCommand(invoked.Add), RetryCommand = new ActionCommand(invoked.Add), CommandParameter = "Reply"
        };
        var window = Show(actions);
        try
        {
            var buttons = actions.GetVisualDescendants().OfType<AlderButton>().Where(button => button.IsEffectivelyVisible).ToArray();
            Assert.Equal(new[] { "Copy", "Retry" }, buttons.Select(button => button.Name));
            Assert.All(buttons, button => Assert.Equal(new Size(32, 32), button.Bounds.Size));
            Assert.Equal(buttons[0].Bounds.Right, buttons[1].Bounds.Left);
            var idleGlyph = buttons[0].Icon;
            foreach (var button in buttons)
            {
                var point = button.TranslatePoint(new Point(16, 16), window)!.Value;
                window.MouseMove(point);
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
            }
            Assert.Equal(new[] { "Reply", "Reply" }, invoked);
            Assert.NotSame(idleGlyph, buttons[0].Icon);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MessageActionToolTipsOpenImmediatelyAndFollowTheActiveButton()
    {
        var invoked = false;
        var command = new ActionCommand(_ => invoked = true);
        var actions = new AlderChatMessageActions
        {
            CopyCommand = command, LikeCommand = command, DislikeCommand = command, RetryCommand = command,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var window = Show(actions);
        try
        {
            foreach (var (name, label) in new[]
            {
                ("Copy", "Copy"), ("Like", "Good response"), ("Dislike", "Bad response"), ("Retry", "Regenerate")
            })
            {
                var button = actions.GetVisualDescendants().OfType<AlderButton>().Single(child => child.Name == name);
                var origin = button.TranslatePoint(default, window)!.Value;
                window.MouseMove(origin + new Vector(button.Bounds.Width / 2, button.Bounds.Height / 2));
                Pump(window);

                var tip = Assert.Single(ToolTips());
                var frame = Assert.IsType<Border>(Assert.Single(tip.GetVisualChildren()));
                var text = Assert.IsType<AlderTextBlock>(frame.Child);
                Assert.Equal(label, text.Text);
                Assert.Equal(new Thickness(8), frame.Padding);
                Assert.Equal(new CornerRadius(12), frame.CornerRadius);
                Assert.Equal(default, frame.BorderThickness);
                Assert.Equal(32, frame.Bounds.Height);
                var tipOrigin = frame.TranslatePoint(default, window)!.Value;
                Assert.InRange(Math.Abs(tipOrigin.X + frame.Bounds.Width / 2 - origin.X - button.Bounds.Width / 2), 0, 1);
                Assert.Equal(3, origin.Y - tipOrigin.Y - frame.Bounds.Height);
                Assert.Equal(500, AlderToolTip.GetCloseDelay(button));
                Assert.True(AlderToolTip.GetAnimateWhenWarm(button));
            }

            window.MouseMove(default);
            Pump(window);
            Assert.Single(ToolTips());
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump(window);
            Assert.Empty(ToolTips());

            var copy = actions.GetVisualDescendants().OfType<AlderButton>().Single(child => child.Name == "Copy");
            Assert.True(copy.Focus(NavigationMethod.Tab));
            Pump(window);
            Assert.True(copy.IsFocused);
            Assert.Single(ToolTips());

            var copyCenter = copy.TranslatePoint(new Rect(copy.Bounds.Size).Center, window)!.Value;
            window.MouseMove(copyCenter);
            window.MouseDown(copyCenter, MouseButton.Left);
            window.MouseUp(copyCenter, MouseButton.Left);
            Pump(window);
            Assert.True(invoked);
            Assert.Empty(ToolTips());
        }
        finally { window.Close(); }

        IEnumerable<Control> ToolTips() => window.GetVisualDescendants().OfType<Control>()
            .Where(control => control.Name == "AlderToolTipSurface");
    }

    [AvaloniaFact]
    public void TenThousandMessagesRealizeAndParseOnlyTheViewport()
    {
        var entries = new ObservableCollection<TranscriptEntry>(Enumerable.Range(0, 10_000)
            .Select(i => MarkdownEntry.FromTextDeferred($"Message {i}")));
        var chat = new AlderChatConversation { ItemsSource = entries, Height = 260 };
        var window = Show(chat);
        try
        {
            chat.ScrollToEnd();
            Pump(window);
            Assert.InRange(chat.Panel.RealizedCount, 1, 60);
            Assert.InRange(entries.Cast<MarkdownEntry>().Count(e => e.IsParsed), 1, 100);
            entries.Add(MarkdownEntry.FromTextDeferred("Append"));
            Pump(window);
            Assert.True(chat.IsPinnedToBottom);
            Assert.Equal(entries.Count, chat.Panel.RowCount);
            chat.ItemsSource = null;
            chat.Entries.Add(MarkdownEntry.FromText("New conversation"));
            Pump(window);
            Assert.Equal(1, chat.Panel.RowCount);
        }
        finally { window.Close(); }
    }

    private static Window Show(Control control)
    {
        var window = new AlderTestWindow { Width = 500, Height = 430, Content = control };
        AlderMotion.SetReduced(window, true);
        window.Show();
        Pump(window);
        return window;
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class ActionCommand(Action<object?> action) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action(parameter);
    }
}
