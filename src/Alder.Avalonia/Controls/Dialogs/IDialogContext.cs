namespace Alder.Avalonia.Controls;

/// <summary>Lets a view model request that its dialog or <see cref="AlderSalashWindow"/> close with a result.</summary>
public interface IDialogContext
{
    /// <summary>Raised by the dialog content to request dismissal with a result.</summary>
    event EventHandler<DialogResult>? RequestClose;
}
