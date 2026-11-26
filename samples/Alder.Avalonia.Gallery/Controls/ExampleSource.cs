using Alder.Avalonia.Gallery.ViewModels;
using Alder.Avalonia.Gallery.Views;
using Avalonia.Controls;

namespace Alder.Avalonia.Gallery.Controls;

internal static class ExampleSource
{
    public static void AttachFiles(ExampleCard card, UserControl view, bool showOnlyThisCard = false)
    {
        card.PrepareSourcesOnDemand(() =>
        {
            var type = view.GetType();
            var axaml = showOnlyThisCard
                ? (type.Name + ".axaml", GalleryExampleSources.FilteredAxaml(type.Name, card.Name!))
                : ReadFile(type, ".axaml");
            card.SetSourceFiles([axaml],
                [ReadFile(view.DataContext!.GetType(), ".cs"), ReadFile(type, ".axaml.cs")]);
        });
    }

    public static void AttachNavigation(UserControl view)
    {
        var type = view.GetType();
        var files = new Lazy<((string Name, string Text)[] Axaml, (string Name, string Text)[] CSharp)>(
            () => ReadNavigationFiles(view, ReadFile(type, ".axaml")));
        foreach (var card in CardsIn(view))
        {
            card.PrepareSourcesOnDemand(() =>
            {
                if (card.Preview is UserControl { DataContext: NavigationBindingViewModel model } preview)
                {
                    var example = preview.GetType();
                    card.SetSourceFiles([ReadFile(example, ".axaml")],
                        [ReadFile(model.GetType(), ".cs"), ReadFile(example, ".axaml.cs")]);
                }
                else
                {
                    var (axaml, csharp) = files.Value;
                    card.SetSourceFiles(axaml, csharp);
                }
            });
        }
    }

    private static ((string Name, string Text)[] Axaml, (string Name, string Text)[] CSharp) ReadNavigationFiles(
        UserControl view, (string Name, string Text) source)
    {
        var type = view.GetType();
        (string Name, string Text)[] axaml =
        [
            source,
            ReadFile(typeof(NavigationPreviewSidebar), ".axaml"),
            ReadFile(typeof(NavigationPreviewNavbar), ".axaml"),
            ReadFile(typeof(NavigationPreviewAccount), ".axaml"),
            ReadFile(typeof(NavigationPreviewSidebar), ".axaml", "NavigationPreviewResources")
        ];
        (string Name, string Text)[] csharp =
        [
            ReadFile(view.DataContext!.GetType(), ".cs"),
            ReadFile(typeof(NavigationPreview), ".cs"),
            ReadFile(typeof(NavigationPreviewCatalog), ".cs"),
            ReadFile(type, ".axaml.cs"),
            ReadFile(typeof(NavigationPreviewSidebar), ".axaml.cs"),
            ReadFile(typeof(NavigationPreviewNavbar), ".axaml.cs"),
            ReadFile(typeof(NavigationPreviewAccount), ".axaml.cs")
        ];
        if (view is AppLayoutExample)
            csharp = [.. csharp, ReadFile(typeof(LayoutSamples), ".cs")];
        return (axaml, csharp);
    }

    public static void Attach(UserControl view)
    {
        string viewName = view.GetType().Name;
        if (view is GalleryExampleSection section)
        {
            foreach (var template in section.Examples)
                template.SetSources = card => PopulateCard(view, viewName, card);
            return;
        }

        foreach (var card in CardsIn(view))
            card.PrepareSourcesOnDemand(() => PopulateCard(view, viewName, card));
    }

    private static IEnumerable<ExampleCard> CardsIn(UserControl view)
    {
        var viewName = view.GetType().Name;
        foreach (var name in GalleryExampleSources.CardNames(viewName))
            yield return view.FindControl<ExampleCard>(name)
                ?? throw new InvalidOperationException($"Could not find {name} in {viewName}.axaml.");
    }

    private static void PopulateCard(UserControl view, string viewName, ExampleCard card)
    {
        var (axaml, viewModel, codeBehind, usage) = GalleryExampleSources.Excerpt(viewName, card.Name!);
        if (view is ChainOfThoughtExample && card.Name == "SampleDefault")
        {
            var modelType = view.DataContext!.GetType();
            card.SetSourceFiles([(viewName + ".axaml", axaml)],
                [(viewName + ".cs", usage), ReadFile(modelType, ".cs"),
                    ReadFile(modelType, ".cs", "AiExampleViewModel.Chain"), ReadFile(typeof(AiLivePreview), ".cs")]);
        }
        else
            card.SetSources(axaml, viewModel, codeBehind, usage);
    }

    private static (string Name, string Text) ReadFile(Type type, string extension, string? fileName = null)
    {
        var name = (fileName ?? type.Name) + extension;
        return (name, GalleryExampleSources.FullFile(name));
    }
}
