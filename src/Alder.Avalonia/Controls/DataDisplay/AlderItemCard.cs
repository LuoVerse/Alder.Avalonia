using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;


/// <summary>The surface treatment of an item card or group.</summary>
public enum ItemCardVariant
{
    /// <summary>A raised surface.</summary>
    Default,
    /// <summary>A recessed surface.</summary>
    Secondary,
    /// <summary>A higher contrast surface.</summary>
    Tertiary,
    /// <summary>A transparent card with an outline.</summary>
    Outline,
    /// <summary>A flat transparent card.</summary>
    Transparent
}

/// <summary>The arrangement of an item card group.</summary>
public enum ItemCardLayout
{
    /// <summary>A vertical list with row dividers.</summary>
    List,
    /// <summary>A responsive grid of cards.</summary>
    Grid
}

/// <summary>A templated settings or information row with optional media, trailing controls, and a row command.</summary>
public sealed class AlderItemCard : ContentControl
{
    /// <summary>Identifies the animated feedback settings.</summary>
    public static readonly StyledProperty<AlderPressableFeedback?> FeedbackProperty =
        AlderButton.FeedbackProperty.AddOwner<AlderItemCard>();

    /// <summary>Optional motion settings for this control's press and hover feedback.</summary>
    public AlderPressableFeedback? Feedback { get => GetValue(FeedbackProperty); set => SetValue(FeedbackProperty, value); }

    /// <summary>Identifies the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AlderItemCard, string?>(nameof(Title));
    /// <summary>Identifies the <see cref="Description"/> property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AlderItemCard, string?>(nameof(Description));
    /// <summary>Identifies the <see cref="Icon"/> property.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<AlderItemCard, object?>(nameof(Icon));
    /// <summary>Identifies the <see cref="IconTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> IconTemplateProperty =
        AvaloniaProperty.Register<AlderItemCard, IDataTemplate?>(nameof(IconTemplate));
    /// <summary>Identifies the <see cref="Action"/> property.</summary>
    public static readonly StyledProperty<object?> ActionProperty =
        AvaloniaProperty.Register<AlderItemCard, object?>(nameof(Action));
    /// <summary>Identifies the <see cref="ActionTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> ActionTemplateProperty =
        AvaloniaProperty.Register<AlderItemCard, IDataTemplate?>(nameof(ActionTemplate));
    /// <summary>Identifies the <see cref="Command"/> property.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<AlderItemCard, ICommand?>(nameof(Command));
    /// <summary>Identifies the <see cref="CommandParameter"/> property.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<AlderItemCard, object?>(nameof(CommandParameter));
    /// <summary>Identifies the <see cref="Variant"/> property.</summary>
    public static readonly StyledProperty<ItemCardVariant> VariantProperty =
        AvaloniaProperty.Register<AlderItemCard, ItemCardVariant>(nameof(Variant));
    /// <summary>Identifies the <see cref="Orientation"/> property.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<AlderItemCard, Orientation>(nameof(Orientation), Orientation.Horizontal);
    /// <summary>Identifies the <see cref="Size"/> property.</summary>
    public static readonly StyledProperty<AlderSize> SizeProperty =
        AvaloniaProperty.Register<AlderItemCard, AlderSize>(nameof(Size), AlderSize.Medium);

    /// <summary>Gets or sets the title.</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    /// <summary>Gets or sets the description.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>Gets or sets the icon.</summary>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    /// <summary>Gets or sets the icon template.</summary>
    public IDataTemplate? IconTemplate { get => GetValue(IconTemplateProperty); set => SetValue(IconTemplateProperty, value); }
    /// <summary>Gets or sets the action.</summary>
    public object? Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }
    /// <summary>Gets or sets the action template.</summary>
    public IDataTemplate? ActionTemplate { get => GetValue(ActionTemplateProperty); set => SetValue(ActionTemplateProperty, value); }
    /// <summary>Gets or sets the command.</summary>
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    /// <summary>Gets or sets the command parameter.</summary>
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
    /// <summary>Gets or sets the variant.</summary>
    public ItemCardVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>Gets or sets the orientation.</summary>
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    /// <summary>Gets or sets the size.</summary>
    public AlderSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Action is not AlderButton { Flyout: AlderMenuFlyout or AlderPopover }) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (Action is AlderButton { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } button &&
                button.IsAttachedToVisualTree()) button.PrepareFlyout();
        }, DispatcherPriority.Background);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        AlderPressableFeedback.OnOwnerPropertyChanged(this, change);
    }
}

/// <summary>A set of item cards with a shared surface and a responsive list or grid layout.</summary>
public sealed class AlderItemCardGroup : HeaderedItemsControl
{
    /// <summary>Identifies the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AlderItemCardGroup, string?>(nameof(Title));
    /// <summary>Identifies the <see cref="Description"/> property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AlderItemCardGroup, string?>(nameof(Description));
    /// <summary>Identifies the <see cref="Variant"/> property.</summary>
    public static readonly StyledProperty<ItemCardVariant> VariantProperty =
        AvaloniaProperty.Register<AlderItemCardGroup, ItemCardVariant>(nameof(Variant));
    /// <summary>Identifies the <see cref="Layout"/> property.</summary>
    public static readonly StyledProperty<ItemCardLayout> LayoutProperty =
        AvaloniaProperty.Register<AlderItemCardGroup, ItemCardLayout>(nameof(Layout));
    /// <summary>Identifies the <see cref="Columns"/> property.</summary>
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<AlderItemCardGroup, int>(nameof(Columns), 2, validate: value => value > 0);
    /// <summary>Identifies the <see cref="MinColumnWidth"/> property.</summary>
    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<AlderItemCardGroup, double>(nameof(MinColumnWidth), 220, validate: value => double.IsFinite(value) && value > 0);

    /// <summary>Gets or sets the title.</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    /// <summary>Gets or sets the description.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>Gets or sets the variant.</summary>
    public ItemCardVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>Gets or sets the layout.</summary>
    public ItemCardLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }
    /// <summary>Gets or sets the columns.</summary>
    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }
    /// <summary>Gets or sets the min column width.</summary>
    public double MinColumnWidth { get => GetValue(MinColumnWidthProperty); set => SetValue(MinColumnWidthProperty, value); }

    /// <summary>Initializes a card group.</summary>
    public AlderItemCardGroup() => UpdatePanel();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LayoutProperty || change.Property == ColumnsProperty || change.Property == MinColumnWidthProperty)
            UpdatePanel();
    }

    private void UpdatePanel() => SetCurrentValue(ItemsPanelProperty, new FuncTemplate<Panel?>(() =>
        Layout == ItemCardLayout.List
            ? new DisplayGroupPanel { MaxColumns = 1, Gap = 1, ShowDividers = true }
            : new DisplayGroupPanel { MaxColumns = Columns, MinimumColumnWidth = MinColumnWidth, Gap = 12 }));
}
