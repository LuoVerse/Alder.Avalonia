using Avalonia.Media;

namespace Avalonia;

/// <summary>Configures optional fonts supplied by Alder.Avalonia.</summary>
public static class AlderAppBuilderExtensions
{
    private const string PuHuiTiFamily = "avares://Alder.Avalonia/Assets/Fonts/PuHuiTi#Alibaba PuHuiTi 3.0";

    /// <summary>Uses the bundled Alibaba PuHuiTi 3.0 as the application's default font.</summary>
    /// <param name="builder">The application builder to configure.</param>
    /// <param name="fontOptions">
    /// Existing font options. The supplied <see cref="FontManagerOptions.DefaultFamilyName"/>
    /// becomes the fallback after PuHuiTi.
    /// </param>
    /// <returns>The application builder.</returns>
    public static AppBuilder WithAlderPuHuiTiFont(this AppBuilder builder, FontManagerOptions? fontOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var fallbackFamilies = fontOptions?.DefaultFamilyName?
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(name => name != FontFamily.DefaultFontFamilyName)
            .ToArray() ?? [];

        return builder.With(new FontManagerOptions
        {
            DefaultFamilyName = fallbackFamilies.Length == 0
                ? PuHuiTiFamily
                : $"{PuHuiTiFamily}, {string.Join(", ", fallbackFamilies)}",
            FontFamilyMappings = fontOptions?.FontFamilyMappings,
            FontFallbacks =
            [
                new FontFallback
                {
                    FontFamily = new FontFamily(PuHuiTiFamily),
                    UnicodeRange = UnicodeRange.Parse(
                        "U+2E80-2FDF, U+3000-303F, U+3100-312F, U+31A0-31EF, U+3400-4DBF, U+4E00-9FFF, " +
                        "U+F900-FAFF, U+FE10-FE1F, U+FE30-FE4F, U+FF01-FF60, U+FFE0-FFE6, U+20000-323AF")
                },
                // Missing-glyph lookup can still receive $Default, so register the fallback families explicitly.
                .. fallbackFamilies.Select(name => new FontFallback { FontFamily = new FontFamily(name) }),
                .. (fontOptions?.FontFallbacks ?? [])
            ]
        });
    }
}
