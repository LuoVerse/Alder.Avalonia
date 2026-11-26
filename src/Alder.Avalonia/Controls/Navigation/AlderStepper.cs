using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Indicates whether a step is pending, active, or complete.</summary>
public enum AlderStepStatus
{
    /// <summary>Indicates a step that has not been reached.</summary>
    Inactive,
    /// <summary>Indicates the current step.</summary>
    Active,
    /// <summary>Indicates a completed step.</summary>
    Complete
}
/// <summary>Selects the default glyph used for steps without a custom icon.</summary>
public enum AlderStepperIndicator
{
    /// <summary>Displays the step number.</summary>
    Number,
    /// <summary>Displays a bullet in place of a number.</summary>
    Bullet
}

/// <summary>An ordered progress indicator with optional step navigation.</summary>
public sealed class AlderStepper : ItemsControl
{
    /// <summary>The zero-based current step. Use the item count to complete every step.</summary>
    public static readonly StyledProperty<int> ActiveStepProperty =
        AvaloniaProperty.Register<AlderStepper, int>(nameof(ActiveStep), defaultBindingMode: BindingMode.TwoWay,
            validate: value => value >= -1);
    /// <summary>Identifies the <see cref="Orientation"/> Avalonia property.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<AlderStepper, Orientation>(nameof(Orientation), Orientation.Horizontal);
    /// <summary>Identifies the <see cref="Size"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderSize> SizeProperty =
        AvaloniaProperty.Register<AlderStepper, AlderSize>(nameof(Size), AlderSize.Medium);
    /// <summary>Identifies the <see cref="IsInteractive"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsInteractiveProperty =
        AvaloniaProperty.Register<AlderStepper, bool>(nameof(IsInteractive), true);
    /// <summary>Identifies the <see cref="Indicator"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderStepperIndicator> IndicatorProperty =
        AvaloniaProperty.Register<AlderStepper, AlderStepperIndicator>(nameof(Indicator));
    /// <summary>Identifies the <see cref="AccentBrush"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<AlderStepper, IBrush?>(nameof(AccentBrush));
    /// <summary>Identifies the <see cref="OnAccentBrush"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> OnAccentBrushProperty =
        AvaloniaProperty.Register<AlderStepper, IBrush?>(nameof(OnAccentBrush));
    /// <summary>Identifies the <see cref="CompletedIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> CompletedIconProperty =
        AvaloniaProperty.Register<AlderStepper, Geometry?>(nameof(CompletedIcon));

    /// <summary>The zero-based index of the selected step.</summary>
    public int ActiveStep { get => GetValue(ActiveStepProperty); set => SetValue(ActiveStepProperty, value); }
    /// <summary>Whether the control is laid out horizontally or vertically.</summary>
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    /// <summary>The size preset used for the control.</summary>
    public AlderSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>Whether the user can activate another step.</summary>
    public bool IsInteractive { get => GetValue(IsInteractiveProperty); set => SetValue(IsInteractiveProperty, value); }
    /// <summary>The default glyph for steps that do not supply a custom icon.</summary>
    public AlderStepperIndicator Indicator { get => GetValue(IndicatorProperty); set => SetValue(IndicatorProperty, value); }
    /// <summary>The brush for the accented step indicator and connector.</summary>
    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    /// <summary>The foreground brush for content drawn over the accent brush.</summary>
    public IBrush? OnAccentBrush { get => GetValue(OnAccentBrushProperty); set => SetValue(OnAccentBrushProperty, value); }
    /// <summary>The geometry displayed inside a completed step's indicator.</summary>
    public Geometry? CompletedIcon { get => GetValue(CompletedIconProperty); set => SetValue(CompletedIconProperty, value); }

    /// <summary>Initializes a new instance of <see cref="AlderStepper"/>.</summary>
    public AlderStepper() => ItemsView.CollectionChanged += (_, _) => RefreshSteps();

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<AlderStepperStep>(item, out recycleKey);

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new AlderStepperStep();

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        ((AlderStepperStep)container).Update(this, index);
    }

    /// <inheritdoc />
    protected override void ContainerIndexChangedOverride(Control container, int oldIndex, int newIndex)
    {
        base.ContainerIndexChangedOverride(container, oldIndex, newIndex);
        ((AlderStepperStep)container).Update(this, newIndex);
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(Control container)
    {
        ((AlderStepperStep)container).Update(null, -1);
        base.ClearContainerForItemOverride(container);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActiveStepProperty || change.Property == OrientationProperty ||
            change.Property == SizeProperty || change.Property == IsInteractiveProperty ||
            change.Property == IndicatorProperty || change.Property == AccentBrushProperty ||
            change.Property == OnAccentBrushProperty || change.Property == CompletedIconProperty)
            RefreshSteps();
    }

    private void RefreshSteps()
    {
        foreach (var step in GetRealizedContainers().OfType<AlderStepperStep>())
            step.Update(this, IndexFromContainer(step));
    }
}

/// <summary>Represents a single step with an optional title, description, and icon.</summary>
public sealed class AlderStepperStep : Button
{
    /// <summary>Identifies the <see cref="Title"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AlderStepperStep, string?>(nameof(Title));
    /// <summary>Identifies the <see cref="Description"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AlderStepperStep, string?>(nameof(Description));
    /// <summary>Identifies the <see cref="Icon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<AlderStepperStep, Geometry?>(nameof(Icon));
    /// <summary>Identifies the <see cref="TrailingContent"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> TrailingContentProperty =
        AvaloniaProperty.Register<AlderStepperStep, object?>(nameof(TrailingContent));
    /// <summary>Identifies the <see cref="IndicatorTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> IndicatorTemplateProperty =
        AvaloniaProperty.Register<AlderStepperStep, IDataTemplate?>(nameof(IndicatorTemplate));
    /// <summary>Identifies the <see cref="Status"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderStepperStep, AlderStepStatus> StatusProperty =
        AvaloniaProperty.RegisterDirect<AlderStepperStep, AlderStepStatus>(nameof(Status), step => step.Status);
    /// <summary>Identifies the <see cref="Number"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderStepperStep, int> NumberProperty =
        AvaloniaProperty.RegisterDirect<AlderStepperStep, int>(nameof(Number), step => step.Number);
    internal static readonly DirectProperty<AlderStepperStep, Geometry?> DisplayIconProperty =
        AvaloniaProperty.RegisterDirect<AlderStepperStep, Geometry?>(nameof(DisplayIcon), step => step.DisplayIcon);
    internal static readonly DirectProperty<AlderStepperStep, IBrush?> StepAccentProperty =
        AvaloniaProperty.RegisterDirect<AlderStepperStep, IBrush?>(nameof(StepAccent), step => step.StepAccent);
    internal static readonly DirectProperty<AlderStepperStep, IBrush?> StepOnAccentProperty =
        AvaloniaProperty.RegisterDirect<AlderStepperStep, IBrush?>(nameof(StepOnAccent), step => step.StepOnAccent);

    private AlderStepper? owner;
    private Grid? stepLayout;

    /// <summary>The primary text displayed for the content.</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    /// <summary>The optional supporting text displayed with the content.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>The optional geometry displayed with the content.</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    /// <summary>Optional content aligned to the trailing edge of the step title.</summary>
    public object? TrailingContent { get => GetValue(TrailingContentProperty); set => SetValue(TrailingContentProperty, value); }
    /// <summary>An optional indicator template whose data is this step, including its current status.</summary>
    public IDataTemplate? IndicatorTemplate { get => GetValue(IndicatorTemplateProperty); set => SetValue(IndicatorTemplateProperty, value); }
    /// <summary>The state of the step relative to the active step.</summary>
    public AlderStepStatus Status { get => field; private set => SetAndRaise(StatusProperty, ref field, value); }
    /// <summary>The one-based number displayed for the step.</summary>
    public int Number { get => field; private set => SetAndRaise(NumberProperty, ref field, value); }
    internal Geometry? DisplayIcon { get => field; private set => SetAndRaise(DisplayIconProperty, ref field, value); }
    internal IBrush? StepAccent { get => field; private set => SetAndRaise(StepAccentProperty, ref field, value); }
    internal IBrush? StepOnAccent { get => field; private set => SetAndRaise(StepOnAccentProperty, ref field, value); }

    internal void Update(AlderStepper? stepper, int index)
    {
        owner = stepper;
        Number = index + 1;
        Status = stepper is null || index < 0 || index > stepper.ActiveStep ? AlderStepStatus.Inactive
            : index == stepper.ActiveStep ? AlderStepStatus.Active : AlderStepStatus.Complete;
        StepAccent = stepper?.AccentBrush;
        StepOnAccent = stepper?.OnAccentBrush;

        PseudoClasses.Set(":first", index <= 0);
        PseudoClasses.Set(":last", stepper is null || index >= stepper.ItemCount - 1);
        PseudoClasses.Set(":vertical", stepper?.Orientation == Orientation.Vertical);
        RefreshColumns();
        PseudoClasses.Set(":small", stepper?.Size == AlderSize.Small);
        PseudoClasses.Set(":large", stepper?.Size == AlderSize.Large);
        PseudoClasses.Set(":display-only", stepper is { IsInteractive: false });
        UpdateIndicator();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty || change.Property == IndicatorTemplateProperty)
            UpdateIndicator();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        stepLayout = e.NameScope.Find<Grid>("StepLayout");
        RefreshColumns();
    }

    private void RefreshColumns()
    {
        if (stepLayout is null) return;
        bool vertical = owner?.Orientation == Orientation.Vertical;
        if (stepLayout.ColumnDefinitions.Count != (vertical ? 2 : 3))
            stepLayout.ColumnDefinitions = new ColumnDefinitions(vertical ? "Auto,*" : "*,Auto,*");
    }

    /// <inheritdoc />
    protected override void OnClick()
    {
        if (owner is not { IsInteractive: true } stepper || !IsEffectivelyEnabled || Number < 1) return;
        stepper.SetCurrentValue(AlderStepper.ActiveStepProperty, Number - 1);
        base.OnClick();
    }

    private void UpdateIndicator()
    {
        bool bullet = owner?.Indicator == AlderStepperIndicator.Bullet && Icon is null;
        DisplayIcon = Status == AlderStepStatus.Complete && !bullet ? owner?.CompletedIcon : Icon;
        PseudoClasses.Set(":bullet", bullet);
        PseudoClasses.Set(":icon", DisplayIcon is not null);
        PseudoClasses.Set(":custom-indicator", IndicatorTemplate is not null);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new StepPeer(this);

    private sealed class StepPeer(AlderStepperStep step) : ButtonAutomationPeer(step)
    {
        protected override string? GetNameCore() => AutomationProperties.GetName(step) ?? step.Title ?? base.GetNameCore();
        protected override AutomationControlType GetAutomationControlTypeCore() =>
            step.owner is { IsInteractive: false } ? AutomationControlType.ListItem : AutomationControlType.Button;
    }
}


internal enum StepperRailSegment { Leading, Trailing, Vertical }

/// <summary>Draws one half of a horizontal connector, or a full vertical connector.</summary>
internal sealed class AlderStepperRail : Control
{
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<AlderStepperRail, IBrush?>(nameof(TrackBrush));
    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<AlderStepperRail, IBrush?>(nameof(FillBrush));
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<AlderStepperRail, double>(nameof(Progress));
    public static readonly StyledProperty<StepperRailSegment> SegmentProperty =
        AvaloniaProperty.Register<AlderStepperRail, StepperRailSegment>(nameof(Segment));

    static AlderStepperRail() => AffectsRender<AlderStepperRail>(
        TrackBrushProperty, FillBrushProperty, ProgressProperty, SegmentProperty);

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public StepperRailSegment Segment { get => GetValue(SegmentProperty); set => SetValue(SegmentProperty, value); }

    public override void Render(DrawingContext context)
    {
        var track = new Rect(Bounds.Size);
        context.DrawRectangle(TrackBrush, null, track);
        // Both halves receive the same progress so the fill crosses the join without a gap.
        var fraction = Math.Clamp(Segment switch
        {
            StepperRailSegment.Leading => Progress * 2 - 1,
            StepperRailSegment.Trailing => Progress * 2,
            _ => Progress
        }, 0, 1);
        if (fraction > 0)
            context.DrawRectangle(FillBrush, null, Segment == StepperRailSegment.Vertical
                ? track.WithHeight(track.Height * fraction)
                : track.WithWidth(track.Width * fraction));
    }
}
