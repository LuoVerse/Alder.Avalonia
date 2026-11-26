using System.Text.Json;
using System.Text.Json.Serialization;

namespace Alder.Editor.Lsp;

/// <summary>A zero-based LSP position with a UTF-16 character offset.</summary>
public readonly record struct TextPosition(int Line, int Character);
/// <summary>A range with an inclusive start and an exclusive end.</summary>
public readonly record struct TextRange(TextPosition Start, TextPosition End);
/// <summary>Content marked as plain text or Markdown by the language server.</summary>
public sealed record Markup(string Kind, string Value);
/// <summary>A hover code fragment with its syntax-highlighting language.</summary>
public sealed record CodeSample(string Language, string Value);
/// <summary>Plain or marked-up documentation for a language feature.</summary>
public union Documentation(string, Markup);
/// <summary>A diagnostic identifier supplied by a language server.</summary>
public union DiagnosticCode(long, string);
/// <summary>A diagnostic associated with a range of the current document.</summary>
public sealed record Diagnostic(TextRange Range, string Message, DiagnosticCode? Code, int? Severity);
/// <summary>Text to substitute for a single range.</summary>
public sealed record Replacement(TextRange Range, string NewText);
/// <summary>A completion edit with separate ranges for insert and replace modes.</summary>
public sealed record InsertReplace(TextRange Insert, TextRange Replace, string NewText);
/// <summary>The edit to apply when accepting a completion.</summary>
[JsonUnion(TypeClassifier = typeof(ProtocolShape))]
public union CompletionEdit(Replacement, InsertReplace);
/// <summary>A completion candidate and its optional insertion and documentation details.</summary>
public sealed record Completion(string Label, string? InsertText, string? Detail, Documentation? Documentation, CompletionEdit? TextEdit);
/// <summary>Completion candidates with an indication of whether further typing should trigger another request.</summary>
public sealed record CompletionList(Completion[] Items, bool IsIncomplete);
/// <summary>The two response shapes accepted for an LSP completion request.</summary>
public union CompletionResponse(Completion[], CompletionList);

/// <summary>A single text, markup, or code entry in a hover response.</summary>
[JsonUnion(TypeClassifier = typeof(ProtocolShape))]
public union HoverPart(string, Markup, CodeSample);
/// <summary>The content of a hover response, which may contain several entries.</summary>
[JsonUnion(TypeClassifier = typeof(ProtocolShape))]
public union HoverContents(string, Markup, CodeSample, HoverPart[]);
/// <summary>Hover content and the optional range to which it applies.</summary>
public sealed record Hover(HoverContents Contents, TextRange? Range);

/// <summary>A symbol in a document outline with its optional children.</summary>
public sealed record DocumentSymbol(string Name, int Kind, TextRange Range, TextRange SelectionRange, DocumentSymbol[]? Children);
/// <summary>A range in the document identified by a URI.</summary>
public sealed record Location(string Uri, TextRange Range);
/// <summary>An entry returned by a language server that provides a flat symbol list.</summary>
public sealed record SymbolInformation(string Name, int Kind, Location Location, string? ContainerName);
/// <summary>A hierarchical or flat document symbol.</summary>
[JsonUnion(TypeClassifier = typeof(ProtocolShape))]
public union Symbol(DocumentSymbol, SymbolInformation);
/// <summary>One segment of an inlay hint label with an optional tooltip.</summary>
public sealed record LabelPart(string Value, Documentation? Tooltip);
/// <summary>Inlay hint text or an array of individually annotated label segments.</summary>
public union InlayLabel(string, LabelPart[]);
/// <summary>An inline annotation supplied by the language server.</summary>
public sealed record InlayHint(TextPosition Position, InlayLabel Label, int? Kind, Documentation? Tooltip);

internal sealed record SyncOptions(bool OpenClose, int Change);
internal union DocumentSync(int, SyncOptions);
internal sealed record ServerCapabilities(DocumentSync? TextDocumentSync, string? PositionEncoding);
internal sealed record InitializeResult(ServerCapabilities Capabilities);
internal sealed record PublishedDiagnostics(string Uri, int? Version, Diagnostic[] Diagnostics);
internal union MessageId(long, string);

/// <summary>LSP leaves alternatives untagged. Inspect a copy of the reader at object boundaries only.</summary>
internal sealed class ProtocolShape : JsonTypeClassifierFactory
{
    public override bool CanClassify(JsonTypeClassifierContext context) =>
        context.DeclaringType == typeof(CompletionEdit) || context.DeclaringType == typeof(Symbol) ||
        context.DeclaringType == typeof(HoverPart) || context.DeclaringType == typeof(HoverContents);

    public override JsonTypeClassifier CreateJsonClassifier(JsonTypeClassifierContext context, JsonSerializerOptions options)
    {
        if (context.DeclaringType == typeof(CompletionEdit)) return EditType;
        if (context.DeclaringType == typeof(Symbol)) return SymbolType;
        return HoverType;
    }

    private static Type? EditType(ref Utf8JsonReader reader) =>
        Contains(reader, "range"u8) ? typeof(Replacement) : typeof(InsertReplace);
    private static Type? SymbolType(ref Utf8JsonReader reader) =>
        Contains(reader, "location"u8) ? typeof(SymbolInformation) : typeof(DocumentSymbol);
    private static Type? HoverType(ref Utf8JsonReader reader) => reader.TokenType switch
    {
        JsonTokenType.StartArray => typeof(HoverPart[]),
        JsonTokenType.String => typeof(string),
        JsonTokenType.StartObject => Contains(reader, "language"u8) ? typeof(CodeSample) : typeof(Markup),
        _ => null
    };

    private static bool Contains(Utf8JsonReader cursor, ReadOnlySpan<byte> key)
    {
        if (cursor.TokenType != JsonTokenType.StartObject) return false;
        while (cursor.Read() && cursor.TokenType == JsonTokenType.PropertyName)
        {
            if (cursor.ValueTextEquals(key)) return true;
            cursor.Read();
            cursor.Skip();
        }
        return false;
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(InitializeResult))]
[JsonSerializable(typeof(CompletionResponse))]
[JsonSerializable(typeof(Hover))]
[JsonSerializable(typeof(Symbol[]))]
[JsonSerializable(typeof(InlayHint[]))]
[JsonSerializable(typeof(PublishedDiagnostics))]
[JsonSerializable(typeof(MessageId))]
[JsonSerializable(typeof(TextPosition))]
[JsonSerializable(typeof(TextRange))]
internal sealed partial class ProtocolJson : JsonSerializerContext;
