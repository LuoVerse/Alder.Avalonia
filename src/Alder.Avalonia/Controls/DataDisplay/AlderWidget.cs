using Avalonia;
using Avalonia.Controls.Templates;

namespace Alder.Avalonia.Controls;

/// <summary>A dashboard surface with a title, actions, content, legend, and footer slots.</summary>
public sealed partial class AlderWidget : AlderSurface
{
    /// <summary>The inset inside the raised content surface. Use zero for edge-to-edge rows.</summary>
    [GeneratedStyledProperty]
    public partial Thickness ContentPadding { get; set; }

    /// <summary>Clips content to the inner surface's rounded corners without clipping its shadow.</summary>
    [GeneratedStyledProperty]
    public partial bool ClipContent { get; set; }

    /// <summary>Identifies the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AlderWidget, string?>(nameof(Title));
    /// <summary>Identifies the <see cref="Description"/> property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AlderWidget, string?>(nameof(Description));
    /// <summary>Identifies the <see cref="Header"/> property.</summary>
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<AlderWidget, object?>(nameof(Header));
    /// <summary>Identifies the <see cref="HeaderTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> HeaderTemplateProperty =
        AvaloniaProperty.Register<AlderWidget, IDataTemplate?>(nameof(HeaderTemplate));
    /// <summary>Identifies the <see cref="Actions"/> property.</summary>
    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<AlderWidget, object?>(nameof(Actions));
    /// <summary>Identifies the <see cref="Footer"/> property.</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<AlderWidget, object?>(nameof(Footer));
    /// <summary>Identifies the <see cref="FooterTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> FooterTemplateProperty =
        AvaloniaProperty.Register<AlderWidget, IDataTemplate?>(nameof(FooterTemplate));
    /// <summary>Identifies the <see cref="Legend"/> property.</summary>
    public static readonly StyledProperty<object?> LegendProperty =
        AvaloniaProperty.Register<AlderWidget, object?>(nameof(Legend));
    /// <summary>Identifies the <see cref="LegendTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> LegendTemplateProperty =
        AvaloniaProperty.Register<AlderWidget, IDataTemplate?>(nameof(LegendTemplate));
    /// <summary>Identifies the <see cref="IsLoading"/> property.</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<AlderWidget, bool>(nameof(IsLoading));
    /// <summary>Identifies the <see cref="EmptyContent"/> property.</summary>
    public static readonly StyledProperty<object?> EmptyContentProperty =
        AvaloniaProperty.Register<AlderWidget, object?>(nameof(EmptyContent));

    /// <summary>Gets or sets the title.</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    /// <summary>Gets or sets the description.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>Gets or sets the header.</summary>
    public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    /// <summary>Gets or sets the header template.</summary>
    public IDataTemplate? HeaderTemplate { get => GetValue(HeaderTemplateProperty); set => SetValue(HeaderTemplateProperty, value); }
    /// <summary>Gets or sets the actions.</summary>
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }
    /// <summary>Gets or sets the footer.</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }
    /// <summary>Gets or sets the footer template.</summary>
    public IDataTemplate? FooterTemplate { get => GetValue(FooterTemplateProperty); set => SetValue(FooterTemplateProperty, value); }
    /// <summary>Gets or sets the legend.</summary>
    public object? Legend { get => GetValue(LegendProperty); set => SetValue(LegendProperty, value); }
    /// <summary>Gets or sets the legend template.</summary>
    public IDataTemplate? LegendTemplate { get => GetValue(LegendTemplateProperty); set => SetValue(LegendTemplateProperty, value); }
    /// <summary>Gets or sets the is loading.</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }
    /// <summary>Gets or sets the empty content.</summary>
    public object? EmptyContent { get => GetValue(EmptyContentProperty); set => SetValue(EmptyContentProperty, value); }
}
