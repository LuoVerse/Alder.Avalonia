using System.Windows.Input;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Validates related controls and delegates submission to an application command.</summary>
public sealed class AlderForm : ContentControl
{
    /// <summary>Identifies the <see cref="SubmitCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> SubmitCommandProperty = AvaloniaProperty.Register<AlderForm, ICommand?>(nameof(SubmitCommand));
    /// <summary>Identifies the <see cref="SubmitCommandParameter"/> property.</summary>
    public static readonly StyledProperty<object?> SubmitCommandParameterProperty = AvaloniaProperty.Register<AlderForm, object?>(nameof(SubmitCommandParameter));
    /// <summary>Identifies the <see cref="IsSubmitting"/> property.</summary>
    public static readonly StyledProperty<bool> IsSubmittingProperty = AvaloniaProperty.Register<AlderForm, bool>(nameof(IsSubmitting));

    private readonly FormSubmitCommand submit;
    private ICommand? observedCommand;
    private bool executing;

    /// <summary>Initializes a form with a validation-aware submit command.</summary>
    public AlderForm() => submit = new(this);

    /// <summary>Gets or sets the command invoked after the enabled fields pass validation.</summary>
    public ICommand? SubmitCommand { get => GetValue(SubmitCommandProperty); set => SetValue(SubmitCommandProperty, value); }
    /// <summary>Gets or sets the application parameter passed to <see cref="SubmitCommand"/>.</summary>
    public object? SubmitCommandParameter { get => GetValue(SubmitCommandParameterProperty); set => SetValue(SubmitCommandParameterProperty, value); }
    /// <summary>Gets or sets whether an asynchronous submission is still running. Bind it to the application's submission state.</summary>
    public bool IsSubmitting { get => GetValue(IsSubmittingProperty); set => SetValue(IsSubmittingProperty, value); }
    /// <summary>Gets the command for submit controls. It validates the form before invoking <see cref="SubmitCommand"/>.</summary>
    public ICommand Submit => submit;

    /// <summary>Validates the form and runs the submit command if allowed. Invalid submission moves focus to the first invalid field.</summary>
    public bool TrySubmit()
    {
        if (!CanSubmit() || SubmitCommand is not { } command)
            return false;

        executing = true;
        try
        {
            submit.Notify();
            Control? firstError = null;
            foreach (var control in EnumerateControls(this))
            {
                bool valid = control is AlderTextField textField ? textField.Validate() : !FormValidation.IsInvalid(control);
                if (!valid)
                    firstError ??= control;
            }

            if (firstError is not null)
            {
                FocusInvalid(firstError);
                return false;
            }

            var parameter = SubmitCommandParameter;
            if (IsSubmitting || !IsEffectivelyEnabled || SubmitCommand != command || !command.CanExecute(parameter))
                return false;

            command.Execute(parameter);
            return true;
        }
        finally
        {
            executing = false;
            submit.Notify();
        }
    }

    /// <summary>Clears the form's required-field checks. Bound values and application errors remain under the application's control.</summary>
    public void ResetValidation()
    {
        foreach (var field in EnumerateControls(this, enabledAndVisibleOnly: false).OfType<AlderTextField>())
            field.ResetValidation();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new FormPeer(this);

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        ObserveCommand(SubmitCommand);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        ObserveCommand(null);
        base.OnDetachedFromLogicalTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SubmitCommandProperty && ((ILogical)this).IsAttachedToLogicalTree)
            ObserveCommand(SubmitCommand);
        else if (change.Property == SubmitCommandProperty || change.Property == SubmitCommandParameterProperty ||
                 change.Property == IsSubmittingProperty || change.Property == IsEffectivelyEnabledProperty)
            submit?.Notify();
    }

    private bool CanSubmit() => !executing && !IsSubmitting && IsEffectivelyEnabled &&
        SubmitCommand?.CanExecute(SubmitCommandParameter) == true;

    private void ObserveCommand(ICommand? command)
    {
        if (observedCommand is not null) observedCommand.CanExecuteChanged -= OnCommandStateChanged;
        observedCommand = command;
        if (observedCommand is not null) observedCommand.CanExecuteChanged += OnCommandStateChanged;
        submit.Notify();
    }

    private void OnCommandStateChanged(object? sender, EventArgs e) => submit.Notify();

    private static IEnumerable<Control> EnumerateControls(Control parent, bool enabledAndVisibleOnly = true)
    {
        foreach (var child in parent.GetLogicalChildren().OfType<Control>())
        {
            if (child is AlderForm || (enabledAndVisibleOnly && (!child.IsVisible || !child.IsEffectivelyEnabled)))
                continue;

            yield return child;
            if (child is AlderTextField)
                continue;

            foreach (var descendant in EnumerateControls(child, enabledAndVisibleOnly))
                yield return descendant;
        }
    }

    private void FocusInvalid(Control invalid)
    {
        foreach (var expander in invalid.GetLogicalAncestors().TakeWhile(parent => parent != this).OfType<Expander>().Reverse())
            expander.SetCurrentValue(Expander.IsExpandedProperty, true);

        if (!TryFocus())
            Dispatcher.UIThread.Post(() => TryFocus(), DispatcherPriority.Loaded);

        bool TryFocus()
        {
            if (!this.IsLogicalAncestorOf(invalid) || !invalid.IsEffectivelyVisible || !invalid.IsEffectivelyEnabled ||
                !FormValidation.IsInvalid(invalid))
                return false;
            var focused = invalid is AlderTextField textField ? textField.FocusEditor() :
                invalid.Focus(NavigationMethod.Tab) || invalid.GetVisualDescendants().OfType<Control>().Any(control => control.Focus(NavigationMethod.Tab));
            if (focused)
                invalid.BringIntoView();
            return focused;
        }
    }

    private sealed class FormSubmitCommand(AlderForm owner) : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => owner.CanSubmit();
        public void Execute(object? parameter) => owner.TrySubmit();
        internal void Notify() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FormPeer(AlderForm owner) : ContentControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}
