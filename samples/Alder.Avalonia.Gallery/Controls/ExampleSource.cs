using System.Text.RegularExpressions;
using System.Xml.Linq;
using Alder.Avalonia.Gallery.ViewModels;
using Alder.Avalonia.Gallery.Views;
using Avalonia.Controls;

namespace Alder.Avalonia.Gallery.Controls;

internal static partial class ExampleSource
{
    private static readonly XNamespace Gallery = "using:Alder.Avalonia.Gallery.Controls";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    public static void AttachFiles(ExampleCard card, UserControl view, bool showOnlyThisCard = false)
    {
        var type = view.GetType();
        var axaml = ReadFile(type, ".axaml");
        if (showOnlyThisCard)
        {
            var document = XDocument.Parse(axaml.Text, LoadOptions.PreserveWhitespace);
            document.Descendants(Gallery + nameof(ExampleCard))
                .Where(element => (string?)element.Attribute(Xaml + "Name") != card.Name).Remove();
            axaml = (axaml.Name, document.ToString(SaveOptions.DisableFormatting));
        }
        card.SetSourceFiles([axaml],
            [ReadFile(view.DataContext!.GetType(), ".cs"), ReadFile(type, ".axaml.cs")]);
    }

    public static void AttachNavigation(UserControl view)
    {
        var type = view.GetType();
        var source = ReadFile(type, ".axaml");
        var root = XDocument.Parse(source.Text).Root!;
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
        foreach (var element in root.Descendants(Gallery + nameof(ExampleCard)))
        {
            var name = (string)element.Attribute(Xaml + "Name")!;
            var card = view.FindControl<ExampleCard>(name)
                ?? throw new InvalidOperationException($"Could not find {name} in {source.Name}.");
            if (card.Preview is UserControl { DataContext: NavigationBindingViewModel model } preview)
            {
                var example = preview.GetType();
                card.SetSourceFiles([ReadFile(example, ".axaml")],
                    [ReadFile(model.GetType(), ".cs"), ReadFile(example, ".axaml.cs")]);
            }
            else
                card.SetSourceFiles(axaml, csharp);
        }
    }

    public static void Attach(UserControl view)
    {
        var type = view.GetType();
        var resource = type.FullName + ".axaml";
        using var stream = type.Assembly.GetManifestResourceStream(resource)
            ?? throw new FileNotFoundException(resource);
        var modelType = view.DataContext!.GetType();
        var modelCode = ReadCSharp(modelType, ".cs");
        var viewCode = ReadCSharp(type, ".axaml.cs");
        var root = XDocument.Load(stream, LoadOptions.PreserveWhitespace).Root!;
        var namespaces = root.Attributes().Where(attribute => attribute.IsNamespaceDeclaration);
        var localResources = root.Element(root.Name + ".Resources")?.Elements().ToArray() ?? [];

        foreach (var element in root.Descendants(Gallery + nameof(ExampleCard)))
        {
            var name = (string)element.Attribute(Xaml + "Name")!;
            var card = view.FindControl<ExampleCard>(name)
                ?? throw new InvalidOperationException($"Could not find {name} in {resource}.");
            var preview = element.Element(Gallery + "ExampleCard.Preview")!.Elements().Single();
            var fragment = IncludeLocalResources(preview, localResources);
            var feedback = element.Element(Gallery + "ExampleCard.Feedback");
            var parts = feedback is null ? [fragment] : new[] { fragment, feedback };

            var attributes = parts.SelectMany(part => part.DescendantsAndSelf().Attributes()).ToArray();
            var namedControls = attributes.Where(attribute => attribute.Name == Xaml + "Name" || attribute.Name == "Name")
                .Select(attribute => attribute.Value);
            var codeBehind = viewCode.CodeBehind(type.Name, attributes.Select(attribute => attribute.Value), namedControls)
                .ToArray();

            var bindings = FindBindings(attributes, modelType.Name);
            bindings[modelType.Name].UnionWith(CSharpExcerpt.ModelReferences(codeBehind, modelType.Name));
            var viewModel = modelCode.ViewModel(modelType.Name, bindings);
            var csharp = element.Element(Gallery + "ExampleCard.CSharpSource")?.Value;
            if (string.IsNullOrWhiteSpace(csharp))
                throw new InvalidOperationException($"Missing C# source for {name} in {resource}.");

            var axaml = SourceFormatter.AxamlFragment(fragment, namespaces);
            var usage = SourceFormatter.CSharp(csharp);
            if (view is ChainOfThoughtExample && name == "SampleDefault")
            {
                card.SetSourceFiles([(type.Name + ".axaml", axaml)],
                    [(type.Name + ".cs", usage), ReadFile(modelType, ".cs"),
                        ReadFile(modelType, ".cs", "AiExampleViewModel.Chain"), ReadFile(typeof(AiLivePreview), ".cs")]);
            }
            else
                card.SetSources(axaml, viewModel, CSharpExcerpt.Format(codeBehind), usage);
        }
    }

    private static CSharpExcerpt ReadCSharp(Type type, string extension)
        => new(ReadFile(type, extension).Text);

    private static (string Name, string Text) ReadFile(Type type, string extension, string? fileName = null)
    {
        var name = (fileName ?? type.Name) + extension;
        var resource = fileName is null ? type.FullName + extension : type.Namespace + "." + name;
        using var stream = type.Assembly.GetManifestResourceStream(resource)
            ?? throw new FileNotFoundException(resource);
        using var reader = new StreamReader(stream);
        return (name, reader.ReadToEnd());
    }

    private static Dictionary<string, HashSet<string>> FindBindings(IEnumerable<XAttribute> attributes, string modelType)
    {
        var bindings = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            [modelType] = new(StringComparer.Ordinal)
        };
        foreach (var attribute in attributes)
        {
            var match = TypedContextBindingReference().Match(attribute.Value);
            string typeName;
            if (match.Success)
                typeName = match.Groups["context"].Value;
            else
            {
                match = BindingReference().Match(attribute.Value);
                if (!match.Success) continue;

                var dataType = attribute.Parent!.AncestorsAndSelf().Attributes(Xaml + "DataType").FirstOrDefault()?.Value;
                typeName = dataType?.Split(':')[^1] ?? modelType;
            }

            if (!bindings.TryGetValue(typeName, out var members))
                bindings.Add(typeName, members = new HashSet<string>(StringComparer.Ordinal));
            members.Add(match.Groups["member"].Value);
        }
        return bindings;
    }

    [GeneratedRegex(@"\{(?:Compiled)?Binding\s+(?:Path\s*=\s*)?!*(?<member>[\p{L}_][\p{L}\p{Nd}_]*)(?=\s*[,}.\[])")]
    private static partial Regex BindingReference();

    [GeneratedRegex(@"\{(?:Compiled)?Binding\s+[^,}]*?\(\((?:\w+:)?(?<context>\w+)\)DataContext\)\.(?<member>\w+)")]
    private static partial Regex TypedContextBindingReference();

    private static XElement IncludeLocalResources(XElement preview, XElement[] resources)
    {
        if (resources.Length == 0) return preview;

        var usedKeys = ResourceKeys(preview).ToHashSet(StringComparer.Ordinal);
        var usedResources = new HashSet<XElement>();
        int previousCount;
        do
        {
            previousCount = usedResources.Count;
            foreach (var resource in resources)
            {
                if (resource.DescendantsAndSelf().Attributes(Xaml + "Key").Any(key => usedKeys.Contains(key.Value))
                    && usedResources.Add(resource))
                    usedKeys.UnionWith(ResourceKeys(resource));
            }
        } while (usedResources.Count != previousCount);

        if (usedResources.Count == 0) return preview;

        var fragment = new XElement(preview);
        fragment.AddFirst(new XElement(fragment.Name + ".Resources", resources.Where(usedResources.Contains)));
        return fragment;
    }

    private static IEnumerable<string> ResourceKeys(XElement element) =>
        element.DescendantsAndSelf().Attributes().SelectMany(attribute => ResourceReference().Matches(attribute.Value))
            .Select(match => match.Groups[1].Value);

    [GeneratedRegex(@"\{(?:Static|Dynamic)Resource\s+([^\s{}]+)\}")]
    private static partial Regex ResourceReference();
}
