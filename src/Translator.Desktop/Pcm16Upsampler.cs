using System.Buffers.Binary;

namespace Translator.Desktop;

// Stateful linear interpolation from the app's 16 kHz mono PCM16 to the
// 24 kHz PCM16 stream required by OpenAI's translation WebSocket.
internal sealed class Pcm16Upsampler
{
    private long _sourceIndex = -1;
    private long _nextOutputNumerator;
    private short _previous;

    public byte[] Convert(ReadOnlySpan<byte> pcm16)
    {
        if ((pcm16.Length & 1) != 0) throw new ArgumentException("PCM16 örnekleri çift bayt olmalı.", nameof(pcm16));
        var output = new byte[(pcm16.Length / 2 * 3 + 4) * 2];
        var written = 0;
        for (var offset = 0; offset < pcm16.Length; offset += 2)
        {
            var current = BinaryPrimitives.ReadInt16LittleEndian(pcm16[offset..]);
            _sourceIndex++;
            if (_sourceIndex == 0)
            {
                BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(written), current);
                written += 2;
                _nextOutputNumerator = 2;
            }
            else
            {
                while (_nextOutputNumerator <= _sourceIndex * 3)
                {
                    var fractionNumerator = _nextOutputNumerator - (_sourceIndex - 1) * 3;
                    var interpolated = (_previous * (3 - fractionNumerator) + current * fractionNumerator) / 3;
                    BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(written), (short)interpolated);
                    written += 2;
                    _nextOutputNumerator += 2;
                }
            }
            _previous = current;
        }
        return output.AsSpan(0, written).ToArray();
    }
}
