namespace Translator.Desktop;

// Both capture devices produce 16 kHz, mono, signed 16-bit PCM. A single
// 100 ms clock prevents two independent callbacks from doubling stream speed.
internal sealed class Pcm16AudioMixer : IDisposable
{
    private const int FrameBytes = 3200;
    private const int MaxQueuedBytes = FrameBytes * 20;
    private readonly Queue<byte> _application = new();
    private readonly Queue<byte> _microphone = new();
    private readonly object _gate = new();
    private Timer? _timer;
    private bool _disposed;
    private bool _paused;

    public event Action<byte[]>? AudioAvailable;

    public void AddApplication(byte[] data) => Add(_application, data);
    public void AddMicrophone(byte[] data) => Add(_microphone, data);

    private void Add(Queue<byte> queue, byte[] data)
    {
        lock (_gate)
        {
            if (_disposed || _paused) return;
            foreach (var value in data) queue.Enqueue(value);
            while (queue.Count > MaxQueuedBytes) queue.Dequeue();
        }
    }

    public void Start() => _timer = new Timer(_ => EmitFrame(), null, 100, 100);

    public void SetPaused(bool paused)
    {
        lock (_gate)
        {
            _paused = paused;
            _application.Clear();
            _microphone.Clear();
        }
    }

    private void EmitFrame()
    {
        byte[]? frame;
        lock (_gate)
        {
            if (_disposed || _paused || (_application.Count == 0 && _microphone.Count == 0)) return;
            frame = MixFrame(_application, _microphone);
        }
        AudioAvailable?.Invoke(frame);
    }

    internal static byte[] MixFrame(Queue<byte> application, Queue<byte> microphone)
    {
        var output = new byte[FrameBytes];
        for (var i = 0; i < FrameBytes; i += 2)
        {
            var app = ReadSample(application);
            var mic = ReadSample(microphone);
            // Give the microphone equal intelligibility while preserving headroom.
            var mixed = Math.Clamp((int)Math.Round(app * 0.7 + mic * 0.7), short.MinValue, short.MaxValue);
            output[i] = (byte)mixed;
            output[i + 1] = (byte)(mixed >> 8);
        }
        return output;
    }

    private static short ReadSample(Queue<byte> queue)
    {
        if (queue.Count < 2) return 0;
        var low = queue.Dequeue();
        var high = queue.Dequeue();
        return (short)(low | high << 8);
    }

    public void Dispose()
    {
        lock (_gate) _disposed = true;
        if (_timer is not null)
        {
            using var finished = new ManualResetEvent(false);
            if (_timer.Dispose(finished)) finished.WaitOne(TimeSpan.FromSeconds(2));
        }
        _timer = null;
    }
}
