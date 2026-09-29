using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Translator.Desktop;

internal abstract class RealtimeWebSocketSession : ILiveSession
{
    private readonly ClientWebSocket _socket = new();
    private readonly Channel<byte[]> _audio = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(60)
    {
        SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.DropOldest
    });
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly StreamingTranscript _transcript = new();
    private readonly Stopwatch _elapsed = new();
    private Task? _sendTask;
    private Task? _receiveTask;
    private string _key = "";

    public event Action<IReadOnlyList<TranscriptSegment>>? TextChanged;
    public event Action<string>? Failed;
    public int CommittedOriginalCount => _transcript.CommittedOriginalCount;
    protected TimeSpan Elapsed => _elapsed.Elapsed;
    protected abstract string ProviderName { get; }
    protected abstract Uri Endpoint(string key);
    protected abstract void ConfigureSocket(ClientWebSocketOptions options, string key);
    protected abstract string SetupMessage(string targetLanguage, string? sourceLanguage);
    protected abstract string AudioMessage(byte[] pcm16);
    protected abstract string? FinishMessage { get; }
    protected abstract bool IsReady(JsonElement message);
    protected virtual bool IsFinished(JsonElement message) => false;
    protected abstract void HandleContent(JsonElement message);

    public async Task ConnectAsync(string key, string targetLanguage, string? sourceLanguage, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException(ProviderName + " " + UiLocalizer.T("API anahtarı gerekli."));
        _key = key;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(18));
        try
        {
            ConfigureSocket(_socket.Options, key);
            await _socket.ConnectAsync(Endpoint(key), timeout.Token);
            _elapsed.Start();
            _receiveTask = ReceiveLoopAsync();
            await SendTextAsync(SetupMessage(targetLanguage, sourceLanguage), timeout.Token);
            await _ready.Task.WaitAsync(timeout.Token);
            _sendTask = SendLoopAsync();
        }
        catch (Exception ex)
        {
            _socket.Abort();
            throw new InvalidOperationException($"{ProviderName} {UiLocalizer.T("bağlantısı kurulamadı:")} {Clean(ex.Message)}", ex);
        }
    }

    public bool TryQueueAudio(byte[] data) => _audio.Writer.TryWrite(data);

    private async Task SendLoopAsync()
    {
        try
        {
            await foreach (var data in _audio.Reader.ReadAllAsync())
                await SendTextAsync(AudioMessage(data), CancellationToken.None);
            if (_socket.State == WebSocketState.Open && FinishMessage is { } final)
                await SendTextAsync(final, CancellationToken.None);
        }
        catch (Exception ex) { Failed?.Invoke(ProviderName + ": " + Clean(ex.Message)); }
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[65536];
        using var message = new MemoryStream();
        try
        {
            while (_socket.State is WebSocketState.Open or WebSocketState.CloseSent)
            {
                var part = await _socket.ReceiveAsync(buffer, CancellationToken.None);
                if (part.MessageType == WebSocketMessageType.Close) break;
                if (part.MessageType != WebSocketMessageType.Text) continue;
                message.Write(buffer, 0, part.Count);
                if (!part.EndOfMessage) continue;
                var finished = ProcessServerMessage(Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length));
                message.SetLength(0);
                if (finished) break;
            }
            if (!_ready.Task.IsCompleted) _ready.TrySetException(new InvalidOperationException(UiLocalizer.T("Sunucu bağlantıyı kurulumdan önce kapattı.")));
        }
        catch (Exception ex)
        {
            var detail = ProviderName + ": " + Clean(ex.Message);
            _ready.TrySetException(new InvalidOperationException(detail));
            Failed?.Invoke(detail);
        }
    }

    internal bool ProcessServerMessage(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (TryReadError(root, out var error))
        {
            var detail = ProviderName + ": " + Clean(error);
            _ready.TrySetException(new InvalidOperationException(detail));
            Failed?.Invoke(detail);
            return false;
        }
        if (IsReady(root)) _ready.TrySetResult();
        HandleContent(root);
        return IsFinished(root);
    }

    protected void Original(string text, TimeSpan? start = null)
    {
        _transcript.AppendOriginal(text, start ?? Elapsed);
        TextChanged?.Invoke(_transcript.Segments);
    }

    protected void Translation(string text)
    {
        _transcript.AppendTranslation(text);
        TextChanged?.Invoke(_transcript.Segments);
    }

    public async Task StopAsync()
    {
        _audio.Writer.TryComplete();
        if (_sendTask is not null) await _sendTask;
        else if (_socket.State == WebSocketState.Open && FinishMessage is { } final)
        {
            try { await SendTextAsync(final, CancellationToken.None); } catch { }
        }
        if (_receiveTask is not null)
        {
            if (await Task.WhenAny(_receiveTask, Task.Delay(5000)) != _receiveTask) _socket.Abort();
            try { await _receiveTask; } catch { }
        }
        _transcript.Finish();
        TextChanged?.Invoke(_transcript.Segments);
        _socket.Dispose();
    }

    private Task SendTextAsync(string text, CancellationToken cancellation) =>
        _socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, cancellation);

    private string Clean(string value) => string.IsNullOrEmpty(_key) ? value : value.Replace(_key, UiLocalizer.T("[API anahtarı]"), StringComparison.Ordinal);

    private static bool TryReadError(JsonElement root, out string error)
    {
        error = "";
        if (!root.TryGetProperty("error", out var value))
        {
            if (root.TryGetProperty("type", out var kind) && kind.GetString() is "error" or "session.error")
            {
                error = root.TryGetProperty("message", out var detail) ? detail.GetString() ?? UiLocalizer.T("İstek reddedildi.") : UiLocalizer.T("İstek reddedildi.");
                return true;
            }
            return false;
        }
        error = value.ValueKind == JsonValueKind.Object && value.TryGetProperty("message", out var message)
            ? message.GetString() ?? UiLocalizer.T("İstek reddedildi.") : value.ToString();
        return true;
    }
}
