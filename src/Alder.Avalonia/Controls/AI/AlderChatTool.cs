using System.Windows.Input;
using Avalonia;
using Alder.NativeMarkdown.UI.Extensions;
using Avalonia.Controls;

namespace Alder.Avalonia.Controls;

/// <summary>An expandable tool call with input, result and optional approval content.</summary>
public partial class AlderChatTool : AlderDisclosure
{
    /// <summary>Identifies the <see cref="DisplayTitle"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderChatTool, string> DisplayTitleProperty =
        AvaloniaProperty.RegisterDirect<AlderChatTool, string>(nameof(DisplayTitle), tool => tool.DisplayTitle);

    /// <summary>Shared compact JSON renderer for literal tool payloads, with no toolbar or collapsing.</summary>
    public static MarkdownServices PayloadServices { get; } = new()
    {
        Highlighter = AlderMarkdown.ChatServices.Highlighter,
        CodeBlocks = AlderMarkdown.ChatServices.CodeBlocks with { ShowHeader = false, VerticalInset = 6, HorizontalInset = 10, UseInsetForScrollbar = true }
    };

    /// <summary>Lifecycle state of this tool call.</summary>
    [GeneratedStyledProperty]
    public partial AiRunStatus Status { get; set; }

    /// <summary>Optional emphasized tool name following the trigger header.</summary>
    [GeneratedStyledProperty] public partial string? ToolName { get; set; }

    /// <summary>Optional caption above the input payload.</summary>
    [GeneratedStyledProperty] public partial string? ArgumentsLabel { get; set; }

    /// <summary>Optional caption above the successful output.</summary>
    [GeneratedStyledProperty] public partial string? ResultLabel { get; set; }

    /// <summary>Literal input payload.</summary>
    [GeneratedStyledProperty]
    public partial string? Arguments { get; set; }

    /// <summary>Literal result or error text.</summary>
    [GeneratedStyledProperty]
    public partial string? Result { get; set; }

    /// <summary>Optional approval card for this invocation.</summary>
    [GeneratedStyledProperty]
    public partial object? Approval { get; set; }

    /// <summary>Optional command shown in the default approval actions while the tool is pending.</summary>
    [GeneratedStyledProperty] public partial ICommand? ApproveCommand { get; set; }

    /// <summary>Optional rejection command shown in the default approval actions while the tool is pending.</summary>
    [GeneratedStyledProperty] public partial ICommand? RejectCommand { get; set; }

    /// <summary>Parameter supplied to the default approval commands.</summary>
    [GeneratedStyledProperty] public partial object? ApprovalCommandParameter { get; set; }

    /// <summary>The combined plain text label used for the trigger and its streaming highlight.</summary>
    public string DisplayTitle => displayTitle;

    private string displayTitle = "";

    /// <inheritdoc/>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        PseudoClasses.Set(":compact", e.NewSize.Width < 644);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeaderProperty || change.Property == ToolNameProperty)
        {
            string? header = Header as string;
            SetAndRaise(DisplayTitleProperty, ref displayTitle,
                string.IsNullOrEmpty(header) ? ToolName ?? "" :
                string.IsNullOrEmpty(ToolName) ? header : header + " " + ToolName);
        }
        if (change.Property == ArgumentsProperty || change.Property == ResultProperty ||
            change.Property == ApprovalProperty || change.Property == ContentProperty || change.Property == StatusProperty ||
            change.Property == ApproveCommandProperty || change.Property == RejectCommandProperty)
        {
            bool arguments = !string.IsNullOrEmpty(Arguments);
            bool result = !string.IsNullOrEmpty(Result);
            bool approval = Status == AiRunStatus.Pending && (Approval is not null || ApproveCommand is not null || RejectCommand is not null);
            PseudoClasses.Set(":has-arguments", arguments);
            PseudoClasses.Set(":has-result", result);
            PseudoClasses.Set(":requires-approval", approval);
            PseudoClasses.Set(":default-approval", approval && Approval is null);
            PseudoClasses.Set(":has-details", arguments || result || approval || Content is not null);
        }
    }
}

/// <summary>A disclosure for a group of consecutive tool calls.</summary>
public sealed class AlderChatToolGroup : AlderChatTool
{
    /// <summary>Creates a group without an individual tool status marker.</summary>
    public AlderChatToolGroup() => Classes.Add("tool-group");

    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(AlderChatTool);
}
