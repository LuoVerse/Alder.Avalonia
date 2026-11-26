using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Paints recognized mention and command ranges using the native editor's shaped text layout.</summary>
public sealed class AlderMentionHighlights : TemplatedControl
{
    private TextPresenter? presenter;
    internal TextBox? Editor { get; set; }
    internal IReadOnlyList<string>? Candidates { get; set; }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Candidates is not { Count: > 0 } tokens || Editor?.Text is not { Length: > 0 } text) return;
        if (presenter is null || !presenter.IsAttachedToVisualTree())
            presenter = Editor.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        if (presenter?.TranslatePoint(default, this) is not { } origin || !string.IsNullOrEmpty(presenter.PreeditText)) return;
        var layout = presenter.TextLayout;
        double excess = Math.Min(0, presenter.Bounds.Height - layout.Height);
        double top = presenter.VerticalAlignment switch
        {
            global::Avalonia.Layout.VerticalAlignment.Center => excess / 2,
            global::Avalonia.Layout.VerticalAlignment.Bottom => excess,
            _ => 0
        };
        var offset = new Vector(origin.X, origin.Y + top);
        using var clip = context.PushClip(new Rect(Bounds.Size));
        using var opacity = context.PushOpacity(.19);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] is not ('@' or '/') || i > 0 && IsTokenChar(text[i - 1])) continue;
            int end = i + 1;
            while (end < text.Length && IsTokenChar(text[end])) end++;
            foreach (string token in tokens)
            {
                if (!text.AsSpan(i, end - i).Equals(token, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var rect in layout.HitTestTextRange(i, end - i))
                    context.DrawRectangle(Foreground, null, rect.Translate(offset), 3, 3);
                break;
            }
            i = end - 1;
        }
    }

    private static bool IsTokenChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '-';
}
