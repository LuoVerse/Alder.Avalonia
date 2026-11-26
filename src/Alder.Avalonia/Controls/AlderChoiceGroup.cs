using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Controls how many child choices can be active in a group.</summary>
public enum ChoiceMode
{
    /// <summary>Allows at most one selected choice.</summary>
    Single,
    /// <summary>Allows multiple selected choices.</summary>
    Multiple
}

/// <summary>Direct toggle children use Key as their stable, binding-independent identity.</summary>
public class AlderChoiceGroup : AlderButtonGroup
{
    /// <summary>Identifies the <see cref="GetKey"/> attached property.</summary>
    public static readonly AttachedProperty<string?> KeyProperty =
        AvaloniaProperty.RegisterAttached<AlderChoiceGroup, ToggleButton, string?>("Key");
    /// <summary>Identifies the <see cref="SelectedKeys"/> Avalonia property.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> SelectedKeysProperty =
        AvaloniaProperty.Register<AlderChoiceGroup, IReadOnlyList<string>>(nameof(SelectedKeys),
            Array.Empty<string>(), defaultBindingMode: BindingMode.TwoWay, coerce: (_, keys) => keys ?? Array.Empty<string>());
    /// <summary>Identifies the <see cref="Mode"/> Avalonia property.</summary>
    public static readonly StyledProperty<ChoiceMode> ModeProperty =
        AvaloniaProperty.Register<AlderChoiceGroup, ChoiceMode>(nameof(Mode));
    /// <summary>Identifies the <see cref="IsRequired"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsRequiredProperty =
        AvaloniaProperty.Register<AlderChoiceGroup, bool>(nameof(IsRequired));
    /// <summary>Identifies the <see cref="IsInvalid"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderChoiceGroup, bool> IsInvalidProperty =
        AvaloniaProperty.RegisterDirect<AlderChoiceGroup, bool>(nameof(IsInvalid), g => g.IsInvalid);

    private bool updating;
    private bool invalid;
    /// <summary>Whether the control presents its invalid state.</summary>
    public bool IsInvalid => invalid;
    /// <summary>Whether the field is presented as required.</summary>
    public bool IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }
    /// <summary>The behavior mode selected for the control.</summary>
    public ChoiceMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    /// <summary>The keys representing the group's current selection.</summary>
    public IReadOnlyList<string> SelectedKeys { get => GetValue(SelectedKeysProperty); set => SetValue(SelectedKeysProperty, value); }
    /// <summary>Gets the key used to include a child toggle in the group's selection.</summary>
    public static string? GetKey(ToggleButton control) => control.GetValue(KeyProperty);
    /// <summary>Sets the key used to include a child toggle in the group's selection.</summary>
    public static void SetKey(ToggleButton control, string? value) => control.SetValue(KeyProperty, value);

    /// <summary>Whether the group has an error independent of its own selection rules.</summary>
    protected virtual bool HasExternalError => false;
    /// <summary>Whether selection changes from child interaction are disabled.</summary>
    protected virtual bool IsSelectionReadOnly => false;

    static AlderChoiceGroup() => KeyProperty.Changed.AddClassHandler<ToggleButton>((option, _) =>
    {
        if (option.Parent is AlderChoiceGroup group) group.ApplySelection();
    });

    /// <summary>Initializes a new instance of <see cref="AlderChoiceGroup"/>.</summary>
    public AlderChoiceGroup()
    {
        Joined = false;
        Orientation = Orientation.Vertical;
        Spacing = 8;
        AddHandler(ToggleButton.IsCheckedChangedEvent, OnChecked);
        Children.CollectionChanged += OnChildrenChanged;
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null && e.Action != NotifyCollectionChangedAction.Move)
        {
            foreach (var old in e.OldItems.OfType<ToggleButton>()) old.Classes.Remove("alder-invalid");
            var removed = e.OldItems.OfType<ToggleButton>().Select(GetKey)
                .Except(Children.OfType<ToggleButton>().Select(GetKey)).ToHashSet();
            var remaining = SelectedKeys.Where(k => !removed.Contains(k)).ToArray();
            if (remaining.Length != SelectedKeys.Count) SetCurrentValue(SelectedKeysProperty, remaining);
        }
        ApplySelection();
    }

    private void OnChecked(object? sender, RoutedEventArgs e)
    {
        if (updating || e.Source is not ToggleButton item || item.Parent != this || GetKey(item) is not { } key)
            return;
        if (IsSelectionReadOnly)
        {
            ApplySelection();
            return;
        }
        // Native radio activation unchecks its sibling before the new checked event bubbles.
        if (item is RadioButton && item.IsChecked != true &&
            Children.OfType<RadioButton>().Any(radio => radio.IsChecked == true)) return;
        var otherKeys = SelectedKeys.Where(k => k != key);
        var next = item.IsChecked == true
            ? Mode == ChoiceMode.Single ? new[] { key } : otherKeys.Append(key).ToArray()
            : otherKeys.ToArray();
        if (!SelectedKeys.SequenceEqual(next)) SetCurrentValue(SelectedKeysProperty, next);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedKeysProperty || change.Property == ModeProperty || change.Property == IsRequiredProperty)
            ApplySelection();
    }

    /// <summary>Updates the child toggles from the group's selected keys.</summary>
    protected void ApplySelection()
    {
        if (updating) return;
        if (Mode == ChoiceMode.Single && SelectedKeys.Count > 1)
        {
            SetCurrentValue(SelectedKeysProperty, new[] { SelectedKeys[0] });
            return;
        }
        updating = true;
        try
        {
            bool hasError = HasExternalError || IsRequired && SelectedKeys.Count == 0;
            foreach (var child in Children.OfType<ToggleButton>())
            {
                if (GetKey(child) is { } key)
                    child.SetCurrentValue(ToggleButton.IsCheckedProperty, SelectedKeys.Contains(key));
                child.Classes.Set("alder-invalid", hasError);
            }
            SetAndRaise(IsInvalidProperty, ref invalid, hasError);
            OnSelectionApplied();
        }
        finally { updating = false; }
    }

    /// <summary>Called after the current selection has been applied to the child toggles.</summary>
    protected virtual void OnSelectionApplied() { }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source is not ToggleButton current || current.Parent != this || current is CheckBox) return;
        if (this is AlderToggleButtonGroup && this.FindAncestorOfType<AlderToolbar>() is not null) return;
        var items = Children.OfType<ToggleButton>().Where(x => x.IsEffectivelyEnabled && x.IsVisible).ToArray();
        var direction = e.Key switch { Key.Up => -1, Key.Down => 1,
            Key.Left => FlowDirection == FlowDirection.RightToLeft ? 1 : -1,
            Key.Right => FlowDirection == FlowDirection.RightToLeft ? -1 : 1, _ => 0 };
        if (direction == 0 || items.Length == 0) return;
        var target = items[(Array.IndexOf(items, current) + direction + items.Length) % items.Length];
        target.Focus(NavigationMethod.Directional);
        if (target is RadioButton && !IsSelectionReadOnly) target.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
        e.Handled = true;
    }
}

/// <summary>Coordinates the checked keys of a group of checkboxes.</summary>
public sealed class AlderCheckboxGroup : AlderChoiceGroup
{
    /// <summary>Initializes a new instance of <see cref="AlderCheckboxGroup"/>.</summary>
    public AlderCheckboxGroup() => Mode = ChoiceMode.Multiple;
}

/// <summary>Coordinates the selected key and validation state of a radio group.</summary>
public class AlderRadioGroup : AlderChoiceGroup
{
    /// <summary>Identifies the <see cref="IsReadOnly"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<AlderRadioGroup, bool>(nameof(IsReadOnly));
    /// <summary>Identifies the <see cref="HasValidationError"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> HasValidationErrorProperty =
        AvaloniaProperty.Register<AlderRadioGroup, bool>(nameof(HasValidationError));

    /// <summary>Whether the value is protected from user edits.</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    /// <summary>Whether the group should show a validation error for its current selection.</summary>
    public bool HasValidationError { get => GetValue(HasValidationErrorProperty); set => SetValue(HasValidationErrorProperty, value); }

    /// <inheritdoc />
    protected override bool IsSelectionReadOnly => IsReadOnly;
    /// <inheritdoc />
    protected override bool HasExternalError => HasValidationError || DataValidationErrors.GetHasErrors(this);

    static AlderRadioGroup() => ModeProperty.OverrideMetadata<AlderRadioGroup>(
        new StyledPropertyMetadata<ChoiceMode>(ChoiceMode.Single, coerce: (_, _) => ChoiceMode.Single));

    /// <summary>Initializes a new instance of <see cref="AlderRadioGroup"/>.</summary>
    public AlderRadioGroup()
    {
        Spacing = 16;
        Focusable = true;
        IsTabStop = false;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);
        AddHandler(PointerPressedEvent, OnPreviewPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (e.Source == this) PreferredOption()?.Focus(e.NavigationMethod);
    }

    /// <inheritdoc />
    protected override void OnSelectionApplied() =>
        KeyboardNavigation.SetTabOnceActiveElement(this, PreferredOption());

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => Orientation == Orientation.Horizontal && !FullWidth
        ? LayoutRows(availableSize, arrange: false)
        : base.MeasureOverride(availableSize);

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Orientation != Orientation.Horizontal || FullWidth) return base.ArrangeOverride(finalSize);
        LayoutRows(finalSize, arrange: true);
        return finalSize;
    }

    private Size LayoutRows(Size available, bool arrange)
    {
        double width = 0, height = 0, rowWidth = 0, rowHeight = 0;
        int rowStart = 0;
        bool hasItems = false;
        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.IsVisible) continue;
            if (!arrange) child.Measure(available);
            var size = child.DesiredSize;
            if (hasItems && rowWidth + Spacing + size.Width > available.Width)
            {
                if (arrange) ArrangeRow(rowStart, i, height, rowHeight);
                height += rowHeight + Spacing;
                width = Math.Max(width, rowWidth);
                rowStart = i;
                rowWidth = rowHeight = 0;
                hasItems = false;
            }
            rowWidth += (hasItems ? Spacing : 0) + size.Width;
            rowHeight = Math.Max(rowHeight, size.Height);
            hasItems = true;
        }
        if (arrange) ArrangeRow(rowStart, Children.Count, height, rowHeight);
        return new Size(Math.Max(width, rowWidth), height + rowHeight);
    }

    private void ArrangeRow(int start, int end, double y, double height)
    {
        double x = 0;
        for (int i = start; i < end; i++)
        {
            var child = Children[i];
            if (!child.IsVisible) continue;
            double width = child.DesiredSize.Width;
            child.Arrange(new Rect(x, y, width, height));
            x += width + Spacing;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HasValidationErrorProperty || change.Property == DataValidationErrors.HasErrorsProperty)
            ApplySelection();
        if (change.Property == IsInvalidProperty) AlderLabel.SetIsInvalid(this, IsInvalid);
        else if (change.Property == IsRequiredProperty)
            AutomationProperties.SetIsRequiredForForm(this, IsRequired);
    }

    private RadioButton? PreferredOption()
    {
        RadioButton? first = null;
        foreach (var option in Children.OfType<RadioButton>())
        {
            if (!option.IsEffectivelyEnabled || !option.IsVisible) continue;
            if (option.IsChecked == true) return option;
            first ??= option;
        }
        return first;
    }

    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsReadOnly || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || e.Source is not Visual source) return;
        var option = source as RadioButton ?? source.FindAncestorOfType<RadioButton>();
        if (option?.Parent != this) return;
        option.Focus(NavigationMethod.Pointer);
        e.Handled = true;
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (IsReadOnly && e.Key is Key.Space or Key.Enter && e.Source is RadioButton option && option.Parent == this)
            e.Handled = true;
    }
}

/// <summary>Coordinates the selected keys of a group of toggle buttons.</summary>
public sealed class AlderToggleButtonGroup : AlderChoiceGroup
{
    /// <summary>Initializes a new instance of <see cref="AlderToggleButtonGroup"/>.</summary>
    public AlderToggleButtonGroup()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 0;
        Joined = true;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);
    }
}
