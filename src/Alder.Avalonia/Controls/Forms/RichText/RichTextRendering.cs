using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;


namespace Alder.Avalonia.Controls;

/// <summary>Shapes rich runs in the same layout used for glyphs, selection, IME and caret hit testing.</summary>
public sealed class RichTextPresenter : TextPresenter
{
    private static readonly FontFamily Monospace = FontFamily.Parse("Menlo, Consolas, monospace");
    internal AlderRichTextEditor? Owner { get; set; }
    private double width = double.PositiveInfinity;
    private readonly TextRunCache shaped = new();
    internal void Refresh(int firstChangedOffset = 0)
    {
        shaped.InvalidateFrom(firstChangedOffset);
        InvalidateTextLayout();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        shaped.Invalidate();
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // Width, caret and selection can reuse shaping. Font metrics and paint cannot.
        if (change.Property.Name is "FontFamily" or "FontSize" or "Foreground" or "FontStyle" or
            "FontWeight" or "FontStretch" or "FlowDirection" or "LetterSpacing" or "FontFeatures")
            shaped?.Invalidate();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        width = availableSize.Width;
        return base.MeasureOverride(availableSize);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        width = finalSize.Width;
        return base.ArrangeOverride(finalSize);
    }

    /// <inheritdoc />
    protected override TextLayout CreateTextLayout()
    {
        var doc = Owner?.Document;
        if (doc is null || !string.IsNullOrEmpty(PreeditText) || doc.Length != (Text?.Length ?? 0))
            return base.CreateTextLayout();
        var source = new RunSource(this, doc);
        var paragraph = new GenericTextParagraphProperties(FlowDirection, TextAlignment, true, false,
            source.Resolve(RichBlock.Paragraph, default), TextWrapping, FontSize * 1.85, 0, LetterSpacing);
        return new TextLayout(source, paragraph, null, width > 0 ? width : double.PositiveInfinity,
            double.PositiveInfinity, 0, shaped);
    }

    // Ask for the next run on demand. Reflow and selection reuse shaped runs through TextRunCache.
    private sealed class RunSource(RichTextPresenter host, RichDocument doc) : ITextSource
    {
        private readonly Dictionary<(RichBlock, RichMark, bool), TextRunProperties> styles = new();
        private int row = -1, start, end;

        public TextRun? GetTextRun(int index)
        {
            if (index >= doc.Length) return null;
            if (row < 0 || index < start || index > end)
            {
                if (row >= 0 && index == end + 1) { row++; start = index; }
                else { row = doc.Row(index); start = doc.Start(row); }
                end = start + doc.Line(row).Length;
            }
            var line = doc.Line(row);
            if (index == end) return new TextCharacters("\n", Resolve(line.Kind, default));
            int offset = index - start;
            foreach (var run in line.Runs)
            {
                if (offset < run.Text.Length) return new TextCharacters(run.Text.AsMemory(offset), Resolve(line.Kind, run.Style));
                offset -= run.Text.Length;
            }
            throw new InvalidOperationException("Text run is outside its paragraph.");
        }

        internal TextRunProperties Resolve(RichBlock block, RichStyle style)
        {
            var key = (block, style.Marks, style.Link is not null);
            if (styles.TryGetValue(key, out var properties)) return properties;
            var mark = style.Marks;
            bool heading = block is >= RichBlock.Heading1 and <= RichBlock.Heading3;
            bool quote = block == RichBlock.Quote;
            bool code = block == RichBlock.CodeBlock || mark.HasFlag(RichMark.Code);
            var face = new Typeface(code ? Monospace : host.FontFamily,
                mark.HasFlag(RichMark.Italic) ? FontStyle.Italic : host.FontStyle,
                heading || mark.HasFlag(RichMark.Bold) ? FontWeight.Bold : host.FontWeight);
            bool underline = mark.HasFlag(RichMark.Underline) || style.Link is not null;
            TextDecorationCollection? decorations = mark.HasFlag(RichMark.Strike)
                ? underline ? [..TextDecorations.Underline, ..TextDecorations.Strikethrough] : TextDecorations.Strikethrough
                : underline ? TextDecorations.Underline : null;
            double size = host.FontSize * (block switch { RichBlock.Heading1 => 1.65, RichBlock.Heading2 => 1.4, RichBlock.Heading3 => 1.2, _ => 1.0 });
            var ink = quote ? Brush("AlderMuted") : host.Foreground;
            properties = new GenericTextRunProperties(face, size, decorations, ink,
                mark.HasFlag(RichMark.Code) ? Brush("AlderNeutral") : null);
            styles.Add(key, properties);
            return properties;
        }
        private IBrush? Brush(string key) => host.TryFindResource(key, host.ActualThemeVariant, out var value) ? value as IBrush : host.Foreground;
    }

}

/// <summary>Draws structural list markers and quotation rules outside the selectable text.</summary>
public sealed class RichTextGutter : Control
{
    private TextLayout? paintedLayout;
    internal RichTextPresenter? Presenter { get; set; }

    internal void LayoutChanged()
    {
        if (Presenter is { } p && !ReferenceEquals(paintedLayout, p.TextLayout)) InvalidateVisual();
    }

    private readonly Dictionary<int, FormattedText> labels = new();
    private (int Version, Typeface Face, double Size, IBrush? Ink, FlowDirection Direction)? markerStyle;
    private double indent;

    internal double UpdateMarkers(AlderRichTextEditor editor)
    {
        var doc = editor.Document;
        var face = new Typeface(editor.FontFamily, editor.FontStyle, editor.FontWeight);
        var style = (doc.StructureVersion, face, editor.FontSize, editor.Foreground, FlowDirection);
        if (markerStyle == style) return indent;
        markerStyle = style;
        labels.Clear();
        indent = doc.CodeBlocks > 0 ? 8 : 0;
        if (doc.MarkerCount == 0) return indent;
        var lists = new Dictionary<RichContainer, (RichContainer? Item, long Ordinal)>(ReferenceEqualityComparer.Instance);
        RichContainer[] open = [];
        for (int i = 0; i < doc.Count; i++)
        {
            var paragraph = doc.Line(i);
            int shared = 0;
            var path = paragraph.Parents;
            while (shared < open.Length && shared < path.Length && ReferenceEquals(open[shared], path[shared])) shared++;
            for (int j = shared; j < open.Length; j++)
                if (open[j].Type == "orderedList") lists.Remove(open[j]);
            open = path;
            if (paragraph.Kind == RichBlock.Quote) indent = Math.Max(indent, 18);
            if (paragraph.Kind == RichBlock.BulletList) indent = Math.Max(indent, 24);
            if (paragraph.Kind != RichBlock.OrderedList) continue;
            var list = paragraph.Parents.Last(p => p.Type == "orderedList");
            var item = paragraph.Parents.LastOrDefault(p => p.Type == "listItem");
            if (!lists.TryGetValue(list, out var state))
                state = (null, (list.Attributes?["start"] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<int>(out var start) ? start : 1) - 1L);
            else if (item != null && ReferenceEquals(item, state.Item)) continue;
            lists[list] = (item, ++state.Ordinal);
            var label = new FormattedText(state.Ordinal.ToString(CultureInfo.CurrentCulture) + ".", CultureInfo.CurrentCulture,
                FlowDirection, face, editor.FontSize, editor.Foreground);
            labels[i] = label;
            indent = Math.Max(Math.Max(indent, 24), Math.Ceiling(label.Width) + 6);
        }
        return indent;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (Presenter is not { Owner: { } editor } surface) return;
        var doc = editor.Document;
        paintedLayout = surface.TextLayout;
        if (doc.Length != (surface.Text?.Length ?? 0) || paintedLayout.TextLines.Count == 0) return;
        var visualLines = paintedLayout.TextLines;
        var origin = surface.TranslatePoint(default, this) ?? default;
        var rule = this.TryFindResource("AlderBorder", ActualThemeVariant, out var border) ? border as IBrush : editor.Foreground;
        var fill = this.TryFindResource("AlderNeutral", ActualThemeVariant, out var background) ? background as IBrush : null;
        var muted = this.TryFindResource("AlderMuted", ActualThemeVariant, out var hintBrush) ? hintBrush as IBrush : editor.Foreground;
        FormattedText? hint = null;
        int visualIndex = 0, offset = 0;
        double y = origin.Y;
        for (int row = 0; row < doc.Count && visualIndex < visualLines.Count; row++)
        {
            var line = doc.Line(row);
            var first = visualLines[visualIndex];
            double top = y;
            offset += line.Length + 1;
            do { y += visualLines[visualIndex++].Height; }
            while (visualIndex < visualLines.Count && visualLines[visualIndex].FirstTextSourceIndex < offset);
            if (line.Kind == RichBlock.Quote && rule is not null)
                context.FillRectangle(rule, new Rect(0, top + 1, 2, Math.Max(1, y - top - 2)));
            else if (line.Kind == RichBlock.CodeBlock && fill is not null)
                context.DrawRectangle(fill, null, new Rect(0, top, Bounds.Width, y - top), 6, 6);
            else if (line.Kind == RichBlock.HorizontalRule && rule is not null)
                context.FillRectangle(rule, new Rect(0, top + first.Height / 2, Bounds.Width, 1));
            else if (line.Kind == RichBlock.BulletList)
                context.DrawEllipse(editor.Foreground, null, new Point(origin.X - 10, top + first.Baseline - editor.FontSize * .33), 2, 2);
            else if (line.Kind == RichBlock.Paragraph && line.Length == 0 && doc.Length > 0 && editor.EmptyParagraphPlaceholder is { Length: > 0 } text)
            {
                hint ??= new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection, new Typeface(editor.FontFamily),
                    editor.FontSize, muted);
                context.DrawText(hint, new Point(origin.X, top + first.Baseline - hint.Baseline));
            }
            else if (labels.TryGetValue(row, out var label))
                context.DrawText(label, new Point(origin.X - 6 - label.Width, top + first.Baseline - label.Baseline));
        }
    }
}
