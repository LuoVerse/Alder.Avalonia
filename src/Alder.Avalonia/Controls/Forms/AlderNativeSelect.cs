using Avalonia;
using Avalonia.Controls;

namespace Alder.Avalonia.Controls;

/// <summary>Presents a simple single-choice field with an Avalonia popup that follows the field's theme.</summary>
public sealed class AlderNativeSelect : AlderSelect
{
    static AlderNativeSelect() => SelectionModeProperty.OverrideMetadata<AlderNativeSelect>(
        new StyledPropertyMetadata<SelectionMode>(SelectionMode.Single, coerce: (_, _) => SelectionMode.Single));
}
