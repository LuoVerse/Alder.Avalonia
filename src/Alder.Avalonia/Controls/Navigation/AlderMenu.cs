using Avalonia.Controls;

namespace Alder.Avalonia.Controls;

/// <summary>A desktop menu bar that uses Alder menu items and the native menu interaction model.</summary>
public sealed class AlderMenu : Menu
{
    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new AlderMenuItem();
}
