namespace Alder.Avalonia.Controls;

/// <summary>Lets a dialog's view model close its own dialog and return a result.</summary>
public interface IDialogContext
{
    /// <summary>Raised by the dialog content to request dismissal with a result.</summary>
    event EventHandler<DialogResult>? RequestClose;
}
