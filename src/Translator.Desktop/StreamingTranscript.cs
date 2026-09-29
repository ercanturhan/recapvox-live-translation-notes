using System.Text;

namespace Translator.Desktop;

// OpenAI and Gemini provide independent source/target text fragments, without
// guaranteed sentence IDs shared between the two streams. Pair by order only.
internal sealed class StreamingTranscript
{
    private readonly List<(TimeSpan? Start, string Text)> _original = [];
    private readonly List<string> _translation = [];
    private readonly StringBuilder _currentOriginal = new();
    private readonly StringBuilder _currentTranslation = new();
    private TimeSpan? _currentStart;

    public int CommittedOriginalCount => _original.Count;
    public IReadOnlyList<TranscriptSegment> Segments { get; private set; } = [];

    public void AppendOriginal(string fragment, TimeSpan? start)
    {
        if (string.IsNullOrEmpty(fragment)) return;
        if (_currentOriginal.Length == 0) _currentStart = start;
        _currentOriginal.Append(fragment);
        SplitOriginal();
        Publish();
    }

    public void AppendTranslation(string fragment)
    {
        if (string.IsNullOrEmpty(fragment)) return;
        _currentTranslation.Append(fragment);
        SplitTranslation();
        Publish();
    }

    public void Finish()
    {
        if (_currentOriginal.Length > 0) AddOriginal(_currentOriginal.ToString());
        if (_currentTranslation.Length > 0) AddTranslation(_currentTranslation.ToString());
        _currentOriginal.Clear();
        _currentTranslation.Clear();
        Publish();
    }

    private void SplitOriginal()
    {
        while (SentenceEnd(_currentOriginal.ToString()) is var end && end > 0)
        {
            var text = _currentOriginal.ToString();
            AddOriginal(text[..end]);
            _currentOriginal.Clear();
            _currentOriginal.Append(text[end..].TrimStart());
            _currentStart = null;
        }
    }

    private void SplitTranslation()
    {
        while (SentenceEnd(_currentTranslation.ToString()) is var end && end > 0)
        {
            var text = _currentTranslation.ToString();
            AddTranslation(text[..end]);
            _currentTranslation.Clear();
            _currentTranslation.Append(text[end..].TrimStart());
        }
    }

    private void AddOriginal(string text)
    {
        text = text.Trim();
        if (text.Length > 0) _original.Add((_currentStart, text));
    }

    private void AddTranslation(string text)
    {
        text = text.Trim();
        if (text.Length > 0) _translation.Add(text);
    }

    private void Publish()
    {
        var rows = new List<TranscriptSegment>();
        var count = Math.Max(_original.Count, _translation.Count);
        for (var i = 0; i < count; i++)
            rows.Add(new TranscriptSegment(i < _original.Count ? _original[i].Start : null,
                i < _original.Count ? _original[i].Text : "", i < _translation.Count ? _translation[i] : ""));
        if (_currentOriginal.Length > 0)
        {
            var index = _original.Count;
            var value = _currentOriginal.ToString().Trim();
            if (index < rows.Count) rows[index] = rows[index] with { Start = _currentStart, Original = value };
            else rows.Add(new TranscriptSegment(_currentStart, value, ""));
        }
        if (_currentTranslation.Length > 0)
        {
            var index = _translation.Count;
            var value = _currentTranslation.ToString().Trim();
            if (index < rows.Count) rows[index] = rows[index] with { Translation = value };
            else rows.Add(new TranscriptSegment(null, "", value));
        }
        Segments = rows;
    }

    private static int SentenceEnd(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] is not ('.' or '!' or '?' or '。' or '؟')) continue;
            var after = i + 1;
            if (after < value.Length && value[after] is '"' or '”' or '\'') after++;
            if (after == value.Length || char.IsWhiteSpace(value[after])) return after;
        }
        return 0;
    }
}
