using System.Globalization;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>A compact emoji toggle with an animated reaction count.</summary>
public sealed partial class AlderEmojiReactionButton : ToggleButton
{
    /// <summary>The emoji displayed beside the count.</summary>
    [GeneratedStyledProperty(DefaultValue = "👍")]
    public partial string Emoji { get; set; }

    /// <summary>The visual size of the reaction button.</summary>
    [GeneratedStyledProperty(DefaultValue = AlderSize.Medium)]
    public partial AlderSize Size { get; set; }

    /// <summary>Whether the selection and count are protected from user input.</summary>
    [GeneratedStyledProperty]
    public partial bool IsReadOnly { get; set; }

    /// <summary>Whether a user toggle adjusts <see cref="Count"/> by one.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool AutoUpdateCount { get; set; }

    /// <summary>Identifies the <see cref="Count"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> CountProperty = AvaloniaProperty.Register<AlderEmojiReactionButton, int>(
        nameof(Count), defaultBindingMode: BindingMode.TwoWay, coerce: (_, count) => Math.Max(0, count));

    /// <summary>The number of reactions represented by the button.</summary>
    public int Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    private TextBlock? countText;
    private TextBlock? emojiText;
    private AlderNumberStepValue? countMotion;
    private bool replacingDataContext;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        countMotion?.Stop();
        base.OnApplyTemplate(e);
        countText = e.NameScope.Find<TextBlock>("PART_Count");
        emojiText = e.NameScope.Find<TextBlock>("PART_Emoji");
        countMotion = e.NameScope.Find<AlderNumberStepValue>("PART_CountMotion");
    }

    /// <inheritdoc />
    protected override void OnClick()
    {
        if (IsEffectivelyEnabled && !IsReadOnly) base.OnClick();
    }

    /// <inheritdoc />
    protected override void Toggle()
    {
        if (!IsEffectivelyEnabled || IsReadOnly) return;
        bool select = IsChecked != true;
        SetCurrentValue(IsCheckedProperty, select);
        if (AutoUpdateCount)
            SetCurrentValue(CountProperty, select ? (int)Math.Min((long)Count + 1, int.MaxValue) : Count - 1);
        if (select && !AlderMotion.GetReduced(this) && emojiText is { } text &&
            ElementComposition.GetElementVisual(text) is { } visual)
        {
            visual.CenterPoint = new Vector3D(text.Bounds.Width / 2, text.Bounds.Height / 2, 0);
            var bounce = visual.Compositor.CreateVector3DKeyFrameAnimation();
            bounce.Duration = TimeSpan.FromMilliseconds(320);
            bounce.InsertKeyFrame(0, new Vector3D(.87, .87, 1));
            bounce.InsertKeyFrame(.37f, new Vector3D(1.22, 1.22, 1), new CubicEaseOut());
            bounce.InsertKeyFrame(.74f, new Vector3D(.97, .97, 1));
            bounce.InsertKeyFrame(1, new Vector3D(1, 1, 1));
            visual.StartAnimation(nameof(CompositionVisual.Scale), bounce);
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CountProperty && countText is { } text && this.IsAttachedToVisualTree() &&
            !replacingDataContext && !AlderMotion.GetReduced(this))
        {
            var values = change.GetOldAndNewValue<int>();
            countMotion?.Roll(text, values.oldValue.ToString(CultureInfo.CurrentCulture),
                values.newValue.ToString(CultureInfo.CurrentCulture), values.newValue > values.oldValue ? 1 : -1);
        }
        else if (change.Property == AlderMotion.ReducedProperty && AlderMotion.GetReduced(this))
        {
            countMotion?.Stop();
            if (emojiText is { } glyph) OverlayMotion.Reset(glyph);
        }
    }

    /// <inheritdoc />
    protected override void OnDataContextBeginUpdate()
    {
        replacingDataContext = true;
        countMotion?.Stop();
        base.OnDataContextBeginUpdate();
    }

    /// <inheritdoc />
    protected override void OnDataContextEndUpdate()
    {
        base.OnDataContextEndUpdate();
        replacingDataContext = false;
    }
}
