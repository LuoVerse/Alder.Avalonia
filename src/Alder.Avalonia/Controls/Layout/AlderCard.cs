using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Layout;

namespace Alder.Avalonia.Controls;

/// <summary>A surface with optional header, title, description and footer slots.</summary>
public sealed class AlderCard : AlderSurface
{
    /// <summary>Identifies the <see cref="Header"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> HeaderProperty = AvaloniaProperty.Register<AlderCard, object?>(nameof(Header));
    /// <summary>Identifies the <see cref="HeaderTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> HeaderTemplateProperty = AvaloniaProperty.Register<AlderCard, IDataTemplate?>(nameof(HeaderTemplate));
    /// <summary>Identifies the <see cref="Title"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<AlderCard, string?>(nameof(Title));
    /// <summary>Identifies the <see cref="Description"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<AlderCard, string?>(nameof(Description));
    /// <summary>Identifies the <see cref="Footer"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> FooterProperty = AvaloniaProperty.Register<AlderCard, object?>(nameof(Footer));
    /// <summary>Identifies the <see cref="FooterTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> FooterTemplateProperty = AvaloniaProperty.Register<AlderCard, IDataTemplate?>(nameof(FooterTemplate));
    /// <summary>Identifies the <see cref="Orientation"/> Avalonia property.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<AlderCard, Orientation>(nameof(Orientation), Orientation.Vertical);
    /// <summary>Identifies the <see cref="Spacing"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<AlderCard, double>(nameof(Spacing), 12, validate: value => double.IsFinite(value) && value >= 0);
    /// <summary>Identifies the <see cref="HasHeader"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderCard, bool> HasHeaderProperty =
        AvaloniaProperty.RegisterDirect<AlderCard, bool>(nameof(HasHeader), card => card.HasHeader);

    /// <summary>The optional content displayed in the header.</summary>
    public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    /// <summary>The data template used to present the header.</summary>
    public IDataTemplate? HeaderTemplate { get => GetValue(HeaderTemplateProperty); set => SetValue(HeaderTemplateProperty, value); }
    /// <summary>The primary text displayed for the content.</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    /// <summary>The optional supporting text displayed with the content.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>The optional content displayed in the footer.</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }
    /// <summary>The data template used to present the footer.</summary>
    public IDataTemplate? FooterTemplate { get => GetValue(FooterTemplateProperty); set => SetValue(FooterTemplateProperty, value); }
    /// <summary>Whether the control is laid out horizontally or vertically.</summary>
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    /// <summary>The space between items in device-independent pixels.</summary>
    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <summary>Whether the card has content for the header region.</summary>
    public bool HasHeader
    {
        get => field;
        private set => SetAndRaise(HasHeaderProperty, ref field, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeaderProperty || change.Property == TitleProperty || change.Property == DescriptionProperty)
            HasHeader = Header is not null || !string.IsNullOrEmpty(Title) || !string.IsNullOrEmpty(Description);
    }
}
