using Alder.NativeMarkdown.Core.Model;
using Alder.NativeMarkdown.Core.Streaming;
using Alder.NativeMarkdown.UI;
using Alder.NativeMarkdown.UI.Extensions;
using Avalonia;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Displays selectable Markdown with a stable completed prefix during streaming.</summary>
public class AlderMarkdown : MarkdownView
{
    /// <summary>Shared defaults for Markdown, code blocks and virtualized conversations.</summary>
    public static MarkdownServices ChatServices { get; } = new()
    {
        Highlighter = new AlderCodeHighlighter(),
        CodeBlocks = new CodeBlockOptions { ShowDownload = false, ShowWrapToggle = false, CollapseAfterLines = 0 }
    };

    /// <summary>Initializes the shared streaming and code rendering services.</summary>
    public AlderMarkdown() => MarkdownServices.SetServices(this, ChatServices);

    /// <summary>Starts a fresh reply. Producers may Push from any thread and Complete when finished.</summary>
    public MarkdownStream BeginStream()
    {
        bool reduced = AlderMotion.GetReduced(this);
        var stream = MarkdownServices.GetServices(this).CreateStream(smooth: !reduced, fadeDuration: reduced ? TimeSpan.Zero : null);
        stream.ShowCaret = !reduced;
        SetCurrentValue(StreamProperty, stream);
        return stream;
    }
}

/// <summary>Displays a single literal code block using the Markdown fence renderer and indexed piece tree.</summary>
public sealed partial class AlderCodeBlock : MarkdownView
{
    /// <summary>The exact source, including indentation and line endings.</summary>
    [GeneratedStyledProperty(DefaultValue = "")]
    public partial string Code { get; set; }

    /// <summary>The grammar identifier displayed in the block header.</summary>
    [GeneratedStyledProperty(DefaultValue = "text")]
    public partial string Language { get; set; }

    private long version;
    private DocumentSnapshot? snapshot;
    internal override DocumentSnapshot? ExplicitSnapshot => snapshot;

    /// <summary>Sets up a literal block without serializing source into a Markdown fence.</summary>
    public AlderCodeBlock()
    {
        MarkdownServices.SetServices(this, AlderMarkdown.ChatServices);
        UpdateBlock();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CodeProperty || change.Property == LanguageProperty) UpdateBlock();
    }

    private void UpdateBlock()
    {
        var text = Code ?? "";
        snapshot = new DocumentSnapshot(new(), 0, [new CodeBlock(Language, Language, text, true, false)],
            ++version, 0, text.Length, 0, true);
        if (this.IsAttachedToVisualTree()) ApplyLatest();
    }
}
