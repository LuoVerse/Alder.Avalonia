using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Alder.Avalonia.Gallery.SourceGenerator;

internal static class ExampleMarkup
{
    internal static readonly XNamespace Gallery = "using:Alder.Avalonia.Gallery.Controls";
    internal static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    internal static string? CardName(XElement card) =>
        (string?)card.Attribute(Xaml + "Name")
        ?? (card.Parent?.Name == Gallery + "GalleryExampleTemplate" ? (string?)card.Parent.Attribute("Name") : null);

    internal static Dictionary<string, HashSet<string>> FindBindings(IEnumerable<XAttribute> attributes, string modelType)
    {
        var bindings = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            [modelType] = new(StringComparer.Ordinal)
        };
        foreach (var attribute in attributes)
        {
            var match = TypedContextBindingReference.Match(attribute.Value);
            string typeName;
            if (match.Success)
                typeName = match.Groups["context"].Value;
            else
            {
                match = BindingReference.Match(attribute.Value);
                if (!match.Success) continue;

                var dataType = attribute.Parent!.AncestorsAndSelf().Attributes(Xaml + "DataType").FirstOrDefault()?.Value;
                typeName = dataType?.Split(':').Last() ?? modelType;
            }

            if (!bindings.TryGetValue(typeName, out var members))
                bindings.Add(typeName, members = new HashSet<string>(StringComparer.Ordinal));
            members.Add(match.Groups["member"].Value);
        }
        return bindings;
    }

    private static readonly Regex BindingReference = new(@"\{(?:Compiled)?Binding\s+(?:Path\s*=\s*)?!*(?<member>[\p{L}_][\p{L}\p{Nd}_]*)(?=\s*[,}.\[])", RegexOptions.CultureInvariant);

    private static readonly Regex TypedContextBindingReference = new(@"\{(?:Compiled)?Binding\s+[^,}]*?\(\((?:\w+:)?(?<context>\w+)\)DataContext\)\.(?<member>\w+)", RegexOptions.CultureInvariant);

    internal static XElement IncludeLocalResources(XElement preview, XElement[] resources)
    {
        if (resources.Length == 0) return preview;

        var usedKeys = new HashSet<string>(ResourceKeys(preview), StringComparer.Ordinal);
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
        element.DescendantsAndSelf().Attributes().SelectMany(attribute => ResourceReference.Matches(attribute.Value).Cast<Match>())
            .Select(match => match.Groups[1].Value);

    private static readonly Regex ResourceReference = new(@"\{(?:Static|Dynamic)Resource\s+([^\s{}]+)\}", RegexOptions.CultureInvariant);
}
