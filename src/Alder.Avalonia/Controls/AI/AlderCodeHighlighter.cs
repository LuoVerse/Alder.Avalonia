using System.Collections;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Alder.NativeCode.Monarch;
using Alder.NativeMarkdown.Core.Highlighting;

namespace Alder.Avalonia.Controls;

/// <summary>Shares grammar rules across controls and keeps incremental line state per code block.</summary>
/// <remarks>Uses the per-line checkpoints of StreamMark's TextMate sessions with Alder's Monarch grammar.</remarks>
internal sealed class AlderCodeHighlighter : IAppendStableSyntaxHighlighter
{
    public bool SupportsLanguage(string? language) =>
        !string.IsNullOrWhiteSpace(language) && AlderMonarch.Supports(language);

    public IHighlightSession CreateSession(string language, HighlightThemeKind theme) =>
        new Session(AlderMonarch.Get(language), new CodeSyntaxPalette(theme));

    public (uint Foreground, uint Background) GetThemeColors(HighlightThemeKind theme) =>
        theme == HighlightThemeKind.Dark ? (0xffe1e4e8, 0xff232325) : (0xff24292e, 0xffefeff0);

    private sealed class Session(MonarchTokenizer? lexer, CodeSyntaxPalette palette) : IHighlightSession
    {
        // Very long generated lines still render in full. Keeping the incoming state prevents a
        // truncated string or comment from leaking a false grammar state into later lines.
        private const int MaxHighlightedLineLength = 4096;
        private string _previous = "";
        private LineSnapshot? _snapshot;

        public IReadOnlyList<ImmutableArray<HighlightToken>> Update(string code)
        {
            var old = _snapshot?.Lines ?? ImmutableList<Line>.Empty;
            var prefix = _previous.AsSpan().CommonPrefixLength(code);
            if (_snapshot is not null && prefix == _previous.Length && prefix == code.Length)
                return _snapshot;

            var append = prefix == _previous.Length;
            // The final line has no terminator and must be re-evaluated even on a pure append.
            var keep = append ? Math.Max(0, old.Count - 1) : CompletedPrefix(old, prefix);
            var lines = old.ToBuilder();
            lines.RemoveRange(keep, lines.Count - keep);
            var offset = keep == 0 ? 0 : lines[keep - 1].End;
            var state = keep == 0 ? lexer?.GetInitialState() : lines[keep - 1].State;

            // A CR emitted by itself already committed an end of line. A following LF only
            // advances the stored offset. Its syntax and previously published spans stay valid.
            if (offset > 0 && offset < code.Length && code[offset - 1] == '\r' && code[offset] == '\n')
            {
                offset++;
                lines[keep - 1] = lines[keep - 1] with { End = offset };
            }

            var delta = code.Length - _previous.Length;
            var suffixStart = append ? code.Length : CommonSuffixStart(_previous, code, prefix);
            while (true)
            {
                var length = code.AsSpan(offset).IndexOfAny('\r', '\n');
                var terminated = length >= 0;
                var end = terminated ? offset + length : code.Length;
                var next = !terminated ? end
                    : end + (code[end] == '\r' && end + 1 < code.Length && code[end + 1] == '\n' ? 2 : 1);
                var tokens = Tokenize(code, offset, end, terminated, ref state);
                lines.Add(new(next, state, tokens));
                if (!terminated)
                    break;

                offset = next;
                // Reuse the suffix once its text and incoming grammar state match the old
                // snapshot. This covers edits to a literal block as well as streaming appends.
                if (!append && offset >= suffixStart && TryReuseSuffix(old, lines, offset, delta, state))
                    break;
            }
            _previous = code;
            return _snapshot = new LineSnapshot(lines.ToImmutable());
        }

        private ImmutableArray<HighlightToken> Tokenize(string code, int start, int end, bool terminated, ref IState? state)
        {
            if (lexer is null || state is null || end - start > MaxHighlightedLineLength)
                return [];
            var line = code[start..end];
            try
            {
                var result = lexer.Tokenize(line, terminated, state);
                var tokens = palette.Colorize(line, result.Tokens);
                state = result.EndState;
                return tokens;
            }
            catch (RegexMatchTimeoutException)
            {
                return [];
            }
        }

        private static bool TryReuseSuffix(ImmutableList<Line> old, ImmutableList<Line>.Builder lines,
            int offset, int delta, IState? state)
        {
            var oldOffset = offset - delta;
            var previous = CompletedPrefix(old, oldOffset) - 1;
            if (previous < 0 || old[previous].End != oldOffset)
                return false;
            var oldState = old[previous].State;
            if (!ReferenceEquals(state, oldState) && (oldState is null || state?.Equals(oldState) != true))
                return false;
            for (var i = previous + 1; i < old.Count; i++)
            {
                var line = old[i];
                lines.Add(delta == 0 ? line : line with { End = line.End + delta });
            }
            return true;
        }

        private static int CompletedPrefix(ImmutableList<Line> lines, int offset)
        {
            var low = 0;
            var high = Math.Max(0, lines.Count - 1);
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (lines[middle].End <= offset)
                    low = middle + 1;
                else
                    high = middle;
            }
            return low;
        }

        private static int CommonSuffixStart(string previous, string current, int prefix)
        {
            var remaining = Math.Min(previous.Length, current.Length) - prefix;
            var suffix = 0;
            while (suffix < remaining && previous[^(suffix + 1)] == current[^(suffix + 1)])
                suffix++;
            return current.Length - suffix;
        }

        public void Dispose()
        {
            _snapshot = null;
            _previous = "";
        }

        private readonly record struct Line(int End, IState? State, ImmutableArray<HighlightToken> Tokens);

        private sealed class LineSnapshot(ImmutableList<Line> lines) : IReadOnlyList<ImmutableArray<HighlightToken>>
        {
            public ImmutableList<Line> Lines { get; } = lines;
            public int Count => Lines.Count;
            public ImmutableArray<HighlightToken> this[int index] => Lines[index].Tokens;

            public IEnumerator<ImmutableArray<HighlightToken>> GetEnumerator()
            {
                foreach (var line in Lines)
                    yield return line.Tokens;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
