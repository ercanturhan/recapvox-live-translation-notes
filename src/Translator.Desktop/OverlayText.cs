namespace Translator.Desktop;

internal static class OverlayText
{
    public static IReadOnlyList<string> Chunks(IReadOnlyList<TranscriptSegment> segments, bool translation, int maxWords = 9)
    {
        maxWords = Math.Clamp(maxWords, 5, 24);
        var chunks = new List<string>();
        var current = new List<string>();
        foreach (var segment in segments)
        {
            var value = translation ? segment.Translation : segment.Original;
            if (string.IsNullOrWhiteSpace(value)) continue;
            var words = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                current.Add(word);
                var sentenceBreak = EndsSentence(word) && current.Count >= Math.Max(5, maxWords * 2 / 3);
                var clauseBreak = EndsClause(word) && current.Count >= Math.Max(5, maxWords * 3 / 4);
                if (current.Count < maxWords && !sentenceBreak && !clauseBreak) continue;
                chunks.Add(string.Join(" ", current));
                current.Clear();
            }
        }
        if (current.Count > 0) chunks.Add(string.Join(" ", current));
        return chunks;
    }

    private static bool EndsSentence(string word) => EndsWithAny(word.TrimEnd('"', '\'', '”', '’', ')'), '.', '!', '?', '。', '؟');
    private static bool EndsClause(string word) => EndsWithAny(word.TrimEnd('"', '\'', '”', '’', ')'), ',', ';', ':', '،');
    private static bool EndsWithAny(string value, params char[] endings) => value.Length > 0 && endings.Contains(value[^1]);

    // A fresh sentence block replaces the previous one; never use a character
    // tail of the entire session history.
    public static TranscriptSegment Current(IReadOnlyList<TranscriptSegment> segments)
    {
        string translation = "";
        for (var i = segments.Count - 1; i >= 0; i--)
        {
            if (!string.IsNullOrWhiteSpace(segments[i].Translation))
            {
                translation = CurrentChunk(segments[i].Translation);
                break;
            }
        }
        for (var i = segments.Count - 1; i >= 0; i--)
            if (!string.IsNullOrWhiteSpace(segments[i].Original) || !string.IsNullOrWhiteSpace(segments[i].Translation))
                return segments[i] with
                {
                    Original = CurrentChunk(segments[i].Original),
                    Translation = translation
                };
        return new TranscriptSegment(null, "", "");
    }

    private static string CurrentChunk(string value)
    {
        var words = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        const int wordsPerBlock = 22;
        var start = Math.Max(0, (words.Length - 1) / wordsPerBlock * wordsPerBlock);
        return string.Join(" ", words.Skip(start));
    }
}

internal sealed class OverlayCaptionStream
{
    private IReadOnlyList<string> _chunks = [];
    private int _visibleCount;
    private bool _initialized;

    public bool HasPending => _visibleCount < _chunks.Count;
    public string VisibleText => string.Join("\n", _chunks.Skip(Math.Max(0, _visibleCount - 2)).Take(Math.Min(2, _visibleCount)));

    public void Reset()
    {
        _chunks = [];
        _visibleCount = 0;
        _initialized = false;
    }

    public bool Update(IReadOnlyList<string> chunks)
    {
        if (chunks.Count == 0 && _chunks.Count > 0) return false;
        var before = VisibleText;
        _chunks = chunks;
        if (!_initialized)
        {
            _visibleCount = chunks.Count;
            _initialized = true;
        }
        else if (chunks.Count < _visibleCount) _visibleCount = chunks.Count;
        else if (_visibleCount == 0 && chunks.Count > 0) _visibleCount = 1;
        return before != VisibleText;
    }

    public bool Advance()
    {
        if (!HasPending) return false;
        _visibleCount++;
        return true;
    }
}
