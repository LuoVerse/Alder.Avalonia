using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace Alder.Avalonia.Controls;

/// <summary>Groups related fields with an accessible legend, supporting text, and optional actions.</summary>
public sealed class AlderFieldset : ContentControl
{
    /// <summary>Identifies the <see cref="Legend"/> property.</summary>
    public static readonly StyledProperty<string?> LegendProperty = AvaloniaProperty.Register<AlderFieldset, string?>(nameof(Legend));
    /// <summary>Identifies the <see cref="Description"/> property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty = AlderTextField.DescriptionProperty.AddOwner<AlderFieldset>();
    /// <summary>Identifies the <see cref="Actions"/> property.</summary>
    public static readonly StyledProperty<object?> ActionsProperty = AvaloniaProperty.Register<AlderFieldset, object?>(nameof(Actions));

    /// <summary>Gets or sets the heading and accessible name of the group.</summary>
    public string? Legend { get => GetValue(LegendProperty); set => SetValue(LegendProperty, value); }
    /// <summary>Gets or sets the optional explanation below the legend.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>Gets or sets the optional content displayed after the fields.</summary>
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new FieldsetPeer(this);

    private sealed class FieldsetPeer(AlderFieldset owner) : ContentControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.Legend;
        protected override string? GetHelpTextCore() => base.GetHelpTextCore() is { Length: > 0 } help ? help : owner.Description;
    }
}
