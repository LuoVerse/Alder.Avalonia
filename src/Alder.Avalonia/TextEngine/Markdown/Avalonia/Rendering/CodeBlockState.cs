#pragma warning disable CS1591 // Imported Markdown engine.
using System.Collections.Immutable;
using System.Diagnostics;
using Avalonia.Threading;
using Alder.NativeMarkdown.Core.Highlighting;

namespace Alder.NativeMarkdown.UI.Rendering;

/// <summary>
/// 一个代码块的共享状态：代码、行起点索引、增量高亮会话与结果。保存在文档的块状态存储中，
/// 同一代码块的所有视图（含长代码分段后的各段、回收后重建的视图）共用，避免重复切行和重复着色。
/// 代码和行索引由界面线程维护，高亮会话由后台任务串行使用，结果在界面线程发布。
/// </summary>
internal sealed class CodeBlockState
{
    private static readonly TimeSpan HighlightThrottle = TimeSpan.FromMilliseconds(40);
    private const int DirectLineIndexLimit = 4096;

    private readonly SyncLock _highlightLock = new();
    private Alder.NativeCode.PieceTreeBase? _buffer;
    private List<int>? _lineStarts;
    private readonly List<string?> _lineTexts = [];
    private IHighlightSession? _session;
    private bool _highlightRunning;
    private bool _highlightUnavailable;
    private bool _disposed;
    private string? _highlightPending;
    private bool _highlightPendingOpen;
    private HighlightResult? _readyHighlight;
    private CancellationTokenSource? _throttleCts;
    private long _lastHighlightTimestamp;
    private int _visibleTokenLines = int.MaxValue;

    private CodeBlockState(string? language, ISyntaxHighlighter? highlighter, HighlightThemeKind theme)
    {
        Language = language;
        Highlighter = highlighter;
        Theme = theme;
    }

    // Chunk views can miss revisions while recycled. Only the immediately preceding one can reuse the prefix.
    internal long Revision { get; private set; }
    internal int FirstChangedLine { get; private set; }
    internal int FirstHighlightedLine { get; private set; }

    public string Code { get; private set; } = "";

    public string? Language { get; }

    public ISyntaxHighlighter? Highlighter { get; }

    public HighlightThemeKind Theme { get; }

    /// <summary>每行在 <see cref="Code"/> 中的起始偏移。</summary>
    public int LineStart(int row) => _buffer?.GetOffsetAt(row + 1, 1)
        ?? (row > 0 && _lineStarts is { } starts && row <= starts.Count ? starts[row - 1] : 0);

    public int LineCount => _buffer?.GetLineCount() ?? (_lineStarts?.Count ?? 0) + 1;

    /// <summary>最近一次着色结果（按行）以及对应的代码。</summary>
    public IReadOnlyList<ImmutableArray<HighlightToken>> Tokens { get; private set; } = [];

    public string TokensCode { get; private set; } = "";

    /// <summary>高亮结果更新（界面线程）。</summary>
    public event Action? HighlightUpdated;

    internal const string StateName = "code.state";

    /// <summary>取得（或创建）块的共享状态；语言、高亮器或明暗主题变化时重建。</summary>
    public static CodeBlockState For(RenderContext context, int blockKey, string? language, int segment = 0)
    {
        var highlighter = context.Plugins.Highlighter;
        var theme = context.Style.HighlightTheme;
        var stateKey = BlockView.StateKey(StateName, segment);
        var existing = context.State.Get<CodeBlockState?>(blockKey, stateKey, null);
        if (existing is not null && existing.Language == language && ReferenceEquals(existing.Highlighter, highlighter) && existing.Theme == theme)
        {
            return existing;
        }
        existing?.Dispose();
        var state = new CodeBlockState(language, highlighter, theme);
        context.State.Set(blockKey, stateKey, state);
        return state;
    }

    public void Update(string code, bool isOpen = false)
    {
        if (ReferenceEquals(code, Code) || (code == Code && Code.Length > 0))
        {
            if (!isOpen)
            {
                FlushOpenHighlight(code);
            }
            return;
        }
        // 只追加时从上一个完整行继续切分，保留前面已完成行的起点与切片缓存
        if (code.StartsWith(Code, StringComparison.Ordinal))
        {
            var last = LineCount - 1;
            FirstChangedLine = last;
            if (_lineTexts.Count > last) _lineTexts.RemoveRange(last, _lineTexts.Count - last);
            if (_buffer is { } buffer)
            {
                buffer.Insert(buffer.GetLength(), code[Code.Length..]);
            }
            else
            {
                ExtendLineStarts(code, Code.Length);
            }
        }
        else
        {
            FirstChangedLine = 0;
            // Preserve colors from completed lines in the shared prefix while the worker
            // reparses the edit. The grammar state of the remaining lines may have changed.
            var prefix = code.AsSpan().CommonPrefixLength(TokensCode);
            _visibleTokenLines = Math.Min(_visibleTokenLines, CompletedLineCount(prefix));
            _buffer = null;
            _lineStarts?.Clear();
            ExtendLineStarts(code, 0);
            _lineTexts.Clear();
        }
        Revision++;
        Code = code;
        RequestHighlight(code, isOpen);
    }

    private int CompletedLineCount(int offset)
    {
        var low = 0;
        var high = Math.Min(LineCount - 1, Tokens.Count);
        while (low < high)
        {
            var middle = low + (high - low + 1) / 2;
            if (LineStart(middle) <= offset)
                low = middle;
            else
                high = middle - 1;
        }
        return low;
    }

    public string LineText(int line)
    {
        // Empty and single-line fences can use the source string directly.
        if (_buffer is null && _lineStarts is not { Count: > 0 })
        {
            return Code;
        }
        if (line < _lineTexts.Count && _lineTexts[line] is { } cached)
        {
            return cached;
        }
        string text;
        if (_buffer is { } buffer)
        {
            text = buffer.GetLineContent(line + 1);
        }
        else
        {
            var start = LineStart(line);
            var end = line < _lineStarts!.Count ? _lineStarts[line] : Code.Length;
            text = Code.AsSpan(start, end - start).TrimEnd("\r\n").ToString();
        }
        while (_lineTexts.Count <= line)
        {
            _lineTexts.Add(null);
        }
        _lineTexts[line] = text;
        return text;
    }

    // Keep short fences on the string they already own. Scan only the new suffix
    // and retain the piece tree for longer multi-line text.
    private void ExtendLineStarts(string code, int offset)
    {
        if (code.Length > DirectLineIndexLimit &&
            (_lineStarts is { Count: > 0 } || code.AsSpan(offset).ContainsAny('\r', '\n')))
        {
            _buffer = new([], "\n", false);
            _buffer.Insert(0, code);
            _lineStarts = null;
            return;
        }
        if (offset > 0 && Code[offset - 1] == '\r' && code[offset] == '\n')
        {
            _lineStarts![^1] = ++offset;
        }
        while (offset < code.Length)
        {
            var next = code.AsSpan(offset).IndexOfAny('\r', '\n');
            if (next < 0)
            {
                break;
            }
            offset += next + 1;
            if (code[offset - 1] == '\r' && offset < code.Length && code[offset] == '\n')
            {
                offset++;
            }
            (_lineStarts ??= []).Add(offset);
        }
    }

    private void FlushOpenHighlight(string code)
    {
        CancellationTokenSource? cancel = null;
        lock (_highlightLock)
        {
            if (_highlightPending is not null || _throttleCts is not null)
            {
                _highlightPendingOpen = false;
                cancel = _throttleCts;
                _throttleCts = null;
            }
        }
        if (cancel is not null)
        {
            try
            {
                cancel.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
        else if (!ReferenceEquals(TokensCode, code) && TokensCode != code)
        {
            RequestHighlight(code, isOpen: false);
        }
    }

    // 后台增量着色：同一时刻最多一个任务，期间到达的新代码合并为一次；开放流式代码块按约 25fps 节流，闭合后立即着色
    private void RequestHighlight(string code, bool isOpen)
    {
        if (Highlighter is null || string.IsNullOrEmpty(Language))
        {
            return;
        }
        CancellationTokenSource? cancel = null;
        var start = false;
        lock (_highlightLock)
        {
            if (_disposed || _highlightUnavailable)
            {
                return;
            }
            _highlightPending = code;
            _highlightPendingOpen = isOpen;
            if (!isOpen && _throttleCts is not null)
            {
                cancel = _throttleCts;
                _throttleCts = null;
            }
            if (!_highlightRunning)
            {
                _highlightRunning = true;
                start = true;
            }
        }
        if (cancel is not null)
        {
            try
            {
                cancel.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
        if (start)
        {
            _ = Task.Run(RunHighlightAsync);
        }
    }

    private async Task RunHighlightAsync()
    {
        // Checking language support and constructing the session can trigger grammar
        // initialization. Keep it on the same worker that updates the session.
        IHighlightSession? session = _session;
        if (session is null)
        {
            try
            {
                if (Highlighter!.SupportsLanguage(Language))
                {
                    session = Highlighter.CreateSession(Language!, Theme);
                }
            }
            catch (Exception)
            {
                // A plugin may fail during initialization. Plain code remains usable.
            }
            lock (_highlightLock)
            {
                _session = session;
                if (session is null)
                {
                    _highlightUnavailable = true;
                    _highlightPending = null;
                    _highlightRunning = false;
                    return;
                }
            }
        }
        var waited = false;
        while (true)
        {
            string? code;
            var wait = TimeSpan.Zero;
            CancellationTokenSource? delayCts = null;
            lock (_highlightLock)
            {
                code = _highlightPending;
                var isOpen = _highlightPendingOpen;
                if (code is null)
                {
                    _highlightRunning = false;
                    if (!_disposed)
                    {
                        return;
                    }
                    _session = null;
                }
                else if (isOpen && !waited && _lastHighlightTimestamp != 0)
                {
                    var elapsed = Stopwatch.GetElapsedTime(_lastHighlightTimestamp);
                    if (elapsed < HighlightThrottle)
                    {
                        wait = HighlightThrottle - elapsed;
                        delayCts = new CancellationTokenSource();
                        _throttleCts = delayCts;
                    }
                }
                if (wait <= TimeSpan.Zero)
                {
                    _highlightPending = null;
                }
            }
            if (code is null)
            {
                session.Dispose();
                return;
            }
            if (wait > TimeSpan.Zero && delayCts is not null)
            {
                try
                {
                    await Task.Delay(wait, delayCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    lock (_highlightLock)
                    {
                        if (ReferenceEquals(_throttleCts, delayCts))
                        {
                            _throttleCts = null;
                        }
                    }
                    delayCts.Dispose();
                }
                waited = true;
                continue;
            }
            waited = false;
            _lastHighlightTimestamp = Stopwatch.GetTimestamp();
            IReadOnlyList<ImmutableArray<HighlightToken>> tokens;
            try
            {
                tokens = session.Update(code);
            }
            catch (Exception)
            {
                continue;
            }
            QueueHighlightResult(code, tokens);
        }
    }

    private void QueueHighlightResult(string code, IReadOnlyList<ImmutableArray<HighlightToken>> tokens)
    {
        bool post;
        lock (_highlightLock)
        {
            if (_disposed)
                return;
            post = _readyHighlight is null;
            _readyHighlight = new HighlightResult(code, tokens);
        }
        if (post)
            Dispatcher.UIThread.Post(PublishHighlight, DispatcherPriority.Background);
    }

    private void PublishHighlight()
    {
        HighlightResult? ready;
        lock (_highlightLock)
        {
            ready = _readyHighlight;
            _readyHighlight = null;
        }
        if (ready is not { } result || !Code.StartsWith(result.Code, StringComparison.Ordinal))
            return;
        FirstHighlightedLine = 0;
        if (Highlighter is IAppendStableSyntaxHighlighter)
        {
            if (_visibleTokenLines != int.MaxValue)
                FirstHighlightedLine = _visibleTokenLines;
            else if (result.Code.StartsWith(TokensCode, StringComparison.Ordinal))
                FirstHighlightedLine = Math.Max(0, Tokens.Count - 1);
        }
        _visibleTokenLines = int.MaxValue;
        Tokens = result.Tokens;
        TokensCode = result.Code;
        HighlightUpdated?.Invoke();
    }

    private readonly record struct HighlightResult(string Code, IReadOnlyList<ImmutableArray<HighlightToken>> Tokens);

    /// <summary>已发布的着色覆盖当前源码的前缀。尾行继续追加时保留原有颜色，余下字符等待新快照。</summary>
    public ImmutableArray<HighlightToken> TokensFor(int line) =>
        (uint)line < (uint)Math.Min(Tokens.Count, _visibleTokenLines) ? Tokens[line] : default;

    public void Dispose()
    {
        CancellationTokenSource? cancel;
        IHighlightSession? session = null;
        lock (_highlightLock)
        {
            _disposed = true;
            _highlightPending = null;
            _readyHighlight = null;
            cancel = _throttleCts;
            _throttleCts = null;
            if (!_highlightRunning)
            {
                session = _session;
                _session = null;
            }
        }
        if (cancel is not null)
        {
            try
            {
                cancel.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
        session?.Dispose();
    }
}
