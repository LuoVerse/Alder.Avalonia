using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;

namespace Alder.Avalonia.Controls;

/// <summary>Lays out buttons and lets them share sizes, variants, and joined borders.</summary>
public class AlderButtonGroup : StackPanel
{
    /// <summary>Identifies the <see cref="Joined"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> JoinedProperty =
        AvaloniaProperty.Register<AlderButtonGroup, bool>(nameof(Joined), true);
    /// <summary>Identifies the <see cref="Size"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderSize?> SizeProperty =
        AvaloniaProperty.Register<AlderButtonGroup, AlderSize?>(nameof(Size));
    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly StyledProperty<ButtonVariant?> VariantProperty =
        AvaloniaProperty.Register<AlderButtonGroup, ButtonVariant?>(nameof(Variant));

    /// <summary>Identifies the <see cref="FullWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> FullWidthProperty =
        AvaloniaProperty.Register<AlderButtonGroup, bool>(nameof(FullWidth));
    /// <summary>Identifies the <see cref="IsDisabled"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsDisabledProperty =
        AvaloniaProperty.Register<AlderButtonGroup, bool>(nameof(IsDisabled));
    /// <summary>Identifies the <see cref="ShowSeparators"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowSeparatorsProperty =
        AvaloniaProperty.Register<AlderButtonGroup, bool>(nameof(ShowSeparators), true);
    /// <summary>Identifies the <see cref="GetShowSeparator"/> attached property.</summary>
    public static readonly AttachedProperty<bool?> ShowSeparatorProperty =
        AvaloniaProperty.RegisterAttached<AlderButtonGroup, Button, bool?>("ShowSeparator");

    /// <summary>Whether adjacent buttons share a continuous outline.</summary>
    public bool Joined { get => GetValue(JoinedProperty); set => SetValue(JoinedProperty, value); }
    /// <summary>The size preset used for the control.</summary>
    public AlderSize? Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>The visual treatment applied to the control.</summary>
    public ButtonVariant? Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>Whether the group stretches and distributes available space among its buttons.</summary>
    public bool FullWidth { get => GetValue(FullWidthProperty); set => SetValue(FullWidthProperty, value); }

    /// <summary>Disables direct buttons by default. A button can override it with a local IsEnabled value.</summary>
    public bool IsDisabled { get => GetValue(IsDisabledProperty); set => SetValue(IsDisabledProperty, value); }
    /// <summary>Whether joined buttons show dividers unless an item supplies an override.</summary>
    public bool ShowSeparators { get => GetValue(ShowSeparatorsProperty); set => SetValue(ShowSeparatorsProperty, value); }
    /// <summary>Gets the per-button separator override. Null uses the group's setting.</summary>
    public static bool? GetShowSeparator(Button target) => target.GetValue(ShowSeparatorProperty);
    /// <summary>Sets the per-button separator override. Null uses the group's setting.</summary>
    public static void SetShowSeparator(Button target, bool? value) => target.SetValue(ShowSeparatorProperty, value);

    private readonly Dictionary<Button, List<IDisposable>> defaults = new();
    private static readonly string[] EdgeClasses = ["leading", "trailing", "top", "bottom", "middle"];

    static AlderButtonGroup() => AffectsMeasure<AlderButtonGroup>(FullWidthProperty);

    /// <summary>Initializes a new instance of <see cref="AlderButtonGroup"/>.</summary>
    public AlderButtonGroup() => Orientation = Orientation.Horizontal;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (Children is null) return;
        if (change.Property == OrientationProperty || change.Property == JoinedProperty ||
            change.Property == ShowSeparatorsProperty || change.Property == FullWidthProperty)
            UpdateEdges();
        if (change.Property == SizeProperty || change.Property == VariantProperty || change.Property == IsDisabledProperty)
            foreach (var button in Children.OfType<Button>()) ApplyDefaults(button);
    }

    /// <inheritdoc />
    protected override void ChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        base.ChildrenChanged(sender, e);
        if (e.Action != NotifyCollectionChangedAction.Move)
        {
            if (e.OldItems is not null)
                foreach (var old in e.OldItems.OfType<Button>())
                {
                    old.PropertyChanged -= ChildChanged;
                    old.Classes.Remove("alder-group-full");
                    old.Classes.Remove("alder-grouped");
                    SetEdge(old, null);
                    RemoveDefaults(old);
                }
            if (e.NewItems is not null)
                foreach (var child in e.NewItems.OfType<Button>())
                {
                    child.PropertyChanged += ChildChanged;
                    ApplyDefaults(child);
                }
        }
        UpdateEdges();
    }

    private void ApplyDefaults(Button button)
    {
        RemoveDefaults(button);
        var entries = new List<IDisposable>(3);
        if (Size is { } size && button is AlderButton or AlderToggleButton)
            entries.Add(button.SetValue(AlderButton.SizeProperty, size, BindingPriority.Style)!);
        if (Variant is { } variant && button is AlderButton hero)
            entries.Add(hero.SetValue(AlderButton.VariantProperty, variant, BindingPriority.Style)!);
        if (IsDisabled)
            entries.Add(button.SetValue(IsEnabledProperty, false, BindingPriority.Style)!);
        if (entries.Count > 0) defaults.Add(button, entries);
    }

    private void RemoveDefaults(Button button)
    {
        if (defaults.Remove(button, out var entries))
            foreach (var entry in entries) entry.Dispose();
    }

    private void ChildChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty || e.Property == ShowSeparatorProperty) UpdateEdges();
    }

    private void UpdateEdges()
    {
        var children = Children.OfType<Button>().ToArray();
        foreach (var child in children)
        {
            child.Classes.Set("alder-group-full", FullWidth);
            child.Classes.Set("alder-grouped", Joined && child.IsVisible);
            if (!child.IsVisible || !Joined) SetEdge(child, null);
        }
        if (!Joined) return;
        var buttons = children.Where(b => b.IsVisible).ToArray();
        var horizontal = Orientation == Orientation.Horizontal;
        for (var i = 0; i < buttons.Length; i++)
        {
            string? edge = null;
            if (buttons.Length > 1)
            {
                if (i == 0) edge = horizontal ? "leading" : "top";
                else if (i == buttons.Length - 1) edge = horizontal ? "trailing" : "bottom";
                else edge = "middle";
            }
            SetEdge(buttons[i], edge);
            buttons[i].Classes.Set("alder-horizontal", edge == "middle" && horizontal);
            buttons[i].Classes.Set("alder-vertical", edge == "middle" && !horizontal);
            bool separator = i > 0 && (GetShowSeparator(buttons[i]) ?? ShowSeparators);
            buttons[i].Classes.Set("alder-divider-x", horizontal && separator);
            buttons[i].Classes.Set("alder-divider-y", !horizontal && separator);
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (!FullWidth)
            return base.MeasureOverride(availableSize);

        if (Orientation == Orientation.Vertical)
        {
            var desired = base.MeasureOverride(availableSize);
            return double.IsFinite(availableSize.Width) ? desired.WithWidth(availableSize.Width) : desired;
        }

        var count = Children.Count(static child => child.IsVisible);
        if (count == 0) return default;
        var gapWidth = Spacing * (count - 1);
        var width = Math.Max(0, availableSize.Width - gapWidth) / count;
        double height = 0;
        double widest = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            child.Measure(new Size(width, availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
            widest = Math.Max(widest, child.DesiredSize.Width);
        }
        return new Size(double.IsFinite(width) ? availableSize.Width : widest * count + gapWidth, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (!FullWidth || Orientation == Orientation.Vertical)
            return base.ArrangeOverride(finalSize);

        var count = Children.Count(static child => child.IsVisible);
        if (count == 0) return finalSize;

        var width = Math.Max(0, finalSize.Width - Spacing * (count - 1)) / count;
        var scale = UseLayoutRounding ? LayoutHelper.GetLayoutScale(this) : 1;
        var index = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            var start = index * (width + Spacing);
            var end = start + width;
            if (UseLayoutRounding)
            {
                start = LayoutHelper.RoundLayoutValue(start, scale);
                end = LayoutHelper.RoundLayoutValue(end, scale);
            }
            child.Arrange(new Rect(start, 0, end - start, finalSize.Height));
            index++;
        }

        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    private sealed class GroupPeer(AlderButtonGroup owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }

    private static void SetEdge(Control control, string? edge)
    {
        control.Classes.Set("alder-joined", edge is not null);
        if (edge != "middle")
        {
            control.Classes.Remove("alder-horizontal");
            control.Classes.Remove("alder-vertical");
        }
        foreach (var name in EdgeClasses) control.Classes.Set("alder-" + name, edge == name);
        if (edge is null)
        {
            control.Classes.Remove("alder-divider-x");
            control.Classes.Remove("alder-divider-y");
        }
    }
}
