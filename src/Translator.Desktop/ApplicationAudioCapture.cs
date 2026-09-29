using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Translator.Desktop;

internal sealed class ApplicationAudioCapture : IAsyncDisposable
{
    private WasapiRecorder? _recorder;

    public event Action<byte[]>? AudioAvailable;
    public event Action<Exception?>? Stopped;

    public async Task StartAsync(int processId)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            throw new PlatformNotSupportedException("Seçili uygulama sesi için Windows 10 build 19041 veya üstü gerekli.");
        if (_recorder is not null) throw new InvalidOperationException("Ses yakalama zaten açık.");

        _recorder = await new WasapiRecorderBuilder()
            .WithProcessLoopback((uint)processId, ProcessLoopbackMode.IncludeTargetProcessTree)
            .WithFormat(new WaveFormat(16000, 16, 1))
            .BuildAsync();
        _recorder.DataAvailable += (buffer, _, _, _) =>
        {
            if (!buffer.IsEmpty) AudioAvailable?.Invoke(buffer.ToArray());
        };
        _recorder.RecordingStopped += (_, args) => Stopped?.Invoke(args.Exception);
        _recorder.StartRecording();
    }

    public async ValueTask DisposeAsync()
    {
        if (_recorder is null) return;
        _recorder.StopRecording();
        await _recorder.DisposeAsync();
        _recorder = null;
    }
}
