using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Alder.Avalonia.Controls;

/// <summary>One of the icon hover animations available for expandable actions.</summary>
public enum AlderActionIconMotion
{
    /// <summary>Leaves the icon still.</summary>
    None,
    /// <summary>Moves the icon diagonally upward and back.</summary>
    Send,
    /// <summary>Shifts the icon diagonally in both directions.</summary>
    Copy,
    /// <summary>Moves the icon down and back.</summary>
    Download,
    /// <summary>Compresses the icon and restores its size.</summary>
    Archive,
    /// <summary>Rings the icon around its top edge.</summary>
    Bell,
    /// <summary>Rotates the icon by a quarter turn.</summary>
    Settings
}

/// <summary>A native command button with an icon and a label shown by its expandable action bar.</summary>
public sealed class AlderExpandableActionBarItem : Button
{
    /// <summary>Identifies the icon geometry.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<AlderExpandableActionBarItem, object?>(nameof(Icon));
    /// <summary>Identifies the optional badge text.</summary>
    public static readonly StyledProperty<string?> BadgeProperty =
        AvaloniaProperty.Register<AlderExpandableActionBarItem, string?>(nameof(Badge));
    /// <summary>Identifies the optional shortcut hint.</summary>
    public static readonly StyledProperty<string?> ShortcutProperty =
        AvaloniaProperty.Register<AlderExpandableActionBarItem, string?>(nameof(Shortcut));
    /// <summary>Identifies the icon's motion preset.</summary>
    public static readonly StyledProperty<AlderActionIconMotion> IconMotionProperty =
        AvaloniaProperty.Register<AlderExpandableActionBarItem, AlderActionIconMotion>(nameof(IconMotion));

    /// <summary>Gets or sets the icon used in both the collapsed and expanded states.</summary>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    /// <summary>Gets or sets the optional badge shown next to the action.</summary>
    public string? Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
    /// <summary>Gets or sets the shortcut hint displayed with the label. Use a key binding to register the shortcut.</summary>
    public string? Shortcut { get => GetValue(ShortcutProperty); set => SetValue(ShortcutProperty, value); }
    /// <summary>Gets or sets the one-shot motion played when a hovering pointer enters the button.</summary>
    public AlderActionIconMotion IconMotion { get => GetValue(IconMotionProperty); set => SetValue(IconMotionProperty, value); }

    /// <summary>Identifies the size preset inherited from the enclosing action bar.</summary>
    public static readonly StyledProperty<AlderExpandableActionBarSize> SizeProperty =
        AlderExpandableActionBar.SizeProperty.AddOwner<AlderExpandableActionBarItem>();
    /// <summary>Gets or sets the size preset for this action.</summary>
    public AlderExpandableActionBarSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    private Control? icon;
    private readonly MatrixTransform iconTransform = new();
    private int motionGeneration;

    internal void SetHighlighted(bool value) => PseudoClasses.Set(":highlighted", value);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        StopIconMotion();
        base.OnApplyTemplate(e);
        icon = e.NameScope.Find<Control>("PART_Icon");
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Touch || !IsEffectivelyEnabled) return;
        (ItemsControl.ItemsControlFromItemContainer(this) as AlderExpandableActionBar)?.SetHovered(this);
        if (!AlderMotion.GetReduced(this) && icon is { } target && TopLevel.GetTopLevel(this) is { } top &&
            Motions.TryGetValue(IconMotion, out var motion))
            PlayIconMotion(target, top, motion);
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type == PointerType.Touch) return;
        StopIconMotion();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch && IsEffectivelyEnabled &&
            ItemsControl.ItemsControlFromItemContainer(this) is AlderExpandableActionBar owner && owner.RevealFromTouch(this))
        {
            e.Handled = true;
            return;
        }
        base.OnPointerPressed(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        bool disabled = change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled;
        if (disabled)
            (ItemsControl.ItemsControlFromItemContainer(this) as AlderExpandableActionBar)?.ClearHovered(this);
        if (disabled || change.Property == AlderMotion.ReducedProperty && AlderMotion.GetReduced(this) ||
            change.Property == IconMotionProperty)
            StopIconMotion();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopIconMotion();
        base.OnDetachedFromVisualTree(e);
    }

    private void PlayIconMotion(Control target, TopLevel top, IconSequence motion)
    {
        StopIconMotion();
        int generation = motionGeneration;
        long started = Stopwatch.GetTimestamp();
        target.RenderTransformOrigin = IconMotion == AlderActionIconMotion.Bell
            ? new RelativePoint(.5, 0, RelativeUnit.Relative) : RelativePoint.Center;
        iconTransform.Matrix = Matrix.Identity;
        target.RenderTransform = iconTransform;
        top.RequestAnimationFrame(Advance);

        void Advance(TimeSpan _)
        {
            if (generation != motionGeneration) return;
            double progress = Stopwatch.GetElapsedTime(started).TotalMilliseconds / motion.Milliseconds;
            if (progress >= 1)
            {
                target.ClearValue(RenderTransformProperty);
                return;
            }
            iconTransform.Matrix = motion.At(progress);
            top.RequestAnimationFrame(Advance);
        }
    }

    private void StopIconMotion()
    {
        motionGeneration++;
        icon?.ClearValue(RenderTransformProperty);
    }

    private static readonly SplineEasing IconEase = new(.16, 1, .3, 1);
    private static readonly Dictionary<AlderActionIconMotion, IconSequence> Motions = new()
    {
        [AlderActionIconMotion.Send] = new(520, [new(0), new(.45, X: 2, Y: -2), new(1)]),
        [AlderActionIconMotion.Copy] = new(520, [new(0), new(.4, X: 1.5, Y: -1.5), new(.7, X: -1, Y: 1), new(1)]),
        [AlderActionIconMotion.Download] = new(540, [new(0), new(.42, Y: 2.5), new(1)]),
        [AlderActionIconMotion.Archive] = new(520, [new(0), new(.45, Scale: .9), new(1)]),
        [AlderActionIconMotion.Bell] = new(680,
            [new(0), new(.18, Angle: 14), new(.36, Angle: -12), new(.54, Angle: 8), new(.72, Angle: -5), new(1)]),
        [AlderActionIconMotion.Settings] = new(720, [new(0), new(1, Angle: 90)])
    };

    private readonly record struct IconFrame(double Offset, double X = 0, double Y = 0, double Scale = 1, double Angle = 0);

    private sealed record IconSequence(int Milliseconds, IconFrame[] Frames)
    {
        public Matrix At(double progress)
        {
            for (int index = 1; index < Frames.Length; index++)
            {
                var to = Frames[index];
                if (progress > to.Offset) continue;
                var from = Frames[index - 1];
                double mix = IconEase.Ease((progress - from.Offset) / (to.Offset - from.Offset));
                double scale = Lerp(from.Scale, to.Scale, mix);
                return Matrix.CreateScale(scale, scale)
                    * Matrix.CreateRotation(Lerp(from.Angle, to.Angle, mix) * Math.PI / 180)
                    * Matrix.CreateTranslation(Lerp(from.X, to.X, mix), Lerp(from.Y, to.Y, mix));
            }
            return Matrix.Identity;
        }

        private static double Lerp(double from, double to, double progress) => from + (to - from) * progress;
    }
}
