using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Input;

namespace Alder.Avalonia.Controls;

/// <summary>Edits a hexadecimal color or an individual color channel.</summary>
public sealed class AlderColorField : AlderInputGroup
{
    /// <summary>Identifies the <see cref="Value"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderColorValue> ValueProperty =
        AvaloniaProperty.Register<AlderColorField, AlderColorValue>(nameof(Value), AlderColorValue.Default, defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="ColorSpace"/> Avalonia property.</summary>
    public static readonly StyledProperty<ColorSpace> ColorSpaceProperty =
        AvaloniaProperty.Register<AlderColorField, ColorSpace>(nameof(ColorSpace), ColorSpace.Hsb);
    /// <summary>Identifies the <see cref="Channel"/> Avalonia property.</summary>
    public static readonly StyledProperty<ColorChannel?> ChannelProperty =
        AvaloniaProperty.Register<AlderColorField, ColorChannel?>(nameof(Channel));
    /// <summary>Identifies the <see cref="Culture"/> Avalonia property.</summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<AlderColorField, CultureInfo?>(nameof(Culture));
    /// <summary>Identifies the <see cref="ValidationMessage"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> ValidationMessageProperty =
        AvaloniaProperty.Register<AlderColorField, string?>(nameof(ValidationMessage));
    /// <summary>Identifies the <see cref="HasInvalidInput"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderColorField, bool> HasInvalidInputProperty =
        AvaloniaProperty.RegisterDirect<AlderColorField, bool>(nameof(HasInvalidInput), field => field.HasInvalidInput);

    /// <summary>The color last committed by the editor.</summary>
    public AlderColorValue Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <summary>The color model used to interpret the editable components.</summary>
    public ColorSpace ColorSpace { get => GetValue(ColorSpaceProperty); set => SetValue(ColorSpaceProperty, value); }
    /// <summary>The component to edit, or null to edit the whole color as text.</summary>
    public ColorChannel? Channel { get => GetValue(ChannelProperty); set => SetValue(ChannelProperty, value); }
    /// <summary>The culture used to format or interpret displayed values.</summary>
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }
    /// <summary>The validation text displayed for an invalid color entry.</summary>
    public string? ValidationMessage { get => GetValue(ValidationMessageProperty); set => SetValue(ValidationMessageProperty, value); }
    /// <summary>Whether the current text cannot be committed as a color.</summary>
    public bool HasInvalidInput => hasInvalidInput;

    private bool hasInvalidInput;
    private bool committing;
    private CultureInfo InputCulture => Culture ?? CultureInfo.CurrentCulture;

    /// <summary>Initializes a new instance of <see cref="AlderColorField"/>.</summary>
    public AlderColorField() => ResetInput();

    /// <summary>Attempts to commit the text as a color or channel value.</summary>
    public bool Commit()
    {
        if (IsReadOnly || !IsEffectivelyEnabled) return !HasInvalidInput;

        // A dirty comparison keeps formatted display text from quantizing the model.
        if (Text == FormatValue()) return true;

        AlderColorValue value;
        if (Channel is { } channel)
        {
            var space = AlderColorValue.SpaceFor(channel, ColorSpace);
            double maximum = AlderColorValue.Maximum(channel);
            string text = Text?.Trim() ?? "";
            if (channel == ColorChannel.Hue) text = text.TrimEnd('°');
            else if (maximum == 100 && text.EndsWith(InputCulture.NumberFormat.PercentSymbol, StringComparison.Ordinal))
                text = text[..^InputCulture.NumberFormat.PercentSymbol.Length].TrimEnd();

            if (!double.TryParse(text, NumberStyles.Float, InputCulture, out double number) ||
                !double.IsFinite(number) || number < 0 || number > maximum)
                return FailCommit();

            value = Value.Edit(space, channel, number / maximum);
        }
        else
        {
            if (!AlderColorValue.TryParse(Text, out value)) return FailCommit();
            value = value.WithPreviousHue(Value.Hue);
        }

        committing = true;
        try { SetCurrentValue(ValueProperty, value); }
        finally { committing = false; }
        ResetInput();
        return true;
    }

    /// <summary>Replaces the draft text with a representation of the committed color.</summary>
    public void ResetInput()
    {
        SetCurrentValue(TextProperty, FormatValue());
        SetInvalid(false);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (IsFocused && !IsReadOnly && e.Key == Key.Enter)
        {
            Commit();
            e.Handled = true;
        }
        else if (IsFocused && !IsReadOnly && e.Key == Key.Escape && Text != FormatValue())
        {
            ResetInput();
            e.Handled = true;
        }
        else
            base.OnKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (e.Source == this) Commit();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if ((change.Property == ValueProperty && !committing) || change.Property == ChannelProperty ||
            change.Property == ColorSpaceProperty || change.Property == CultureProperty ||
            (change.Property == IsReadOnlyProperty && IsReadOnly))
            ResetInput();
        else if (change.Property == TextProperty && HasInvalidInput)
            SetInvalid(false);
    }

    private string FormatValue()
    {
        if (Channel is not { } channel) return Value.ToString();
        return Value.GetChannel(channel, ColorSpace).ToString("0.##", InputCulture);
    }

    private bool FailCommit()
    {
        SetInvalid(true);
        return false;
    }

    private void SetInvalid(bool invalid)
    {
        SetAndRaise(HasInvalidInputProperty, ref hasInvalidInput, invalid);
        PseudoClasses.Set(":invalid-color", invalid);
    }
}
