using System.Collections.Concurrent;
using static Alder.NativeCode.Monarch.MonarchBuilder;

namespace Alder.NativeCode.Monarch;

/// <summary>Compiles only requested grammars. Sessions share immutable rules and retain their own line state.</summary>
internal static class AlderMonarch
{
    private static readonly Dictionary<string, Func<MonarchLanguage>> Grammars = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tsx"] = () => WithJsx(WithTemplateStrings(TypescriptLanguage.Create())),
        ["jsx"] = () => WithJsx(WithTemplateStrings(JavascriptLanguage.Create())),
        ["csharp"] = CsharpLanguage.Create,
        ["typescript"] = () => WithTemplateStrings(TypescriptLanguage.Create()),
        ["javascript"] = () => WithTemplateStrings(JavascriptLanguage.Create()),
        ["css"] = CssLanguage.Create,
        ["html"] = HtmlLanguage.Create,
        ["xml"] = XmlLanguage.Create,
        ["json"] = JsonLanguage.Create,
        ["python"] = PythonLanguage.Create,
        ["go"] = GoLanguage.Create,
        ["rust"] = RustLanguage.Create,
        ["java"] = JavaLanguage.Create,
        ["cpp"] = CppLanguage.Create,
        ["c"] = CLanguage.Create,
        ["sql"] = SqlLanguage.Create,
        ["shell"] = ShellLanguage.Create,
        ["powershell"] = PowershellLanguage.Create,
        ["yaml"] = YamlLanguage.Create,
        ["kotlin"] = KotlinLanguage.Create,
    };
    private static readonly ConcurrentDictionary<string, Lazy<MonarchTokenizer>> Cache = new(StringComparer.OrdinalIgnoreCase);

    internal static string Resolve(string name)
    {
        var identifier = name.AsSpan().Trim().TrimStart('{').TrimStart('.');
        var end = identifier.IndexOfAny(" \t\r\n,}");
        if (end >= 0)
            identifier = identifier[..end];
        if (identifier.StartsWith("language-", StringComparison.OrdinalIgnoreCase))
            identifier = identifier["language-".Length..];
        return identifier.ToString().ToLowerInvariant() switch
        {
            "c#" or "cs" => "csharp",
            "ts" or "text/typescript" or "application/typescript" => "typescript",
            "js" or "mjs" or "cjs" or "text/javascript" or "application/javascript" => "javascript",
            "py" => "python", "rs" => "rust", "yml" => "yaml", "xaml" or "axaml" or "text/xml" => "xml",
            "htm" or "text/html" => "html", "application/json" => "json",
            "bash" or "sh" or "zsh" => "shell", "ps1" or "pwsh" => "powershell", "c++" or "cxx" => "cpp",
            "text/css" => "css",
            var language => language
        };
    }

    private static MonarchLanguage WithTemplateStrings(MonarchLanguage language)
    {
        var grammar = language.Tokenizer;
        // Only the outer interpolation delimiters share the template string's color.
        grammar["string_backtick"] =
        [
            R(@"\$\{", "string", "@templateExpression"),
            R(@"\$(?!\{)", "string"),
            ..grammar["string_backtick"]
        ];
        grammar["templateExpression"] =
        [
            R(@"\{", "delimiter.bracket", "@bracketCounting"),
            R(@"\}", "string", "@pop"),
            Include("common")
        ];
        return language;
    }

    // Extend the base grammars without modifying generated Monarch rules. Normal JS/TS expressions
    // inside JSX attributes keep the original tokenizer and its nested-brace state.
    private static MonarchLanguage WithJsx(MonarchLanguage language)
    {
        var grammar = language.Tokenizer;
        grammar["common"] =
        [
            Include("@jsxOpening"),
            ..grammar["common"]
        ];
        grammar["jsxOpening"] =
        [
            R(@"(<)([A-Za-z][\w.:-]*)(?=[\s/>]|$)(?!>\s*\()", Groups("delimiter", Token("tag", next: "@jsxTag"))),
            R("<>", "delimiter", "@jsxContent")
        ];
        grammar["jsxTag"] =
        [
            R("/>", "delimiter", "@pop"),
            R(">", Token("delimiter", switchTo: "@jsxContent")),
            R(@"\{", "delimiter.bracket", "@bracketCounting"),
            R("=", "operator"),
            R("\"", "string", "@jsxDoubleQuote"),
            R("'", "string", "@jsxSingleQuote"),
            R(@"[\w.:-]+", "attribute"),
            R(@"\s+|.", "")
        ];
        grammar["jsxContent"] =
        [
            R(@"(</)([A-Za-z][\w.:-]*)(?=[\s>]|$)", Groups("delimiter", Token("tag", switchTo: "@jsxClosing"))),
            R("</>", "delimiter", "@pop"),
            Include("@jsxOpening"),
            R(@"\{", "delimiter.bracket", "@bracketCounting"),
            R("[^<{]+|.", "")
        ];
        grammar["jsxClosing"] = [R(">", "delimiter", "@pop"), R(@"\s+|.", "")];
        grammar["jsxDoubleQuote"] = [R(@"[^""]+", "string"), R("\"", "string", "@pop")];
        grammar["jsxSingleQuote"] = [R("[^']+", "string"), R("'", "string", "@pop")];
        return language;
    }

    internal static bool Supports(string name) => Grammars.ContainsKey(Resolve(name));
    internal static MonarchTokenizer? Get(string name)
    {
        var id = Resolve(name);
        return Grammars.ContainsKey(id) ? Cache.GetOrAdd(id, static key => new Lazy<MonarchTokenizer>(
            () => new(key, MonarchCompile.Compile(key, Grammars[key]())))).Value : null;
    }
}
