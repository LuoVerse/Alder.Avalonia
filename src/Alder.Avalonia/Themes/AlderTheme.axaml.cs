using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Alder.Avalonia.Themes;

/// <summary>Loads the shared resources and control themes that style Alder and supported native controls.</summary>
/// <remarks>
/// Add this style after the application's base theme. Override semantic resources in the host's theme dictionaries
/// to customize a window or subtree while retaining the component templates.
/// </remarks>
public partial class AlderTheme : Styles
{
    /// <summary>Initializes a new instance of <see cref="AlderTheme"/>.</summary>
    public AlderTheme() => AvaloniaXamlLoader.Load(this);
}
