using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;

namespace Alder.Avalonia.Controls;

/// <summary>Coordinates standalone disclosures using item state, initial keys or a two-way binding.</summary>
public class AlderDisclosureGroup : ItemsControl
{
    /// <summary>Identifies the <see cref="AllowsMultipleExpanded"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> AllowsMultipleExpandedProperty =
        AvaloniaProperty.Register<AlderDisclosureGroup, bool>(nameof(AllowsMultipleExpanded));
    /// <summary>Identifies the <see cref="ExpandedKeys"/> Avalonia property.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> ExpandedKeysProperty =
        AvaloniaProperty.Register<AlderDisclosureGroup, IReadOnlyList<string>?>(nameof(ExpandedKeys), defaultBindingMode: BindingMode.TwoWay,
            coerce: (owner, keys) => !owner.GetValue(AllowsMultipleExpandedProperty) && keys is { Count: > 1 } ? [keys[0]] : keys);
    /// <summary>Identifies the <see cref="DefaultExpandedKeys"/> Avalonia property.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> DefaultExpandedKeysProperty =
        AvaloniaProperty.Register<AlderDisclosureGroup, IReadOnlyList<string>?>(nameof(DefaultExpandedKeys));

    private readonly Dictionary<AlderDisclosure, int> items = [];
    private bool synchronizing;

    /// <summary>The disclosure controls currently tracked by the group.</summary>
    protected IReadOnlyDictionary<AlderDisclosure, int> DisclosureItems => items;

    /// <summary>Whether expanding a disclosure leaves other disclosures open.</summary>
    public bool AllowsMultipleExpanded { get => GetValue(AllowsMultipleExpandedProperty); set => SetValue(AllowsMultipleExpandedProperty, value); }
    /// <summary>The keys of currently expanded disclosures.</summary>
    public IReadOnlyList<string>? ExpandedKeys { get => GetValue(ExpandedKeysProperty); set => SetValue(ExpandedKeysProperty, value); }

    /// <summary>Seeds keyed items while ExpandedKeys has not been supplied or changed by the user.</summary>
    public IReadOnlyList<string>? DefaultExpandedKeys { get => GetValue(DefaultExpandedKeysProperty); set => SetValue(DefaultExpandedKeysProperty, value); }

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<Control>(item, out recycleKey);

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new AlderDisclosure();

    /// <inheritdoc />
    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        if (container is not AlderDisclosure disclosure) return;
        items.Add(disclosure, index);
        disclosure.PropertyChanged += OnItemChanged;
        Synchronize();
        OnLayoutItemsChanged();
    }

    /// <inheritdoc />
    protected override void ContainerIndexChangedOverride(Control container, int oldIndex, int newIndex)
    {
        base.ContainerIndexChangedOverride(container, oldIndex, newIndex);
        if (container is not AlderDisclosure disclosure) return;
        items[disclosure] = newIndex;
        OnLayoutItemsChanged();
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(Control container)
    {
        if (container is AlderDisclosure disclosure) RemoveItem(disclosure);
        base.ClearContainerForItemOverride(container);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AllowsMultipleExpandedProperty)
        {
            CoerceValue(ExpandedKeysProperty);
            Synchronize();
        }
        else if (change.Property == ExpandedKeysProperty || change.Property == DefaultExpandedKeysProperty)
            Synchronize();
    }

    private void OnItemChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        var item = (AlderDisclosure)sender!;
        if (change.Property == ParentProperty && item.Parent != this)
        {
            RemoveItem(item);
            return;
        }
        if (change.Property == IsVisibleProperty)
        {
            OnLayoutItemsChanged();
            return;
        }

        if (synchronizing) return;
        if (change.Property == AlderDisclosure.KeyProperty)
            Synchronize();
        else if (change.Property == Expander.IsExpandedProperty)
            Synchronize(item);
    }

    private void RemoveItem(AlderDisclosure item)
    {
        if (!items.Remove(item)) return;
        item.PropertyChanged -= OnItemChanged;
        OnItemRemoved(item);
        OnLayoutItemsChanged();
    }

    private void Synchronize(AlderDisclosure? changed = null)
    {
        if (synchronizing) return;
        synchronizing = true;
        try
        {
            var requested = ExpandedKeys ?? DefaultExpandedKeys;
            if (!AllowsMultipleExpanded && requested is { Count: > 1 })
                requested = [requested[0]];
            var ordered = items.OrderBy(pair => pair.Value).Select(pair => pair.Key).ToArray();
            if (changed is null && requested is not null)
            {
                foreach (var item in ordered)
                {
                    if (item.Key is { } key)
                        item.SetCurrentValue(Expander.IsExpandedProperty, requested.Contains(key));
                }
            }

            if (!AllowsMultipleExpanded)
            {
                var keep = changed is { IsExpanded: true } ? changed : ordered.FirstOrDefault(item => item.IsExpanded);
                foreach (var item in ordered)
                    if (item != keep && item.IsExpanded) item.SetCurrentValue(Expander.IsExpandedProperty, false);
            }

            if (changed is not null && requested is not null)
            {
                var keys = ordered.Where(item => item.IsExpanded).Select(item => item.Key).OfType<string>().ToArray();
                if (ExpandedKeys is null || !requested.SequenceEqual(keys)) SetCurrentValue(ExpandedKeysProperty, keys);
            }
        }
        finally { synchronizing = false; }
    }

    /// <summary>Called after the set of disclosure controls in the layout changes.</summary>
    protected virtual void OnLayoutItemsChanged() { }

    /// <summary>Called when a disclosure is removed from the group.</summary>
    protected virtual void OnItemRemoved(AlderDisclosure item) { }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source is not ToggleButton header || e.Key is not (Key.Up or Key.Down or Key.Home or Key.End))
            return;
        var headers = items.OrderBy(pair => pair.Value)
            .Select(pair => pair.Key.Trigger)
            .OfType<ToggleButton>()
            .Where(button => button.IsEffectivelyEnabled && button.IsEffectivelyVisible)
            .ToArray();
        int index = Array.IndexOf(headers, header);
        if (index < 0) return;
        int next = e.Key switch
        {
            Key.Home => 0,
            Key.End => headers.Length - 1,
            Key.Up => (index + headers.Length - 1) % headers.Length,
            _ => (index + 1) % headers.Length
        };
        headers[next].Focus(NavigationMethod.Directional);
        e.Handled = true;
    }
}
