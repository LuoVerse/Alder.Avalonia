using Alder.Avalonia.Controls;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed record TypographyScaleExample(
    string Name, string Metrics, string Sample, TypographyType Type, bool ShowSeparator = true);

public sealed class TypographyViewModel : GalleryViewModel
{
    public string Heading => Text("TypographyLeadHeading");

    public IReadOnlyList<TypographyScaleExample> Scale =>
    [
        new("h1", "36 / 600 / 40", Heading, TypographyType.H1),
        new("h2", "30 / 600 / 36", Text("TypographyScaleH2"), TypographyType.H2),
        new("h3", "24 / 600 / 32", Text("TypographyScaleH3"), TypographyType.H3),
        new("h4", "20 / 600 / 28", Text("TypographyScaleH4"), TypographyType.H4),
        new("h5", "18 / 600 / 28", Text("TypographyScaleH5"), TypographyType.H5),
        new("h6", "16 / 600 / 24", Text("TypographyScaleH6"), TypographyType.H6),
        new("body", "16 / 400 / 28", Text("TypographyScaleBody"), TypographyType.Body),
        new("body-sm", "14 / 400 / 24", Text("TypographyScaleSmall"), TypographyType.BodySmall),
        new("body-xs", "12 / 400 / 20", Text("TypographyScaleExtraSmall"), TypographyType.BodyExtraSmall),
        new("code", "14 / mono / 20", "dotnet add package Alder.Avalonia", TypographyType.Code, false)
    ];
}
