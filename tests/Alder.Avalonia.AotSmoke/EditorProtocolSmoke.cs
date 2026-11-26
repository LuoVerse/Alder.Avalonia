using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Alder.Editor;
using Alder.Editor.Lsp;

namespace Alder.Avalonia.AotSmoke;

internal static class EditorProtocolSmoke
{
    // Exercise the same frames as an external process, including fragmented headers and raw UTF-8.
    public static async Task Run()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var token = timeout.Token;
        var requests = new Pipe();
        var responses = new Pipe();
        await using var serverInput = requests.Reader.AsStream();
        await using var serverOutput = responses.Writer.AsStream();
        await using var clientInput = responses.Reader.AsStream();
        await using var clientOutput = requests.Writer.AsStream();
        var server = Serve(serverInput, serverOutput, token);
        var buffer = new EditorBuffer();
        buffer.Replace(0, 0, "a😀\nsecond");
        await using (var client = await EditorLanguageSession.ConnectAsync(buffer, clientInput, clientOutput,
            new Uri("file:///notes.txt"), "plaintext", token))
        {
            buffer.Replace(3, 0, "好");
            var position = new TextPosition(0, 4);
            var completions = await client.GetCompletionsAsync(position, token);
            Require(completions is [{ Label: "完成", TextEdit: { } edit }] && edit is Replacement);
            var hover = await client.GetHoverAsync(position, token);
            Require(hover?.Contents is { } contents && contents is HoverPart[] parts && parts.Length == 2 && parts[1] is CodeSample);
            var symbols = await client.GetDocumentSymbolsAsync(token);
            Require(symbols.Count == 2 && symbols[0] is DocumentSymbol && symbols[1] is SymbolInformation);
            var hints = await client.GetInlayHintsAsync(new(new(0, 0), position), token);
            Require(hints is [{ Label: var label }] && label is LabelPart[] { Length: 1 });
            Require(client.Diagnostics is [{ Code: { } code }] && code is long number && number == 42);
        }
        await server;
    }

    private static async Task Serve(Stream input, Stream output, CancellationToken token)
    {
        bool opened = false, changed = false, closed = false, shutdown = false;
        const string range = """{"start":{"line":0,"character":0},"end":{"line":0,"character":1}}""";
        while (true)
        {
            using var frame = JsonDocument.Parse(await ReadFrame(input, token));
            var root = frame.RootElement;
            var method = root.GetProperty("method").GetString();
            var args = root.TryGetProperty("params", out var p) ? p : default;
            string result;
            switch (method)
            {
                case "initialize":
                    result = """{"capabilities":{"textDocumentSync":{"openClose":true,"change":2},"positionEncoding":"utf-16"}}""";
                    break;
                case "initialized": continue;
                case "textDocument/didOpen":
                    Require(args.GetProperty("textDocument").GetProperty("text").GetString() == "a😀\nsecond");
                    opened = true; continue;
                case "textDocument/didChange":
                    var delta = args.GetProperty("contentChanges")[0];
                    Require(opened && delta.GetProperty("range").GetProperty("start").GetProperty("character").GetInt32() == 3 &&
                        delta.GetProperty("text").GetString() == "好");
                    changed = true;
                    await WriteFrame(output, """{"jsonrpc":"2.0","method":"textDocument/publishDiagnostics","params":{"uri":"file:///notes.txt","version":2,"diagnostics":[{"range":R,"message":"Spelling","code":42}]}}""".Replace(":R", ":" + range), token);
                    continue;
                case "textDocument/completion":
                    Require(changed);
                    result = """{"items":[{"label":"完成","textEdit":{"newText":"好","range":R}}],"isIncomplete":false}"""; break;
                case "textDocument/hover":
                    result = """{"contents":["note",{"value":"hello","language":"text"}]}"""; break;
                case "textDocument/documentSymbol":
                    result = """[{"name":"A","kind":2,"range":R,"selectionRange":R},{"name":"B","kind":4,"location":{"uri":"file:///notes.txt","range":R}}]"""; break;
                case "textDocument/inlayHint":
                    result = """[{"position":{"line":0,"character":0},"label":[{"value":"x","tooltip":{"kind":"plaintext","value":"hint"}}]}]"""; break;
                case "textDocument/didClose": closed = true; continue;
                case "shutdown": Require(closed); shutdown = true; result = "null"; break;
                case "exit": Require(shutdown); return;
                default: throw new InvalidOperationException(method);
            }
            var id = root.GetProperty("id").GetInt64();
            await WriteFrame(output, "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":" + result.Replace(":R", ":" + range) + "}", token);
        }
    }

    private static async Task<byte[]> ReadFrame(Stream stream, CancellationToken token)
    {
        using var header = new MemoryStream();
        var one = new byte[1];
        while (header.Length < 1024)
        {
            await stream.ReadExactlyAsync(one, token);
            header.WriteByte(one[0]);
            if (header.GetBuffer().AsSpan(0, (int)header.Length).EndsWith("\r\n\r\n"u8)) break;
        }
        var size = int.Parse(Encoding.ASCII.GetString(header.ToArray()).Split(':')[1].Trim());
        var body = new byte[size];
        await stream.ReadExactlyAsync(body, token);
        return body;
    }
    private static async Task WriteFrame(Stream stream, string json, CancellationToken token)
    {
        var body = Encoding.UTF8.GetBytes(json);
        await stream.WriteAsync("Content-"u8.ToArray(), token);
        await stream.WriteAsync(Encoding.ASCII.GetBytes($"Length: {body.Length}\r\n\r\n"), token);
        await stream.WriteAsync(body, token);
        await stream.FlushAsync(token);
    }
    private static void Require(bool ok)
    {
        if (!ok) throw new InvalidOperationException("Editor LSP protocol smoke assertion failed.");
    }
}
