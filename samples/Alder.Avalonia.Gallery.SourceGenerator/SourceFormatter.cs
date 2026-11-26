using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Alder.Avalonia.Gallery.SourceGenerator;

internal static class SourceFormatter
{
    public static string CSharp(string source)
    {
        var lines = source.Trim('\n').TrimEnd().Split('\n');
        var indent = lines.Where(line => !string.IsNullOrWhiteSpace(line))
            .Min(line => line.Length - line.TrimStart().Length);
        return string.Join("\n", lines.Select(line => string.IsNullOrWhiteSpace(line) ? "" : line.Substring(indent)));
    }

    public static string AxamlFragment(XElement element, IEnumerable<XAttribute> namespaces)
    {
        // Use the containing view's prefixes while formatting, then display just the fragment.
        var wrapper = new XElement(element.Name.Namespace + "Snippet", namespaces, new XElement(element));
        var formatted = Axaml(wrapper.ToString(SaveOptions.DisableFormatting));
        int start = formatted.IndexOf('>') + 1;
        var body = formatted.Substring(start, formatted.LastIndexOf("</", StringComparison.Ordinal) - start);
        var lines = body.Trim('\n').Split('\n');
        return string.Join("\n", lines.Select(line => line.StartsWith("  ", StringComparison.Ordinal) ? line.Substring(2) : line));
    }

    private static string Axaml(string source)
    {
        try
        {
            var document = XDocument.Parse(source, LoadOptions.PreserveWhitespace);
            Indent(document.Root!, 0);
            // Wrap attributes for wide start tags, keeping comments, inline runs and
            // binding values intact. Validate the candidate against the indented tree.
            var compact = document.ToString(SaveOptions.DisableFormatting);
            var result = compact;
            if (compact.Split('\n').Any(line => line.Length > 100))
            {
                var output = new StringBuilder();
                using (var writer = XmlWriter.Create(output, new XmlWriterSettings
                {
                    OmitXmlDeclaration = true,
                    Indent = true,
                    IndentChars = "  ",
                    NewLineChars = "\n",
                    NewLineOnAttributes = true,
                    NewLineHandling = NewLineHandling.Entitize,
                }))
                    document.WriteTo(writer);

                var candidate = XDocument.Parse(output.ToString(), LoadOptions.PreserveWhitespace);
                Indent(candidate.Root!, 0);
                if (XNode.DeepEquals(document.Root, candidate.Root))
                    result = output.ToString();
            }
            return (document.Declaration is null ? "" : document.Declaration + "\n")
                + result.TrimEnd('\r', '\n');
        }
        catch (XmlException)
        {
            return source;
        }
    }

    private static void Indent(XElement element, int depth)
    {
        // Whitespace in text runs, mixed content and explicit preservation scopes is data.
        if (element.Attribute(XNamespace.Xml + "space")?.Value == "preserve"
            || element.Name.LocalName is "TextBlock" or "Span" or "Run" or "Bold" or "Italic"
            || element.Nodes().OfType<XText>().Any(t => t is XCData || !string.IsNullOrWhiteSpace(t.Value))
            || !element.Elements().Any())
            return;

        foreach (var space in element.Nodes().OfType<XText>().ToArray())
            space.Remove();
        foreach (var node in element.Nodes().ToArray())
        {
            node.AddBeforeSelf(new XText("\n" + new string(' ', (depth + 1) * 2)));
            if (node is XElement child)
                Indent(child, depth + 1);
        }
        element.Add(new XText("\n" + new string(' ', depth * 2)));
    }
}
