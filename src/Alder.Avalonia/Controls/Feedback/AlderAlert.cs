using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Inline status message with optional custom content, indicator and actions.</summary>
public sealed class AlderAlert : ContentControl
{
    /// <summary>Identifies the <see cref="Title"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<AlderAlert, string?>(nameof(Title));
    /// <summary>Identifies the <see cref="Description"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<AlderAlert, string?>(nameof(Description));
    /// <summary>Identifies the <see cref="Color"/> Avalonia property.</summary>
    public static readonly StyledProperty<FeedbackColor> ColorProperty = AvaloniaProperty.Register<AlderAlert, FeedbackColor>(nameof(Color));
    /// <summary>Identifies the <see cref="Indicator"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> IndicatorProperty = AvaloniaProperty.Register<AlderAlert, object?>(nameof(Indicator));
    /// <summary>Identifies the <see cref="ShowIndicator"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowIndicatorProperty = AvaloniaProperty.Register<AlderAlert, bool>(nameof(ShowIndicator), true);
    /// <summary>Identifies the <see cref="Actions"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> ActionsProperty = AvaloniaProperty.Register<AlderAlert, object?>(nameof(Actions));
    /// <summary>Identifies the <see cref="BoxShadow"/> Avalonia property.</summary>
    public static readonly StyledProperty<BoxShadows> BoxShadowProperty = Border.BoxShadowProperty.AddOwner<AlderAlert>();

    /// <summary>The primary text displayed for the content.</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    /// <summary>The optional supporting text displayed with the content.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>The color applied to the control.</summary>
    public FeedbackColor Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    /// <summary>The optional indicator displayed alongside the main content.</summary>
    public object? Indicator { get => GetValue(IndicatorProperty); set => SetValue(IndicatorProperty, value); }
    /// <summary>Whether to show the indicator.</summary>
    public bool ShowIndicator { get => GetValue(ShowIndicatorProperty); set => SetValue(ShowIndicatorProperty, value); }
    /// <summary>The optional actions shown alongside the alert description.</summary>
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }
    /// <summary>The shadows drawn around the surface.</summary>
    public BoxShadows BoxShadow { get => GetValue(BoxShadowProperty); set => SetValue(BoxShadowProperty, value); }

    private AlertPeer? peer;

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => peer = new AlertPeer(this);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (peer is null) return;
        if (change.Property == TitleProperty)
            peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, GetMessage(change.OldValue as string, Description), GetMessage(Title, Description));
        else if (change.Property == DescriptionProperty)
            peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, GetMessage(Title, change.OldValue as string), GetMessage(Title, Description));
    }

    private static string? GetMessage(string? title, string? description) =>
        string.IsNullOrEmpty(title) ? description : string.IsNullOrEmpty(description) ? title : title + " " + description;

    private sealed class AlertPeer(AlderAlert owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string? GetNameCore() => base.GetNameCore() ?? GetMessage(owner.Title, owner.Description);
    }
}
