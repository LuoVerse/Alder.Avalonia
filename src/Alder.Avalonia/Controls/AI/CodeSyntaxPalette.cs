using System.Collections.Immutable;
using Alder.NativeCode.Monarch;
using Alder.NativeMarkdown.Core.Highlighting;

namespace Alder.Avalonia.Controls;

/// <summary>
/// Maps grammar tokens to semantic roles, then paints those roles in the code block's light or dark palette.
/// Fences and standalone code blocks use the same mapping, including inside embedded languages.
/// </summary>
internal readonly struct CodeSyntaxPalette(HighlightThemeKind theme)
{
    private enum Role : byte
    {
        Plain,
        Comment,
        String,
        Escape,
        Number,
        Keyword,
        Type,
        Function,
        Property,
        Parameter,
        MarkupAttribute,
        Tag,
        Constant,
        Punctuation
    }

    public ImmutableArray<HighlightToken> Colorize(ReadOnlySpan<char> line, IReadOnlyList<Token> syntax)
    {
        ImmutableArray<HighlightToken>.Builder? colors = null;
        for (var i = 0; i < syntax.Count; i++)
        {
            var token = syntax[i];
            var start = token.Offset;
            var end = i + 1 < syntax.Count ? Math.Min(syntax[i + 1].Offset, line.Length) : line.Length;
            if (end <= start)
                continue;
            var role = GetRole(token, line, start, end);
            if (role == Role.Plain)
                continue;

            var foreground = Color(role);
            var style = role == Role.Comment ? HighlightFontStyle.Italic : HighlightFontStyle.None;
            colors ??= ImmutableArray.CreateBuilder<HighlightToken>(syntax.Count);
            if (colors.Count > 0)
            {
                var previous = colors[^1];
                if (previous.Start + previous.Length == start && previous.Foreground == foreground && previous.Style == style)
                {
                    colors[^1] = previous with { Length = end - previous.Start };
                    continue;
                }
            }
            colors.Add(new(start, end - start, foreground, style));
        }
        return colors is null ? [] : colors.DrainToImmutable();
    }

    private static Role GetRole(Token token, ReadOnlySpan<char> line, int start, int end)
    {
        var type = token.Type.AsSpan();
        var separator = type.IndexOf('.');
        var family = separator < 0 ? type : type[..separator];
        var language = token.Language;
        if (type is "attribute.json")
            return Role.Property;
        if (type.StartsWith("attribute.value.number.", StringComparison.Ordinal) ||
            type.StartsWith("attribute.value.unit.", StringComparison.Ordinal))
            return Role.Number;
        if (type.StartsWith("string.escape.", StringComparison.Ordinal))
            return Role.Escape;
        if (language == "csharp" && type.StartsWith("string.quote.", StringComparison.Ordinal) &&
            line[start..end] is "{" or "}")
            return Role.Punctuation;

        return family switch
        {
            "comment" => Role.Comment,
            "string" or "regexp" => Role.String,
            "tag" or "metatag" => Role.Tag,
            "attribute" => type.StartsWith("attribute.value.", StringComparison.Ordinal) ? Role.String
                : language is "html" or "xml" or "jsx" or "tsx" ? Role.MarkupAttribute : Role.Property,
            "number" => Role.Number,
            "constant" or "predefined" => Role.Constant,
            "keyword" => KeywordRole(language, line[start..end]),
            "type" or "typeIdentifier" or "namespace" => language == "yaml" ? Role.Property : Role.Type,
            "function" or "method" => Role.Function,
            "delimiter" or "operator" => Role.Punctuation,
            "identifier" => IdentifierRole(language, line, start, end),
            _ => Role.Plain
        };
    }

    private static Role KeywordRole(string language, ReadOnlySpan<char> keyword)
    {
        if (keyword is "true" or "false" or "null" or "undefined" or "None" or "True" or "False")
            return Role.Constant;

        if (language is "typescript" or "tsx" or "csharp" && keyword is
            "any" or "bigint" or "bool" or "boolean" or "byte" or "char" or "decimal" or "double" or "float" or
            "int" or "long" or "never" or "number" or "object" or "short" or "string" or "symbol" or "uint" or
            "ulong" or "unknown" or "void")
            return Role.Type;
        return Role.Keyword;
    }

    private static Role IdentifierRole(string language, ReadOnlySpan<char> line, int start, int end)
    {
        var after = line[end..].TrimStart();
        var before = line[..start].TrimEnd();
        if (language == "csharp" && char.IsUpper(line[start]) &&
            (after.StartsWith(".") || before.EndsWith(" new", StringComparison.Ordinal) || before is "new" ||
             before.EndsWith(" class", StringComparison.Ordinal) || before is "class"))
            return Role.Type;
        if (after.StartsWith("(") && language is not ("yaml" or "html" or "xml" or "css" or "shell"))
            return Role.Function;
        if (IsScript(language) && !before.IsEmpty && before[^1] is '{' or '(' or ',' && after.StartsWith(":"))
        {
            var parenthesis = before.LastIndexOf('(');
            return parenthesis > before.LastIndexOf(')') && parenthesis > before.LastIndexOf('{')
                ? Role.Parameter : Role.Property;
        }
        return Role.Plain;
    }

    private static bool IsScript(string language) => language is "typescript" or "tsx" or "javascript" or "jsx";

    // Arc CodeBlock's OKLCH role colors, expressed as sRGB. The muted ink is adjusted for Alder's code surface.
    private uint Color(Role role) => theme == HighlightThemeKind.Dark ? role switch
    {
        Role.Comment => 0xff99999f,
        Role.String or Role.Escape => 0xff64cf80,
        Role.Number => 0xfff2af48,
        Role.Keyword or Role.Constant => 0xffaea7ff,
        Role.Type or Role.Tag => 0xff4fc9fe,
        Role.Function => 0xffff9b93,
        Role.Property or Role.Parameter or Role.MarkupAttribute => 0xffdeb459,
        Role.Punctuation => 0xffb4b4b4,
        _ => 0
    } : role switch
    {
        Role.Comment => 0xff686870,
        Role.String or Role.Escape => 0xff006b2d,
        Role.Number => 0xff9d4700,
        Role.Keyword or Role.Constant => 0xff584cad,
        Role.Type or Role.Tag => 0xff006292,
        Role.Function => 0xff852e2c,
        Role.Property or Role.Parameter or Role.MarkupAttribute => 0xff704b00,
        Role.Punctuation => 0xff585858,
        _ => 0
    };
}
