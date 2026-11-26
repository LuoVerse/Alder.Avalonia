using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Windows.Input;

namespace Alder.Avalonia.Controls;

/// <summary>A keyboard-navigable toolbar of host-provided message actions.</summary>
public sealed partial class AlderChatMessageActions : ContentControl
{
    /// <summary>Copies the message.</summary>
    [GeneratedStyledProperty]
    public partial ICommand? CopyCommand { get; set; }

    /// <summary>Marks the response as helpful.</summary>
    [GeneratedStyledProperty]
    public partial ICommand? LikeCommand { get; set; }

    /// <summary>Marks the response as unhelpful.</summary>
    [GeneratedStyledProperty]
    public partial ICommand? DislikeCommand { get; set; }

    /// <summary>Requests a new response.</summary>
    [GeneratedStyledProperty]
    public partial ICommand? RetryCommand { get; set; }

    /// <summary>The message passed to each action.</summary>
    [GeneratedStyledProperty]
    public partial object? CommandParameter { get; set; }

    /// <summary>Optional replacement for the copy glyph.</summary>
    [GeneratedStyledProperty] public partial Geometry? CopyIcon { get; set; }

    /// <summary>Optional replacement for the helpful-response glyph.</summary>
    [GeneratedStyledProperty] public partial Geometry? LikeIcon { get; set; }

    /// <summary>Positive, negative, or no rating on this response.</summary>
    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay)] public partial bool? IsHelpful { get; set; }

    private DispatcherTimer? copied;

    /// <summary>Updates transient copy feedback and the selected rating after activation.</summary>
    public AlderChatMessageActions() => AddHandler(Button.ClickEvent, OnAction, handledEventsToo: true);

    private void OnAction(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button button || button.TemplatedParent != this) return;
        switch (button.Name)
        {
            case "Like": SetCurrentValue(IsHelpfulProperty, IsHelpful == true ? null : true); break;
            case "Dislike": SetCurrentValue(IsHelpfulProperty, IsHelpful == false ? null : false); break;
            case "Copy":
                PseudoClasses.Set(":copied", true);
                copied?.Stop();
                copied ??= new DispatcherTimer(TimeSpan.FromSeconds(1.5), DispatcherPriority.Background, (_, _) => ResetCopy());
                copied.Start();
                break;
        }
    }

    private void ResetCopy()
    {
        copied?.Stop();
        PseudoClasses.Set(":copied", false);
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ResetCopy();
        base.OnDetachedFromVisualTree(e);
    }
}
