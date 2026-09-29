using System.Runtime.InteropServices;

namespace Translator.Desktop;

internal sealed class MicrophoneCapture : IDisposable
{
    private const uint CallbackFunction = 0x00030000;
    private const uint DataMessage = 0x3C0;
    private readonly WaveInProc _callback;
    private readonly List<(IntPtr Header, IntPtr Data)> _buffers = [];
    private IntPtr _device;
    private bool _running;

    public event Action<byte[]>? AudioAvailable;

    public MicrophoneCapture() => _callback = OnWaveIn;

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveFormat
    {
        public ushort FormatTag;
        public ushort Channels;
        public uint SamplesPerSec;
        public uint AvgBytesPerSec;
        public ushort BlockAlign;
        public ushort BitsPerSample;
        public ushort ExtraSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public IntPtr Data;
        public uint BufferLength;
        public uint BytesRecorded;
        public IntPtr User;
        public uint Flags;
        public uint Loops;
        public IntPtr Next;
        public IntPtr Reserved;
    }

    private delegate void WaveInProc(IntPtr handle, uint message, IntPtr instance, IntPtr parameter1, IntPtr parameter2);

    [DllImport("winmm.dll")]
    private static extern uint waveInOpen(out IntPtr handle, uint deviceId, ref WaveFormat format, WaveInProc callback, IntPtr instance, uint flags);
    [DllImport("winmm.dll")]
    private static extern uint waveInPrepareHeader(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")]
    private static extern uint waveInAddBuffer(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")]
    private static extern uint waveInStart(IntPtr handle);
    [DllImport("winmm.dll")]
    private static extern uint waveInStop(IntPtr handle);
    [DllImport("winmm.dll")]
    private static extern uint waveInReset(IntPtr handle);
    [DllImport("winmm.dll")]
    private static extern uint waveInUnprepareHeader(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")]
    private static extern uint waveInClose(IntPtr handle);

    public void Start()
    {
        if (_running) return;
        var format = new WaveFormat { FormatTag = 1, Channels = 1, SamplesPerSec = 16000, AvgBytesPerSec = 32000, BlockAlign = 2, BitsPerSample = 16 };
        uint result = waveInOpen(out _device, uint.MaxValue, ref format, _callback, IntPtr.Zero, CallbackFunction);
        if (result != 0) throw new InvalidOperationException($"Mikrofon açılamadı (Windows kodu {result}).");
        try
        {
            uint size = (uint)Marshal.SizeOf<WaveHeader>();
            for (int i = 0; i < 4; i++)
            {
                IntPtr data = Marshal.AllocHGlobal(3200);
                IntPtr header = Marshal.AllocHGlobal((int)size);
                _buffers.Add((header, data));
                Marshal.StructureToPtr(new WaveHeader { Data = data, BufferLength = 3200 }, header, false);
                Check(waveInPrepareHeader(_device, header, size), "Mikrofon tamponu hazırlanamadı");
                Check(waveInAddBuffer(_device, header, size), "Mikrofon tamponu başlatılamadı");
            }
            _running = true;
            Check(waveInStart(_device), "Mikrofon kaydı başlatılamadı");
        }
        catch { Dispose(); throw; }
    }

    private void OnWaveIn(IntPtr handle, uint message, IntPtr instance, IntPtr parameter1, IntPtr parameter2)
    {
        if (message != DataMessage || !_running) return;
        var header = Marshal.PtrToStructure<WaveHeader>(parameter1);
        if (header.BytesRecorded > 0)
        {
            var bytes = new byte[header.BytesRecorded];
            Marshal.Copy(header.Data, bytes, 0, bytes.Length);
            AudioAvailable?.Invoke(bytes);
        }
        if (_running) waveInAddBuffer(handle, parameter1, (uint)Marshal.SizeOf<WaveHeader>());
    }

    private static void Check(uint result, string message)
    {
        if (result != 0) throw new InvalidOperationException($"{message} (Windows kodu {result}).");
    }

    public void Dispose()
    {
        _running = false;
        if (_device != IntPtr.Zero)
        {
            waveInStop(_device);
            waveInReset(_device);
            uint size = (uint)Marshal.SizeOf<WaveHeader>();
            foreach (var buffer in _buffers) waveInUnprepareHeader(_device, buffer.Header, size);
            waveInClose(_device);
            _device = IntPtr.Zero;
        }
        foreach (var buffer in _buffers)
        {
            Marshal.FreeHGlobal(buffer.Header);
            Marshal.FreeHGlobal(buffer.Data);
        }
        _buffers.Clear();
    }
}
