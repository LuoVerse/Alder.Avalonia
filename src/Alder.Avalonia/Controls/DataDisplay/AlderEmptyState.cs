using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Alder.Avalonia.Controls;

/// <summary>Selects the border and background treatment of an empty state.</summary>
public enum EmptyStateVariant
{
    /// <summary>Uses an unadorned surface.</summary>
    Plain,
    /// <summary>Draws an outline around the surface.</summary>
    Outline,
    /// <summary>Fills the background of the surface.</summary>
    Filled
}
/// <summary>Controls how the media slot presents its content.</summary>
public enum EmptyStateMediaVariant
{
    /// <summary>Presents the media slot as supplied.</summary>
    Plain,
    /// <summary>Uses the styled icon presentation for the media slot.</summary>
    Icon
}

/// <summary>Presents an optional illustration, a message and actions when content is unavailable.</summary>
public sealed class AlderEmptyState : ContentControl
{
    /// <summary>Identifies the <see cref="Title"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AlderEmptyState, string?>(nameof(Title));
    /// <summary>Identifies the <see cref="Description"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AlderEmptyState, string?>(nameof(Description));
    /// <summary>Identifies the <see cref="Media"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> MediaProperty =
        AvaloniaProperty.Register<AlderEmptyState, object?>(nameof(Media));
    /// <summary>Identifies the <see cref="MediaTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> MediaTemplateProperty =
        AvaloniaProperty.Register<AlderEmptyState, IDataTemplate?>(nameof(MediaTemplate));
    /// <summary>Identifies the <see cref="Size"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderSize> SizeProperty =
        AvaloniaProperty.Register<AlderEmptyState, AlderSize>(nameof(Size), AlderSize.Medium);
    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly StyledProperty<EmptyStateVariant> VariantProperty =
        AvaloniaProperty.Register<AlderEmptyState, EmptyStateVariant>(nameof(Variant));
    /// <summary>Identifies the <see cref="MediaVariant"/> Avalonia property.</summary>
    public static readonly StyledProperty<EmptyStateMediaVariant> MediaVariantProperty =
        AvaloniaProperty.Register<AlderEmptyState, EmptyStateMediaVariant>(nameof(MediaVariant));

    /// <summary>The primary text displayed for the content.</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    /// <summary>The optional supporting text displayed with the content.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>The leading illustration, icon, or other media content.</summary>
    public object? Media { get => GetValue(MediaProperty); set => SetValue(MediaProperty, value); }
    /// <summary>The data template used to render the media slot.</summary>
    public IDataTemplate? MediaTemplate { get => GetValue(MediaTemplateProperty); set => SetValue(MediaTemplateProperty, value); }
    /// <summary>The size preset used for the control.</summary>
    public AlderSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>The visual treatment applied to the control.</summary>
    public EmptyStateVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>The presentation applied to the media slot.</summary>
    public EmptyStateMediaVariant MediaVariant { get => GetValue(MediaVariantProperty); set => SetValue(MediaVariantProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != MediaProperty) return;
        if (change.OldValue is Control previous) LogicalChildren.Remove(previous);
        if (change.NewValue is Control next) LogicalChildren.Add(next);
    }
}
