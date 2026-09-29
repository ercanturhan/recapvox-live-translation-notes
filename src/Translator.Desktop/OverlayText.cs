namespace Translator.Desktop;

internal static class OverlayText
{
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
