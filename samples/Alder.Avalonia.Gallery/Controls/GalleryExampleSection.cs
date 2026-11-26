using Avalonia.Controls;
using Avalonia.Markup.Xaml.Templates;

namespace Alder.Avalonia.Gallery.Controls;

public class GalleryExampleSection : UserControl
{
    public IList<GalleryExampleTemplate> Examples { get; } = new List<GalleryExampleTemplate>();

    internal void AttachExample(ExampleCard card) => LogicalChildren.Add(card);
}

public sealed class GalleryExampleTemplate : Template
{
    public string Name { get; set; } = "";

    internal Action<ExampleCard>? SetSources { get; set; }

    internal ExampleCard CreateCard()
    {
        var card = (ExampleCard)Build()!;
        card.Name = Name;
        if (SetSources is { } setSources)
            card.PrepareSourcesOnDemand(() => setSources(card));
        return card;
    }
}
