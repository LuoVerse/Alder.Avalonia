using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Alder.Avalonia.Gallery.SourceGenerator;

internal sealed class CSharpExcerpt
{
    private readonly TypeDeclarationSyntax[] types;

    public CSharpExcerpt(string source)
    {
        var declarations = CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes();
        types = declarations.OfType<TypeDeclarationSyntax>()
            .Where(type => type.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax).ToArray();
        AttachesFiles = declarations.OfType<InvocationExpressionSyntax>().Any(call => call.Expression is
            MemberAccessExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.ValueText: "ExampleSource" },
                Name.Identifier.ValueText: "AttachFiles"
            });
    }

    public bool AttachesFiles { get; }

    public IEnumerable<MemberDeclarationSyntax> CodeBehind(string typeName, IEnumerable<string> handlers, IEnumerable<string> controls)
    {
        var type = types.FirstOrDefault(type => type.Identifier.ValueText == typeName);
        if (type is null) return [];

        // Named preview controls can also have event subscriptions set in the constructor.
        var handlerNames = new HashSet<string>(handlers, StringComparer.Ordinal);
        var controlNames = new HashSet<string>(controls, StringComparer.Ordinal);
        var roots = new HashSet<string>(type.Members.OfType<MethodDeclarationSyntax>()
            .SelectMany(MemberNames).Where(handlerNames.Contains), StringComparer.Ordinal);
        roots.UnionWith(controlNames);
        return SelectMembers(type, roots, controlNames);
    }

    public string ViewModel(string typeName, IReadOnlyDictionary<string, HashSet<string>> bindings)
    {
        var model = types.FirstOrDefault(type => type.Identifier.ValueText == typeName);
        var declarations = new Dictionary<TypeDeclarationSyntax, TypeDeclarationSyntax>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in types)
        {
            if (!bindings.TryGetValue(type.Identifier.ValueText, out var roots)) continue;
            var members = SelectMembers(type, roots).ToArray();
            // Positional records declare bindable properties in their parameter list.
            if (members.Length == 0 && type.ParameterList is null) continue;
            declarations.Add(type, type.WithMembers(SyntaxFactory.List(members)));
            names.UnionWith(members.SelectMany(Names));
        }

        // Include model item types declared in the same file, including positional records.
        foreach (var type in types)
        {
            if (type != model && names.Contains(type.Identifier.ValueText) && !declarations.ContainsKey(type))
                declarations.Add(type, type);
        }

        return Format(types.Where(declarations.ContainsKey).Select(type => declarations[type]));
    }

    public static IEnumerable<string> ModelReferences(IEnumerable<MemberDeclarationSyntax> codeBehind, string modelName)
    {
        var nodes = codeBehind.SelectMany(member => member.DescendantNodes()).ToArray();
        var variables = new HashSet<string>(nodes.OfType<DeclarationPatternSyntax>()
            .Where(pattern => pattern.Type is IdentifierNameSyntax type && type.Identifier.ValueText == modelName)
            .Select(pattern => pattern.Designation).OfType<SingleVariableDesignationSyntax>()
            .Select(variable => variable.Identifier.ValueText), StringComparer.Ordinal);
        return nodes.OfType<MemberAccessExpressionSyntax>()
            .Where(access => access.Expression is IdentifierNameSyntax target && variables.Contains(target.Identifier.ValueText))
            .Select(access => access.Name.Identifier.ValueText);
    }

    public static string Format(IEnumerable<MemberDeclarationSyntax> members) =>
        string.Join("\n\n", members.Select(member => member.NormalizeWhitespace(eol: "\n").ToFullString().Trim()));

    private static IEnumerable<MemberDeclarationSyntax> SelectMembers(
        TypeDeclarationSyntax type, HashSet<string> roots, HashSet<string>? controls = null)
    {
        var names = new HashSet<string>(roots, StringComparer.Ordinal);
        var fields = controls is null ? null
            : new HashSet<string>(type.Members.OfType<FieldDeclarationSyntax>().SelectMany(MemberNames), StringComparer.Ordinal);
        var selected = new Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax>();
        int previousCount;
        do
        {
            previousCount = names.Count;
            foreach (var member in type.Members)
            {
                var excerpt = member switch
                {
                    ConstructorDeclarationSyntax constructor => SelectConstructor(constructor, names, controls),
                    MethodDeclarationSyntax method when fields is not null && method.Modifiers.Any(SyntaxKind.OverrideKeyword)
                        && Names(method).Any(name => controls!.Contains(name) || (names.Contains(name) && fields.Contains(name))) => method,
                    _ => MemberNames(member).Any(names.Contains) ? member : null
                };
                if (excerpt is null) continue;
                selected[member] = excerpt;
                names.UnionWith(Names(excerpt));
            }
        } while (names.Count != previousCount);

        return type.Members.Where(selected.ContainsKey).Select(member => selected[member]);
    }

    private static ConstructorDeclarationSyntax? SelectConstructor(
        ConstructorDeclarationSyntax constructor, HashSet<string> names, HashSet<string>? controls)
    {
        bool ReferencesExample(ExpressionSyntax expression) =>
            Names(Target(expression)).Any(names.Contains)
            || (controls is not null && Names(expression).Any(controls.Contains));

        if (constructor.ExpressionBody is { } expression)
            return ReferencesExample(expression.Expression) ? constructor : null;

        if (constructor.Body is not { } body) return null;
        var selected = new HashSet<ExpressionStatementSyntax>(body.Statements.OfType<ExpressionStatementSyntax>()
            .Where(statement => ReferencesExample(statement.Expression)));
        if (selected.Count == 0) return null;

        return constructor.WithBody(body.WithStatements(SyntaxFactory.List(
            body.Statements.Where(statement => statement is ExpressionStatementSyntax expressionStatement
                && (selected.Contains(expressionStatement) || expressionStatement.Expression is InvocationExpressionSyntax
                    { Expression: IdentifierNameSyntax { Identifier.ValueText: "InitializeComponent" } })))));
    }

    // Only the target chooses an initializer. References inside a command lambda are dependencies
    // after its initializer is selected, since other commands may write to the same status property.
    private static ExpressionSyntax Target(ExpressionSyntax expression) => expression switch
    {
        AssignmentExpressionSyntax assignment => assignment.Left,
        InvocationExpressionSyntax invocation => invocation.Expression,
        _ => expression
    };

    private static IEnumerable<string> MemberNames(MemberDeclarationSyntax member) => member switch
    {
        BaseFieldDeclarationSyntax field => field.Declaration.Variables.Select(variable => variable.Identifier.ValueText),
        PropertyDeclarationSyntax property => [property.Identifier.ValueText],
        MethodDeclarationSyntax method => [method.Identifier.ValueText],
        TypeDeclarationSyntax nested => [nested.Identifier.ValueText],
        _ => []
    };

    private static IEnumerable<string> Names(SyntaxNode node) =>
        node.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().Select(name => name.Identifier.ValueText);
}
