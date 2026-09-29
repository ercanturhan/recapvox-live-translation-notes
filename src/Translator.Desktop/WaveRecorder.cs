using System.IO;
using System.Text;

namespace Translator.Desktop;

internal sealed class WaveRecorder : IDisposable
{
    private readonly object _gate = new();
    private readonly FileStream _stream;
    private long _length;
    private bool _disposed;

    public WaveRecorder(string path)
    {
        _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        _stream.Write(new byte[44]);
    }

    public void Write(byte[] pcm)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _stream.Write(pcm);
            _length += pcm.Length;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            if (_length > uint.MaxValue - 36) throw new InvalidOperationException("WAV kaydı 4 GB sınırını aştı.");
            _stream.Position = 0;
            using (var writer = new BinaryWriter(_stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write((uint)(36 + _length));
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write((uint)16);
                writer.Write((ushort)1);
                writer.Write((ushort)1);
                writer.Write((uint)16000);
                writer.Write((uint)32000);
                writer.Write((ushort)2);
                writer.Write((ushort)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write((uint)_length);
                writer.Flush();
            }
            _stream.Dispose();
        }
    }
}
