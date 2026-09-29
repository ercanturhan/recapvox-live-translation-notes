using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace Translator.Desktop;

internal sealed class SonioxSession : ILiveSession
{
    private readonly ClientWebSocket _socket = new();
    private readonly Channel<byte[]> _audio = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(60)
    {
        SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.DropOldest
    });
    private readonly TranscriptAssembler _transcript = new();
    private Task? _sendTask;
    private Task? _receiveTask;

    public event Action<IReadOnlyList<TranscriptSegment>>? TextChanged;
    public event Action<string>? Failed;
    public int CommittedOriginalCount => _transcript.CommittedOriginalCount;

    public Task ConnectAsync(string key, string targetLanguage, string? sourceLanguage, CancellationToken cancellation) =>
        ConnectAsync(key, targetLanguage, sourceLanguage, cancellation, true);

    public async Task ConnectAsync(string key, string targetLanguage, string? sourceLanguage, CancellationToken cancellation, bool sonioxTranslation, string? clientReferenceId = null)
    {
        await _socket.ConnectAsync(new Uri("wss://stt-rt.soniox.com/transcribe-websocket"), cancellation);
        var config = JsonSerializer.Serialize(new
        {
            api_key = key,
            client_reference_id = clientReferenceId,
            model = "stt-rt-v5",
            audio_format = "pcm_s16le",
            sample_rate = 16000,
            num_channels = 1,
            language_hints = string.IsNullOrWhiteSpace(sourceLanguage) ? null : new[] { sourceLanguage },
            language_hints_strict = string.IsNullOrWhiteSpace(sourceLanguage) ? (bool?)null : true,
            enable_endpoint_detection = true,
            translation = sonioxTranslation ? new { type = "one_way", target_language = targetLanguage } : null
        }, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
        var configBytes = Encoding.UTF8.GetBytes(config);
        await _socket.SendAsync(configBytes, WebSocketMessageType.Text, true, cancellation);
        _sendTask = SendLoopAsync();
        _receiveTask = ReceiveLoopAsync();
    }

    public bool TryQueueAudio(byte[] data) => _audio.Writer.TryWrite(data);

    private async Task SendLoopAsync()
    {
        try
        {
            await foreach (var data in _audio.Reader.ReadAllAsync())
                await _socket.SendAsync(data, WebSocketMessageType.Binary, true, CancellationToken.None);
            if (_socket.State == WebSocketState.Open)
                await _socket.SendAsync(ReadOnlyMemory<byte>.Empty, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (Exception ex) { Failed?.Invoke(ex.Message); }
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[65536];
        using var message = new MemoryStream();
        try
        {
            while (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseSent)
            {
                var part = await _socket.ReceiveAsync(buffer, CancellationToken.None);
                if (part.MessageType == WebSocketMessageType.Close) break;
                message.Write(buffer, 0, part.Count);
                if (!part.EndOfMessage) continue;
                using var document = JsonDocument.Parse(message.ToArray());
                message.SetLength(0);
                var root = document.RootElement;
                if (root.TryGetProperty("error_code", out var error) && error.ValueKind != JsonValueKind.Null)
                {
                    var detail = root.TryGetProperty("error_message", out var messageValue) ? messageValue.ToString() : error.ToString();
                    Failed?.Invoke(detail);
                    break;
                }
                if (root.TryGetProperty("tokens", out var tokens))
                {
                    _transcript.Process(tokens);
                    TextChanged?.Invoke(_transcript.Segments);
                }
                if (root.TryGetProperty("finished", out var finished) && finished.ValueKind == JsonValueKind.True) break;
            }
        }
        catch (Exception ex) { Failed?.Invoke(ex.Message); }
    }

    public async Task StopAsync()
    {
        _audio.Writer.TryComplete();
        if (_sendTask is not null) await _sendTask;
        if (_receiveTask is not null)
        {
            var completed = await Task.WhenAny(_receiveTask, Task.Delay(3000));
            if (completed != _receiveTask) _socket.Abort();
            try { await _receiveTask; } catch { }
        }
        _transcript.Finish();
        TextChanged?.Invoke(_transcript.Segments);
        _socket.Dispose();
    }
}
