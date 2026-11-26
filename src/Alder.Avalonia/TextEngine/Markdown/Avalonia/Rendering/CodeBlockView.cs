#pragma warning disable CS1591 // Imported Markdown engine.
using System.Collections.Immutable;
using System.Globalization;
using Alder.Avalonia.Controls;
using Alder.NativeCode.Monarch;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Alder.NativeMarkdown.UI.Extensions;
using Alder.NativeMarkdown.UI.Selection;
using Alder.NativeMarkdown.Core.Highlighting;
using Alder.NativeMarkdown.Core.Model;

namespace Alder.NativeMarkdown.UI.Rendering;

/// <summary>
/// 代码块：头部（语言 / 路径、插件操作、换行切换、复制）+ 代码区。代码按行排版并缓存；
/// 切行与增量高亮放在块共享状态 <see cref="CodeBlockState"/> 中，回收后重建或分段显示都不重复计算。
/// 会话视图中的超长代码会拆成多段（<see cref="SetChunk"/>），只有第一段显示头部，所有分段共用一个选区片段。
/// </summary>
public sealed class CodeBlockView : BlockView
{
    private const double HeaderHeight = 32;
    private const double Padding = 12;
    private const string WrapState = "code.wrap";
    internal const string ExpandedState = "code.expanded";
    private const double FooterHeight = 30;

    private readonly CodeCopyButton _copy = new();
    private IconButton? _wrap;
    private IconButton? _download;
    private IconButton? _expand;
    private readonly CodeTextView _code = new();
    private BlockActionBar? _actions;
    private readonly List<Control> _headerButtons = [];
    private TextLayout? _title;
    private string? _titleText;
    private double _titleMaxWidth;
    private string? _parsedInfo;
    private int _parsedFirstLine = 1;
    private IDisposable? _copiedTimer;
    private int _copyRequest;
    private int _chunk;
    private int _chunkCount = 1;
    private int _linesPerChunk = int.MaxValue;
    // 折叠：超长的已完成代码块只显示前若干行，底部"显示全部"
    private bool _collapsible;
    private bool _collapsed;
    private int _lineCount;
    // 行号
    private bool _lineNumbers;
    private int _firstLineNumber = 1;
    private int _lineNumberStart;
    private int _lineNumberDigits;
    private MarkdownStyle? _lineNumbersStyle;
    private readonly List<TextLayout> _numbers = [];
    private double _gutter;
    private SegmentKey _segmentKey;
    // Avalonia 在块的 Bounds 变化（哪怕只是位置）时重跑 Render：上方内容展开时块每帧都在移动。
    // 边框笔、分段外形与折叠渐隐按输入缓存，不在每次 Render 里新建（Pen 与画刷的构造要走属性系统）。
    private IPen? _framePen;
    private IBrush? _framePenBrush;
    private StreamGeometry? _chunkGeometry;
    private (Size Size, double Top, double Bottom) _chunkGeometryKey;
    private IBrush? _fadeBrush;
    private Color _fadeColor;

    public CodeBlockView()
    {
        AddChild(_code);
        AddChild(_copy);
        _headerButtons.Add(_copy);
        _copy.Click += async (_, _) => await CopyAsync();
    }

    public string Code => _code.Text;

    public bool Wrap
    {
        get => _code.Wrap;
        set
        {
            _code.Wrap = value;
            if (_wrap is { } button && button.IsActive != value)
            {
                button.IsActive = value;
                button.InvalidateVisual();
            }
        }
    }

    /// <summary>当前分段（0 起）与分段总数。</summary>
    public (int Index, int Count) Chunk => (_chunk, _chunkCount);

    private bool HasHeader => IsFirstChunk && (Context?.Services.CodeBlocks.ShowHeader ?? true);
    private bool IsFirstChunk => _chunk == 0;

    private bool IsLastChunk => _chunk == _chunkCount - 1;

    /// <summary>作为超长代码的第 <paramref name="index"/> 段（每段 <paramref name="linesPerChunk"/> 行）显示。</summary>
    public void SetChunk(int index, int count, int linesPerChunk)
    {
        _chunk = index;
        _chunkCount = Math.Max(1, count);
        _linesPerChunk = count <= 1 ? int.MaxValue : linesPerChunk;
        for (var i = 0; i < _headerButtons.Count; i++)
        {
            _headerButtons[i].IsVisible = HasHeader;
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    // 流式超长代码块在尾部新增分段时，前面已满的分段只同步最新模型与总分段数，不触发测量或重绘
    internal void SyncChunkMetadata(CodeBlock block, int count)
    {
        ReplaceBlockReference(block);
        _chunkCount = Math.Max(1, count);
        _code.SyncText(block.Code);
    }

    private double TopHeight => IsFirstChunk ? (HasHeader ? HeaderHeight : 0) + VerticalInset : 0;

    private double BottomHeight => IsLastChunk ? (_collapsible ? FooterHeight : VerticalInset - ReservedScrollbarSpace) : 0;
    private double VerticalInset => Context?.Services.CodeBlocks.VerticalInset ?? CodeBlockOptions.Default.VerticalInset;
    private double HorizontalInset => Context?.Services.CodeBlocks.HorizontalInset ?? CodeBlockOptions.Default.HorizontalInset;
    // Use the existing bottom inset as the scroll gutter so a growing line
    // cannot move the bottom edge when it first exceeds the viewport width.
    private double ReservedScrollbarSpace => _chunkCount != 1 || _collapsible ? 0
        : Context?.Services.CodeBlocks.UseInsetForScrollbar == true ? Math.Min(VerticalInset, CodeTextView.ScrollbarHeight)
        : Block is CodeBlock { IsFenced: true } && VerticalInset >= CodeTextView.ScrollbarHeight ? CodeTextView.ScrollbarHeight : 0;

    /// <summary>是否处于折叠状态。</summary>
    public bool IsCollapsed => _collapsed;

    /// <summary>折叠状态变化（会话视图据此重新分段）。</summary>
    public static readonly global::Avalonia.Interactivity.RoutedEvent<global::Avalonia.Interactivity.RoutedEventArgs> ExpandedChangedEvent =
        global::Avalonia.Interactivity.RoutedEvent.Register<CodeBlockView, global::Avalonia.Interactivity.RoutedEventArgs>("ExpandedChanged", global::Avalonia.Interactivity.RoutingStrategies.Bubble);

    /// <summary>按选项与块状态判断代码块是否应折叠（会话视图分段时也用）。</summary>
    internal static bool ShouldCollapse(CodeBlock code, RenderContext context, int key, out int lines)
    {
        var existing = context.State.Get<CodeBlockState?>(key, CodeBlockState.StateName, null);
        lines = existing is not null && ReferenceEquals(existing.Code, code.Code)
            ? existing.LineCount
            : code.GetLineCount();
        var options = context.Services.CodeBlocks;
        return IsCollapsible(code, options, lines) && !context.State.Get(key, ExpandedState, false);
    }

    private static bool ShouldCollapse(CodeBlock code, RenderContext context, int key, int segment, int lines)
    {
        var options = context.Services.CodeBlocks;
        return IsCollapsible(code, options, lines) && !context.State.Get(key, StateKey(ExpandedState, segment), false);
    }

    private static bool IsCollapsible(CodeBlock code, Extensions.CodeBlockOptions options, int lines) =>
        options.CollapseAfterLines > 0 && !code.IsOpen && lines > options.CollapseAfterLines && lines > options.CollapsedLines;

    /// <summary>展开 / 折叠（状态保存在块状态存储中，回收后保留）。</summary>
    public void ToggleExpanded()
    {
        if (Context is null || Block is not CodeBlock code || !_collapsible)
        {
            return;
        }
        Context.State.Set(TopKey, StateKey(ExpandedState, _segmentKey.Segment), _collapsed);
        var segment = _segmentKey.Segment;
        Update(Context, code, TopKey, ref segment);
        RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(ExpandedChangedEvent, this));
    }

    /// <summary>下载：交给 <see cref="Extensions.CodeBlockOptions.Save"/>，默认弹出保存对话框写入文件。</summary>
    public async Task DownloadAsync()
    {
        if (Context is null || Block is not CodeBlock code)
        {
            return;
        }
        var fence = Plugins.CodeFenceInfo.Parse(code.Info);
        var request = new Extensions.CodeSaveRequest(Extensions.CodeFileNames.Suggest(code.Language, fence.Path), code.Code, code.Language, fence.Path);
        if (Context.Services.CodeBlocks.Save is { } save)
        {
            await save(request);
            return;
        }
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanSave: true } storage)
        {
            return;
        }
        var file = await storage.SaveFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerSaveOptions { SuggestedFileName = request.SuggestedFileName });
        if (file is null)
        {
            return;
        }
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
        await writer.WriteAsync(request.Code);
    }

    protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged)
    {
        var code = (CodeBlock)block;
        var style = context.Style;
        var options = context.Services.CodeBlocks;
        var showWrap = HasHeader && options.ShowWrapToggle;
        var showDownload = HasHeader && options.ShowDownload;
        var rebuildHeader = contextChanged || previous is not CodeBlock old || old.Info != code.Info || old.Language != code.Language;
        if (showWrap && _wrap is null)
        {
            _wrap = AddHeaderButton(MarkdownIcons.Wrap, "Wrap lines", ToggleWrap);
            rebuildHeader = true;
        }
        if (showDownload && _download is null)
        {
            _download = AddHeaderButton(MarkdownIcons.Download, "Download", async (_, _) => await DownloadAsync());
            rebuildHeader = true;
        }
        if (_copyRequest != 0 && previous is CodeBlock previousCode && previousCode.Code != code.Code)
            ResetCopy();
        if (rebuildHeader)
        {
            _parsedInfo = code.Info;
            _parsedFirstLine = 1;
            _titleText = LanguageLabel(code.Language);
            if (code.Info.AsSpan().IndexOfAny(' ', '\t') >= 0)
                _titleText = FormatFenceTitle(_titleText, code.Info);
            DisposeTitle();
            if (_wrap is not null || _download is not null || _actions is not null || !context.Plugins.BlockActions.IsEmpty)
                UpdateHeaderActions(context, code);
        }
        _segmentKey = KeyFor(context, topKey, ref segment);
        var seg = _segmentKey.Segment;
        Wrap = context.State.Get(topKey, StateKey(WrapState, seg), false);
        var shared = CodeBlockState.For(context, topKey, code.Language, seg);
        shared.Update(code.Code, code.IsOpen);
        if (code.IsOpen)
        {
            // 看着流式写出来的代码块完成后不折叠（避免突然收起）
            context.State.Set(topKey, StateKey(ExpandedState, seg), true);
        }
        var oldCollapsed = _collapsed;
        var oldCollapsible = _collapsible;
        var oldLineCount = _lineCount;
        _lineCount = shared.LineCount;
        _collapsed = ShouldCollapse(code, context, topKey, seg, _lineCount) && _chunkCount == 1;
        _collapsible = _chunkCount == 1 && IsCollapsible(code, options, _lineCount) || _chunkCount > 1 && IsLastChunk && IsCollapsible(code, options, _lineCount);
        if (_collapsible)
        {
            if (_expand is null)
            {
                _expand = new IconButton();
                _expand.Click += (_, _) => ToggleExpanded();
                AddChild(_expand);
            }
            _expand.Foreground = style.SecondaryForeground;
            _expand.HoverBackground = style.HoverBackground;
            _expand.Icon = _collapsed ? MarkdownIcons.Chevron : MarkdownIcons.ChevronUp;
            _expand.Label = _collapsed ? options.ExpandText(_lineCount) : options.CollapseText();
        }
        if (_expand is not null) _expand.IsVisible = _collapsible;
        _copy.IsVisible = HasHeader;
        if (_wrap is not null) _wrap.IsVisible = showWrap;
        if (_download is not null) _download.IsVisible = showDownload;
        var lineStart = _linesPerChunk == int.MaxValue ? 0 : _chunk * _linesPerChunk;
        var codeChanged = _code.Set(context, _segmentKey, shared, lineStart,
            _collapsed ? options.CollapsedLines : _linesPerChunk, ReservedScrollbarSpace, code.IsOpen);
        var numbersChanged = (_lineNumbers || options.ShowLineNumbers) && UpdateLineNumbers(context, code, shared, lineStart);
        var frameChanged = contextChanged || previous is null || numbersChanged || oldCollapsed != _collapsed || oldCollapsible != _collapsible;
        if (frameChanged || codeChanged || oldLineCount != _lineCount)
        {
            InvalidateMeasure();
        }
        if (frameChanged)
        {
            InvalidateVisual();
        }
    }

    private string FormatFenceTitle(string title, string? info)
    {
        var fence = Plugins.CodeFenceInfo.Parse(info);
        _parsedFirstLine = fence.StartLine ?? 1;
        if (fence.Path is { } path)
            title += " · " + path + (fence.StartLine is { } start ? $"#L{start}" + (fence.EndLine is { } end && end != start ? $"-{end}" : "") : "");
        return title;
    }

    private IconButton AddHeaderButton(Geometry icon, string name, EventHandler click)
    {
        var button = new IconButton { Icon = icon };
        global::Avalonia.Automation.AutomationProperties.SetName(button, name);
        button.Click += click;
        AddChild(button);
        return button;
    }

    private void ToggleWrap(object? sender, EventArgs e)
    {
        Wrap = !Wrap;
        Context?.State.Set(TopKey, StateKey(WrapState, _segmentKey.Segment), Wrap);
        InvalidateMeasure();
    }

    private static int DigitCount(int n)
    {
        var digits = 1;
        while (n >= 10)
        {
            n /= 10;
            digits++;
        }
        return digits;
    }

    private bool UpdateLineNumbers(RenderContext context, CodeBlock code, CodeBlockState shared, int lineStart)
    {
        var show = context.Services.CodeBlocks.ShowLineNumbers;
        if (!show)
        {
            var wasShowing = _lineNumbers;
            _lineNumbers = false;
            DisposeNumbers();
            _gutter = 0;
            _lineNumberDigits = 0;
            _lineNumbersStyle = null;
            return wasShowing;
        }
        if (!ReferenceEquals(_parsedInfo, code.Info) && _parsedInfo != code.Info)
        {
            _parsedInfo = code.Info;
            _parsedFirstLine = Plugins.CodeFenceInfo.Parse(code.Info).StartLine ?? 1;
        }
        var firstLine = _parsedFirstLine;
        var style = context.Style;
        var last = firstLine + shared.LineCount - 1;
        var digits = DigitCount(Math.Max(1, last));
        var count = _code.VisibleLineCount;
        var rebuild = !_lineNumbers || !ReferenceEquals(_lineNumbersStyle, style) || _firstLineNumber != firstLine || _lineNumberStart != lineStart || _lineNumberDigits != digits;
        _lineNumbers = true;
        _firstLineNumber = firstLine;
        _lineNumberStart = lineStart;
        _lineNumbersStyle = style;
        var typeface = new Typeface(style.MonospaceFontFamily);
        if (rebuild)
        {
            _lineNumberDigits = digits;
            using var probe = new TextLayout(new string('0', digits), typeface, style.CodeFontSize, style.SecondaryForeground);
            _gutter = Math.Ceiling(probe.WidthIncludingTrailingWhitespace) + 14;
            DisposeNumbers();
        }
        if (_numbers.Count == count && !rebuild)
        {
            return false;
        }
        if (_numbers.Count > count)
        {
            for (var i = count; i < _numbers.Count; i++)
            {
                _numbers[i].Dispose();
            }
            _numbers.RemoveRange(count, _numbers.Count - count);
        }
        for (var i = _numbers.Count; i < count; i++)
        {
            // 与代码行相同的行高，保证基线对齐
            _numbers.Add(new TextLayout((_firstLineNumber + lineStart + i).ToString(), typeface, style.CodeFontSize, style.SecondaryForeground,
                TextAlignment.Right, maxWidth: _gutter - 14, lineHeight: style.CodeLineHeight));
        }
        return true;
    }

    private async Task CopyAsync()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }
        var request = ++_copyRequest;
        try
        {
            await clipboard.SetTextAsync(_code.Text);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or UnauthorizedAccessException)
        {
            return;
        }
        if (request != _copyRequest || !this.IsAttachedToVisualTree()) return;
        _copy.SetCopied(true, animate: true);
        _copiedTimer?.Dispose();
        _copiedTimer = DispatcherTimer.RunOnce(() =>
        {
            _copiedTimer = null;
            _copy.SetCopied(false, animate: true);
        }, TimeSpan.FromSeconds(2));
    }

    private void ResetCopy()
    {
        _copyRequest++;
        _copiedTimer?.Dispose();
        _copiedTimer = null;
        _copy.SetCopied(false, animate: false);
    }

    // 插件操作（例如"应用到文件""在终端运行"）排在内置按钮左边
    private void UpdateHeaderActions(RenderContext context, CodeBlock code)
    {
        if (_actions is not null || !context.Plugins.BlockActions.IsEmpty)
        {
            _actions ??= new BlockActionBar(this, AddChild, RemoveChild);
            _actions.Rebuild(context, Plugins.BlockActionTargets.Code, code, HasHeader);
        }
        _headerButtons.Clear();
        _headerButtons.Add(_copy);
        if (_wrap is not null) _headerButtons.Add(_wrap);
        if (_download is not null) _headerButtons.Add(_download);
        if (_actions is not null)
        {
            foreach (var button in _actions.Buttons)
                _headerButtons.Add(button);
        }
        for (var i = 1; i < _headerButtons.Count; i++)
        {
            if (_headerButtons[i] is not IconButton button) continue;
            button.Foreground = context.Style.SecondaryForeground;
            button.HoverBackground = context.Style.HoverBackground;
            button.InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_lineNumbers && _numbers.Count == 0 && Context is not null && Block is CodeBlock code && _code.State is { } shared)
        {
            UpdateLineNumbers(Context, code, shared, _lineNumberStart);
        }
        for (var i = 0; i < _headerButtons.Count; i++)
        {
            _headerButtons[i].Measure(Size.Infinity);
        }
        _expand?.Measure(Size.Infinity);
        var inner = Math.Max(0, availableSize.Width - HorizontalInset * 2 - _gutter);
        _code.Measure(new Size(inner, double.PositiveInfinity));
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : _code.DesiredSize.Width + HorizontalInset * 2;
        return new Size(width, TopHeight + _code.DesiredSize.Height + BottomHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = finalSize.Width - Padding;
        for (var i = 0; i < _headerButtons.Count; i++)
        {
            var button = _headerButtons[i];
            if (!button.IsVisible)
            {
                continue;
            }
            x -= button.DesiredSize.Width;
            button.Arrange(new Rect(x, 8 + (24 - button.DesiredSize.Height) / 2, button.DesiredSize.Width, button.DesiredSize.Height));
            x -= 2;
        }
        double titleWidth = Math.Max(0, x - Padding);
        if (_titleMaxWidth != titleWidth)
        {
            _titleMaxWidth = titleWidth;
            DisposeTitle();
            InvalidateVisual();
        }
        _code.Arrange(new Rect(HorizontalInset + _gutter, TopHeight, Math.Max(0, finalSize.Width - HorizontalInset * 2 - _gutter), _code.DesiredSize.Height));
        if (_expand is { IsVisible: true } expand)
        {
            expand.Arrange(new Rect(Padding - 6, finalSize.Height - FooterHeight + (FooterHeight - expand.DesiredSize.Height) / 2, expand.DesiredSize.Width, expand.DesiredSize.Height));
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (Context is null)
        {
            return;
        }
        var style = Context.Style;
        var r = style.CornerRadius.TopLeft;
        bool hasBorder = style.CodeBorderBrush is not ISolidColorBrush { Color.A: 0 };
        var bounds = hasBorder && _chunkCount == 1 ? new Rect(Bounds.Size).Deflate(0.5) : new Rect(Bounds.Size);
        if (_chunkCount == 1)
        {
            context.DrawRectangle(style.CodeBackground, hasBorder ? FramePenFor(style.CodeBorderBrush) : null, bounds, r, r);
        }
        else
        {
            RenderChunkBackground(context, style, bounds, r);
        }
        if (HasHeader && _titleMaxWidth > 0)
        {
            if (_title is null && _titleText is not null)
            {
                _title = CreateTitleLayout(_titleText, style, _titleMaxWidth);
            }
            _title?.Draw(context, new Point(Padding, 8 + (24 - _title.Height) / 2));
        }
        if (_lineNumbers)
        {
            var tops = _code.LineTops;
            for (var i = 0; i < _numbers.Count && i < tops.Count; i++)
            {
                _numbers[i].Draw(context, new Point(HorizontalInset, TopHeight + tops[i]));
            }
        }
        if (_collapsed && style.CodeBackground is ISolidColorBrush { Color: var bg })
        {
            RenderFoldFade(context, bg);
        }
    }

    private IPen FramePenFor(IBrush brush)
    {
        if (_framePen is null || !ReferenceEquals(_framePenBrush, brush))
        {
            _framePenBrush = brush;
            _framePen = new Pen(brush, 1);
        }
        return _framePen;
    }

    private void RenderChunkBackground(DrawingContext context, MarkdownStyle style, Rect bounds, double r)
    {
        // 分段：首段只有上圆角，末段只有下圆角，中间段直角；各段背景首尾相接
        var top = IsFirstChunk ? r : 0;
        var bottom = IsLastChunk ? r : 0;
        var key = (Bounds.Size, top, bottom);
        var geometry = _chunkGeometry;
        if (geometry is null || _chunkGeometryKey != key)
        {
            geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(new Point(bounds.Left + top, bounds.Top), true);
                g.LineTo(new Point(bounds.Right - top, bounds.Top));
                if (top > 0)
                {
                    g.ArcTo(new Point(bounds.Right, bounds.Top + top), new Size(top, top), 0, false, SweepDirection.Clockwise);
                }
                g.LineTo(new Point(bounds.Right, bounds.Bottom - bottom));
                if (bottom > 0)
                {
                    g.ArcTo(new Point(bounds.Right - bottom, bounds.Bottom), new Size(bottom, bottom), 0, false, SweepDirection.Clockwise);
                }
                g.LineTo(new Point(bounds.Left + bottom, bounds.Bottom));
                if (bottom > 0)
                {
                    g.ArcTo(new Point(bounds.Left, bounds.Bottom - bottom), new Size(bottom, bottom), 0, false, SweepDirection.Clockwise);
                }
                g.LineTo(new Point(bounds.Left, bounds.Top + top));
                if (top > 0)
                {
                    g.ArcTo(new Point(bounds.Left + top, bounds.Top), new Size(top, top), 0, false, SweepDirection.Clockwise);
                }
                g.EndFigure(true);
            }
            _chunkGeometry = geometry;
            _chunkGeometryKey = key;
        }
        context.DrawGeometry(style.CodeBackground, null, geometry);
    }

    private void RenderFoldFade(DrawingContext context, Color bg)
    {
        // 折叠：代码区底部渐隐到背景色
        var fadeHeight = Math.Min(36, _code.Bounds.Height);
        var top = _code.Bounds.Bottom - fadeHeight;
        if (_fadeBrush is null || _fadeColor != bg)
        {
            _fadeColor = bg;
            _fadeBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.FromArgb(0, bg.R, bg.G, bg.B), 0), new GradientStop(bg, 1) },
            };
        }
        context.FillRectangle(_fadeBrush, new Rect(1, top, Bounds.Width - 2, fadeHeight));
    }

    public override IEnumerable<ISelectableSegment> Segments() => [_code];

    internal static string LanguageLabel(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return "Plain text";
        var label = language.Trim().TrimStart('{', '.').TrimEnd('}');
        if (label.StartsWith("language-", StringComparison.OrdinalIgnoreCase))
            label = label["language-".Length..];

        return AlderMonarch.Resolve(label) switch
        {
            "csharp" => "C#",
            "typescript" => "TypeScript",
            "javascript" => "JavaScript",
            "python" => "Python",
            "powershell" => "PowerShell",
            "rust" => "Rust",
            "cpp" => "C++",
            "text" or "plaintext" or "" => "Plain text",
            "xml" when label.Equals("axaml", StringComparison.OrdinalIgnoreCase) => "Avalonia XAML",
            var name => name
        };
    }

    private static TextLayout CreateTitleLayout(string title, MarkdownStyle style, double width) =>
        new(title, new Typeface(style.MonospaceFontFamily), 12, style.CodeHeaderForeground,
            textTrimming: TextTrimming.CharacterEllipsis, maxWidth: width, lineHeight: 16, maxLines: 1);

    private void DisposeTitle()
    {
        _title?.Dispose();
        _title = null;
    }

    private void DisposeNumbers()
    {
        for (var i = 0; i < _numbers.Count; i++)
        {
            _numbers[i].Dispose();
        }
        _numbers.Clear();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ResetCopy();
        DisposeTitle();
        if (_numbers.Count > 0)
        {
            DisposeNumbers();
            _lineNumbersStyle = null;
            InvalidateMeasure();
        }
    }
}

/// <summary>
/// 代码文本：等宽、逐行排版并缓存，支持不换行时的横向滚动。可只显示一个行窗口（长代码分段），
/// 偏移始终是整个代码块中的全局偏移，因此各分段共用一个选区片段。
/// </summary>
public sealed class CodeTextView : Control, ISelectableSegment
{
    private const int LinesPerStrip = 16;
    private static readonly Cursor IbeamCursor = new(StandardCursorType.Ibeam);

    private sealed class Line
    {
        public required string Text;
        public ImmutableArray<HighlightToken> Tokens;
        public TextLayout? Layout;
        public double Width;
    }

    /// <summary>每 16 行代码共享一个轻量子视觉，流式追加或异步高亮着色时只重录变化的分条，避免重绘整块代码。</summary>
    private sealed class CodeStripVisual(CodeTextView owner) : Visual
    {
        private int _firstLine;
        private int _endLine;

        public bool SetLines(int firstLine, int endLine)
        {
            if (_firstLine == firstLine && _endLine == endLine)
                return false;
            _firstLine = firstLine;
            _endLine = endLine;
            return true;
        }

        public bool SetBounds(Rect bounds)
        {
            if (Bounds == bounds)
            {
                return false;
            }
            Bounds = bounds;
            return true;
        }

        public override void Render(DrawingContext context) => owner.RenderLines(context, _firstLine, _endLine, localOrigin: true);
    }

    private readonly List<Line> _lines = [];
    private readonly List<int> _lineStarts = [];
    private readonly List<double> _lineTops = [];
    private readonly List<CodeStripVisual> _strips = [];
    private CodeStripVisual? _tailVisual;
    private int _inlineLineCount;
    private bool _hasStreamed;
    private readonly Dictionary<(uint Foreground, HighlightFontStyle Style), GenericTextRunProperties> _runProps = [];
    private GenericTextParagraphProperties? _codeParagraph;
    private RenderContext? _context;
    private CodeBlockState? _state;
    private string _text = "";
    private long _appliedRevision = -1;
    private int _windowStart;
    private int _windowLines = int.MaxValue;
    private bool _wrap;
    private double _layoutWidth = double.NaN;
    private double _offsetX;
    private double _contentWidth;
    internal const double ScrollbarHeight = 8;
    private double _reservedScrollbarSpace;
    private bool _hover;
    private bool _allStripsDirty = true;
    private int _dirtyLineStart = int.MaxValue;
    private int _dirtyLineEnd = -1;

    /// <summary>窗口内各行相对自身的顶部位置（行号用，测量后有效）。</summary>
    internal IReadOnlyList<double> LineTops => _lineTops;

    internal int VisibleLineCount => _lines.Count;

    /// <summary>当前代码分条视觉数（测试与诊断用）。</summary>
    internal int StripCount => _strips.Count;

    internal CodeBlockState? State => _state;

    internal string VisibleLineText(int index) => _lines[index].Text;

    internal ImmutableArray<HighlightToken> VisibleLineTokens(int index) => _lines[index].Tokens;

    public SegmentKey Key { get; private set; }

    /// <summary>整个代码块的文本（选区与复制按全局偏移计算）。</summary>
    public string Text => _text;

    public Control View => this;

    public bool Wrap
    {
        get => _wrap;
        set
        {
            if (_wrap != value)
            {
                _wrap = value;
                _offsetX = 0;
                _codeParagraph = null;
                DisposeLineLayouts();
                _layoutWidth = double.NaN;
                _allStripsDirty = true;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }
    }

    public CodeTextView()
    {
        ClipToBounds = true;
    }

    internal void SyncText(string code) => _text = code;

    protected override global::Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new Automation.TextSegmentAutomationPeer(this, () => _text);

    internal bool Set(RenderContext context, SegmentKey key, CodeBlockState state, int lineStart, int lineCount,
        double reservedScrollbarSpace = 0, bool isOpen = false)
    {
        var contextChanged = !ReferenceEquals(_context, context);
        if (_context is not null && contextChanged)
        {
            _context.Selection.Changed -= InvalidateVisual;
        }
        if (contextChanged && VisualRoot is not null)
        {
            context.Selection.Changed += InvalidateVisual;
        }
        var styleChanged = _context is null || !ReferenceEquals(_context.Style, context.Style);
        if (styleChanged)
        {
            _runProps.Clear();
            _codeParagraph = null;
        }
        _context = context;
        Key = key;
        var stateChanged = !ReferenceEquals(_state, state);
        var streamed = isOpen || !stateChanged && _hasStreamed;
        var modeChanged = streamed != _hasStreamed && _lines.Count > 1;
        _hasStreamed = streamed;
        if (stateChanged)
        {
            if (_state is not null)
            {
                _state.HighlightUpdated -= OnHighlightUpdated;
            }
            _state = state;
            if (VisualRoot is not null)
            {
                state.HighlightUpdated += OnHighlightUpdated;
            }
        }
        var reset = stateChanged || styleChanged || lineStart != _windowStart || lineCount != _windowLines;
        if (reset && _lines.Count != 0)
            ClearLines();
        _windowStart = lineStart;
        _windowLines = lineCount;
        _text = state.Code;
        var gutterChanged = _reservedScrollbarSpace != reservedScrollbarSpace;
        if (gutterChanged || modeChanged)
        {
            _reservedScrollbarSpace = reservedScrollbarSpace;
            InvalidateMeasure();
            InvalidateVisual();
        }
        return ApplyWindow(forceChanged: reset || contextChanged, highlightOnly: false) || gutterChanged || modeChanged;
    }

    // 只为窗口内的行建立排版对象；文本与着色未变的行保留缓存。
    // 异步高亮仅着色且不换行、尺寸未变时只重录受影响的分条，完全跳过 Measure。
    private bool ApplyWindow(bool forceChanged = false, bool highlightOnly = false)
    {
        var state = _state!;
        var previousLineCount = _lines.Count;
        var end = (int)Math.Min((long)_windowStart + _windowLines, state.LineCount);
        int start = _windowStart;
        if (!forceChanged)
        {
            int dirty = highlightOnly ? state.FirstHighlightedLine :
                _appliedRevision == state.Revision ? end :
                _appliedRevision + 1 == state.Revision ? state.FirstChangedLine : 0;
            start = Math.Clamp(dirty, _windowStart, Math.Max(_windowStart, end));
        }
        _appliedRevision = state.Revision;
        int index = Math.Min(start - _windowStart, _lines.Count);
        if (_lineStarts.Count > index) _lineStarts.RemoveRange(index, _lineStarts.Count - index);
        var textOrCountChanged = forceChanged;
        var tokensChanged = false;
        var localDirtyStart = int.MaxValue;
        var localDirtyEnd = -1;
        for (var i = _windowStart + index; i < end; i++, index++)
        {
            var text = state.LineText(i);
            _lineStarts.Add(state.LineStart(i));
            var tokens = state.TokensFor(i);
            if (index < _lines.Count)
            {
                var line = _lines[index];
                var textChanged = !ReferenceEquals(line.Text, text) && line.Text != text;
                var tokenChanged = !SameTokens(line.Tokens, tokens);
                if (textChanged || tokenChanged)
                {
                    line.Text = text;
                    line.Tokens = tokens;
                    line.Layout?.Dispose();
                    line.Layout = null;
                    if (textChanged)
                    {
                        textOrCountChanged = true;
                    }
                    else
                    {
                        tokensChanged = true;
                    }
                    if (index < localDirtyStart)
                    {
                        localDirtyStart = index;
                    }
                    localDirtyEnd = index;
                }
            }
            else
            {
                _lines.Add(new Line { Text = text, Tokens = tokens });
                textOrCountChanged = true;
                if (index < localDirtyStart)
                {
                    localDirtyStart = index;
                }
                localDirtyEnd = index;
            }
        }
        if (_lines.Count > index)
        {
            for (var i = index; i < _lines.Count; i++)
            {
                _lines[i].Layout?.Dispose();
                _lines[i].Layout = null;
            }
            _lines.RemoveRange(index, _lines.Count - index);
            textOrCountChanged = true;
            if (index < localDirtyStart)
            {
                localDirtyStart = index;
            }
            localDirtyEnd = Math.Max(localDirtyEnd, index);
        }
        if (!textOrCountChanged && !tokensChanged)
        {
            return false;
        }
        if (forceChanged)
        {
            _allStripsDirty = true;
        }
        else if (localDirtyStart <= localDirtyEnd)
        {
            _dirtyLineStart = Math.Min(_dirtyLineStart, localDirtyStart);
            _dirtyLineEnd = Math.Max(_dirtyLineEnd, localDirtyEnd);
        }

        // A non-wrapping block fills the available width. Same-line text or color
        // changes can replace the affected text layouts without measuring its ancestors.
        if (!forceChanged && !_wrap && _lines.Count == previousLineCount &&
            double.IsFinite(_layoutWidth) && IsMeasureValid && IsArrangeValid &&
            RefreshMeasuredLines(localDirtyStart, localDirtyEnd))
            return false;

        InvalidateMeasure();
        if (forceChanged || _context?.Selection is { IsEmpty: false } or { HasHighlights: true })
        {
            InvalidateVisual();
        }
        return true;
    }

    private bool RefreshMeasuredLines(int first, int last)
    {
        var oldWidth = _contentWidth;
        var width = oldWidth;
        var widestLineShrank = false;
        for (var i = first; i <= last; i++)
        {
            var line = _lines[i];
            if (line.Layout is not null)
                continue;
            var previousWidth = line.Width;
            if (line.Text.Length == 0)
                line.Width = 0;
            else
                LayoutFor(line, _layoutWidth);
            width = Math.Max(width, line.Width);
            if (previousWidth == oldWidth && line.Width < previousWidth)
                widestLineShrank = true;
        }
        if (widestLineShrank && width == oldWidth)
            width = FindContentWidth();

        // An unreserved scrollbar changes the desired height when it appears or disappears.
        if (_reservedScrollbarSpace == 0 && (oldWidth > _layoutWidth) != (width > _layoutWidth))
            return false;

        var oldOffset = _offsetX;
        _contentWidth = width;
        ClampOffset(Bounds.Width);
        _allStripsDirty |= oldOffset != _offsetX;
        InvalidateDirtyStripsNow();
        if (_hover || _context?.Selection is { IsEmpty: false } or { HasHighlights: true })
            InvalidateVisual();
        return true;
    }

    private double FindContentWidth()
    {
        var width = 0.0;
        foreach (var line in _lines)
            width = Math.Max(width, line.Width);
        return width;
    }

    private void InvalidateDirtyStripsNow()
    {
        if (_allStripsDirty)
        {
            InvalidateAllStrips();
        }
        else if (_dirtyLineStart <= _dirtyLineEnd)
        {
            if (_dirtyLineStart < _inlineLineCount)
                InvalidateVisual();
            if (_dirtyLineEnd >= _inlineLineCount)
                _tailVisual?.InvalidateVisual();
            var lastStrip = Math.Min(_dirtyLineEnd / LinesPerStrip, _strips.Count - 1);
            for (var s = _dirtyLineStart / LinesPerStrip; s <= lastStrip; s++)
                _strips[s].InvalidateVisual();
        }
        ResetDirtyRange();
    }

    private void ResetDirtyRange()
    {
        _allStripsDirty = false;
        _dirtyLineStart = int.MaxValue;
        _dirtyLineEnd = -1;
    }

    private void InvalidateAllStrips()
    {
        if (_inlineLineCount > 0)
            InvalidateVisual();
        _tailVisual?.InvalidateVisual();
        for (var s = 0; s < _strips.Count; s++)
        {
            _strips[s].InvalidateVisual();
        }
    }

    private static bool SameTokens(ImmutableArray<HighlightToken> a, ImmutableArray<HighlightToken> b)
    {
        if (a == b || a.IsDefaultOrEmpty && b.IsDefaultOrEmpty)
        {
            return true;
        }
        if (a.IsDefault || b.IsDefault || a.Length != b.Length)
        {
            return false;
        }
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    private void OnHighlightUpdated()
    {
        if (_state is not null)
        {
            ApplyWindow(forceChanged: false, highlightOnly: true);
        }
    }

    private double LineHeight => _context?.Style.CodeLineHeight ?? 20;

    private GenericTextRunProperties RunPropertiesFor(MarkdownStyle style, uint foreground, HighlightFontStyle fontStyle)
    {
        var key = (foreground, fontStyle);
        if (!_runProps.TryGetValue(key, out var props))
        {
            var brush = foreground == 0 ? style.CodeForeground : BrushCache.Get(foreground);
            var face = new Typeface(style.MonospaceFontFamily,
                fontStyle.HasFlag(HighlightFontStyle.Italic) ? FontStyle.Italic : FontStyle.Normal,
                fontStyle.HasFlag(HighlightFontStyle.Bold) ? FontWeight.Bold : FontWeight.Normal);
            TextDecorationCollection? decorations = fontStyle.HasFlag(HighlightFontStyle.Underline) ? TextDecorations.Underline
                : fontStyle.HasFlag(HighlightFontStyle.Strikethrough) ? TextDecorations.Strikethrough : null;
            props = new GenericTextRunProperties(face, style.CodeFontSize, decorations, brush);
            _runProps[key] = props;
        }
        return props;
    }

    private TextLayout LayoutFor(Line line, double maxWidth)
    {
        if (line.Layout is not null)
        {
            return line.Layout;
        }
        var style = _context!.Style;
        var wrapping = _wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        var width = _wrap ? maxWidth : double.PositiveInfinity;
        line.Layout = line.Tokens.IsDefaultOrEmpty || line.Text.Length == 0
            ? new TextLayout(line.Text.Length == 0 ? " " : line.Text, new Typeface(style.MonospaceFontFamily), style.CodeFontSize,
                style.CodeForeground, textWrapping: wrapping, maxWidth: width, lineHeight: LineHeight)
            : FormatHighlightedLine(line, style, wrapping, width);
        line.Width = line.Layout.WidthIncludingTrailingWhitespace;
        return line.Layout;
    }

    private TextLayout FormatHighlightedLine(Line line, MarkdownStyle style, TextWrapping wrapping, double width)
    {
        _codeParagraph ??= new GenericTextParagraphProperties(FlowDirection.LeftToRight, TextAlignment.Left, true, false,
            RunPropertiesFor(style, 0, HighlightFontStyle.None), wrapping, LineHeight, 0, 0);
        return new TextLayout(new CodeLineTextSource(this, style, line.Text, line.Tokens), _codeParagraph, maxWidth: width);
    }

    /// <summary>Feeds syntax spans straight into Avalonia's formatter without rebuilding a style override list.</summary>
    private sealed class CodeLineTextSource(CodeTextView owner, MarkdownStyle style, string text,
        ImmutableArray<HighlightToken> tokens) : ITextSource
    {
        public TextRun? GetTextRun(int index)
        {
            if ((uint)index >= (uint)text.Length)
                return null;

            // Find the first range that can cover this offset. Formatting can start inside a
            // tab, wrapped line or Unicode cluster, so it must not rely on a forward-only cursor.
            var low = 0;
            var high = tokens.Length;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                var token = tokens[middle];
                if ((long)token.Start + token.Length <= index)
                    low = middle + 1;
                else
                    high = middle;
            }

            var end = text.Length;
            var properties = owner.RunPropertiesFor(style, 0, HighlightFontStyle.None);
            if (low < tokens.Length)
            {
                var token = tokens[low];
                if (token.Start > index)
                    end = Math.Min(end, token.Start);
                else
                {
                    end = (int)Math.Min(end, (long)token.Start + token.Length);
                    properties = owner.RunPropertiesFor(style, token.Foreground, token.Style);
                }
            }

            // An arbitrary grammar boundary must not split a combining mark or emoji sequence.
            if (end < text.Length && (text[end] >= 128 || text[end - 1] >= 128))
            {
                var boundary = index;
                while (boundary < end)
                    boundary += StringInfo.GetNextTextElementLength(text.AsSpan(boundary));
                end = boundary;
            }
            return new TextCharacters(text.AsMemory(index, end - index), properties);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_context is null)
        {
            return default;
        }
        var maxWidth = double.IsFinite(availableSize.Width) ? Math.Max(availableSize.Width, 1) : double.PositiveInfinity;
        if (_wrap && !_layoutWidth.Equals(maxWidth))
        {
            DisposeLineLayouts();
            _allStripsDirty = true;
        }
        _layoutWidth = maxWidth;
        double height = 0, width = 0;
        _lineTops.Clear();
        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            _lineTops.Add(height);
            if (line.Text.Length == 0)
            {
                line.Width = 0;
                height += LineHeight;
                continue;
            }
            var layout = LayoutFor(line, maxWidth);
            height += _wrap ? layout.Height : LineHeight;
            if (line.Width > width)
            {
                width = line.Width;
            }
        }
        var oldOffsetX = _offsetX;
        if (_hover && !_contentWidth.Equals(width))
        {
            InvalidateVisual();
        }
        _contentWidth = width;
        var viewportWidth = double.IsFinite(availableSize.Width) ? availableSize.Width : width;
        ClampOffset(viewportWidth);
        if (!oldOffsetX.Equals(_offsetX))
        {
            _allStripsDirty = true;
        }
        SyncStrips(Math.Max(1, viewportWidth), height);
        var scrollbar = _reservedScrollbarSpace;
        if (scrollbar == 0 && !_wrap && double.IsFinite(availableSize.Width) && width > availableSize.Width)
            scrollbar = ScrollbarHeight;
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : width, height + scrollbar);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = Math.Max(1, finalSize.Width);
        for (var s = 0; s < _strips.Count; s++)
        {
            var b = _strips[s].Bounds;
            if (!b.Width.Equals(width))
            {
                _strips[s].SetBounds(new Rect(b.X, b.Y, width, b.Height));
            }
        }
        if (_tailVisual is { } tail && tail.Bounds.Width != width)
            tail.SetBounds(new Rect(tail.Bounds.X, tail.Bounds.Y, width, tail.Bounds.Height));
        return finalSize;
    }

    private void SyncStrips(double width, double totalHeight)
    {
        if (_lines.Count <= LinesPerStrip)
        {
            if (_strips.Count != 0)
                ResizeStripVisuals(0);
            // Keep the cold single line inline. Once another line arrives, let the
            // tail change independently while the owner retains the completed prefix.
            var splitTail = _hasStreamed && _lines.Count > 1 && _windowStart + _lines.Count == _state!.LineCount;
            var inlineLineCount = splitTail ? _lines.Count - 1 : _lines.Count;
            if (_inlineLineCount != inlineLineCount || _allStripsDirty || _dirtyLineStart < inlineLineCount)
                InvalidateVisual();
            _inlineLineCount = inlineLineCount;
            if (splitTail)
                SyncTailVisual(width, totalHeight, inlineLineCount);
            else if (_tailVisual is not null)
                RemoveTailVisual();
        }
        else
        {
            if (_tailVisual is not null)
                RemoveTailVisual();
            if (_inlineLineCount != 0)
            {
                _inlineLineCount = 0;
                InvalidateVisual();
            }
            SyncStripVisuals(width, totalHeight);
        }
        ResetDirtyRange();
    }

    private void SyncTailVisual(double width, double totalHeight, int line)
    {
        if (_tailVisual is null)
        {
            _tailVisual = new CodeStripVisual(this);
            VisualChildren.Add(_tailVisual);
        }
        var top = _lineTops[line];
        var linesChanged = _tailVisual.SetLines(line, line + 1);
        var boundsChanged = _tailVisual.SetBounds(new Rect(0, top, width, Math.Max(1, totalHeight - top)));
        if (linesChanged || boundsChanged || _allStripsDirty || _dirtyLineEnd >= line)
            _tailVisual.InvalidateVisual();
    }

    private void RemoveTailVisual()
    {
        VisualChildren.Remove(_tailVisual!);
        _tailVisual = null;
    }

    private void ResizeStripVisuals(int stripCount)
    {
        while (_strips.Count > stripCount)
        {
            VisualChildren.Remove(_strips[^1]);
            _strips.RemoveAt(_strips.Count - 1);
        }
        while (_strips.Count < stripCount)
        {
            var strip = new CodeStripVisual(this);
            _strips.Add(strip);
            VisualChildren.Add(strip);
        }
    }

    private void SyncStripVisuals(double width, double totalHeight)
    {
        var stripCount = (_lines.Count + LinesPerStrip - 1) / LinesPerStrip;
        ResizeStripVisuals(stripCount);
        for (var s = 0; s < stripCount; s++)
        {
            var firstLine = s * LinesPerStrip;
            var lastLine = Math.Min(_lines.Count, firstLine + LinesPerStrip) - 1;
            var stripTop = _lineTops[firstLine];
            var stripBottom = lastLine + 1 < _lines.Count ? _lineTops[lastLine + 1] : totalHeight;
            var strip = _strips[s];
            var linesChanged = strip.SetLines(firstLine, lastLine + 1);
            var boundsChanged = strip.SetBounds(new Rect(0, stripTop, width, Math.Max(1, stripBottom - stripTop)));
            if (_allStripsDirty || linesChanged || boundsChanged || (lastLine >= _dirtyLineStart && firstLine <= _dirtyLineEnd))
            {
                strip.InvalidateVisual();
            }
        }
    }

    private void RenderLines(DrawingContext context, int first, int end, bool localOrigin)
    {
        end = Math.Min(end, Math.Min(_lines.Count, _lineTops.Count));
        if (first >= end || _context is null)
        {
            return;
        }
        var stripTop = localOrigin ? _lineTops[first] : 0;
        for (var i = first; i < end; i++)
        {
            var line = _lines[i];
            if (line.Text.Length == 0)
            {
                continue;
            }
            var layout = LayoutFor(line, _layoutWidth);
            layout.Draw(context, new Point(-_offsetX, _lineTops[i] - stripTop));
        }
    }

    private void ClampOffset(double viewport) => _offsetX = Math.Clamp(_offsetX, 0, Math.Max(0, _contentWidth - viewport));

    public override void Render(DrawingContext context)
    {
        if (_context is null)
        {
            return;
        }
        // Hit area, search highlights and selection are drawn below the text.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

        if (!_context.Selection.IsEmpty || _context.Selection.HasHighlights)
        {
            RenderHighlights(context, _context);
        }

        // 横向滚动条（悬停时显示）
        if (!_wrap && _contentWidth > Bounds.Width + 0.5 && _hover)
        {
            var ratio = Bounds.Width / _contentWidth;
            var thumbWidth = Math.Max(24, Bounds.Width * ratio);
            var x = (_offsetX / (_contentWidth - Bounds.Width)) * (Bounds.Width - thumbWidth);
            context.FillRectangle(_context.Style.HoverBackground, new Rect(x, Bounds.Height - 6, thumbWidth, 4), 2);
        }
        if (_inlineLineCount > 0)
            RenderLines(context, 0, _inlineLineCount, localOrigin: false);
    }

    private void RenderHighlights(DrawingContext context, RenderContext renderContext)
    {
        var style = renderContext.Style;
        var selected = renderContext.Selection.GetSegmentRange(Key, _text.Length);
        List<(int Start, int End, bool Current)>? highlights = null;
        if (renderContext.Selection.HasHighlights)
        {
            foreach (var h in renderContext.Selection.GetHighlights(Key, _text.Length))
            {
                (highlights ??= []).Add(h);
            }
        }
        if (highlights is not null || selected is not null)
        {
            using (context.PushTransform(Matrix.CreateTranslation(-_offsetX, 0)))
            {
                for (var i = 0; i < _lines.Count; i++)
                {
                    var line = _lines[i];
                    var y = i < _lineTops.Count ? _lineTops[i] : i * LineHeight;
                    var lineStart = _lineStarts[i];
                    var lineEnd = lineStart + line.Text.Length;
                    TextLayout? layout = null;
                    if (highlights is not null)
                    {
                        foreach (var (hs, he, current) in highlights)
                        {
                            var s = Math.Max(hs, lineStart) - lineStart;
                            var e = Math.Min(he, lineEnd) - lineStart;
                            if (e > s)
                            {
                                layout ??= LayoutFor(line, _layoutWidth);
                                var brush = current ? style.SearchCurrentBackground : style.SearchHighlightBackground;
                                foreach (var rect in layout.HitTestTextRange(s, e - s))
                                {
                                    context.FillRectangle(brush, rect.Translate(new Vector(0, y)), 2);
                                }
                            }
                        }
                    }
                    if (selected is { } range)
                    {
                        var s = Math.Max(range.Start, lineStart);
                        var e = Math.Min(range.End, lineEnd + 1);
                        if (e > s)
                        {
                            layout ??= LayoutFor(line, _layoutWidth);
                            var hasNewline = range.End > lineEnd && lineEnd < _text.Length;
                            var textEnd = Math.Min(e, lineEnd);
                            foreach (var rect in layout.HitTestTextRange(s - lineStart, Math.Max(0, textEnd - s)))
                            {
                                context.FillRectangle(style.SelectionBackground, rect.Translate(new Vector(0, y)));
                            }
                            if (hasNewline)
                            {
                                var endRect = layout.HitTestTextPosition(line.Text.Length);
                                context.FillRectangle(style.SelectionBackground, new Rect(endRect.X, y + endRect.Y, style.CodeFontSize * 0.5, endRect.Height));
                            }
                        }
                    }
                }
            }
        }

    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hover = true;
        Cursor = IbeamCursor;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (_wrap || _contentWidth <= Bounds.Width)
        {
            base.OnPointerWheelChanged(e);
            return;
        }
        // 横向滚动：触控板横向手势，或按住 Shift 的纵向滚轮
        var dx = e.Delta.X != 0 ? -e.Delta.X : e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -e.Delta.Y : 0;
        if (dx == 0)
        {
            base.OnPointerWheelChanged(e);
            return;
        }
        var oldOffsetX = _offsetX;
        _offsetX += dx * 40;
        ClampOffset(Bounds.Width);
        if (!oldOffsetX.Equals(_offsetX))
        {
            InvalidateVisual();
            InvalidateAllStrips();
        }
        e.Handled = true;
    }

    public int HitTest(Point point)
    {
        if (_lines.Count == 0)
        {
            return 0;
        }
        var p = new Point(point.X + _offsetX, point.Y);
        double y = 0;
        for (var i = 0; i < _lines.Count; i++)
        {
            var layout = LayoutFor(_lines[i], _layoutWidth);
            var height = _wrap ? layout.Height : LineHeight;
            if (p.Y < y + height || i == _lines.Count - 1)
            {
                var hit = layout.HitTestPoint(new Point(p.X, Math.Max(0, p.Y - y)));
                return _lineStarts[i] + Math.Clamp(hit.TextPosition, 0, _lines[i].Text.Length);
            }
            y += height;
        }
        return 0;
    }

    public Rect GetCharacterBounds(int offset)
    {
        double y = 0;
        for (var i = 0; i < _lines.Count; i++)
        {
            var layout = LayoutFor(_lines[i], _layoutWidth);
            var height = _wrap ? layout.Height : LineHeight;
            var lineEnd = _lineStarts[i] + _lines[i].Text.Length;
            if (offset <= lineEnd || i == _lines.Count - 1)
            {
                var rect = layout.HitTestTextPosition(Math.Clamp(offset - _lineStarts[i], 0, _lines[i].Text.Length));
                return rect.Translate(new Vector(-_offsetX, y));
            }
            y += height;
        }
        return default;
    }

    private void DisposeLineLayouts()
    {
        for (var i = 0; i < _lines.Count; i++)
        {
            _lines[i].Layout?.Dispose();
            _lines[i].Layout = null;
        }
    }

    private void ClearLines()
    {
        DisposeLineLayouts();
        _lines.Clear();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_context is not null)
        {
            _context.Selection.Changed += InvalidateVisual;
        }
        if (_state is not null)
        {
            _state.HighlightUpdated += OnHighlightUpdated;
            // 回收期间可能已有新的着色结果
            ApplyWindow();
        }
        if (_lines.Count > 0 && double.IsNaN(_layoutWidth))
        {
            InvalidateMeasure();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_context is not null)
        {
            _context.Selection.Changed -= InvalidateVisual;
        }
        if (_state is not null)
        {
            _state.HighlightUpdated -= OnHighlightUpdated;
        }
        DisposeLineLayouts();
        _layoutWidth = double.NaN;
        _allStripsDirty = true;
        InvalidateMeasure();
    }
}

/// <summary>按 ARGB 复用画刷。</summary>
internal static class BrushCache
{
    private static readonly Dictionary<uint, IBrush> Brushes = [];

    public static IBrush Get(uint argb)
    {
        lock (Brushes)
        {
            if (!Brushes.TryGetValue(argb, out var brush))
            {
                brush = new ImmutableSolidColorBrush(Color.FromUInt32(argb));
                Brushes[argb] = brush;
            }
            return brush;
        }
    }
}
