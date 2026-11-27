using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>An anchored color editor with live updates and an optional explicit commit.</summary>
public class AlderColorPicker : TemplatedControl
{
    /// <summary>Identifies the <see cref="Value"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderColorValue> ValueProperty =
        AvaloniaProperty.Register<AlderColorPicker, AlderColorValue>(nameof(Value), AlderColorValue.Default, defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="DraftValue"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderColorValue> DraftValueProperty =
        AvaloniaProperty.Register<AlderColorPicker, AlderColorValue>(nameof(DraftValue), AlderColorValue.Default, defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="IsReadOnly"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<AlderColorPicker, bool>(nameof(IsReadOnly));
    /// <summary>Identifies the <see cref="ShowAlpha"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowAlphaProperty =
        AvaloniaProperty.Register<AlderColorPicker, bool>(nameof(ShowAlpha));
    /// <summary>Identifies the <see cref="ShowField"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowFieldProperty =
        AvaloniaProperty.Register<AlderColorPicker, bool>(nameof(ShowField));
    /// <summary>Identifies the <see cref="CommitOnApply"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> CommitOnApplyProperty =
        AvaloniaProperty.Register<AlderColorPicker, bool>(nameof(CommitOnApply));
    /// <summary>Identifies the <see cref="Label"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AlderColorPicker, string?>(nameof(Label));
    /// <summary>Identifies the <see cref="Placement"/> Avalonia property.</summary>
    public static readonly StyledProperty<PlacementMode> PlacementProperty =
        AvaloniaProperty.Register<AlderColorPicker, PlacementMode>(nameof(Placement), PlacementMode.BottomEdgeAlignedLeft);
    /// <summary>Identifies the <see cref="PopupTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> PopupTemplateProperty =
        AvaloniaProperty.Register<AlderColorPicker, IDataTemplate?>(nameof(PopupTemplate));
    /// <summary>Identifies the <see cref="PopupHeaderTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> PopupHeaderTemplateProperty =
        AvaloniaProperty.Register<AlderColorPicker, IDataTemplate?>(nameof(PopupHeaderTemplate));
    /// <summary>Identifies the <see cref="IsOpen"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderColorPicker, bool> IsOpenProperty =
        AvaloniaProperty.RegisterDirect<AlderColorPicker, bool>(nameof(IsOpen), picker => picker.IsOpen);

    /// <summary>The committed color shared with the caller.</summary>
    /// <remarks>Bind this property to persist the selection. With live editing it updates as the draft changes.</remarks>
    public AlderColorValue Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <summary>The color currently being edited in the open picker.</summary>
    /// <remarks>The draft starts from <see cref="Value"/> on each open. Custom popup editors can bind to this property.</remarks>
    public AlderColorValue DraftValue { get => GetValue(DraftValueProperty); set => SetValue(DraftValueProperty, value); }
    /// <summary>Whether the value is protected from user edits.</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    /// <summary>Whether the picker exposes opacity editing.</summary>
    public bool ShowAlpha { get => GetValue(ShowAlphaProperty); set => SetValue(ShowAlphaProperty, value); }
    /// <summary>Whether a color text editor is visible in the picker.</summary>
    public bool ShowField { get => GetValue(ShowFieldProperty); set => SetValue(ShowFieldProperty, value); }
    /// <summary>Publishes editor changes only when Apply is invoked.</summary>
    /// <remarks>The default is live editing. Set this to true when the consumer needs to stage or discard edits.</remarks>
    public bool CommitOnApply { get => GetValue(CommitOnApplyProperty); set => SetValue(CommitOnApplyProperty, value); }
    /// <summary>The text that identifies the control or value.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>The requested position of the color editor relative to the trigger.</summary>
    public PlacementMode Placement { get => GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }
    /// <summary>A template with the picker as its data context. Bind editors to DraftValue.</summary>
    public IDataTemplate? PopupTemplate { get => GetValue(PopupTemplateProperty); set => SetValue(PopupTemplateProperty, value); }
    /// <summary>Optional content before the standard editor, with this picker as the data context.</summary>
    public IDataTemplate? PopupHeaderTemplate { get => GetValue(PopupHeaderTemplateProperty); set => SetValue(PopupHeaderTemplateProperty, value); }
    /// <summary>Whether the surface is open.</summary>
    public bool IsOpen => isOpen;

    private AlderColorPickerFlyout? flyout;
    private Button? trigger;
    private AlderColorField? field;
    private Control? pane;
    private bool isOpen;
    private bool syncing;
    private NavigationMethod focusReturnMethod;
    private bool restoreFocus;

    /// <summary>Opens the picker and initializes its draft from the committed value.</summary>
    public void Open()
    {
        if (trigger is not { } anchor || flyout is not { } editor || IsReadOnly ||
            !IsEffectivelyEnabled || !this.IsAttachedToVisualTree()) return;
        if (editor.IsClosing) editor.CloseImmediately();
        else if (IsOpen) return;

        ResetDraft();
        restoreFocus = false;
        editor.Placement = Placement;
        editor.HorizontalOffset = Placement switch
        {
            PlacementMode.Left or PlacementMode.LeftEdgeAlignedTop or PlacementMode.LeftEdgeAlignedBottom => -8,
            PlacementMode.Right or PlacementMode.RightEdgeAlignedTop or PlacementMode.RightEdgeAlignedBottom => 8,
            _ => 0
        };
        editor.VerticalOffset = Placement switch
        {
            PlacementMode.Top or PlacementMode.TopEdgeAlignedLeft or PlacementMode.TopEdgeAlignedRight => -8,
            PlacementMode.Bottom or PlacementMode.BottomEdgeAlignedLeft or PlacementMode.BottomEdgeAlignedRight => 8,
            _ => 0
        };
        if (UseFlipSafePlacement && Placement is PlacementMode.BottomEdgeAlignedRight or PlacementMode.TopEdgeAlignedRight)
            editor.AlignToRightEdge(Placement == PlacementMode.BottomEdgeAlignedRight);
        editor.ShowAt(anchor);
    }

    internal virtual bool UseFlipSafePlacement => false;

    /// <summary>Commits the draft and closes the picker if the color field is valid.</summary>
    public bool Apply() => Apply(NavigationMethod.Tab);

    private bool Apply(NavigationMethod focusMethod)
    {
        if (!IsOpen || IsReadOnly || !IsEffectivelyEnabled || flyout?.IsClosing == true) return false;
        if (field is { IsVisible: true } input && !input.Commit())
        {
            input.Focus();
            return false;
        }

        PublishDraft();
        Cancel(focusMethod);
        return true;
    }

    /// <summary>Closes the picker and discards its uncommitted draft.</summary>
    /// <remarks>Values already published by live editing remain committed.</remarks>
    public void Cancel() => Cancel(NavigationMethod.Tab);

    private void Cancel(NavigationMethod focusMethod)
    {
        if (!IsOpen) return;
        restoreFocus = true;
        focusReturnMethod = focusMethod;
        flyout?.Hide();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (trigger is not null) trigger.Click -= OnTrigger;
        base.OnApplyTemplate(e);
        trigger = e.NameScope.Find<Button>("PART_Trigger");
        if (trigger is not null) trigger.Click += OnTrigger;
        BuildFlyout();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        restoreFocus = false;
        flyout?.CloseImmediately();
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty && !syncing)
            ResetDraft();
        else if (change.Property == DraftValueProperty && !syncing && !CommitOnApply &&
                 IsOpen && flyout?.IsClosing == false)
            PublishDraft();
        else if (change.Property == PopupTemplateProperty && trigger is not null)
            BuildFlyout();
        else if ((change.Property == IsReadOnlyProperty && IsReadOnly) ||
                 (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled))
        {
            restoreFocus = false;
            flyout?.CloseImmediately();
        }
    }

    private void BuildFlyout()
    {
        if (flyout is not null)
        {
            restoreFocus = false;
            flyout.CloseImmediately();
            flyout.Opened -= OnOpened;
            flyout.Closing -= OnClosing;
            flyout.Closed -= OnClosed;
        }
        if (pane is not null)
        {
            if (FindPart<Button>("PART_Apply") is { } oldApply) oldApply.Click -= OnApplyClick;
            if (FindPart<Button>("PART_Cancel") is { } oldCancel) oldCancel.Click -= OnCancelClick;
            pane.RemoveHandler(KeyDownEvent, OnPaneKeyDown);
            pane.DataContext = null;
        }

        flyout = null;
        pane = trigger is null ? null : PopupTemplate?.Build(this);
        field = FindPart<AlderColorField>("PART_Field");
        if (pane is null) return;
        pane.DataContext = this;
        if (FindPart<Button>("PART_Apply") is { } apply) apply.Click += OnApplyClick;
        if (FindPart<Button>("PART_Cancel") is { } cancel) cancel.Click += OnCancelClick;
        pane.AddHandler(KeyDownEvent, OnPaneKeyDown, RoutingStrategies.Tunnel);
        flyout = new AlderColorPickerFlyout(pane) { OverlayInputPassThroughElement = trigger };
        flyout.Opened += OnOpened;
        flyout.Closing += OnClosing;
        flyout.Closed += OnClosed;

        // Build the editor's control templates before the first pointer press, without opening a popup.
        var presenter = pane;
        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(pane, presenter) && !IsOpen && !IsReadOnly && this.IsAttachedToVisualTree())
                presenter.Measure(Size.Infinity);
        }, DispatcherPriority.Background);
    }

    private T? FindPart<T>(string name) where T : Control =>
        pane?.GetLogicalDescendants().OfType<T>().FirstOrDefault(part => part.Name == name);

    private void OnTrigger(object? sender, RoutedEventArgs e)
    {
        if (IsOpen && flyout?.IsClosing != true) Cancel(FocusMethod(sender));
        else Open();
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e) => Apply(FocusMethod(sender));
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Cancel(FocusMethod(sender));

    private static NavigationMethod FocusMethod(object? sender) =>
        sender is InputElement element && element.Classes.Contains(":focus-visible")
            ? NavigationMethod.Tab
            : NavigationMethod.Pointer;

    private void OnPaneKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        Cancel();
    }

    private void OnOpened(object? sender, EventArgs e) => SetAndRaise(IsOpenProperty, ref isOpen, true);

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (e.Cancel) return;
        if (restoreFocus && trigger is { } anchor && anchor.IsAttachedToVisualTree())
            anchor.Focus(focusReturnMethod);
        if (!CommitOnApply && !restoreFocus && flyout?.IsClosing == false && field is { IsVisible: true } input)
            input.Commit();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        SetAndRaise(IsOpenProperty, ref isOpen, false);
        ResetDraft();
        restoreFocus = false;
    }

    private void PublishDraft()
    {
        syncing = true;
        try { SetCurrentValue(ValueProperty, !CommitOnApply || ShowAlpha ? DraftValue : DraftValue.With(3, Value.Alpha)); }
        finally { syncing = false; }
    }

    private void ResetDraft()
    {
        syncing = true;
        try
        {
            SetCurrentValue(DraftValueProperty, Value);
            field?.ResetInput();
        }
        finally { syncing = false; }
    }
}
