using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Controls.Presenters;
using Avalonia.Platform.Storage;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Alder.Avalonia.Controls;

/// <summary>A multiline chat composer with draft, attachment and toolbar slots and send/stop commands.</summary>
public sealed partial class AlderPromptInput : TemplatedControl
{
    /// <summary>Layout and surface used by the composer.</summary>
    [GeneratedStyledProperty] public partial AiPromptVariant Variant { get; set; }

    /// <summary>Optional mention and slash-command candidates, including their @ or / prefix.</summary>
    [GeneratedStyledProperty] public partial IReadOnlyList<string>? Tokens { get; set; }

    /// <summary>Optional presentation for each string in the token suggestion menu.</summary>
    [GeneratedStyledProperty] public partial IDataTemplate? TokenItemTemplate { get; set; }

    /// <summary>Current draft.</summary>
    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial string? Text { get; set; }

    /// <summary>Hint shown when the draft is empty.</summary>
    [GeneratedStyledProperty]
    public partial string? Placeholder { get; set; }

    /// <summary>Shows the stop action for an active reply.</summary>
    [GeneratedStyledProperty]
    public partial bool IsRunning { get; set; }

    /// <summary>Shows a pending indicator while the submitted draft is being accepted.</summary>
    [GeneratedStyledProperty] public partial bool IsSubmitting { get; set; }

    /// <summary>Prevents editing while the composer is submitting or generating.</summary>
    [GeneratedStyledProperty(DefaultValue = true)] public partial bool LockInputOnRun { get; set; }

    /// <summary>Caps the height of the editable area before it scrolls.</summary>
    [GeneratedStyledProperty(DefaultValue = 240d)] public partial double MaxEditorHeight { get; set; }

    /// <summary>Optional attachments, queued prompts or review context.</summary>
    [GeneratedStyledProperty]
    public partial object? Attachments { get; set; }

    /// <summary>Optional leading toolbar next to the attachment action in a multiline composer.</summary>
    [GeneratedStyledProperty] public partial object? LeadingTools { get; set; }

    /// <summary>Content alongside the submit button.</summary>
    [GeneratedStyledProperty]
    public partial object? Tools { get; set; }

    /// <summary>Opens an application-owned attachment picker.</summary>
    [GeneratedStyledProperty]
    public partial ICommand? AttachCommand { get; set; }

    /// <summary>Receives an IReadOnlyList&lt;IStorageFile&gt; when files are dropped on the composer.</summary>
    [GeneratedStyledProperty]
    public partial ICommand? FileDropCommand { get; set; }

    /// <summary>Receives the draft when the user submits it.</summary>
    [GeneratedStyledProperty]
    public partial ICommand? SendCommand { get; set; }

    /// <summary>Stops the active response.</summary>
    [GeneratedStyledProperty]
    public partial ICommand? StopCommand { get; set; }

    /// <summary>Optional action, such as voice input, in place of send when the draft is empty.</summary>
    [GeneratedStyledProperty] public partial object? EmptyAction { get; set; }

    /// <summary>Allows attachment-only prompts.</summary>
    [GeneratedStyledProperty]
    public partial bool AllowEmpty { get; set; }

    private TextBox? editor;
    private ListBox? choices;
    private TextPresenter? presenter;
    private AlderMentionHighlights? highlights;
    private ContentPresenter? attachmentSlot;
    private ContentPresenter? trailingTools;
    private StackPanel? leadingToolbar;
    private Popup? tokenPopup;
    private int tokenStart;
    private int dismissedTokenStart = -1;
    private char dismissedTrigger;

    /// <summary>Initializes the prompt composer.</summary>
    public AlderPromptInput()
    {
        AddHandler(DragDrop.DragEnterEvent, OnFileDragOver);
        AddHandler(DragDrop.DragOverEvent, OnFileDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnFileDragLeave);
        AddHandler(DragDrop.DropEvent, OnFileDrop);
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (editor is not null)
        {
            editor.RemoveHandler(KeyDownEvent, OnEditorKey);
            editor.PropertyChanged -= OnEditorChanged;
            editor.RemoveHandler(ScrollViewer.ScrollChangedEvent, OnEditorScroll);
        }
        if (attachmentSlot is not null) attachmentSlot.SizeChanged -= ComposerSizeChanged;
        if (trailingTools is not null) trailingTools.SizeChanged -= ComposerSizeChanged;
        if (leadingToolbar is not null) leadingToolbar.SizeChanged -= ComposerSizeChanged;
        if (tokenPopup is not null) tokenPopup.IsOpen = false;
        choices?.RemoveHandler(PointerPressedEvent, OnChoicePressed);
        base.OnApplyTemplate(e);
        editor = e.NameScope.Find<TextBox>("PART_Editor");
        choices = e.NameScope.Find<ListBox>("PART_Tokens");
        tokenPopup = e.NameScope.Find<Popup>("PART_TokenPopup");
        if (tokenPopup is not null) tokenPopup.PlacementTarget = editor;
        attachmentSlot = e.NameScope.Find<ContentPresenter>("AttachmentSlot");
        trailingTools = e.NameScope.Find<ContentPresenter>("TrailingTools");
        leadingToolbar = e.NameScope.Find<StackPanel>("LeadingToolbar");
        if (attachmentSlot is not null) attachmentSlot.SizeChanged += ComposerSizeChanged;
        if (trailingTools is not null) trailingTools.SizeChanged += ComposerSizeChanged;
        if (leadingToolbar is not null) leadingToolbar.SizeChanged += ComposerSizeChanged;
        presenter = null;
        highlights = e.NameScope.Find<AlderMentionHighlights>("PART_Highlights");
        if (highlights is not null) { highlights.Editor = editor; highlights.Candidates = Tokens; }
        if (editor is not null)
        {
            editor.AddHandler(KeyDownEvent, OnEditorKey, RoutingStrategies.Tunnel);
            editor.PropertyChanged += OnEditorChanged;
            editor.AddHandler(ScrollViewer.ScrollChangedEvent, OnEditorScroll);
        }
        choices?.AddHandler(PointerPressedEvent, OnChoicePressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        UpdateDraftState();
        UpdateComposerLayout();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty) PseudoClasses.Set(":compact-width", Bounds.Width < 608);
        if (change.Property == BoundsProperty || change.Property == TextProperty || change.Property == VariantProperty ||
            change.Property == AttachmentsProperty || change.Property == AttachCommandProperty || change.Property == LeadingToolsProperty)
            UpdateComposerLayout();
        if (change.Property == IsSubmittingProperty || change.Property == IsRunningProperty || change.Property == LockInputOnRunProperty)
        {
            PseudoClasses.Set(":input-locked", LockInputOnRun && (IsSubmitting || IsRunning));
            if (LockInputOnRun && (IsSubmitting || IsRunning)) CloseChoices();
        }
        if (change.Property == TextProperty || change.Property == AllowEmptyProperty) UpdateDraftState();
        if (change.Property == EmptyActionProperty) PseudoClasses.Set(":has-empty-action", EmptyAction is not null);
        if (change.Property == FileDropCommandProperty)
        {
            SetCurrentValue(DragDrop.AllowDropProperty, FileDropCommand is not null);
            PseudoClasses.Set(":file-over", false);
        }
        if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
            PseudoClasses.Set(":file-over", false);
        if (change.Property == TokensProperty)
        {
            if (highlights is not null) { highlights.Candidates = Tokens; highlights.InvalidateVisual(); }
            UpdateChoices();
        }
    }

    private void OnFileDragOver(object? sender, DragEventArgs e)
    {
        if (FileDropCommand is null || !e.DataTransfer.Formats.Contains(DataFormat.File)) return;
        bool accepts = IsEffectivelyEnabled && e.DragEffects.HasFlag(DragDropEffects.Copy);
        PseudoClasses.Set(":file-over", accepts);
        e.DragEffects = accepts ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnFileDragLeave(object? sender, DragEventArgs e) => PseudoClasses.Set(":file-over", false);

    private void OnFileDrop(object? sender, DragEventArgs e)
    {
        PseudoClasses.Set(":file-over", false);
        if (FileDropCommand is not { } command || !e.DataTransfer.Formats.Contains(DataFormat.File)) return;

        if (!IsEffectivelyEnabled || !e.DragEffects.HasFlag(DragDropEffects.Copy) ||
            e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().ToArray() is not { Length: > 0 } files ||
            !command.CanExecute(files))
            e.DragEffects = DragDropEffects.None;
        else
        {
            e.DragEffects = DragDropEffects.Copy;
            command.Execute(files);
        }
        e.Handled = true;
    }

    private void UpdateDraftState() => PseudoClasses.Set(":empty", !AllowEmpty && string.IsNullOrWhiteSpace(Text));

    private void ComposerSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateComposerLayout();

    private void UpdateComposerLayout()
    {
        var lineLayout = Variant is AiPromptVariant.Compact or AiPromptVariant.Inline;
        if (!lineLayout)
        {
            PseudoClasses.Set(":line-layout", false);
            PseudoClasses.Set(":multiline", false);
            return;
        }

        // Measure against the single-row width even when Compact is expanded.
        // Measuring against the expanded editor would switch it back and forth near a wrap.
        var reserved = 64d + (leadingToolbar?.Bounds.Width ?? 0d) + (trailingTools?.Bounds.Width ?? 0d);
        var text = Text;
        bool wraps = false;
        if (!string.IsNullOrEmpty(text) && Bounds.Width > 0 && editor is not null)
        {
            using var layout = new TextLayout(text, new Typeface(editor.FontFamily, editor.FontStyle, editor.FontWeight),
                editor.FontSize, Brushes.Transparent, textWrapping: TextWrapping.Wrap, maxWidth: Math.Max(1, Bounds.Width - reserved));
            wraps = layout.TextLines.Count > 1;
        }
        bool hasAttachments = Attachments is not null && attachmentSlot?.Bounds.Height > 0;
        PseudoClasses.Set(":line-layout", Variant == AiPromptVariant.Inline || (!wraps && !hasAttachments));
        PseudoClasses.Set(":multiline", wraps);
    }

    private void OnEditorScroll(object? sender, ScrollChangedEventArgs e)
    {
        highlights?.InvalidateVisual();
        if (tokenPopup?.IsOpen == true) PositionChoices();
    }

    private void OnEditorChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (Tokens is { Count: > 0 } && (e.Property == TextBox.TextProperty || e.Property == BoundsProperty))
            highlights?.InvalidateVisual();
        if (e.Property == TextBox.CaretIndexProperty || e.Property == TextBox.TextProperty ||
            e.Property == IsKeyboardFocusWithinProperty || e.Property == IsFocusedProperty) UpdateChoices();
    }

    private void UpdateChoices()
    {
        if (choices is null || editor is null) return;
        var text = editor.Text ?? "";
        int end = Math.Clamp(editor.CaretIndex, 0, text.Length);
        tokenStart = end;
        if (editor.IsFocused && editor.IsEffectivelyEnabled && Tokens is { Count: > 0 } tokens)
        {
            while (tokenStart > 0 && !char.IsWhiteSpace(text[tokenStart - 1])) tokenStart--;
            if (tokenStart < end && text[tokenStart] is '@' or '/')
            {
                if (dismissedTokenStart == tokenStart && dismissedTrigger == text[tokenStart])
                {
                    CloseChoices();
                    return;
                }
                dismissedTokenStart = -1;
                var prefix = text[tokenStart..end];
                var matches = tokens.Where(t => t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    !t.Equals(prefix, StringComparison.Ordinal)).Take(6).ToArray();
                if (matches.Length > 0)
                {
                    choices.ItemsSource = matches;
                    choices.SelectedIndex = 0;
                    choices.IsVisible = true;
                    PositionChoices();
                    if (tokenPopup is not null) tokenPopup.IsOpen = true;
                    return;
                }
            }
        }
        dismissedTokenStart = -1;
        CloseChoices();
    }

    private void CloseChoices()
    {
        if (choices is not null) choices.IsVisible = false;
        if (tokenPopup is not null) tokenPopup.IsOpen = false;
    }

    private void PositionChoices()
    {
        if (editor is null || tokenPopup is null) return;
        presenter ??= editor.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        if (presenter is null) return;
        var anchor = presenter.TextLayout.HitTestTextPosition(tokenStart);
        if (presenter.TranslatePoint(anchor.TopLeft, editor) is { } point)
            tokenPopup.PlacementRect = new Rect(point, anchor.Size);
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CloseChoices();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnChoicePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Visual visual || choices is null || !e.GetCurrentPoint(choices).Properties.IsLeftButtonPressed) return;
        var item = visual as ListBoxItem ?? visual.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
        if (item?.Content is string value)
        {
            e.Handled = true;
            CommitToken(value);
        }
    }

    private void CommitToken(string value)
    {
        if (editor is null) return;
        var text = editor.Text ?? "";
        int end = Math.Clamp(editor.CaretIndex, tokenStart, text.Length);
        while (end < text.Length && !char.IsWhiteSpace(text[end])) end++;
        int start = tokenStart;
        editor.SetCurrentValue(TextBox.TextProperty, text[..start] + value + " " + text[end..]);
        editor.CaretIndex = start + value.Length + 1;
        editor.Focus();
        CloseChoices();
    }

    private void OnEditorKey(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || editor is null) return;
        if (e.Key is not (Key.Enter or Key.Tab or Key.Escape or Key.Up or Key.Down)) return;
        presenter ??= editor.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        if (presenter?.PreeditText is { Length: > 0 }) return;
        if (choices is { IsVisible: true })
        {
            if (e.Key == Key.Escape)
            {
                dismissedTokenStart = tokenStart;
                dismissedTrigger = (editor.Text ?? "")[tokenStart];
                CloseChoices();
            }
            else if (e.Key is Key.Up or Key.Down)
                choices.SelectedIndex = Math.Clamp(choices.SelectedIndex + (e.Key == Key.Down ? 1 : -1), 0, choices.ItemCount - 1);
            else if (choices.SelectedItem is string token) CommitToken(token);
            e.Handled = true;
            return;
        }
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        if (!IsRunning && !IsSubmitting && (AllowEmpty || !string.IsNullOrWhiteSpace(Text)) && SendCommand?.CanExecute(Text) == true)
            SendCommand.Execute(Text);
    }
}
