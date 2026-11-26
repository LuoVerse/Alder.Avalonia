using System.Collections.Immutable;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Alder.Avalonia.Gallery.SourceGenerator;

[Generator]
public sealed class ExampleSourceGenerator : IIncrementalGenerator
{
#pragma warning disable RS2008 // The diagnostic belongs to the Gallery build, not a distributed analyzer.
    private static readonly DiagnosticDescriptor InvalidExample = new(
        "ALDERG001", "Invalid Gallery example source", "{0}: {1}", "Gallery",
        DiagnosticSeverity.Error, isEnabledByDefault: true);
#pragma warning restore RS2008

    private static readonly XName ExampleCard = ExampleMarkup.Gallery + "ExampleCard";
    private static readonly XName Preview = ExampleMarkup.Gallery + "ExampleCard.Preview";
    private static readonly XName CSharpSource = ExampleMarkup.Gallery + "ExampleCard.CSharpSource";
    private static readonly XName Feedback = ExampleMarkup.Gallery + "ExampleCard.Feedback";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var inputs = context.AdditionalTextsProvider.Select(static (file, token) =>
            (Name: Path.GetFileName(file.Path), Source: file.GetText(token)?.ToString() ?? ""));
        context.RegisterSourceOutput(inputs.Collect(), Generate);
    }

    private static void Generate(SourceProductionContext context, ImmutableArray<(string Name, string Source)> inputs)
    {
        var files = inputs.ToDictionary(file => file.Name, file => file.Source, StringComparer.Ordinal);
        var models = FindViewModels(files["GalleryView.axaml.cs"]);
        var parsedSources = new Dictionary<string, CSharpExcerpt>(StringComparer.Ordinal);
        var fullFiles = new SortedSet<string>(files.Keys.Where(name =>
            name.StartsWith("NavigationPreview", StringComparison.Ordinal) ||
            name is "NavigationBindingViewModel.cs" or "LayoutSamples.cs" or "AiExampleViewModel.Chain.cs" or "AiLivePreview.cs"),
            StringComparer.Ordinal);
        var names = new StringBuilder();
        var excerpts = new StringBuilder();
        var filtered = new StringBuilder();
        var methods = new StringBuilder();

        foreach (string fileName in files.Keys.Where(name => name.EndsWith("Example.axaml", StringComparison.Ordinal)).OrderBy(name => name, StringComparer.Ordinal))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            try
            {
                string view = Path.GetFileNameWithoutExtension(fileName);
                string codeBehindPath = fileName + ".cs";
                var document = XDocument.Parse(files[fileName], LoadOptions.PreserveWhitespace);
                var root = document.Root!;
                string? model = models.TryGetValue(view, out var configuredModel) ? configuredModel
                    : ((string?)root.Attribute(ExampleMarkup.Xaml + "DataType"))?.Split(':').Last();
                var cards = root.Descendants(ExampleCard)
                    .Select(card => (Element: card, Name: ExampleMarkup.CardName(card)
                        ?? throw new InvalidOperationException("Every example card must have a name."))).ToArray();
                bool navigation = view is "AppLayoutExample" or "SidebarExample";
                bool wholeFile = ReadCSharp(codeBehindPath).AttachesFiles;

                if (navigation || wholeFile || cards.Length == 0)
                {
                    fullFiles.Add(fileName);
                    fullFiles.Add(codeBehindPath);
                    if (model is not null) fullFiles.Add(model + ".cs");
                    if (wholeFile && cards.Length > 0)
                        AddFilteredFiles(view, document, cards.Select(card => card.Name), filtered, methods);
                    if (!navigation) continue;
                }

                names.Append("        ").Append(Literal(view)).Append(" => [")
                    .Append(string.Join(", ", cards.Select(card => Literal(card.Name)))).AppendLine("],");
                if (navigation) continue;

                if (model is null) throw new InvalidOperationException("Declare the view's model using x:DataType or CreateExamples.");
                if (view == "ChainOfThoughtExample") fullFiles.Add(model + ".cs");

                excerpts.Append("        ").Append(Literal(view)).Append(" => ").Append(view).AppendLine("(card),");
                methods.Append("    private static (string Axaml, string ViewModel, string CodeBehind, string Usage) ")
                    .Append(view).AppendLine("(string card) => card switch").AppendLine("    {");
                var namespaces = root.Attributes().Where(attribute => attribute.IsNamespaceDeclaration).ToArray();
                var resources = root.Element(root.Name + ".Resources")?.Elements().ToArray() ?? [];
                var viewCode = ReadCSharp(codeBehindPath);
                var modelCode = ReadCSharp(model + ".cs");
                foreach (var (element, name) in cards)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    var preview = element.Element(Preview)?.Elements().SingleOrDefault()
                        ?? throw new InvalidOperationException($"Missing preview for {name}.");
                    var usage = element.Element(CSharpSource)?.Value;
                    if (string.IsNullOrWhiteSpace(usage)) throw new InvalidOperationException($"Missing C# source for {name}.");
                    var fragment = ExampleMarkup.IncludeLocalResources(preview, resources);
                    var feedback = element.Element(Feedback);
                    var attributes = fragment.DescendantsAndSelf().Attributes()
                        .Concat(feedback?.DescendantsAndSelf().Attributes() ?? Enumerable.Empty<XAttribute>()).ToArray();
                    var namedControls = attributes.Where(attribute => attribute.Name == ExampleMarkup.Xaml + "Name" || attribute.Name == "Name")
                        .Select(attribute => attribute.Value);
                    var codeBehind = viewCode.CodeBehind(view, attributes.Select(attribute => attribute.Value), namedControls).ToArray();
                    var bindings = ExampleMarkup.FindBindings(attributes, model);
                    bindings[model].UnionWith(CSharpExcerpt.ModelReferences(codeBehind, model));

                    methods.Append("        ").Append(Literal(name)).Append(" => (")
                        .Append(SourceText(SourceFormatter.AxamlFragment(fragment, namespaces))).Append(", ")
                        .Append(SourceText(modelCode.ViewModel(model, bindings))).Append(", ")
                        .Append(SourceText(CSharpExcerpt.Format(codeBehind))).Append(", ")
                        .Append(SourceText(SourceFormatter.CSharp(usage!))).AppendLine("),");
                }
                methods.AppendLine("        _ => throw Missing(card)").AppendLine("    };");
            }
            catch (Exception error) when (error is XmlException or InvalidOperationException or ArgumentException)
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidExample, Location.None, fileName, error.Message));
            }
        }

        var fullSource = new StringBuilder();
        foreach (var file in fullFiles)
        {
            if (files.TryGetValue(file, out var contents))
                fullSource.Append("        ").Append(Literal(file)).Append(" => ").Append(SourceText(contents)).AppendLine(",");
            else
                context.ReportDiagnostic(Diagnostic.Create(InvalidExample, Location.None, file, "Source file is unavailable."));
        }

        context.AddSource("GalleryExampleSources.g.cs", $$"""
            // <auto-generated/>
            namespace Alder.Avalonia.Gallery.Controls;

            internal static class GalleryExampleSources
            {
                internal static string[] CardNames(string view) => view switch
                {
            {{names}}        _ => throw Missing(view)
                };

                internal static (string Axaml, string ViewModel, string CodeBehind, string Usage) Excerpt(string view, string card) => view switch
                {
            {{excerpts}}        _ => throw Missing(view)
                };

                internal static string FilteredAxaml(string view, string card) => view switch
                {
            {{filtered}}        _ => throw Missing(view)
                };

                internal static string FullFile(string file) => file switch
                {
            {{fullSource}}        _ => throw Missing(file)
                };

                private static System.InvalidOperationException Missing(string source) => new($"Gallery source is missing for {source}.");

                private static string Decode(System.ReadOnlySpan<byte> text) => System.Text.Encoding.UTF8.GetString(text);

            {{methods}}
            }
            """);

        CSharpExcerpt ReadCSharp(string fileName)
        {
            if (parsedSources.TryGetValue(fileName, out var parsed)) return parsed;
            parsed = new CSharpExcerpt(files.TryGetValue(fileName, out var text) ? text : "");
            parsedSources.Add(fileName, parsed);
            return parsed;
        }
    }

    private static void AddFilteredFiles(string view, XDocument document, IEnumerable<string> cardNames,
        StringBuilder cases, StringBuilder methods)
    {
        cases.Append("        ").Append(Literal(view)).Append(" => Filter").Append(view).AppendLine("(card),");
        var variants = cardNames.Select(name =>
        {
            var copy = new XDocument(document);
            copy.Descendants(ExampleCard).Where(card => ExampleMarkup.CardName(card) != name).Remove();
            return (Name: name, Text: copy.ToString(SaveOptions.DisableFormatting));
        }).ToArray();

        // Whole-file examples share their resources and surrounding markup across the card variants.
        var first = variants[0].Text;
        int commonLimit = variants.Min(variant => variant.Text.Length);
        int prefix = 0;
        while (prefix < commonLimit && variants.All(variant => variant.Text[prefix] == first[prefix])) prefix++;
        int suffix = 0;
        while (suffix < commonLimit - prefix && variants.All(variant =>
                   variant.Text[variant.Text.Length - suffix - 1] == first[first.Length - suffix - 1])) suffix++;
        // The slices must end on Unicode scalar boundaries before encoding the literals as UTF-8.
        if (prefix > 0 && char.IsHighSurrogate(first[prefix - 1])) prefix--;
        if (suffix > 0 && char.IsLowSurrogate(first[first.Length - suffix])) suffix--;

        methods.Append("    private static string Filter").Append(view).Append("(string card) => string.Concat(")
            .Append(SourceText(first.Substring(0, prefix))).AppendLine(", card switch")
            .AppendLine("    {");
        foreach (var (name, text) in variants)
            methods.Append("        ").Append(Literal(name)).Append(" => ")
                .Append(SourceText(text.Substring(prefix, text.Length - prefix - suffix))).AppendLine(",");
        methods.AppendLine("        _ => throw Missing(card)").Append("    }, ")
            .Append(SourceText(first.Substring(first.Length - suffix))).AppendLine(");");
    }

    private static Dictionary<string, string> FindViewModels(string source)
    {
        var models = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var call in CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (call.Expression is not IdentifierNameSyntax { Identifier.ValueText: "CreateExamples" }) continue;
            var arguments = call.ArgumentList.Arguments;
            if (arguments.Count >= 2 &&
                arguments[0].Expression is ObjectCreationExpressionSyntax { Type: IdentifierNameSyntax view } &&
                arguments[1].Expression is ObjectCreationExpressionSyntax { Type: IdentifierNameSyntax model })
                models[view.Identifier.ValueText] = model.Identifier.ValueText;
        }
        return models;
    }

    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, quote: true);

    private static string SourceText(string value) => value.Length == 0 ? "\"\"" : $"Decode({Literal(value)}u8)";
}
