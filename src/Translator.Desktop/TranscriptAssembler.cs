using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Translator.Desktop;

internal sealed record TranscriptSegment(TimeSpan? Start, string Original, string Translation);

internal sealed class TranscriptAssembler
{
    private readonly List<(TimeSpan? Start, string Text)> _original = [];
    private readonly List<string> _translation = [];
    private readonly StringBuilder _currentOriginal = new();
    private readonly StringBuilder _currentTranslation = new();
    private TimeSpan? _currentStart;
    private static readonly Regex SentenceBreak = new(@"[.!?。؟][""”']?(?=\s+\p{Lu}|\s*$)", RegexOptions.Compiled);

    public IReadOnlyList<TranscriptSegment> Segments { get; private set; } = [];
    public int CommittedOriginalCount => _original.Count;

    public void Process(JsonElement tokens)
    {
        var interimOriginal = new StringBuilder();
        var interimTranslation = new StringBuilder();
        TimeSpan? interimStart = null;
        foreach (var token in tokens.EnumerateArray())
        {
            var value = token.TryGetProperty("text", out var text) ? text.GetString() ?? "" : "";
            var final = token.TryGetProperty("is_final", out var flag) && flag.ValueKind == JsonValueKind.True;
            var translated = token.TryGetProperty("translation_status", out var status) && status.GetString() == "translation";
            if (value is "<end>" or "<fin>")
            {
                if (final) { FlushOriginal(); FlushTranslation(); }
                continue;
            }
            if (!final)
            {
                if (translated) interimTranslation.Append(value);
                else
                {
                    if (interimStart is null) interimStart = Timestamp(token);
                    interimOriginal.Append(value);
                }
                continue;
            }
            if (translated)
            {
                _currentTranslation.Append(value);
                SplitTranslation();
            }
            else
            {
                if (_currentOriginal.Length == 0) _currentStart = Timestamp(token);
                _currentOriginal.Append(value);
                SplitOriginal();
            }
        }
        var rows = new List<TranscriptSegment>();
        var count = Math.Max(_original.Count, _translation.Count);
        for (var i = 0; i < count; i++)
            rows.Add(new TranscriptSegment(i < _original.Count ? _original[i].Start : null,
                i < _original.Count ? _original[i].Text : "",
                i < _translation.Count ? _translation[i] : ""));
        if (_currentOriginal.Length > 0 || _currentTranslation.Length > 0 || interimOriginal.Length > 0 || interimTranslation.Length > 0)
            rows.Add(new TranscriptSegment(_currentStart ?? interimStart,
                (_currentOriginal.ToString() + interimOriginal).Trim(),
                (_currentTranslation.ToString() + interimTranslation).Trim()));
        Segments = rows;
    }

    public void Finish()
    {
        FlushOriginal();
        FlushTranslation();
        var rows = new List<TranscriptSegment>();
        for (var i = 0; i < Math.Max(_original.Count, _translation.Count); i++)
            rows.Add(new TranscriptSegment(i < _original.Count ? _original[i].Start : null,
                i < _original.Count ? _original[i].Text : "",
                i < _translation.Count ? _translation[i] : ""));
        Segments = rows;
    }

    private void FlushOriginal()
    {
        var value = _currentOriginal.ToString().Trim();
        if (value.Length > 0) _original.Add((_currentStart, value));
        _currentOriginal.Clear();
        _currentStart = null;
    }

    private void FlushTranslation()
    {
        var value = _currentTranslation.ToString().Trim();
        if (value.Length > 0) _translation.Add(value);
        _currentTranslation.Clear();
    }

    private void SplitOriginal()
    {
        while (FindSentenceBreak(_currentOriginal.ToString()) is { Success: true } match)
        {
            var text = _currentOriginal.ToString();
            var end = match.Index + match.Length;
            var sentence = text[..end].Trim();
            if (sentence.Length > 0) _original.Add((_currentStart, sentence));
            _currentOriginal.Clear();
            _currentOriginal.Append(text[end..].TrimStart());
            // When a provider token contains more than one sentence, only its first
            // sentence has a reliable token start. Do not invent later timestamps.
            _currentStart = null;
        }
    }

    private void SplitTranslation()
    {
        while (FindSentenceBreak(_currentTranslation.ToString()) is { Success: true } match)
        {
            var text = _currentTranslation.ToString();
            var end = match.Index + match.Length;
            var sentence = text[..end].Trim();
            if (sentence.Length > 0) _translation.Add(sentence);
            _currentTranslation.Clear();
            _currentTranslation.Append(text[end..].TrimStart());
        }
    }

    private static TimeSpan? Timestamp(JsonElement token) =>
        token.TryGetProperty("start_ms", out var start) && start.TryGetInt64(out var milliseconds)
            ? TimeSpan.FromMilliseconds(milliseconds) : null;

    private static Match FindSentenceBreak(string text)
    {
        for (var match = SentenceBreak.Match(text); match.Success; match = match.NextMatch())
        {
            // Soniox may deliver the closing quotation mark in a later token.
            // An unmatched quote means a question mark is still inside speech.
            var quoteCount = text[..(match.Index + match.Length)].Count(c => c is '"' or '“' or '”');
            if (quoteCount % 2 != 0) continue;
            return match;
        }
        return Match.Empty;
    }

}
