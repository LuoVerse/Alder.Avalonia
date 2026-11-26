using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Threading;

namespace Alder.Avalonia.Controls;

/// <summary>A single native code editor presented as groups of character slots.</summary>
public sealed class AlderInputOtp : TextBox
{
    /// <summary>A regular expression that accepts ASCII digits in individual OTP slots.</summary>
    public const string DigitsPattern = "[0-9]*";
    /// <summary>A regular expression that accepts ASCII letters in individual OTP slots.</summary>
    public const string LettersPattern = "[A-Za-z]*";
    /// <summary>A regular expression that accepts ASCII letters and digits in individual OTP slots.</summary>
    public const string AlphanumericPattern = "[A-Za-z0-9]*";

    private const int DefaultLength = 6;
    private const double SlotWidth = 38;
    private const double SlotSpacing = 8;
    private const double SeparatorWidth = 6;

    /// <summary>Identifies the <see cref="GroupSize"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> GroupSizeProperty =
        AvaloniaProperty.Register<AlderInputOtp, int>(nameof(GroupSize), 3, validate: size => size >= 0);
    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly StyledProperty<InputVariant> VariantProperty =
        AlderInput.VariantProperty.AddOwner<AlderInputOtp>();
    /// <summary>Identifies the <see cref="IsInvalid"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty =
        AlderInput.IsInvalidProperty.AddOwner<AlderInputOtp>();
    /// <summary>Identifies the <see cref="Pattern"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> PatternProperty =
        AvaloniaProperty.Register<AlderInputOtp, string?>(nameof(Pattern));
    /// <summary>Identifies the <see cref="PasteTransformer"/> Avalonia property.</summary>
    public static readonly StyledProperty<Func<string, string>?> PasteTransformerProperty =
        AvaloniaProperty.Register<AlderInputOtp, Func<string, string>?>(nameof(PasteTransformer));
    /// <summary>Identifies the <see cref="CompletedCommand"/> Avalonia property.</summary>
    public static readonly StyledProperty<ICommand?> CompletedCommandProperty =
        AvaloniaProperty.Register<AlderInputOtp, ICommand?>(nameof(CompletedCommand));

    /// <summary>The number of slots between visual group separators.</summary>
    public int GroupSize { get => GetValue(GroupSizeProperty); set => SetValue(GroupSizeProperty, value); }
    /// <summary>The visual treatment applied to the control.</summary>
    public InputVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>Whether the control presents its invalid state.</summary>
    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }
    /// <summary>A regular expression matching the whole edited value, including partial codes.</summary>
    public string? Pattern { get => GetValue(PatternProperty); set => SetValue(PatternProperty, value); }
    /// <summary>A function that preprocesses clipboard text before the editor filters and inserts it.</summary>
    public Func<string, string>? PasteTransformer { get => GetValue(PasteTransformerProperty); set => SetValue(PasteTransformerProperty, value); }
    /// <summary>Receives the code when the value first reaches MaxLength.</summary>
    public ICommand? CompletedCommand { get => GetValue(CompletedCommandProperty); set => SetValue(CompletedCommandProperty, value); }

    private readonly List<InputOtpSlot> slots = [];
    private Grid? slotsPanel;
    private Regex? patternMatcher;
    private int textVersion;

    static AlderInputOtp()
    {
        MaxLengthProperty.OverrideMetadata<AlderInputOtp>(new StyledPropertyMetadata<int>(DefaultLength,
            coerce: (_, length) => length > 0 ? length : DefaultLength));
        AffectsMeasure<AlderInputOtp>(MaxLengthProperty, GroupSizeProperty);
    }

    /// <summary>Initializes a new instance of <see cref="AlderInputOtp"/>.</summary>
    public AlderInputOtp() => PastingFromClipboard += OnPaste;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        slotsPanel = e.NameScope.Get<Grid>("PART_Slots");
        BuildSlots();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        int separators = GroupSize > 0 ? (MaxLength - 1) / GroupSize : 0;
        double width = MaxLength * SlotWidth + (MaxLength - 1) * SlotSpacing + separators * (SlotSpacing + SeparatorWidth);
        width = Math.Min(width, availableSize.Width);
        return base.MeasureOverride(availableSize.WithWidth(width)).WithWidth(width);
    }

    /// <inheritdoc />
    protected override string? CoerceText(string? value)
    {
        if (value is not null)
        {
            value = SingleLine(value);
            if (value.Length > MaxLength)
            {
                int end = MaxLength;
                if (char.IsHighSurrogate(value[end - 1])) end--;
                value = value[..end];
            }
        }
        return base.CoerceText(value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MaxLengthProperty || change.Property == GroupSizeProperty)
        {
            if (change.Property == MaxLengthProperty) CoerceValue(TextProperty);
            BuildSlots();
        }
        else if (change.Property == PatternProperty)
        {
            patternMatcher = string.IsNullOrEmpty(Pattern) ? null :
                new Regex($"\\A(?:{Pattern})\\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
        }
        else if (change.Property == TextProperty)
        {
            UpdateSlots();
            int version = ++textVersion;
            if (change.GetNewValue<string?>() is { } value && value.Length == MaxLength &&
                (change.GetOldValue<string?>()?.Length ?? 0) < MaxLength && CompletedCommand is not null)
            {
                // Native editing has to finish updating the caret before a command can clear the code.
                Dispatcher.UIThread.Post(() =>
                {
                    if (version == textVersion && Text?.Length == MaxLength && CompletedCommand is { } command &&
                        command.CanExecute(value))
                        command.Execute(value);
                });
            }
        }
        else if (change.Property == CaretIndexProperty || change.Property == SelectionStartProperty ||
                 change.Property == SelectionEndProperty || change.Property == IsFocusedProperty ||
                 change.Property == PlaceholderTextProperty || change.Property == VariantProperty ||
                 change.Property == IsInvalidProperty || change.Property == DataValidationErrors.HasErrorsProperty)
        {
            UpdateSlots();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var buttons = e.GetCurrentPoint(this).Properties;
        if (slotsPanel is null || buttons.IsRightButtonPressed)
        {
            base.OnPointerPressed(e);
            return;
        }
        if (e.Pointer.Type == PointerType.Mouse && !buttons.IsLeftButtonPressed) return;
        if (!Focus(NavigationMethod.Pointer) && !IsFocused) return;

        double x = e.GetPosition(slotsPanel).X;
        var index = slots.FindIndex(slot => x <= slot.Bounds.Right);
        SelectSlot(index < 0 ? slots.Count - 1 : index);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.None && e.Key is Key.Left or Key.Right)
        {
            int index = SelectionStart == SelectionEnd ? CaretIndex : Math.Min(SelectionStart, SelectionEnd);
            int width = Text is { } text && index < text.Length && char.IsSurrogatePair(text, index) ? 2 : 1;
            SelectSlot(index + (e.Key == Key.Left ? -1 : width));
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (e.Handled || string.IsNullOrEmpty(e.Text)) return;
        var text = Text ?? "";
        int start = Math.Min(SelectionStart, SelectionEnd);
        int end = Math.Max(SelectionStart, SelectionEnd);
        if (start == end) start = end = CaretIndex;

        var input = SingleLine(e.Text);
        int take = Math.Clamp(MaxLength - text.Length + end - start, 0, input.Length);
        if (take > 0 && char.IsHighSurrogate(input[take - 1])) take--;
        if (take == 0 || !MatchesPattern(string.Concat(text.AsSpan(0, start), input.AsSpan(0, take), text.AsSpan(end))))
        {
            e.Handled = true;
            return;
        }

        e.Text = input[..take];
        base.OnTextInput(e);
    }

    private bool MatchesPattern(string value)
    {
        try { return patternMatcher?.IsMatch(value) ?? true; }
        catch (RegexMatchTimeoutException) { return false; }
    }

    private async void OnPaste(object? sender, RoutedEventArgs e)
    {
        if (e.Handled) return;
        e.Handled = true;
        var root = TopLevel.GetTopLevel(this);
        if (!IsEffectivelyEnabled || IsReadOnly || root?.Clipboard is not { } clipboard) return;

        string? text;
        try { text = await clipboard.TryGetTextAsync(); }
        catch (Exception error) when (error is InvalidOperationException or ExternalException) { return; }
        if (string.IsNullOrEmpty(text) || IsReadOnly || !IsEffectivelyEnabled || root != TopLevel.GetTopLevel(this)) return;

        OnTextInput(new TextInputEventArgs { Text = PasteTransformer?.Invoke(text) ?? text });
    }

    private void SelectSlot(int index)
    {
        int length = Text?.Length ?? 0;
        int start = Math.Clamp(index, 0, length);
        if (start < length && char.IsLowSurrogate(Text![start]) && start > 0 && char.IsHighSurrogate(Text[start - 1]))
            start--;
        int end = start == length ? start : start + (char.IsSurrogatePair(Text!, start) ? 2 : 1);
        SetCurrentValue(CaretIndexProperty, start);
        SetCurrentValue(SelectionStartProperty, start);
        SetCurrentValue(SelectionEndProperty, end);
    }

    private void BuildSlots()
    {
        if (slotsPanel is null) return;
        slots.Clear();
        slotsPanel.Children.Clear();
        slotsPanel.ColumnDefinitions.Clear();
        for (int index = 0; index < MaxLength; index++)
        {
            if (GroupSize > 0 && index > 0 && index % GroupSize == 0)
            {
                var separator = new Border
                {
                    Height = 2, CornerRadius = new CornerRadius(1), IsHitTestVisible = false,
                    VerticalAlignment = VerticalAlignment.Center
                };
                separator.Classes.Add("otp-separator");
                separator[!Border.BackgroundProperty] = new DynamicResourceExtension("AlderSeparator");
                AddColumn(separator, new GridLength(SeparatorWidth));
            }
            var slot = new InputOtpSlot();
            slot.Classes.Add("otp-slot");
            slot[!CornerRadiusProperty] = this[!CornerRadiusProperty];
            slots.Add(slot);
            AddColumn(slot, new GridLength(1, GridUnitType.Star));
        }
        UpdateSlots();
    }

    private void AddColumn(Control child, GridLength width)
    {
        Grid.SetColumn(child, slotsPanel!.ColumnDefinitions.Count);
        slotsPanel.ColumnDefinitions.Add(new ColumnDefinition(width));
        slotsPanel.Children.Add(child);
    }

    private void UpdateSlots()
    {
        string text = Text ?? "";
        string placeholder = text.Length == 0 && !IsFocused ? PlaceholderText ?? "" : "";
        int start = Math.Min(SelectionStart, SelectionEnd);
        int end = Math.Max(SelectionStart, SelectionEnd);
        int activeIndex = Math.Min(CaretIndex, MaxLength - 1);
        bool invalid = IsInvalid || DataValidationErrors.GetHasErrors(this);
        for (int index = 0; index < slots.Count; index++)
        {
            bool filled = index < text.Length;
            bool active = IsFocused && (start == end ? index == activeIndex : index >= start && index < end);
            string glyph = filled ? GlyphAt(text, index) : index < placeholder.Length ? GlyphAt(placeholder, index) : "";
            slots[index].Update(glyph, filled, active, invalid, Variant == InputVariant.Secondary);
        }
    }

    private static string SingleLine(string value) => value.Replace("\r", "").Replace("\n", "").Replace("\t", "");

    private static string GlyphAt(string value, int index)
    {
        if (index > 0 && char.IsLowSurrogate(value[index]) && char.IsHighSurrogate(value[index - 1])) return "";
        return value.Substring(index, char.IsSurrogatePair(value, index) ? 2 : 1);
    }
}
