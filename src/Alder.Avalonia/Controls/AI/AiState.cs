namespace Alder.Avalonia.Controls;

/// <summary>Lifecycle of a tool or a host-provided progress trace.</summary>
public enum AiRunStatus
{
    /// <summary>Waiting for input or permission.</summary>
    Pending,
    /// <summary>Receiving new events.</summary>
    Running,
    /// <summary>Finished normally.</summary>
    Completed,
    /// <summary>Ended with an error.</summary>
    Failed,
    /// <summary>Stopped by the caller.</summary>
    Stopped
}

/// <summary>Response to a single approval request.</summary>
public enum AiDecision
{
    /// <summary>Awaiting a response.</summary>
    Pending,
    /// <summary>Accepted by the user.</summary>
    Approved,
    /// <summary>Declined by the user.</summary>
    Rejected
}

/// <summary>Surface and layout options for a chat composer.</summary>
public enum AiPromptVariant
{
    /// <summary>Multiline draft on the main surface.</summary>
    Primary,
    /// <summary>Multiline draft on a muted surface.</summary>
    Secondary,
    /// <summary>Multiline draft with tighter insets.</summary>
    Compact,
    /// <summary>Draft and actions on a single toolbar row.</summary>
    Inline
}
