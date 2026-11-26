using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Alder.Avalonia.Controls;

/// <summary>Runs the increment and decrement parts of an Alder numeric input.</summary>
public sealed partial class AlderInputSpinner : Spinner
{
    /// <summary>Whether the buttons, keyboard, and wheel can change the value.</summary>
    [GeneratedStyledProperty(DefaultValue = true)] public partial bool AllowSpin { get; set; }

    /// <summary>Whether to display the spin buttons.</summary>
    [GeneratedStyledProperty(DefaultValue = true)] public partial bool ShowButtonSpinner { get; set; }

    /// <summary>The side of the input that contains the buttons.</summary>
    [GeneratedStyledProperty(DefaultValue = Location.Right)] public partial Location ButtonSpinnerLocation { get; set; }

    private Button? increase;
    private Button? decrease;

    /// <summary>Initializes the default right-hand button layout.</summary>
    public AlderInputSpinner() => UpdateLocation();

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (increase is not null) increase.Click -= Step;
        if (decrease is not null) decrease.Click -= Step;
        base.OnApplyTemplate(e);
        increase = e.NameScope.Find<Button>("PART_IncreaseButton");
        decrease = e.NameScope.Find<Button>("PART_DecreaseButton");
        if (increase is not null) increase.Click += Step;
        if (decrease is not null) decrease.Click += Step;
        UpdateButtons();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AllowSpinProperty) UpdateButtons();
        if (change.Property == ButtonSpinnerLocationProperty) UpdateLocation();
    }

    /// <inheritdoc />
    protected override void OnValidSpinDirectionChanged(ValidSpinDirections oldValue, ValidSpinDirections newValue) =>
        UpdateButtons();

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !AllowSpin) return;
        if (e.Key is Key.Up or Key.Down)
        {
            e.Handled = true;
            OnSpin(new SpinEventArgs(SpinEvent, e.Key == Key.Up ? SpinDirection.Increase : SpinDirection.Decrease));
        }
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.Handled || !AllowSpin || !IsKeyboardFocusWithin || e.Delta.Y == 0) return;
        e.Handled = true;
        OnSpin(new SpinEventArgs(SpinEvent, e.Delta.Y > 0 ? SpinDirection.Increase : SpinDirection.Decrease, true));
    }

    private void Step(object? sender, RoutedEventArgs e)
    {
        if (AllowSpin)
            OnSpin(new SpinEventArgs(SpinEvent, ReferenceEquals(sender, increase) ? SpinDirection.Increase : SpinDirection.Decrease));
    }

    private void UpdateButtons()
    {
        if (increase is not null) increase.IsEnabled = AllowSpin && ValidSpinDirection.HasFlag(ValidSpinDirections.Increase);
        if (decrease is not null) decrease.IsEnabled = AllowSpin && ValidSpinDirection.HasFlag(ValidSpinDirections.Decrease);
    }

    private void UpdateLocation()
    {
        PseudoClasses.Set(":left", ButtonSpinnerLocation == Location.Left);
        PseudoClasses.Set(":right", ButtonSpinnerLocation == Location.Right);
    }
}
