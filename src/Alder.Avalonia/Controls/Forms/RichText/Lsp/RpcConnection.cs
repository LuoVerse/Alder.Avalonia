using System.Buffers;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Alder.Editor.Lsp;

/// <summary>Owns the message pumps. The caller retains ownership of both streams.</summary>
internal sealed class RpcConnection : IAsyncDisposable
{
    private readonly Stream input, output;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Channel<byte[]> writes = Channel.CreateUnbounded<byte[]>(new() { SingleReader = true });
    private readonly ConcurrentDictionary<long, TaskCompletionSource<ReadOnlyMemory<byte>>> pending = new();
    private readonly Task reader, writer;
    private readonly byte[] readBuffer = new byte[8192];
    private int cursor, end;
    private long sequence;
    private Exception? failure;
    internal event Action<string, ReadOnlyMemory<byte>>? Notification;

    internal RpcConnection(Stream input, Stream output)
    {
        this.input = input;
        this.output = output;
        reader = Task.Run(ReadMessages);
        writer = Task.Run(WriteMessages);
    }

    internal async Task<ReadOnlyMemory<byte>> Request(string method, Action<Utf8JsonWriter>? parameters, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var id = Interlocked.Increment(ref sequence);
        var result = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[id] = result;
        try
        {
            Send(method, parameters, id);
            return await result.Task.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (!lifetime.IsCancellationRequested) Notify("$/cancelRequest", w => { w.WriteStartObject(); w.WriteNumber("id", id); w.WriteEndObject(); });
            throw;
        }
        finally { pending.TryRemove(id, out _); }
    }

    internal void Notify(string method, Action<Utf8JsonWriter>? parameters) => Send(method, parameters, null);

    private void Send(string method, Action<Utf8JsonWriter>? parameters, long? id)
    {
        Enqueue(w =>
        {
            if (id.HasValue) w.WriteNumber("id", id.Value);
            w.WriteString("method", method);
            if (parameters is not null) { w.WritePropertyName("params"); parameters(w); }
        });
    }

    private void Enqueue(Action<Utf8JsonWriter> body)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("jsonrpc", "2.0");
            body(json);
            json.WriteEndObject();
        }
        if (!writes.Writer.TryWrite(buffer.WrittenSpan.ToArray())) throw new IOException("Language server connection closed.", failure);
    }

    private async Task WriteMessages()
    {
        try
        {
            await foreach (var frame in writes.Reader.ReadAllAsync(lifetime.Token).ConfigureAwait(false))
            {
                var header = Encoding.ASCII.GetBytes(FormattableString.Invariant($"Content-Length: {frame.Length}\r\n\r\n"));
                await output.WriteAsync(header, lifetime.Token).ConfigureAwait(false);
                await output.WriteAsync(frame, lifetime.Token).ConfigureAwait(false);
                await output.FlushAsync(lifetime.Token).ConfigureAwait(false);
            }
        }
        catch (Exception e) { Stop(e); }
    }

    private async Task ReadMessages()
    {
        var header = new byte[8192];
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                int n = 0;
                do
                {
                    if (n == header.Length) throw new InvalidDataException("LSP header too large.");
                    if (cursor == end)
                    {
                        end = await input.ReadAsync(readBuffer, lifetime.Token).ConfigureAwait(false);
                        if (end == 0) throw new EndOfStreamException();
                        cursor = 0;
                    }
                    header[n++] = readBuffer[cursor++];
                } while (n < 4 || !header.AsSpan(n - 4, 4).SequenceEqual("\r\n\r\n"u8));
                int length = ContentLength(header.AsSpan(0, n));
                var frame = new byte[length];
                int copied = Math.Min(end - cursor, length);
                readBuffer.AsSpan(cursor, copied).CopyTo(frame);
                cursor += copied;
                await input.ReadExactlyAsync(frame.AsMemory(copied), lifetime.Token).ConfigureAwait(false);
                Dispatch(frame);
            }
        }
        catch (Exception e) { Stop(e); }
    }

    private static int ContentLength(ReadOnlySpan<byte> header)
    {
        int length = -1;
        foreach (var slice in header.Split((byte)'\n'))
        {
            var line = header[slice].Trim((byte)'\r');
            var split = line.IndexOf((byte)':');
            if (split < 0 || !Ascii.EqualsIgnoreCase(line[..split], "Content-Length"u8)) continue;
            var value = line[(split + 1)..].Trim((byte)' ');
            if (length != -1 || !Utf8Parser.TryParse(value, out length, out int used) || used != value.Length || length is < 1 or > 32 * 1024 * 1024)
                throw new InvalidDataException("Invalid LSP frame length.");
        }
        return length > 0 ? length : throw new InvalidDataException("Missing Content-Length.");
    }

    private void Dispatch(byte[] frame)
    {
        var r = new Utf8JsonReader(frame);
        if (!r.Read() || r.TokenType != JsonTokenType.StartObject) throw new JsonException();
        MessageId? id = null;
        string? method = null;
        ReadOnlyMemory<byte> result = default, args = default, error = default;
        while (r.Read() && r.TokenType == JsonTokenType.PropertyName)
        {
            bool isId = r.ValueTextEquals("id"u8), isMethod = r.ValueTextEquals("method"u8), isResult = r.ValueTextEquals("result"u8);
            bool isParams = r.ValueTextEquals("params"u8), isError = r.ValueTextEquals("error"u8);
            r.Read();
            int start = (int)r.TokenStartIndex;
            if (isId && r.TokenType != JsonTokenType.Null) id = JsonSerializer.Deserialize(ref r, ProtocolJson.Default.MessageId);
            else if (isMethod) method = r.GetString();
            r.Skip();
            var slice = frame.AsMemory(start, (int)r.BytesConsumed - start);
            if (isResult) result = slice;
            if (isParams) args = slice;
            if (isError) error = slice;
        }
        if (method is null && id is { } response && response is long key && pending.TryRemove(key, out var completion))
        {
            if (!error.IsEmpty) completion.TrySetException(new InvalidOperationException(Encoding.UTF8.GetString(error.Span)));
            else completion.TrySetResult(result);
        }
        else if (method is not null && id is { } request)
        {
            Enqueue(w =>
            {
                w.WritePropertyName("id");
                JsonSerializer.Serialize(w, request, ProtocolJson.Default.MessageId);
                w.WriteStartObject("error"); w.WriteNumber("code", -32601); w.WriteString("message", "Method not supported"); w.WriteEndObject();
            });
        }
        else if (method is not null) Notification?.Invoke(method, args);
    }

    private void Stop(Exception reason)
    {
        Interlocked.CompareExchange(ref failure, reason, null);
        writes.Writer.TryComplete();
        lifetime.Cancel();
        foreach (var entry in pending) entry.Value.TrySetException(reason);
    }

    public async ValueTask DisposeAsync()
    {
        writes.Writer.TryComplete();
        try { await writer.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
        catch (TimeoutException) { }
        Stop(new ObjectDisposedException(nameof(RpcConnection)));
        await Task.WhenAll(reader, writer).ConfigureAwait(false);
        lifetime.Dispose();
    }
}
