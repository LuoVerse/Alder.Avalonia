using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Reports an emoji chosen by the user, including its displayed skin-tone variant.</summary>
public sealed class AlderEmojiPickedEventArgs(AlderEmoji item, string emoji) : EventArgs
{
    /// <summary>The entry selected from the catalog.</summary>
    public AlderEmoji Item { get; } = item;

    /// <summary>The exact emoji sequence chosen by the user.</summary>
    public string Emoji { get; } = emoji;
}

/// <summary>A searchable emoji selection control that opens a popover or renders inline.</summary>
public sealed partial class AlderEmojiPicker : TemplatedControl
{
    /// <summary>The catalog shipped with the control, grouped in navigation order.</summary>
    public static IReadOnlyList<AlderEmojiCategory> DefaultCategories => AlderEmojiCatalog.Categories.Value;

    /// <summary>The currently chosen Unicode emoji sequence.</summary>
    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial string? SelectedEmoji { get; set; }

    /// <summary>The glyph displayed until an emoji is chosen.</summary>
    [GeneratedStyledProperty(DefaultValue = "😀")]
    public partial string PlaceholderEmoji { get; set; }

    /// <summary>Optional trigger content, replacing the selected emoji and placeholder.</summary>
    [GeneratedStyledProperty]
    public partial object? TriggerContent { get; set; }

    /// <summary>Whether the picker is presented directly in the layout.</summary>
    [GeneratedStyledProperty]
    public partial bool IsInline { get; set; }

    /// <summary>Whether the picker popover is open.</summary>
    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial bool IsDropDownOpen { get; set; }

    /// <summary>The size of the picker panel and emoji glyphs.</summary>
    [GeneratedStyledProperty(DefaultValue = AlderSize.Medium)]
    public partial AlderSize Size { get; set; }

    /// <summary>The preferred placement of the popover around its trigger.</summary>
    [GeneratedStyledProperty(DefaultValue = PlacementMode.Bottom)]
    public partial PlacementMode Placement { get; set; }

    /// <summary>The gap between the popover and its trigger, in device-independent pixels.</summary>
    [GeneratedStyledProperty(DefaultValue = 4d)]
    public partial double DropDownOffset { get; set; }

    /// <summary>The skin tone applied to emoji that support variants.</summary>
    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial AlderEmojiSkinTone SkinTone { get; set; }

    /// <summary>Replaces the built-in categories when provided.</summary>
    [GeneratedStyledProperty]
    public partial IReadOnlyList<AlderEmojiCategory>? Categories { get; set; }

    /// <summary>Optional search predicate receiving the entry and the trimmed query.</summary>
    [GeneratedStyledProperty]
    public partial Func<AlderEmoji, string, bool>? Filter { get; set; }

    /// <summary>An optional list the application can share or persist, ordered most recent first.</summary>
    [GeneratedStyledProperty]
    public partial IList<string>? RecentEmojis { get; set; }

    /// <summary>Whether to display the recent section and its category button.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool ShowRecent { get; set; }

    /// <summary>Raised each time a user commits an emoji, including repeated picks of the same entry.</summary>
    public event EventHandler<AlderEmojiPickedEventArgs>? EmojiPicked;

    private const int RecentLimit = 24;
    private readonly ObservableCollection<string> localRecents = [];
    private Button? trigger;
    private ContentPresenter? inline;
    private AlderEmojiPickerPanel? panel;
    private AlderPopover? popup;
    private TopLevel? popupRoot;
    private bool synchronizingClose;
    internal bool IsCommitting { get; private set; }

    internal IList<string> RecentItems => RecentEmojis ?? localRecents;

    internal (int Width, int Height, int Columns, int GlyphSize) Metrics => Size switch
    {
        AlderSize.Small => (240, 280, 5, 20),
        AlderSize.Large => (320, 420, 7, 28),
        _ => (280, 350, 6, 24)
    };

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (trigger is not null) trigger.Click -= OnTriggerClick;
        ReleasePanel();
        base.OnApplyTemplate(e);
        trigger = e.NameScope.Find<Button>("PART_Trigger");
        inline = e.NameScope.Find<ContentPresenter>("PART_Inline");
        if (trigger is not null) trigger.Click += OnTriggerClick;
        UpdatePresentation();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdatePresentation();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (popup is not null)
        {
            popup.HideImmediately();
            SetCurrentValue(IsDropDownOpenProperty, false);
        }
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsInlineProperty)
        {
            ReleasePanel();
            if (IsInline && IsDropDownOpen) SetCurrentValue(IsDropDownOpenProperty, false);
            UpdatePresentation();
        }
        else if (change.Property == IsDropDownOpenProperty && !synchronizingClose)
        {
            if (IsDropDownOpen) UpdatePresentation();
            else popup?.Hide();
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
            SetCurrentValue(IsDropDownOpenProperty, false);
        else if (change.Property == SizeProperty || change.Property == PlacementProperty || change.Property == DropDownOffsetProperty)
            UpdateMetrics();
        else if (change.Property == TriggerContentProperty)
            Classes.Set("custom-trigger", TriggerContent is not null);
        else if (!IsCommitting && (change.Property == CategoriesProperty || change.Property == FilterProperty ||
                 change.Property == SkinToneProperty || change.Property == SelectedEmojiProperty ||
                 change.Property == RecentEmojisProperty || change.Property == ShowRecentProperty))
            panel?.Refresh(change.Property);
    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e) =>
        SetCurrentValue(IsDropDownOpenProperty, !IsDropDownOpen);

    private void UpdatePresentation()
    {
        if (IsInline)
        {
            if (IsDropDownOpen) SetCurrentValue(IsDropDownOpenProperty, false);
            if (inline is not null && inline.Content is null)
            {
                panel = new AlderEmojiPickerPanel(this, isInline: true);
                inline.Content = panel;
                UpdateMetrics();
            }
            return;
        }

        if (!IsDropDownOpen || trigger is null || !this.IsAttachedToVisualTree()) return;
        if (!IsEffectivelyEnabled)
        {
            SetCurrentValue(IsDropDownOpenProperty, false);
            return;
        }

        panel ??= new AlderEmojiPickerPanel(this, isInline: false);
        if (popup is null)
        {
            popup = new AlderPopover
            {
                Content = panel, ContentPadding = default, SurfaceCornerRadius = new CornerRadius(16),
                FocusReturnTarget = trigger, Motion = AnchoredSurfaceMotion.ListBox
            };
            popup.Closing += (_, e) =>
            {
                if (e.Cancel || !IsDropDownOpen) return;
                synchronizingClose = true;
                try { SetCurrentValue(IsDropDownOpenProperty, false); }
                finally { synchronizingClose = false; }
            };
            popup.Closed += (_, _) => StopTrackingPopupRoot();
        }
        if (popup.IsClosing) popup.FinishExit();
        if (popup.IsOpen) return;
        panel.ResetSearch();
        UpdateMetrics();
        popup.ShowAt(trigger);
        if (!popup.IsOpen) SetCurrentValue(IsDropDownOpenProperty, false);
        else if (TopLevel.GetTopLevel(this) is { } top)
        {
            popupRoot = top;
            top.SizeChanged += OnPopupRootSizeChanged;
        }
    }

    private void UpdateMetrics()
    {
        if (panel is null) return;
        var (width, height, _, _) = Metrics;
        if (popup is not null)
        {
            double horizontalOffset = 0;
            if (TopLevel.GetTopLevel(this) is { } top)
            {
                // The overlay positioner fits the surface together with its shadow margins.
                var shadow = AnchoredSurface.ShadowMargin;
                width = Math.Min(width, Math.Max(1, (int)(top.ClientSize.Width - shadow.Left - shadow.Right)));
                height = Math.Min(height, Math.Max(1, (int)(top.ClientSize.Height - shadow.Top - shadow.Bottom)));
                if (trigger?.TranslatePoint(default, top) is { } origin)
                {
                    var left = Placement switch
                    {
                        PlacementMode.Bottom or PlacementMode.Top => origin.X + (trigger.Bounds.Width - width) / 2,
                        PlacementMode.BottomEdgeAlignedLeft or PlacementMode.TopEdgeAlignedLeft => origin.X,
                        PlacementMode.BottomEdgeAlignedRight or PlacementMode.TopEdgeAlignedRight => origin.X + trigger.Bounds.Width - width,
                        _ => double.NaN
                    };
                    if (double.IsFinite(left))
                    {
                        var rightLimit = Math.Max(shadow.Left, top.ClientSize.Width - shadow.Right - width);
                        horizontalOffset = Math.Clamp(left, shadow.Left, rightLimit) - left;
                    }
                }
            }
            popup.MaxContentWidth = width;
            popup.Placement = Placement;
            popup.Offset = DropDownOffset;
            popup.HorizontalOffset = horizontalOffset;
        }
        if (!IsInline) panel.Width = width;
        panel.Height = height;
        panel.Refresh(SizeProperty);
    }

    private void ReleasePanel()
    {
        StopTrackingPopupRoot();
        popup?.HideImmediately();
        if (popup is not null) popup.Content = null;
        popup = null;
        if (inline is not null) inline.Content = null;
        panel = null;
    }

    private void OnPopupRootSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateMetrics();

    private void StopTrackingPopupRoot()
    {
        if (popupRoot is not null) popupRoot.SizeChanged -= OnPopupRootSizeChanged;
        popupRoot = null;
    }

    internal void Pick(AlderEmoji entry, string glyph)
    {
        if (!IsEffectivelyEnabled) return;
        IsCommitting = true;
        try
        {
            SetCurrentValue(SelectedEmojiProperty, glyph);
            var recent = RecentItems;
            if (!recent.IsReadOnly && (recent.Count == 0 || recent[0] != glyph))
            {
                recent.Remove(glyph);
                recent.Insert(0, glyph);
                while (recent.Count > RecentLimit) recent.RemoveAt(recent.Count - 1);
            }
        }
        finally { IsCommitting = false; }
        panel?.Refresh(SelectedEmojiProperty);
        if (!IsInline)
        {
            SetCurrentValue(IsDropDownOpenProperty, false);
            trigger?.Focus(NavigationMethod.Unspecified);
        }
        EmojiPicked?.Invoke(this, new(entry, glyph));
    }
}
