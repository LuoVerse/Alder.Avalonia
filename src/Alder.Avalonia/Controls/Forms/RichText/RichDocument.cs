using System.Text.Json.Nodes;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Buffers;
using Alder.Editor;


namespace Alder.Avalonia.Controls;

[Flags]
internal enum RichMark { None = 0, Bold = 1, Italic = 2, Underline = 4, Strike = 8, Code = 16 }
internal enum RichBlock { Paragraph, Heading1, Heading2, Heading3, Quote, CodeBlock, BulletList, OrderedList, HorizontalRule }
internal readonly record struct RichStyle(RichMark Marks = 0, string? Link = null, JsonObject? LinkAttributes = null);
internal readonly record struct RichRun(string Text, RichStyle Style);
internal sealed record RichContainer(string Type, JsonObject? Attributes = null);
internal sealed record RichParagraph(RichBlock Kind, RichRun[] Runs, RichContainer[] Parents, JsonObject? Attributes = null)
{
    public string Text { get; } = Runs.Length switch { 0 => "", 1 => Runs[0].Text, _ => string.Concat(Runs.Select(r => r.Text)) };
    public int Length => Text.Length;
    public int Characters { get; } = CountCharacters(Runs);
    public int Words { get; } = CountWords(Runs);
    public bool HasMarker => Kind is RichBlock.BulletList or RichBlock.OrderedList or RichBlock.Quote or RichBlock.HorizontalRule;
    internal byte[]? Json { get; set; }
    internal string? JsonText { get; set; }

    private static int CountWords(RichRun[] runs)
    {
        bool space = true;
        int words = 0;
        foreach (var run in runs)
            foreach (char c in run.Text)
            {
                if (space && !char.IsWhiteSpace(c)) words++;
                space = char.IsWhiteSpace(c);
            }
        return words;
    }

    private static int CountCharacters(RichRun[] runs)
    {
        var text = runs.Length == 1 ? runs[0].Text : string.Concat(runs.Select(r => r.Text));
        return new StringInfo(text).LengthInTextElements - text.AsSpan().Count('\u2028');
    }
}

// Paragraphs and runs are immutable. History retains just the paragraphs touched by an edit.
internal sealed class RichDocument
{
    public const string Empty = """{"type":"doc","content":[{"type":"paragraph"}]}""";
    private static readonly RichParagraph Blank = new(RichBlock.Paragraph, [], []);
    private readonly List<RichParagraph> paragraphs = [Blank];
    internal EditorBuffer Buffer { get; } = new();
    private readonly LinkedList<Change> undo = new();
    private readonly Stack<Change> redo = new();
    private long historySize;
    private ArrayBufferWriter<byte>? jsonBuffer;
    private int nestedParagraphs;
    private sealed record Patch(int Offset, string Removed, string Added);
    private sealed record Change(int Row, RichParagraph[] Before, RichParagraph[] After, int Start, int End, Patch? Edit)
    {
        public long Size { get; } = 32L + Before.Sum(p => (long)p.Length) + After.Sum(p => (long)p.Length);
    }
    public int Characters { get; private set; }
    public int Words { get; private set; }
    public int FirstChangedOffset { get; private set; }
    public int CodeBlocks { get; private set; }
    public int StructureVersion { get; private set; }
    public int MarkerCount { get; private set; }
    public int Count => paragraphs.Count;
    public int Length => Buffer.Length;
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public int Start(int row) => Buffer.Start(row);
    public RichParagraph Line(int row) => paragraphs[row];
    public string Text => Buffer.Text;
    public int Row(int offset) => Buffer.Row(Math.Clamp(offset, 0, Length));

    public RichStyle At(int offset)
    {
        var row = Row(offset);
        int column = offset - Start(row);
        foreach (var run in Line(row).Runs)
        {
            if (column < run.Text.Length) return run.Style;
            column -= run.Text.Length;
        }
        return Line(row).Runs.LastOrDefault().Style;
    }

    public RichStyle InputStyle(int offset) => At(offset > Start(Row(offset)) ? offset - 1 : offset);

    public IEnumerable<RichRun> Runs(int start, int length)
    {
        var end = start + length;
        for (var row = Row(start); row < Count && Start(row) < end; row++)
            foreach (var run in Slice(Line(row), start - Start(row), end - Start(row))) yield return run;
    }

    private static IEnumerable<RichRun> Slice(RichParagraph line, int from, int to)
    {
        int offset = 0;
        foreach (var run in line.Runs)
        {
            var left = Math.Max(0, from - offset);
            var right = Math.Min(run.Text.Length, to - offset);
            if (left < right) yield return left == 0 && right == run.Text.Length ? run : run with { Text = run.Text[left..right] };
            offset += run.Text.Length;
            if (offset >= to) yield break;
        }
    }

    private static RichRun[] Merge(IEnumerable<RichRun> runs)
    {
        var result = new List<RichRun>();
        foreach (var run in runs)
        {
            if (run.Text.Length == 0) continue;
            if (result.Count > 0 && result[^1].Style == run.Style)
                result[^1] = run with { Text = result[^1].Text + run.Text };
            else result.Add(run);
        }
        return result.ToArray();
    }

    public void Replace(int start, int removed, string inserted, RichStyle style)
    {
        int first = Row(start), last = Row(start + removed);
        var initial = Line(first);
        var head = Slice(initial, 0, start - Start(first));
        var tail = Slice(Line(last), start + removed - Start(last), int.MaxValue);
        var parts = inserted.Split('\n');
        var replacement = new List<RichParagraph>(parts.Length);
        for (int index = 0; index < parts.Length; index++)
        {
            IEnumerable<RichRun> runs = [new(parts[index], style)];
            if (index == 0) runs = head.Concat(runs);
            if (index == parts.Length - 1) runs = runs.Concat(tail);
            var line = index == 0 ? initial : Following(initial);
            if (line.Kind == RichBlock.HorizontalRule && runs.Any(r => r.Text.Length > 0)) line = Blank;
            replacement.Add(new(line.Kind, Merge(runs), line.Parents, line.Attributes));
        }
        Commit(first, last, replacement.ToArray(), start + removed, start + inserted.Length, new(start, Buffer.Slice(start, removed), inserted));
    }

    // Splice structured blocks in one history entry and retain the rich runs on either side.
    public int InsertBlocks(int start, int removed, RichDocument fragment)
    {
        int first = Row(start), last = Row(start + removed);
        var prefix = Slice(Line(first), 0, start - Start(first)).ToArray();
        var suffix = Slice(Line(last), start + removed - Start(last), int.MaxValue).ToArray();
        var rows = new List<RichParagraph>();
        if (prefix.Length > 0) rows.Add(new(Line(first).Kind, prefix, Line(first).Parents, Line(first).Attributes));
        rows.AddRange(fragment.paragraphs);
        int caret = Start(first) + rows.Sum(p => p.Length + 1) - 1;
        if (suffix.Length > 0) rows.Add(new(Line(last).Kind, suffix, Line(last).Parents, Line(last).Attributes));
        else if (last == Count - 1 && rows[^1].Kind != RichBlock.Paragraph) rows.Add(Blank);
        if (fragment.Line(fragment.Count - 1).Kind == RichBlock.HorizontalRule) caret++;
        int offset = Start(first);
        var added = string.Join('\n', rows.Select(p => p.Text));
        var before = Buffer.Slice(offset, Start(last) + Line(last).Length - offset);
        Commit(first, last, rows.ToArray(), start + removed, caret, new(offset, before, added));
        return caret;
    }

    private static RichParagraph Following(RichParagraph line)
    {
        if (line.Kind is >= RichBlock.Heading1 and <= RichBlock.Heading3 or RichBlock.HorizontalRule) return Blank;
        if (!line.Parents.Any(p => p.Type == "listItem")) return line;
        var path = (RichContainer[])line.Parents.Clone();
        int item = Array.FindLastIndex(path, p => p.Type == "listItem");
        path[item] = new("listItem");
        return new(line.Kind, [], path, line.Attributes);
    }

    public void Format(int start, int length, Func<RichStyle, RichStyle>? inline = null, RichBlock? block = null)
    {
        var first = Row(start);
        var last = Row(start + Math.Max(0, length - 1));
        var lines = new List<RichParagraph>();
        RichContainer? group = block switch
        {
            RichBlock.BulletList => new("bulletList"), RichBlock.OrderedList => new("orderedList"),
            RichBlock.Quote => new("blockquote"), _ => null
        };
        for (int row = first; row <= last; row++)
        {
            var line = Line(row);
            var runs = line.Runs;
            if (inline is not null)
            {
                int from = start - Start(row), to = from + length;
                runs = Merge(Slice(line, 0, from).Concat(Slice(line, from, to).Select(r => r with { Style = inline(r.Style) }))
                    .Concat(Slice(line, to, int.MaxValue)));
            }
            RichContainer[] parents = group?.Type switch
            {
                "blockquote" => [group], "bulletList" or "orderedList" => [group, new("listItem")], _ => []
            };
            lines.Add(block.HasValue ? new(block.Value, runs, parents) : new(line.Kind, runs, line.Parents, line.Attributes));
        }
        Commit(first, last, lines.ToArray(), start + length, start + length);
    }

    private void Commit(int first, int last, RichParagraph[] after, int start, int end, Patch? edit = null)
    {
        var change = new Change(first, paragraphs.GetRange(first, last - first + 1).ToArray(), after, start, end, edit);
        if (edit is not null) Buffer.Replace(edit.Offset, edit.Removed.Length, edit.Added);
        Splice(first, change.Before.Length, after);
        redo.Clear();
        undo.AddLast(change);
        historySize += change.Size;
        while (undo.Count > 1 && (undo.Count > 128 || historySize > 2_000_000))
        {
            historySize -= undo.First!.Value.Size;
            undo.RemoveFirst();
        }
    }

    private void Splice(int row, int remove, RichParagraph[] insert)
    {
        FirstChangedOffset = Start(row);
        bool structureChanged = remove != insert.Length;
        for (int i = 0; !structureChanged && i < remove; i++)
            structureChanged = insert[i].Kind != Line(row + i).Kind || !ReferenceEquals(insert[i].Parents, Line(row + i).Parents);
        if (structureChanged) StructureVersion++;
        for (var i = row; i < row + remove; i++) CountLine(Line(i), -1);
        foreach (var line in insert) CountLine(line, 1);
        int shared = Math.Min(remove, insert.Length);
        for (var i = 0; i < shared; i++) paragraphs[row + i] = insert[i];
        if (remove > shared) paragraphs.RemoveRange(row + shared, remove - shared);
        else if (insert.Length > shared) paragraphs.InsertRange(row + shared, insert.AsSpan(shared));
    }

    private void CountLine(RichParagraph line, int delta)
    {
        Characters += delta * line.Characters;
        Words += delta * line.Words;
        if (line.HasMarker) MarkerCount += delta;
        if (line.Kind == RichBlock.CodeBlock) CodeBlocks += delta;
        if (line.Parents.Length != 0) nestedParagraphs += delta;
    }

    public void Clear() => Commit(0, Count - 1, [Blank], Length, 0, new(0, Text, ""));

    public int Undo()
    {
        var change = undo.Last!.Value;
        undo.RemoveLast();
        historySize -= change.Size;
        redo.Push(change);
        if (change.Edit is { } edit) Buffer.Replace(edit.Offset, edit.Added.Length, edit.Removed);
        Splice(change.Row, change.After.Length, change.Before);
        return change.Start;
    }

    public int Redo()
    {
        var change = redo.Pop();
        undo.AddLast(change);
        historySize += change.Size;
        if (change.Edit is { } edit) Buffer.Replace(edit.Offset, edit.Removed.Length, edit.Added);
        Splice(change.Row, change.Before.Length, change.After);
        return change.End;
    }

    public void Load(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject;
        if ((string?)root?["type"] != "doc" || root["content"] is not JsonArray nodes)
            throw new FormatException("Expected a document with a content array.");
        var result = new List<RichParagraph>();
        foreach (var node in nodes) Read(node, []);
        var text = string.Join('\n', result.Select(p => p.Text));
        if (Text != text) Buffer.Replace(0, Length, text);
        Splice(0, Count, result.Count == 0 ? [Blank] : result.ToArray());
        undo.Clear(); redo.Clear(); historySize = 0;

        void Read(JsonNode? node, RichContainer[] path)
        {
            var type = (string?)node?["type"];
            var attrs = node?["attrs"] as JsonObject;
            var children = node?["content"] as JsonArray;
            if (type is "orderedList" or "bulletList" or "listItem" or "blockquote")
            {
                RichContainer[] nested = [..path, new(type, attrs)];
                if (children is not null) foreach (var child in children) Read(child, nested);
                return;
            }
            var kind = type switch
            {
                "heading" => (int?)attrs?["level"] switch { 1 => RichBlock.Heading1, 2 => RichBlock.Heading2, 3 => RichBlock.Heading3, _ => throw new FormatException("Invalid heading level.") },
                "codeBlock" => RichBlock.CodeBlock,
                "horizontalRule" => RichBlock.HorizontalRule,
                "paragraph" => path.LastOrDefault(p => p.Type != "listItem")?.Type switch
                {
                    "bulletList" => RichBlock.BulletList, "orderedList" => RichBlock.OrderedList,
                    "blockquote" => RichBlock.Quote, _ => RichBlock.Paragraph
                },
                _ => throw new FormatException($"Unsupported block '{type}'.")
            };
            var runs = new List<RichRun>();
            if (children is not null) foreach (var child in children)
            {
                var text = (string?)child?["type"] switch
                {
                    "text" => ((string?)child?["text"] ?? "").ReplaceLineEndings("\u2028"),
                    "hardBreak" => "\u2028", _ => throw new FormatException("Expected inline text.")
                };
                RichStyle style = default;
                if (child?["marks"] is JsonArray marks) foreach (var mark in marks)
                {
                    var name = (string?)mark?["type"];
                    if (name == "link") style = style with { Link = (string?)mark?["attrs"]?["href"], LinkAttributes = mark?["attrs"] as JsonObject };
                    else if (Enum.TryParse<RichMark>(name, true, out var flag) && Enum.IsDefined(flag)) style = style with { Marks = style.Marks | flag };
                    else throw new FormatException($"Unsupported mark '{name}'.");
                }
                runs.Add(new(text, style));
            }
            result.Add(new(kind, Merge(runs), path, attrs));
        }
    }

    public string Save()
    {
        if (nestedParagraphs == 0) return SaveFlatDocument();
        var bytes = jsonBuffer ??= new ArrayBufferWriter<byte>();
        bytes.ResetWrittenCount();
        using var writer = new Utf8JsonWriter(bytes);
        writer.WriteStartObject();
        writer.WriteString("type", "doc");
        writer.WriteStartArray("content");
        RichContainer[] open = [];
        foreach (var line in paragraphs)
        {
            int shared = 0;
            while (shared < open.Length && shared < line.Parents.Length && ReferenceEquals(open[shared], line.Parents[shared])) shared++;
            for (int i = shared; i < open.Length; i++) { writer.WriteEndArray(); writer.WriteEndObject(); }
            foreach (var tag in line.Parents.AsSpan(shared))
            {
                writer.WriteStartObject();
                writer.WriteString("type", tag.Type);
                if (tag.Attributes is not null) { writer.WritePropertyName("attrs"); tag.Attributes.WriteTo(writer); }
                writer.WriteStartArray("content");
            }
            open = line.Parents;
            writer.WriteRawValue(line.Json ??= Encoding.UTF8.GetBytes(line.JsonText ?? Encode(line)), skipInputValidation: true);
        }
        foreach (var _ in open) { writer.WriteEndArray(); writer.WriteEndObject(); }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        if (bytes.Capacity > 2_000_000) jsonBuffer = null;
        return Encoding.UTF8.GetString(bytes.WrittenSpan);
    }

    private string SaveFlatDocument()
    {
        const string prefix = "{\"type\":\"doc\",\"content\":[";
        const string suffix = "]}";
        int length = prefix.Length + suffix.Length + paragraphs.Count - 1;
        foreach (var line in paragraphs) length += (line.JsonText ??= Encode(line)).Length;
        return string.Create(length, paragraphs, static (target, lines) =>
        {
            prefix.CopyTo(target);
            int index = prefix.Length;
            for (int i = 0; i < lines.Count; i++)
            {
                if (i != 0) target[index++] = ',';
                var json = lines[i].JsonText!;
                json.CopyTo(target[index..]);
                index += json.Length;
            }
            suffix.CopyTo(target[index..]);
        });
    }

    private static string Encode(RichParagraph line)
    {
        bool heading = line.Kind is >= RichBlock.Heading1 and <= RichBlock.Heading3;
        var block = Node(heading ? "heading" : line.Kind switch { RichBlock.CodeBlock => "codeBlock", RichBlock.HorizontalRule => "horizontalRule", _ => "paragraph" }, line.Attributes);
        if (heading && block["attrs"] is null) block["attrs"] = new JsonObject { ["level"] = (int)line.Kind };
        var inline = new JsonArray();
        foreach (var run in line.Runs)
        {
            string[] parts = line.Kind == RichBlock.CodeBlock ? [run.Text.Replace('\u2028','\n')] : run.Text.Split('\u2028');
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) inline.Add((JsonNode)Node("hardBreak"));
                if (parts[i].Length == 0) continue;
                var text = Node("text");
                text["text"] = parts[i];
                var marks = new JsonArray();
                foreach (var mark in InlineMarks)
                    if ((run.Style.Marks & mark) != 0) marks.Add((JsonNode)Node(mark.ToString().ToLowerInvariant()));
                if (run.Style.Link is { } href)
                    marks.Add((JsonNode)Node("link", run.Style.LinkAttributes ?? new JsonObject { ["href"] = href }));
                if (marks.Count > 0) text["marks"] = marks;
                inline.Add((JsonNode)text);
            }
        }
        if (inline.Count > 0) block["content"] = inline;
        return block.ToJsonString();
    }

    private static JsonObject Node(string type, JsonObject? attributes = null)
    {
        var node = new JsonObject { ["type"] = type };
        if (attributes is not null) node["attrs"] = attributes.DeepClone();
        return node;
    }

    private static readonly RichMark[] InlineMarks = [RichMark.Bold, RichMark.Italic, RichMark.Underline, RichMark.Strike, RichMark.Code];
}
