using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Alder.NativeMarkdown.UI.Transcript;
using Alder.NativeMarkdown.UI.Extensions;

namespace Alder.Avalonia.Controls;

/// <summary>Virtualizes chat entries and follows replies while the reader stays at the bottom.</summary>
public sealed partial class AlderChatConversation : TranscriptView
{
    /// <summary>Optional observable entry collection owned by the application.</summary>
    [GeneratedStyledProperty]
    public partial IReadOnlyList<TranscriptEntry>? ItemsSource { get; set; }

    /// <summary>Shows a floating scroll-to-bottom button when the reader moves away from the end.</summary>
    [GeneratedStyledProperty]
    public partial bool ShowScrollButton { get; set; }

    /// <summary>Animates scrolling to the latest message and follows growing replies smoothly.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool IsSmoothScrollingEnabled { get; set; }

    /// <summary>Returns to the latest message when an appended entry is a user message.</summary>
    [GeneratedStyledProperty]
    public partial bool ScrollOnUserMessage { get; set; }

    private ScrollContentPresenter? scrollPresenter;
    private AlderButton? scrollButton;
    private INotifyCollectionChanged? observedEntries;
    private readonly LinearGradientBrush fade = new()
    {
        StartPoint = RelativePoint.TopLeft,
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = [new(Colors.Transparent, 0), new(Colors.Black, 0), new(Colors.Black, 1), new(Colors.Transparent, 1)]
    };

    /// <summary>Uses the same Markdown services for streaming replies as standalone Markdown.</summary>
    public AlderChatConversation()
    {
        MarkdownServices.SetServices(this, AlderMarkdown.ChatServices);
        MaxContentWidth = 714;
        EntrySpacing = 40;
        Panel.AnimateFollow = IsSmoothScrollingEnabled;
        UpdateContentPadding();
    }

    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(AlderChatConversation);

    /// <summary>Scrolls to the latest message and resumes following incoming content.</summary>
    public void ScrollToBottom(bool animated = true) => Panel.FollowToEnd(animated);

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObserveEntries();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ObserveEntries(attached: false);
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (scrollPresenter is not null) scrollPresenter.SizeChanged -= UpdateFade;
        if (scrollButton is not null) scrollButton.Click -= ScrollToLatest;
        base.OnApplyTemplate(e);
        scrollPresenter = e.NameScope.Find<ScrollContentPresenter>("PART_ContentPresenter");
        scrollButton = e.NameScope.Find<AlderButton>("PART_ScrollButton");
        if (scrollPresenter is not null)
        {
            scrollPresenter.OpacityMask = fade;
            scrollPresenter.SizeChanged += UpdateFade;
        }
        if (scrollButton is not null) scrollButton.Click += ScrollToLatest;
        UpdateScrollButton();
    }

    /// <inheritdoc/>
    protected override void OnScrollChanged(ScrollChangedEventArgs e)
    {
        UpdateScrollButton();
        base.OnScrollChanged(e);
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (ReferenceEquals(e.Source, this) && e.KeyModifiers == KeyModifiers.None && e.Key is Key.Home or Key.End && ScrollBarMaximum.Y > 0)
        {
            if (e.Key == Key.End) ScrollToBottom();
            else SetCurrentValue(OffsetProperty, default(Vector));
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            Panel.SetEntries(ItemsSource ?? Entries);
            ObserveEntries(this.IsAttachedToVisualTree());
        }
        else if (change.Property == ScrollOnUserMessageProperty) ObserveEntries(this.IsAttachedToVisualTree());
        else if (change.Property == IsSmoothScrollingEnabledProperty) Panel.AnimateFollow = IsSmoothScrollingEnabled;
        else if (change.Property == ShowScrollButtonProperty)
        {
            UpdateContentPadding();
            UpdateScrollButton();
        }
        else if (change.Property == BoundsProperty)
        {
            PseudoClasses.Set(":compact-width", Bounds.Width <= 639);
            if (ShowScrollButton) UpdateContentPadding();
        }
    }

    private void UpdateContentPadding()
    {
        double buttonHeight = Bounds.Width <= 639 ? 36 : 32;
        Panel.ContentPadding = new Thickness(16, 32, 16, 32 + (ShowScrollButton ? buttonHeight : 0));
    }

    private void UpdateScrollButton()
    {
        bool visible = ShowScrollButton && ScrollBarMaximum.Y - Offset.Y > 2;
        if (!visible && scrollButton is { IsFocused: true }) Focus();
        PseudoClasses.Set(":scroll-button-visible", visible);
    }

    private void UpdateFade(object? sender, SizeChangedEventArgs e)
    {
        double edge = e.NewSize.Height > 0 ? Math.Min(.5, 32 / e.NewSize.Height) : 0;
        fade.GradientStops[1].Offset = edge;
        fade.GradientStops[2].Offset = 1 - edge;
    }

    private void ScrollToLatest(object? sender, RoutedEventArgs e) => ScrollToBottom();

    private void ObserveEntries(bool attached = true)
    {
        if (observedEntries is not null) observedEntries.CollectionChanged -= OnEntriesChanged;
        observedEntries = attached && ScrollOnUserMessage ? (ItemsSource ?? Entries) as INotifyCollectionChanged : null;
        if (observedEntries is not null) observedEntries.CollectionChanged += OnEntriesChanged;
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { } items &&
            e.NewStartingIndex + items.Count == (ItemsSource ?? Entries).Count &&
            items.OfType<ContentEntry>().Any(entry => entry.Content is AlderChatMessage { IsUser: true }))
            ScrollToBottom();
    }
}
