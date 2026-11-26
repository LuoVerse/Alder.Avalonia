using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Alder.Editor.Lsp;

namespace Alder.Avalonia.Controls;

/// <summary>Formatting and editing operations supported by the rich text editor.</summary>
#pragma warning disable CS1591
public enum RichTextCommand { Undo, Redo, Bold, Italic, Underline, Strike, Code, Heading1, Heading2, Heading3, Quote, CodeBlock, BulletList, OrderedList, Link, ClearFormatting, ClearContent }
public enum RichTextEditorVariant { Primary, Secondary }

/// <summary>An application-defined entry for the slash command menu.</summary>
public sealed record RichTextSuggestion(string Title, Action<AlderRichTextEditor> Execute)
{
    public string? Description { get; init; }
    public string? IconResourceKey { get; init; }
    public string? Keywords { get; init; }
}

/// <summary>A native rich text editor with a two-way JSON document value and composable commands.</summary>
public sealed partial class AlderRichTextEditor : TemplatedControl
{
    [GeneratedStyledProperty(DefaultValue = RichDocument.Empty, DefaultBindingMode = BindingMode.TwoWay)]
    public partial string Value { get; set; }
    [GeneratedStyledProperty] public partial string? Placeholder { get; set; }
    [GeneratedStyledProperty] public partial string? EmptyParagraphPlaceholder { get; set; }
    [GeneratedStyledProperty] public partial string? Footer { get; set; }
    [GeneratedStyledProperty] public partial int MaxLength { get; set; }
    [GeneratedStyledProperty] public partial bool IsReadOnly { get; set; }
    [GeneratedStyledProperty] public partial RichTextEditorVariant Variant { get; set; }
    [GeneratedStyledProperty(DefaultValue = true)] public partial bool ShowWordCount { get; set; }
    [GeneratedStyledProperty(DefaultValue = true)] public partial bool ShowCharacterCount { get; set; }
    [GeneratedStyledProperty(DefaultValue = true)] public partial bool ShowBubbleMenu { get; set; }
    [GeneratedStyledProperty] public partial bool ShowSlashMenu { get; set; }
    [GeneratedStyledProperty] public partial bool ShowFloatingMenu { get; set; }
    [GeneratedStyledProperty] public partial bool ToolbarAtBottom { get; set; }
    [GeneratedStyledProperty] public partial IReadOnlyList<RichTextCommand>? ToolbarCommands { get; set; }
    [GeneratedStyledProperty] public partial object? AdditionalToolbarContent { get; set; }
    [GeneratedStyledProperty] public partial IReadOnlyList<RichTextSuggestion>? Suggestions { get; set; }

    [GeneratedStyledProperty] public partial IReadOnlyList<RichTextSuggestion>? FloatingCommands { get; set; }

    public static readonly DirectProperty<AlderRichTextEditor, string> PlainTextProperty =
        AvaloniaProperty.RegisterDirect<AlderRichTextEditor, string>(nameof(PlainText), c => c.PlainText);
    public static readonly DirectProperty<AlderRichTextEditor, int> CharacterCountProperty =
        AvaloniaProperty.RegisterDirect<AlderRichTextEditor, int>(nameof(CharacterCount), c => c.CharacterCount);
    public static readonly DirectProperty<AlderRichTextEditor, int> WordCountProperty =
        AvaloniaProperty.RegisterDirect<AlderRichTextEditor, int>(nameof(WordCount), c => c.WordCount);

    public string PlainText { get => field; private set => SetAndRaise(PlainTextProperty, ref field, value); } = "";
    public int CharacterCount { get => field; private set => SetAndRaise(CharacterCountProperty, ref field, value); }
    public int WordCount { get => field; private set => SetAndRaise(WordCountProperty, ref field, value); }
    private readonly RichDocument document = new();
    private TextBox? input;
    private RichTextPresenter? presenter;
    private RichTextGutter? gutter;
    private RichStyle? typing;
    private bool publishing, syncing, backspace;
    private bool Editable => IsEffectivelyEnabled && !IsReadOnly;
    internal RichDocument Document => document;


    /// <summary>Synchronizes this editor's plain-text projection with the server. Dispose to close the document.</summary>
    public Task<EditorLanguageSession> ConnectLanguageServerAsync(Stream input, Stream output, Uri documentUri,
        string languageId = "plaintext", CancellationToken cancellationToken = default) =>
        EditorLanguageSession.ConnectAsync(document.Buffer, input, output, documentUri, languageId, cancellationToken);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        CloseLink(immediately: true);
        ClosePopup();
        if (presenter is not null) presenter.LayoutUpdated -= PresenterLayoutChanged;
        if (input is not null)
        {
            input.PropertyChanged -= InputChanged;
            input.TemplateApplied -= InputTemplateApplied;
            input.RemoveHandler(KeyDownEvent, EditorKeyDown);
        }
        base.OnApplyTemplate(e);
        if (popup is not null) popup.Closed -= PopupClosed;
        popup = e.NameScope.Find<Popup>("PART_Popup");
        if (popup is not null) popup.Closed += PopupClosed;
        input = e.NameScope.Find<TextBox>("PART_Input");
        toolbar = e.NameScope.Find<WrapPanel>("PART_Commands");
        counter = e.NameScope.Find<TextBlock>("PART_Count");
        placeholder = e.NameScope.Find<TextBlock>("PART_Placeholder");
        presenter = null;
        gutter = null;
        if (input is null) return;
        input.Text = PlainText;
        input.PropertyChanged += InputChanged;
        input.TemplateApplied += InputTemplateApplied;
        input.AddHandler(KeyDownEvent, EditorKeyDown, RoutingStrategies.Tunnel);
        BuildToolbar();
    }

    private void InputTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        if (presenter is not null) presenter.LayoutUpdated -= PresenterLayoutChanged;
        presenter = e.NameScope.Find<RichTextPresenter>("PART_TextPresenter");
        gutter = e.NameScope.Find<RichTextGutter>("PART_Gutter");
        if (presenter is not null) { presenter.Owner = this; presenter.LayoutUpdated += PresenterLayoutChanged; }
        if (gutter is not null) gutter.Presenter = presenter;
        Redraw();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty && !publishing)
        {
            CloseLink(immediately: true);
            try { document.Load(string.IsNullOrWhiteSpace(Value) ? RichDocument.Empty : Value); }
            catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException or InvalidOperationException)
            {
                publishing = true;
                try { SetCurrentValue(ValueProperty, change.GetOldValue<string>() ?? RichDocument.Empty); }
                finally { publishing = false; }
                throw;
            }
            typing = null;
            Synchronize(false);
        }
        if (input is null) return;
        if (change.Property == IsReadOnlyProperty || change.Property == IsEffectivelyEnabledProperty)
            CloseLink(immediately: true);
        if (change.Property == ToolbarCommandsProperty) BuildToolbar();
        if (change.Property == SuggestionsProperty || change.Property == FloatingCommandsProperty) { ClosePopup(); QueueContext(); }
        if (change.Property == IsReadOnlyProperty || change.Property == IsEffectivelyEnabledProperty ||
            change.Property == ShowBubbleMenuProperty || change.Property == ShowSlashMenuProperty || change.Property == ShowFloatingMenuProperty)
            ClosePopup();
        if (change.Property == MaxLengthProperty || change.Property == ShowWordCountProperty ||
            change.Property == IsReadOnlyProperty || change.Property == IsEffectivelyEnabledProperty) Refresh();
        if (change.Property == EmptyParagraphPlaceholderProperty || change.Property == ForegroundProperty || change.Property == FontSizeProperty ||
            change.Property == FontFamilyProperty || change.Property == FontWeightProperty || change.Property == FontStyleProperty ||
            change.Property == FlowDirectionProperty) Redraw();
    }

    private static int CountCharacters(string value)
    {
        int count = 0;
        foreach (var line in value.AsSpan().EnumerateLines())
        {
            var text = line;
            while (!text.IsEmpty) { text = text[StringInfo.GetNextTextElementLength(text)..]; count++; }
        }
        return count;
    }

    private void InputChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (syncing || input is null) return;
        if (change.Property == TextBox.TextProperty)
        {
            var old = PlainText;
            var text = (input.Text ?? "").Replace("\r\n", "\n").Replace('\r','\n');
            if (old == text) return;
            var limit = MaxLength;
            if (!Editable || (limit > 0 && CountCharacters(text) > Math.Max(CharacterCount, limit)))
            {
                Synchronize(false);
                // TextBox finishes its IME/caret update after the property notification.
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { presenter?.SetCurrentValue(RichTextPresenter.TextProperty, input?.Text); Redraw(); });
                return;
            }
            var (start, selected) = Selection;
            if (backspace && selected == 0) start = Math.Max(0, start - Math.Max(0, old.Length - text.Length));
            int prefix = Math.Min(start, old.AsSpan().CommonPrefixLength(text));
            int suffix = Math.Min(old.Length, text.Length) - prefix;
            // Native input normally preserves the suffix. Compare it in bulk, including repeated characters.
            if (!old.AsSpan(old.Length - suffix).SequenceEqual(text.AsSpan(text.Length - suffix)))
            {
                suffix = 0;
                while (suffix < old.Length - prefix && suffix < text.Length - prefix &&
                       old[^(suffix + 1)] == text[^(suffix + 1)]) suffix++;
            }
            prefix += old.AsSpan(prefix, old.Length - prefix - suffix)
                .CommonPrefixLength(text.AsSpan(prefix, text.Length - prefix - suffix));
            var added = text.Substring(prefix, text.Length - prefix - suffix);
            document.Replace(prefix, old.Length - prefix - suffix, added, typing ?? document.InputStyle(prefix));
            Synchronize(true, text);
        }
        else if (change.Property == TextBox.CaretIndexProperty || change.Property == TextBox.SelectionStartProperty || change.Property == TextBox.SelectionEndProperty)
        {
            typing = null;
            Refresh();
            QueueContext();
        }
    }

    private void Synchronize(bool publish, string? text = null)
    {
        syncing = true;
        try
        {
            PlainText = text ?? document.Text;
            if (input is not null && input.Text != PlainText) input.SetCurrentValue(TextBox.TextProperty, PlainText);
            CharacterCount = document.Characters;
            WordCount = document.Words;
            if (publish)
            {
                publishing = true;
                try { SetCurrentValue(ValueProperty, document.Save()); }
                finally { publishing = false; }
            }
            Redraw(document.FirstChangedOffset);
            Refresh();
            QueueContext();
        }
        finally { syncing = false; }
    }

    private void PresenterLayoutChanged(object? sender, EventArgs e) => gutter?.LayoutChanged();

    private void Redraw(int firstChangedOffset = 0)
    {
        presenter?.Refresh(firstChangedOffset);
        if (gutter is null) return;
        gutter.IsVisible = document.MarkerCount != 0 || document.CodeBlocks != 0 || !string.IsNullOrEmpty(EmptyParagraphPlaceholder);
        if (presenter is not null) presenter.Margin = new Thickness(gutter.UpdateMarkers(this), 0, 0, 0);
        gutter.InvalidateVisual();
    }

    public void Select(int start, int length)
    {
        if (input is null) return;
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, document.Length - start);
        input.CaretIndex = start + length;
        input.SelectionStart = start;
        input.SelectionEnd = start + length;
    }
    public void FocusContent() => input?.Focus();
    public void InsertText(string text)
    {
        if (input is null || !Editable || string.IsNullOrEmpty(text)) return;
        input.SelectedText = text.ReplaceLineEndings("\n");
        FocusContent();
    }

    /// <summary>Replaces the selection with the blocks of a JSON document as a single undoable edit.</summary>
    public void InsertBlocks(string value)
    {
        if (input is null || !Editable) return;
        var fragment = new RichDocument();
        fragment.Load(value);
        var (start, length) = Selection;
        if (MaxLength > 0 && CharacterCount - CountCharacters(PlainText.Substring(start, length)) + fragment.Characters > MaxLength) return;
        var caret = document.InsertBlocks(start, length, fragment);
        Synchronize(true);
        Select(caret, 0);
        FocusContent();
    }

    private (int Start, int Length) Selection => input is null ? (0, 0) :
        (Math.Min(input.SelectionStart, input.SelectionEnd), Math.Abs(input.SelectionEnd - input.SelectionStart));

    public bool IsActive(RichTextCommand command)
    {
        var (start, length) = Selection;
        if (MarkFor(command) is { } mark)
            return length == 0 ? (typing ?? document.InputStyle(start)).Marks.HasFlag(mark) :
                document.Runs(start, length).Any() && document.Runs(start, length).All(run => run.Style.Marks.HasFlag(mark));
        if (command == RichTextCommand.Link) return document.InputStyle(start).Link is not null;
        return BlockFor(command) == document.Line(document.Row(start)).Kind;
    }

    public void Execute(RichTextCommand command)
    {
        if (!Editable || input is null) return;
        var (start, length) = Selection;
        if (command == RichTextCommand.Link) { OpenLink(); return; }
        CloseLink(immediately: true);
        ClosePopup();
        int? caret = null;
        switch (command)
        {
            case RichTextCommand.Undo:
                if (!document.CanUndo) return;
                caret = document.Undo();
                break;
            case RichTextCommand.Redo:
                if (!document.CanRedo) return;
                caret = document.Redo();
                break;
            case RichTextCommand.ClearContent:
                document.Clear();
                typing = null;
                caret = 0;
                break;
            case RichTextCommand.ClearFormatting:
                document.Format(start, length, _ => default, RichBlock.Paragraph);
                typing = default(RichStyle);
                break;
            default:
                if (MarkFor(command) is { } mark)
                {
                    bool remove = IsActive(command);
                    RichStyle Update(RichStyle s) => s with { Marks = remove ? s.Marks & ~mark : s.Marks | mark };
                    if (length == 0) { typing = Update(typing ?? document.InputStyle(start)); Refresh(); FocusContent(); return; }
                    document.Format(start, length, Update);
                }
                else document.Format(start, length, block: IsActive(command) ? RichBlock.Paragraph : BlockFor(command));
                break;
        }
        Synchronize(true);
        if (caret.HasValue) Select(caret.Value, 0);
        FocusContent();
    }

    public void SetLink(string? address)
    {
        if (!Editable || input is null) return;
        var link = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        var (start, length) = Selection;
        var row = document.Row(start);
        int end = start;
        if (length == 0 && document.InputStyle(start).Link is { } existing)
        {
            while (start > document.Start(row) && document.At(start - 1).Link == existing) start--;
            while (end < document.Start(row) + document.Line(row).Length && document.At(end).Link == existing) end++;
            length = end - start;
        }
        if (length > 0) document.Format(start, length, s => s with { Link = link, LinkAttributes = null });
        else if (link is not null)
        {
            if (MaxLength > 0 && CountCharacters(PlainText.Insert(start, link)) > MaxLength) return;
            document.Replace(start, 0, link, new(Link: link));
        }
        Synchronize(true);
        if (length == 0 && link is not null) Select(start + link.Length, 0);
        if (linkPopover is null) ClosePopup();
        else CloseLink();
        FocusContent();
    }

    private static RichMark? MarkFor(RichTextCommand command) => command switch
    {
        RichTextCommand.Bold => RichMark.Bold, RichTextCommand.Italic => RichMark.Italic,
        RichTextCommand.Underline => RichMark.Underline, RichTextCommand.Strike => RichMark.Strike,
        RichTextCommand.Code => RichMark.Code, _ => null
    };

    private static RichBlock? BlockFor(RichTextCommand command) => command switch
    {
        RichTextCommand.Heading1 => RichBlock.Heading1, RichTextCommand.Heading2 => RichBlock.Heading2,
        RichTextCommand.Heading3 => RichBlock.Heading3, RichTextCommand.BulletList => RichBlock.BulletList,
        RichTextCommand.OrderedList => RichBlock.OrderedList, RichTextCommand.Quote => RichBlock.Quote,
        RichTextCommand.CodeBlock => RichBlock.CodeBlock, _ => null
    };
}
#pragma warning restore CS1591
