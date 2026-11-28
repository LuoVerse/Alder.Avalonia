using Avalonia;
using Avalonia.Controls;

namespace Alder.Avalonia.Controls;

internal static class FormValidation
{
    internal static bool IsInvalid(Control control) =>
        control is not Label && (control is AlderTextField { HasValidationError: true } or AlderCheckBox { IsInvalid: true } ||
            DataValidationErrors.GetHasErrors(control) || AlderLabel.GetIsInvalid(control) ||
            (AvaloniaPropertyRegistry.Instance.IsRegistered(control, AlderInput.IsInvalidProperty) && control.GetValue(AlderInput.IsInvalidProperty)));

    internal static string? GetErrorText(Control control)
    {
        if (DataValidationErrors.GetErrors(control) is not { } errors)
            return null;

        var messages = string.Join(Environment.NewLine, errors
            .Select(error => error is Exception exception ? exception.Message : error?.ToString())
            .Where(message => !string.IsNullOrWhiteSpace(message)));
        return messages.Length == 0 ? null : messages;
    }
}
