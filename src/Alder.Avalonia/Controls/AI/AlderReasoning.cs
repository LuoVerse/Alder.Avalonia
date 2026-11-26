using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>An expandable summary of an explicitly supplied reasoning trace.</summary>
public sealed partial class AlderReasoning : AlderDisclosure
{
    /// <summary>Identifies the <see cref="CollapseDelay"/> Avalonia property.</summary>
    public static readonly StyledProperty<TimeSpan> CollapseDelayProperty =
        AvaloniaProperty.Register<AlderReasoning, TimeSpan>(nameof(CollapseDelay), TimeSpan.FromSeconds(1));

    /// <summary>Identifies the <see cref="ResolvedHeader"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderReasoning, string> ResolvedHeaderProperty =
        AvaloniaProperty.RegisterDirect<AlderReasoning, string>(nameof(ResolvedHeader), owner => owner.ResolvedHeader);

    /// <summary>The default loader shown while reasoning is running.</summary>
    [GeneratedStyledProperty(DefaultValue = AiLoaderVariant.Grid)] public partial AiLoaderVariant LoaderVariant { get; set; }

    /// <summary>Custom content that replaces the default loader while reasoning is running.</summary>
    [GeneratedStyledProperty] public partial object? LoaderContent { get; set; }

    /// <summary>Current state of the displayed reasoning operation.</summary>
    [GeneratedStyledProperty(DefaultValue = AiRunStatus.Completed)] public partial AiRunStatus Status { get; set; }

    /// <summary>A formatted elapsed duration shown when reasoning completes or stops.</summary>
    [GeneratedStyledProperty] public partial string? Duration { get; set; }

    /// <summary>Plain reasoning text. Use Content for a templated or composed trace.</summary>
    [GeneratedStyledProperty] public partial string? Text { get; set; }

    /// <summary>Whether the first visible trace content opens a running disclosure.</summary>
    [GeneratedStyledProperty(DefaultValue = true)] public partial bool AutoExpand { get; set; }

    /// <summary>Whether the streaming indicator is shown.</summary>
    [GeneratedStyledProperty(DefaultValue = true)] public partial bool ShowLoader { get; set; }

    /// <summary>Time to wait before automatically collapsing. A negative value keeps the trace open.</summary>
    public TimeSpan CollapseDelay { get => GetValue(CollapseDelayProperty); set => SetValue(CollapseDelayProperty, value); }

    /// <summary>The localized status text or supplied text header.</summary>
    public string ResolvedHeader => resolvedHeader;

    private static readonly SplineEasing LabelEase = new(.23, 1, .32, 1);
    private static readonly TimeSpan LabelDuration = TimeSpan.FromMilliseconds(150);
    private string resolvedHeader = "";
    private IDisposable? collapseTimer;
    private Control? observedContent;
    private TextBlock? statusLabel;
    private AlderTextShimmer? streamingLabel;
    private ContentPresenter? customHeader;
    private ToggleButton? hookedTrigger;
    private bool userToggled;
    private bool hasOpenedForContent;
    private bool collapseDue;

    /// <summary>Creates a trace with the default disclosure behavior.</summary>
    public AlderReasoning()
    {
        SetValue(AlderChainOfThoughtStep.RunStatusProperty, Status);
        ResourcesChanged += (_, _) => RefreshHeader();
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (hookedTrigger is not null) hookedTrigger.Click -= TriggerClicked;
        base.OnApplyTemplate(e);
        hookedTrigger = Trigger;
        if (hookedTrigger is not null) hookedTrigger.Click += TriggerClicked;
        statusLabel = e.NameScope.Find<TextBlock>("ReasoningStatusLabel");
        streamingLabel = e.NameScope.Find<AlderTextShimmer>("ReasoningStreamingLabel");
        customHeader = e.NameScope.Find<ContentPresenter>("ReasoningCustomHeader");
        RefreshHeader();
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObserveContent();
        RefreshHeader();
        TryExpand();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CancelCollapse();
        UnobserveContent();
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty)
        {
            SetValue(AlderChainOfThoughtStep.RunStatusProperty, Status);
            CancelCollapse();
            if (Status == AiRunStatus.Running)
            {
                userToggled = false;
                hasOpenedForContent = false;
                TryExpand();
            }
            else if (Status == AiRunStatus.Completed && change.GetOldValue<AiRunStatus>() == AiRunStatus.Running)
            {
                ScheduleCollapse();
            }
            RefreshHeader(animate: true);
        }
        else if (change.Property == HeaderProperty || change.Property == HeaderTemplateProperty || change.Property == DurationProperty)
            RefreshHeader();
        else if (change.Property == ContentProperty)
        {
            ObserveContent();
            TryExpand();
        }
        else if (change.Property == TextProperty || change.Property == AutoExpandProperty)
            TryExpand();
        else if (change.Property == IsExpandedProperty && !IsExpanded)
            CancelCollapse();
        else if (change.Property == CollapseDelayProperty && (collapseTimer is not null || collapseDue))
        {
            CancelCollapse();
            ScheduleCollapse();
        }
        else if (change.Property == IsKeyboardFocusWithinProperty && collapseDue && !IsKeyboardFocusWithin)
            Dispatcher.UIThread.Post(TryCollapse);
    }

    private void ObserveContent()
    {
        UnobserveContent();
        if (!this.IsAttachedToVisualTree() || Content is not Control control) return;
        observedContent = control;
        control.PropertyChanged += ContentChanged;
    }

    private void UnobserveContent()
    {
        if (observedContent is not { } control) return;
        control.PropertyChanged -= ContentChanged;
        observedContent = null;
    }

    private void ContentChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBlock.TextProperty || e.Property == AlderMarkdown.MarkdownProperty ||
            e.Property == ContentControl.ContentProperty || e.Property == IsVisibleProperty)
            TryExpand();
    }

    private void TryExpand()
    {
        if (Status != AiRunStatus.Running || hasOpenedForContent || userToggled || !AutoExpand) return;
        if (!HasContent(Content ?? Text)) return;
        hasOpenedForContent = true;
        SetCurrentValue(IsExpandedProperty, true);
    }

    private static bool HasContent(object? content) => content switch
    {
        null => false,
        string text => !string.IsNullOrWhiteSpace(text),
        Control { IsVisible: false } => false,
        TextBlock text => HasContent(text.Text) || text.Inlines?.Count > 0,
        AlderMarkdown markdown => HasContent(markdown.Markdown) || markdown.Document is not null || markdown.Stream is not null,
        ContentControl control => HasContent(control.Content),
        _ => true
    };

    private void TriggerClicked(object? sender, RoutedEventArgs e)
    {
        userToggled = true;
        CancelCollapse();
    }

    private void ScheduleCollapse()
    {
        if (userToggled || !IsExpanded || CollapseDelay < TimeSpan.Zero || !this.IsAttachedToVisualTree()) return;
        collapseTimer = DispatcherTimer.RunOnce(() =>
        {
            collapseTimer = null;
            collapseDue = true;
            TryCollapse();
        }, CollapseDelay);
    }

    private void TryCollapse()
    {
        if (!collapseDue || IsKeyboardFocusWithin) return;
        collapseDue = false;
        SetCurrentValue(IsExpandedProperty, false);
    }

    private void CancelCollapse()
    {
        collapseTimer?.Dispose();
        collapseTimer = null;
        collapseDue = false;
    }

    private void RefreshHeader(bool animate = false)
    {
        bool custom = HeaderTemplate is not null || Header is not null and not string;
        bool running = Status == AiRunStatus.Running;
        var duration = string.IsNullOrWhiteSpace(Duration) ? null : Duration;
        var pattern = Header as string ?? (Status switch
        {
            AiRunStatus.Running or AiRunStatus.Pending => Label("AiThinking", "Thinking"),
            AiRunStatus.Failed => Label("AiReasoningFailed", "Couldn't finish thinking"),
            AiRunStatus.Stopped => duration is null
                ? Label("AiReasoningStopped", "Stopped thinking") : Label("AiReasoningStoppedFor", "Stopped after {0}"),
            _ => duration is null
                ? Label("AiReasoningCompleted", "Thought for a few seconds") : Label("AiReasoningCompletedFor", "Thought for {0}")
        });
        int marker = Header is null && duration is not null ? pattern.IndexOf("{0}", StringComparison.Ordinal) : -1;
        SetAndRaise(ResolvedHeaderProperty, ref resolvedHeader,
            marker < 0 ? pattern : string.Concat(pattern.AsSpan(0, marker), duration, pattern.AsSpan(marker + 3)));
        if (statusLabel is null || streamingLabel is null || customHeader is null) return;

        statusLabel.IsVisible = !custom && !running;
        streamingLabel.IsVisible = !custom && running;
        customHeader.IsVisible = custom;
        statusLabel.Inlines!.Clear();
        if (marker < 0)
        {
            statusLabel.Inlines.Add(new Run(resolvedHeader));
        }
        else
        {
            var emphasized = new Run(duration);
            emphasized[!TextElement.ForegroundProperty] = new DynamicResourceExtension("AlderInk");
            statusLabel.Inlines.Add(new Run(pattern[..marker]));
            statusLabel.Inlines.Add(emphasized);
            statusLabel.Inlines.Add(new Run(pattern[(marker + 3)..]));
        }
        if (animate && !AlderMotion.GetReduced(this))
        {
            var liveLabel = custom ? (Control)customHeader : running ? streamingLabel : statusLabel;
            OverlayMotion.Animate(liveLabel, true, default, 1, LabelDuration, LabelDuration, default, LabelEase);
        }
    }

    private string Label(string key, string fallback) =>
        this.TryFindResource("AlderText." + key, out var value) && value is string label ? label : fallback;
}
