using System.Threading.Channels;

namespace Translator.Desktop;

internal sealed class HybridTranslator
{
    private readonly Channel<(int Index, string Text)> _queue = Channel.CreateUnbounded<(int, string)>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly string _key;
    private readonly string _targetLanguage;
    private readonly Func<string, CancellationToken, Task<string>> _translate;
    private readonly CancellationTokenSource _cancel = new(TimeSpan.FromMinutes(5));
    private readonly Task _worker;
    private int _nextIndex;
    public decimal? EstimatedCostUsd { get; private set; }
    public event Action<int, string>? Translated;
    public event Action<string>? Failed;

    public HybridTranslator(string key, string targetLanguage, Func<string, CancellationToken, Task<string>>? translate = null)
    {
        _key = key;
        _targetLanguage = targetLanguage;
        _translate = translate ?? TranslateDefaultAsync;
        _worker = WorkAsync();
    }

    private async Task<string> TranslateDefaultAsync(string prompt, CancellationToken cancellation)
    {
        var result = await SummaryClient.GenerateWithUsageAsync("DeepSeek", "deepseek-flash", _key, prompt, 256, cancellation);
        var cost = BillingClient.EstimateDeepSeekFlash(result, DateTimeOffset.UtcNow);
        if (cost is not null) EstimatedCostUsd = (EstimatedCostUsd ?? 0) + cost.Value;
        return result.Text;
    }

    public void Observe(IReadOnlyList<TranscriptSegment> segments, int committedCount)
    {
        var limit = Math.Min(segments.Count, committedCount);
        while (_nextIndex < limit)
        {
            var index = _nextIndex++;
            var text = segments[index].Original.Trim();
            if (text.Length > 0) _queue.Writer.TryWrite((index, text));
        }
    }

    public async Task CompleteAsync(IReadOnlyList<TranscriptSegment> segments)
    {
        Observe(segments, segments.Count);
        _queue.Writer.TryComplete();
        try { await _worker.WaitAsync(TimeSpan.FromSeconds(45)); }
        catch (TimeoutException) { _cancel.Cancel(); Failed?.Invoke("Hibrit çeviri 45 saniye içinde tamamlanmadı; kalan bloklar özgün dilde kaydedildi."); }
        _cancel.Dispose();
    }

    private async Task WorkAsync()
    {
        try
        {
            await foreach (var (index, text) in _queue.Reader.ReadAllAsync(_cancel.Token))
            {
                try
                {
                    var prompt = $"Aşağıdaki konuşma cümlesini yalnızca {_targetLanguage} diline çevir. Açıklama, etiket veya ek bilgi yazma.\n\n{text}";
                    var result = await _translate(prompt, _cancel.Token);
                    Translated?.Invoke(index, result);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { Failed?.Invoke("DeepSeek çevirisi: " + ex.Message); }
            }
        }
        catch (OperationCanceledException) { }
    }
}
