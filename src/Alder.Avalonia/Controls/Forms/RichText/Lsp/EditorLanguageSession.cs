using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Alder.Editor.Lsp;

/// <summary>One LSP session for the editor's plain-text projection. Use the editor thread for document requests and disposal.</summary>
public sealed class EditorLanguageSession : IAsyncDisposable
{
    private readonly RpcConnection rpc;
    private readonly EditorBuffer buffer;
    private readonly string uri;
    private int version = 1;
    private bool openClose;
    private int sync;
    private int closed;
    private Diagnostic[] diagnostics = [];
    /// <summary>The most recent diagnostics accepted for the current document version.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics => Volatile.Read(ref diagnostics);

    private EditorLanguageSession(EditorBuffer buffer, Stream input, Stream output, Uri documentUri)
    {
        this.buffer = buffer;
        uri = documentUri.AbsoluteUri;
        rpc = new(input, output);
        rpc.Notification += OnNotification;
    }

    internal static async Task<EditorLanguageSession> ConnectAsync(EditorBuffer buffer, Stream input, Stream output,
        Uri documentUri, string languageId, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageId);
        var session = new EditorLanguageSession(buffer, input, output, documentUri);
        try
        {
            var reply = await session.rpc.Request("initialize", w =>
            {
                w.WriteStartObject();
                w.WriteNull("processId"); w.WriteNull("rootUri");
                w.WriteStartObject("capabilities");
                w.WriteStartObject("general"); w.WriteStartArray("positionEncodings"); w.WriteStringValue("utf-16"); w.WriteEndArray(); w.WriteEndObject();
                w.WriteStartObject("textDocument");
                w.WriteStartObject("documentSymbol"); w.WriteBoolean("hierarchicalDocumentSymbolSupport", true); w.WriteEndObject();
                w.WriteEndObject(); w.WriteEndObject(); w.WriteEndObject();
            }, token);
            var capabilities = JsonSerializer.Deserialize(reply.Span, ProtocolJson.Default.InitializeResult)?.Capabilities ??
                throw new InvalidDataException("Missing language server capabilities.");
            if (capabilities.PositionEncoding is { } encoding && !encoding.Equals("utf-16", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException($"Unsupported position encoding '{encoding}'.");
            if (capabilities.TextDocumentSync is { } setting)
            {
                switch (setting)
                {
                    case int kind: session.sync = kind; session.openClose = kind != 0; break;
                    case SyncOptions options: session.sync = options.Change; session.openClose = options.OpenClose; break;
                }
            }
            session.rpc.Notify("initialized", w => { w.WriteStartObject(); w.WriteEndObject(); });
            if (session.openClose)
                session.rpc.Notify("textDocument/didOpen", w =>
                {
                    w.WriteStartObject(); w.WriteStartObject("textDocument"); w.WriteString("uri", session.uri);
                    w.WriteString("languageId", languageId); w.WriteNumber("version", session.version);
                    w.WriteString("text", buffer.Text); w.WriteEndObject(); w.WriteEndObject();
                });
            buffer.Changed += session.Edited;
            return session;
        }
        catch { await session.rpc.DisposeAsync(); throw; }
    }

    private void Edited(TextEdit change)
    {
        Interlocked.Increment(ref version);
        Volatile.Write(ref diagnostics, []);
        if (sync == 0) return;
        try
        {
            rpc.Notify("textDocument/didChange", w =>
            {
                w.WriteStartObject(); w.WriteStartObject("textDocument"); w.WriteString("uri", uri);
                w.WriteNumber("version", version); w.WriteEndObject(); w.WriteStartArray("contentChanges"); w.WriteStartObject();
                if (sync == 2)
                {
                    w.WritePropertyName("range"); JsonSerializer.Serialize(w, change.Range, ProtocolJson.Default.TextRange);
                    w.WriteString("text", change.Text);
                }
                else w.WriteString("text", buffer.Text);
                w.WriteEndObject(); w.WriteEndArray(); w.WriteEndObject();
            });
        }
        catch (IOException) { buffer.Changed -= Edited; }
    }

    private void OnNotification(string method, ReadOnlyMemory<byte> payload)
    {
        if (method != "textDocument/publishDiagnostics") return;
        var report = JsonSerializer.Deserialize(payload.Span, ProtocolJson.Default.PublishedDiagnostics);
        if (report?.Uri == uri && (report.Version is null || report.Version == Volatile.Read(ref version)))
            Volatile.Write(ref diagnostics, report.Diagnostics ?? []);
    }

    /// <summary>Requests completion candidates at a document position.</summary>
    public async Task<IReadOnlyList<Completion>> GetCompletionsAsync(TextPosition position, CancellationToken token = default)
    {
        var result = await Query("completion", w => Position(w, position), ProtocolJson.Default.CompletionResponse, token);
        return result switch { Completion[] items => items, CompletionList list => list.Items, _ => [] };
    }

    /// <summary>Requests hover information at a document position.</summary>
    public Task<Hover?> GetHoverAsync(TextPosition position, CancellationToken token = default) =>
        Query("hover", w => Position(w, position), ProtocolJson.Default.Hover, token);

    /// <summary>Requests the symbol outline of the current document.</summary>
    public async Task<IReadOnlyList<Symbol>> GetDocumentSymbolsAsync(CancellationToken token = default) =>
        await Query("documentSymbol", null, ProtocolJson.Default.SymbolArray, token) ?? [];

    /// <summary>Requests inlay hints within a document range.</summary>
    public async Task<IReadOnlyList<InlayHint>> GetInlayHintsAsync(TextRange range, CancellationToken token = default) =>
        await Query("inlayHint", w =>
        {
            w.WritePropertyName("range"); JsonSerializer.Serialize(w, range, ProtocolJson.Default.TextRange);
        }, ProtocolJson.Default.InlayHintArray, token) ?? [];

    private async Task<T?> Query<T>(string method, Action<Utf8JsonWriter>? extra, JsonTypeInfo<T> type, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref closed) != 0, this);
        var reply = await rpc.Request("textDocument/" + method, w =>
        {
            w.WriteStartObject(); WriteIdentifier(w); extra?.Invoke(w); w.WriteEndObject();
        }, token).ConfigureAwait(false);
        return reply.Span.SequenceEqual("null"u8) ? default : JsonSerializer.Deserialize(reply.Span, type);
    }

    private void WriteIdentifier(Utf8JsonWriter w)
    {
        w.WriteStartObject("textDocument"); w.WriteString("uri", uri); w.WriteEndObject();
    }
    private static void Position(Utf8JsonWriter w, TextPosition position)
    {
        w.WritePropertyName("position"); JsonSerializer.Serialize(w, position, ProtocolJson.Default.TextPosition);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref closed, 1) != 0) return;
        buffer.Changed -= Edited;
        rpc.Notification -= OnNotification;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            if (openClose) rpc.Notify("textDocument/didClose", w => { w.WriteStartObject(); WriteIdentifier(w); w.WriteEndObject(); });
            await rpc.Request("shutdown", null, timeout.Token).ConfigureAwait(false);
            rpc.Notify("exit", null);
        }
        catch (Exception e) when (e is OperationCanceledException or IOException) { }
        finally { await rpc.DisposeAsync().ConfigureAwait(false); }
    }
}
