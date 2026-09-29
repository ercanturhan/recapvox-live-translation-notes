using System.IO;
using System.Runtime.CompilerServices;

namespace Translator.Desktop;

internal static class WaveFile
{
    public static void Validate(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        if (new string(reader.ReadChars(4)) != "RIFF") throw new InvalidDataException("RIFF WAV dosyası gerekli.");
        reader.ReadUInt32();
        if (new string(reader.ReadChars(4)) != "WAVE") throw new InvalidDataException("WAV dosyası gerekli.");
        bool formatFound = false, dataFound = false;
        while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
        {
            string id = new(reader.ReadChars(4));
            uint size = reader.ReadUInt32();
            long next = reader.BaseStream.Position + size + (size % 2);
            if (next > reader.BaseStream.Length) throw new InvalidDataException("Bozuk WAV başlığı.");
            if (id == "fmt ")
            {
                if (size < 16) throw new InvalidDataException("Bozuk WAV biçimi.");
                ushort codec = reader.ReadUInt16();
                ushort channels = reader.ReadUInt16();
                uint rate = reader.ReadUInt32();
                reader.ReadUInt32(); reader.ReadUInt16();
                ushort bits = reader.ReadUInt16();
                formatFound = codec == 1 && channels == 1 && rate == 16000 && bits == 16;
            }
            if (id == "data") dataFound = true;
            reader.BaseStream.Position = next;
        }
        if (!formatFound || !dataFound) throw new InvalidDataException("Yalnızca 16 kHz, mono, 16 bit PCM WAV destekleniyor.");
    }

    public static async IAsyncEnumerable<byte[]> ReadPacedAsync(string path, [EnumeratorCancellation] CancellationToken cancellation)
    {
        Validate(path);
        using var reader = new BinaryReader(File.OpenRead(path));
        reader.BaseStream.Position = 12;
        while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
        {
            string id = new(reader.ReadChars(4));
            uint size = reader.ReadUInt32();
            long next = reader.BaseStream.Position + size + (size % 2);
            if (id == "data")
            {
                uint remaining = size;
                while (remaining > 0)
                {
                    cancellation.ThrowIfCancellationRequested();
                    int count = (int)Math.Min(3200u, remaining);
                    byte[] data = reader.ReadBytes(count);
                    if (data.Length == 0) yield break;
                    yield return data;
                    remaining -= (uint)data.Length;
                    await Task.Delay(Math.Max(1, data.Length * 1000 / 32000), cancellation);
                }
                yield break;
            }
            reader.BaseStream.Position = next;
        }
    }
}
