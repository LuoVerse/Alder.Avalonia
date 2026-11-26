using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;
using System.Windows.Input;

namespace Alder.Avalonia.Controls;

/// <summary>Presents an approval decision and dispatches at most one response per pending request.</summary>
public sealed partial class AlderApprovalCard : HeaderedContentControl
{
    /// <summary>The shadow painted outside the approval card.</summary>
    [GeneratedStyledProperty] public partial BoxShadows BoxShadow { get; set; }

    /// <summary>Receives the approved request.</summary>
    [GeneratedStyledProperty]
    public partial ICommand? ApproveCommand { get; set; }

    /// <summary>Receives the rejected request.</summary>
    [GeneratedStyledProperty]
    public partial ICommand? RejectCommand { get; set; }

    /// <summary>Request identity passed to the decision handler.</summary>
    [GeneratedStyledProperty]
    public partial object? CommandParameter { get; set; }

    /// <summary>Current decision. Set Pending when presenting a new request.</summary>
    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial AiDecision Decision { get; set; }

    /// <summary>Label for the positive action.</summary>
    [GeneratedStyledProperty]
    public partial string? ApproveLabel { get; set; }

    /// <summary>Label for the negative action.</summary>
    [GeneratedStyledProperty]
    public partial string? RejectLabel { get; set; }

    /// <summary>The visual emphasis of the negative action.</summary>
    [GeneratedStyledProperty(DefaultValue = ButtonVariant.Ghost)]
    public partial ButtonVariant RejectVariant { get; set; }

    /// <summary>Optional step counter or persistent choice at the leading edge of the footer.</summary>
    [GeneratedStyledProperty] public partial object? Footer { get; set; }

    /// <summary>Whether the current step has enough information to confirm.</summary>
    [GeneratedStyledProperty(DefaultValue = true)] public partial bool CanApprove { get; set; }

    /// <summary>Optional dismissal action, independent of the decision.</summary>
    [GeneratedStyledProperty] public partial ICommand? DismissCommand { get; set; }

    private Button? approve;
    private Button? reject;
    private ContentPresenter? title;
    private ContentPresenter? body;
    private static readonly SplineEasing QuestionEase = new(.22, 1, .36, 1);

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (approve is not null) approve.Click -= Choose;
        if (reject is not null) reject.Click -= Choose;
        base.OnApplyTemplate(e);
        approve = e.NameScope.Find<Button>("PART_Approve");
        reject = e.NameScope.Find<Button>("PART_Reject");
        title = e.NameScope.Find<ContentPresenter>("ApprovalTitle");
        body = e.NameScope.Find<ContentPresenter>("ApprovalContent");
        if (approve is not null) approve.Click += Choose;
        if (reject is not null) reject.Click += Choose;
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AlderMotion.ReducedProperty && AlderMotion.GetReduced(this))
        {
            if (title is not null) OverlayMotion.Reset(title);
            if (body is not null) OverlayMotion.Reset(body);
        }
        if (change.Property != HeaderProperty || change.OldValue is not string previous || Header is not string current ||
            previous == current || title is null || body is null || !title.IsVisible || AlderMotion.GetReduced(this))
            return;

        var duration = TimeSpan.FromMilliseconds(200);
        var offset = new Vector3D(0, 4, 0);
        OverlayMotion.Animate(title, true, offset, 1, duration, duration, default, QuestionEase);
        OverlayMotion.Animate(body, true, offset, 1, duration, duration, default, QuestionEase);
    }

    private void Choose(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Respond(ReferenceEquals(sender, approve));
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(global::Avalonia.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || Decision != AiDecision.Pending) return;
        bool confirm = e.Key == global::Avalonia.Input.Key.Enter &&
            (e.KeyModifiers is global::Avalonia.Input.KeyModifiers.Meta or global::Avalonia.Input.KeyModifiers.Control);
        if (!confirm && e.Key != global::Avalonia.Input.Key.Escape) return;
        if (!confirm && DismissCommand is { } dismiss && dismiss.CanExecute(null)) dismiss.Execute(null);
        else Respond(confirm);
        e.Handled = true;
    }

    private void Respond(bool accepted)
    {
        if (Decision != AiDecision.Pending) return;
        if (accepted && !CanApprove) return;
        var command = accepted ? ApproveCommand : RejectCommand;
        var parameter = CommandParameter;
        if (command?.CanExecute(parameter) == false) return;
        SetCurrentValue(DecisionProperty, accepted ? AiDecision.Approved : AiDecision.Rejected);
        command?.Execute(parameter);
    }
}
