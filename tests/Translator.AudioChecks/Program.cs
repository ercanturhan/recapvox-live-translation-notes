using Translator.Desktop;
using System.Text.Json;
using System.Net;
using System.Net.Http;
using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

Exception? uiError = null;
var uiThread = new Thread(() =>
{
    try
    {
        var label = new TextBlock { Text = "Kaynak" };
        var button = new Button { Content = "Ayarlar" };
        var panel = new StackPanel();
        panel.Children.Add(label);
        panel.Children.Add(button);
        var window = new Window { Content = new Border { Child = panel } };
        UiLocalizer.Language = "en";
        UiLocalizer.Apply(window);
        if (label.Text != "Source" || (string)button.Content != "Settings")
            throw new Exception("İç içe arayüz denetimleri çevrilmedi.");
        UiLocalizer.Language = "de";
        UiLocalizer.Apply(window);
        if (label.Text != "Quelle" || (string)button.Content != "Einstellungen")
            throw new Exception("Dil değişimi arayüze uygulanmadı.");
        UiLocalizer.Language = "tr";
        window.Close();
    }
    catch (Exception ex) { uiError = ex; }
});
uiThread.SetApartmentState(ApartmentState.STA);
uiThread.Start();
uiThread.Join();
if (uiError is not null) throw uiError;

var balanceJson = "{\"is_available\":true,\"balance_infos\":[{\"currency\":\"USD\",\"total_balance\":\"12.34\"}]}";
using (var balanceHttp = new HttpClient(new StaticJsonHandler(balanceJson)))
    if (await BillingClient.DeepSeekBalanceAsync("test-key", CancellationToken.None, balanceHttp) != "12.34 USD")
        throw new Exception("DeepSeek bakiye yanıtı okunamadı.");
var billedRecord = new SessionRecord { StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1), EndedAt = DateTimeOffset.UtcNow };
var logsJson = "{\"usage_logs\":[{\"client_reference_id\":\"" + billedRecord.Id.ToString("N") +
    "\",\"cost_usd\":\"0.0123000000\"},{\"client_reference_id\":\"other\",\"cost_usd\":\"5.00\"}],\"next_page_cursor\":null}";
using (var usageHttp = new HttpClient(new StaticJsonHandler(logsJson)))
    if (await BillingClient.SonioxSessionCostAsync("test-key", billedRecord, CancellationToken.None, usageHttp) != 0.0123m)
        throw new Exception("Soniox oturum maliyeti yanlış eşleştirildi.");
Console.WriteLine("DeepSeek bakiye ve Soniox kayıt maliyeti ayrıştırıldı.");
var archiveTestDirectory = Path.Combine(Path.GetTempPath(), "translator-cost-check-" + Guid.NewGuid().ToString("N"));
try
{
    var priced = new SessionRecord { StartedAt = DateTimeOffset.UtcNow, LiveCostUsd = 0.0123m,
        HybridCostEstimateUsd = 0.0002m, LiveCostStatus = "Soniox kesin tutar" };
    ArchiveStore.SaveSession(archiveTestDirectory, priced);
    var stale = new SessionRecord { Id = priced.Id, StartedAt = priced.StartedAt,
        LiveCostStatus = "Soniox maliyeti sorgulanıyor" };
    ArchiveStore.SaveSession(archiveTestDirectory, stale);
    var reloaded = ArchiveStore.LoadSessions(archiveTestDirectory).Single();
    if (reloaded.LiveCostUsd != priced.LiveCostUsd ||
        reloaded.HybridCostEstimateUsd != priced.HybridCostEstimateUsd ||
        reloaded.LiveCostStatus != priced.LiveCostStatus)
        throw new Exception("Kaydedilmiş oturum maliyeti yeniden yazılınca kayboldu.");
}
finally { if (Directory.Exists(archiveTestDirectory)) Directory.Delete(archiveTestDirectory, true); }
var flashUsage = new SummaryClient.GenerationResult("Özet", 1000, 200, 200, 800);
var flashCost = BillingClient.EstimateDeepSeekFlash(flashUsage, new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero));
if (flashCost != (200 * 0.003m + 800 * 0.15m + 200 * 0.6m) / 1_000_000m)
    throw new Exception("DeepSeek Flash tahmini maliyeti hatalı.");
var deepSeekUsageJson = """{"choices":[{"message":{"content":"Özet"}}],"usage":{"prompt_tokens":1000,"completion_tokens":200,"prompt_cache_hit_tokens":200,"prompt_cache_miss_tokens":800}}""";
using (var usageClient = new HttpClient(new FakeSummaryHandler(deepSeekUsageJson)))
{
    var generation = await SummaryClient.GenerateWithUsageAsync("DeepSeek", "deepseek-flash", "test-only-key", "Özetle", 200,
        CancellationToken.None, usageClient);
    if (generation.InputTokens != 1000 || generation.OutputTokens != 200 || generation.CacheMissTokens != 800)
        throw new Exception("DeepSeek token kullanımı ayrıştırılamadı.");
}

UiLocalizer.Language = "en";
if (UiLocalizer.T("Kaydı başlat") != "Start recording" || UiLocalizer.T("Geçmiş") != "History")
    throw new Exception("İngilizce arayüz çevirisi hatalı.");
if ("Gemini 3.5 Live Translate " + UiLocalizer.T("bağlantısı kurulamadı:") + " " +
    UiLocalizer.T("Sunucu bağlantıyı kurulumdan önce kapattı.") !=
    "Gemini 3.5 Live Translate connection failed: The server closed the connection before setup completed.")
    throw new Exception("Gemini bağlantı uyarısı arayüz diline çevrilmedi.");
if (UiLocalizer.TranslateCurrent("Hazır. Kaynağı seçip başlatabilirsiniz.") != "Ready. Choose a source and start recording."
    || UiLocalizer.TranslateCurrent("Henüz özet yok") != "No summary yet"
    || !LanguageCatalog.Find(LanguageCatalog.Live(LiveProviders.Soniox, false), "en")!.ToString().StartsWith("English"))
    throw new Exception("Dil değişiminde dinamik metin veya dil seçicisi güncellenmedi.");
UiLocalizer.Language = "de";
if (UiLocalizer.T("Özet oluştur") != "Zusammenfassung erstellen")
    throw new Exception("Almanca arayüz çevirisi hatalı.");
UiLocalizer.Language = "es";
if (UiLocalizer.T("Ayarlar") != "Ajustes")
    throw new Exception("İspanyolca arayüz çevirisi hatalı.");
UiLocalizer.Language = "tr";
var titledRecord = new SessionRecord { Title = "Deneme toplantısı", StartedAt = DateTimeOffset.Now,
    Summaries = [new SummaryRecord { Text = "Özet" }] };
if (!titledRecord.DisplayName.Contains("Deneme toplantısı") || !titledRecord.DisplayName.Contains("Özet var"))
    throw new Exception("Başlık veya özet göstergesi hatalı.");
Console.WriteLine("Arayüz dilleri, kayıt başlığı ve özet göstergesi doğrulandı.");

var overlayRows = new[]
{
    new TranscriptSegment(null, "First sentence.", "İlk cümle."),
    new TranscriptSegment(null, "Second sentence.", "İkinci cümle.")
};
if (OverlayText.Current(overlayRows).Original != "Second sentence."
    || OverlayText.Current([]).Original != "")
    throw new Exception("Yüzen altyazı güncel cümleye geçemedi.");
var pendingTranslation = new[]
{
    new TranscriptSegment(null, "First sentence.", "İlk cümle."),
    new TranscriptSegment(null, "Second sentence is in progress", "")
};
if (OverlayText.Current(pendingTranslation).Original != "Second sentence is in progress"
    || OverlayText.Current(pendingTranslation).Translation != "İlk cümle.")
    throw new Exception("Son çeviri yeni çeviri gelene kadar görünür kalmadı.");
var longSpeech = new[] { new TranscriptSegment(null, string.Join(" ", Enumerable.Range(1, 50).Select(x => "word" + x)), "") };
if (!OverlayText.Current(longSpeech).Original.StartsWith("word45") || OverlayText.Current(longSpeech).Original.Contains("word1 "))
    throw new Exception("Uzun konuşma sabit boylu bloklara ayrılamadı.");
Console.WriteLine("Yüzen altyazı güncel cümle seçimi doğrulandı.");
var cleanSummaryPrompt = SummaryClient.BuildSummaryPrompt("Toplantı", false, "Türkçe",
    [new TranscriptSegment(TimeSpan.FromSeconds(5), "Karar alındı.", "A decision was made.")]);
if (cleanSummaryPrompt.Contains("00:05") || cleanSummaryPrompt.Contains("[1 @") || !cleanSummaryPrompt.Contains("Karar alındı."))
    throw new Exception("Özet istemi zaman referansı içeriyor.");

var pdfPath = Path.Combine(AppContext.BaseDirectory, "export-check.pdf");
Exception? pdfError = null;
var pdfThread = new Thread(() =>
{
    try { PdfExporter.Save(pdfPath, "Translator oturumu\n\nTürkçe karakterler: ğüşöçı İ\n\n" + string.Concat(Enumerable.Repeat("Uzun toplantı özeti ve çeviri. ", 1800))); }
    catch (Exception ex) { pdfError = ex; }
});
pdfThread.SetApartmentState(ApartmentState.STA);
pdfThread.Start();
pdfThread.Join();
if (pdfError is not null) throw pdfError;
var pdfBytes = File.ReadAllBytes(pdfPath);
if (pdfBytes.Length < 1000 || !System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 8).StartsWith("%PDF-1.4"))
    throw new Exception("PDF dışa aktarma başarısız.");
Console.WriteLine("Türkçe metinli çok sayfalı PDF oluşturuldu.");

static Queue<byte> Pcm(short value)
{
    var bytes = new Queue<byte>();
    for (var i = 0; i < 1600; i++) { bytes.Enqueue((byte)value); bytes.Enqueue((byte)(value >> 8)); }
    return bytes;
}
var appFrame = Pcm(10000);
var micFrame = Pcm(10000);
var mixedFrame = Pcm16AudioMixer.MixFrame(appFrame, micFrame);
if (mixedFrame.Length != 3200 || BitConverter.ToInt16(mixedFrame) != 14000 || appFrame.Count != 0 || micFrame.Count != 0)
    throw new Exception("Uygulama ve mikrofon PCM birleşimi hatalı.");
var microphoneOnly = Pcm16AudioMixer.MixFrame(new Queue<byte>(), Pcm(10000));
if (BitConverter.ToInt16(microphoneOnly) != 7000)
    throw new Exception("Uygulama sessizken mikrofon sesi kayboldu.");
Console.WriteLine("Uygulama ve mikrofon PCM karışımı doğrulandı.");

var sonioxLanguages = LanguageCatalog.Live(LiveProviders.Soniox, false);
var geminiLanguages = LanguageCatalog.Live(LiveProviders.Gemini, false);
if (sonioxLanguages.Count < 60 || LanguageCatalog.Find(sonioxLanguages, "cy") is null
    || LanguageCatalog.Find(sonioxLanguages, "zh-Hant") is not null
    || geminiLanguages.Count < 75 || LanguageCatalog.Find(geminiLanguages, "zh-Hant") is null
    || LanguageCatalog.Find(geminiLanguages, "pt-BR") is null
    || LanguageCatalog.Live(LiveProviders.Hybrid, true)[0].Code != ""
    || LanguageCatalog.Find(LanguageCatalog.Summary(), "tr") is null)
    throw new Exception("Sağlayıcı dil listesi veya otomatik algılama hatalı.");
Console.WriteLine("Sağlayıcı dil listeleri ve otomatik algılama doğrulandı.");

foreach (var (provider, response) in new[]
{
    ("DeepSeek", """{"choices":[{"message":{"content":"DeepSeek özeti"}}]}"""),
    ("OpenAI", """{"output":[{"type":"reasoning"},{"type":"message","content":[{"type":"output_text","text":"OpenAI özeti"}]}]}"""),
    ("Gemini", """{"candidates":[{"content":{"parts":[{"text":"Gemini özeti"}]}}]}"""),
    ("Claude", """{"content":[{"type":"text","text":"Claude özeti"}]}""")
})
{
    using var client = new HttpClient(new FakeSummaryHandler(response));
    var result = await SummaryClient.GenerateAsync(provider, SummaryClient.DefaultModel(provider), "test-only-key", "Özetle", 200,
        CancellationToken.None, client);
    if (result != provider + " özeti") throw new Exception(provider + " yanıtı okunamadı.");
}
Console.WriteLine("Dört özet sağlayıcısının istek/yanıt biçimi doğrulandı.");

var transcript = new TranscriptAssembler();
using (var packet = JsonDocument.Parse("""{"tokens":[{"text":"Hello.","start_ms":1200,"is_final":true,"translation_status":"original"},{"text":"Merhaba.","is_final":true,"translation_status":"translation"}]}"""))
    transcript.Process(packet.RootElement.GetProperty("tokens"));
if (transcript.Segments.Count != 1 || transcript.Segments[0].Start != TimeSpan.FromMilliseconds(1200)
    || transcript.Segments[0].Original != "Hello." || transcript.Segments[0].Translation != "Merhaba.")
    throw new Exception("Zamanlı çift dilli cümle eşleşmesi hatalı.");
Console.WriteLine("Zamanlı çift dilli blok doğrulandı.");

var multiSentence = new TranscriptAssembler();
using (var packet = JsonDocument.Parse("""{"tokens":[{"text":"You default to \"how's your day.\" They give you a one-word answer.","start_ms":11000,"is_final":true,"translation_status":"original"},{"text":"Otomatik olarak günün nasıl geçti diye soruyorsun. Sana tek kelimelik yanıt veriyorlar.","is_final":true,"translation_status":"translation"}]}"""))
    multiSentence.Process(packet.RootElement.GetProperty("tokens"));
if (multiSentence.Segments.Count != 2 || !multiSentence.Segments[0].Original.EndsWith("day.\"")
    || !multiSentence.Segments[1].Original.StartsWith("They give")
    || !multiSentence.Segments[1].Translation.StartsWith("Sana"))
    throw new Exception("Bir token içindeki çoklu cümleler doğru ayrılmadı.");
Console.WriteLine("Bir token içindeki çoklu cümleler doğrulandı.");

var quotedQuestion = new TranscriptAssembler();
using (var packet = JsonDocument.Parse("""{"tokens":[{"text":"You default to, \"How's your day?","start_ms":7000,"is_final":true,"translation_status":"original"},{"text":"Otomatik olarak \"Günün nasıl geçti?","is_final":true,"translation_status":"translation"}]}"""))
    quotedQuestion.Process(packet.RootElement.GetProperty("tokens"));
if (quotedQuestion.Segments.Count != 1)
    throw new Exception("Açık alıntı tamamlanmadan birden fazla blok oluştu.");
using (var packet = JsonDocument.Parse("""{"tokens":[{"text":"\" They give you a one-word answer.","start_ms":10000,"is_final":true,"translation_status":"original"},{"text":"\" diye soruyorsun. Sana tek kelimelik bir cevap veriyorlar.","is_final":true,"translation_status":"translation"}]}"""))
    quotedQuestion.Process(packet.RootElement.GetProperty("tokens"));
if (quotedQuestion.Segments.Count != 2 || quotedQuestion.Segments[0].Original != "You default to, \"How's your day?\""
    || quotedQuestion.Segments[0].Translation != "Otomatik olarak \"Günün nasıl geçti?\" diye soruyorsun."
    || !quotedQuestion.Segments[1].Translation.StartsWith("Sana tek"))
    throw new Exception("Alıntılı soru ve çeviri cümleleri kaydı.");
Console.WriteLine("Alıntılı soru ve çeviri eşleşmesi doğrulandı.");

var sourcePcm = new byte[6400];
for (var i = 0; i < sourcePcm.Length / 2; i++)
    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(sourcePcm.AsSpan(i * 2), (short)(i % 1000));
var allAtOnce = new Pcm16Upsampler().Convert(sourcePcm);
var chunkedResampler = new Pcm16Upsampler();
var splitOutput = chunkedResampler.Convert(sourcePcm.AsSpan(0, 3200)).Concat(chunkedResampler.Convert(sourcePcm.AsSpan(3200))).ToArray();
if (!allAtOnce.SequenceEqual(splitOutput) || allAtOnce.Length < sourcePcm.Length * 1.49)
    throw new Exception("OpenAI 16→24 kHz örnekleme dönüşümü blok sınırında bozuldu.");
Console.WriteLine("OpenAI 16→24 kHz akış dönüşümü doğrulandı.");

var openAiLive = new OpenAiTranslationSession();
IReadOnlyList<TranscriptSegment> openAiRows = [];
openAiLive.TextChanged += rows => openAiRows = rows;
openAiLive.ProcessServerMessage("""{"type":"session.updated"}""");
openAiLive.ProcessServerMessage("""{"type":"session.input_transcript.delta","delta":"Hello.","start_ms":1200}""");
openAiLive.ProcessServerMessage("""{"type":"session.output_transcript.delta","delta":"Merhaba."}""");
if (openAiRows.Count != 1 || openAiRows[0].Original != "Hello." || openAiRows[0].Translation != "Merhaba."
    || openAiRows[0].Start != TimeSpan.FromMilliseconds(1200))
    throw new Exception("OpenAI canlı kaynak/çeviri olayları eşleşmedi.");

var geminiLive = new GeminiTranslationSession();
IReadOnlyList<TranscriptSegment> geminiRows = [];
geminiLive.TextChanged += rows => geminiRows = rows;
geminiLive.ProcessServerMessage("""{"setupComplete":{}}""");
geminiLive.ProcessServerMessage("""{"serverContent":{"inputTranscription":{"text":"Hello."},"outputTranscription":{"text":"Merhaba."}}}""");
if (geminiRows.Count != 1 || geminiRows[0].Original != "Hello." || geminiRows[0].Translation != "Merhaba.")
    throw new Exception("Gemini canlı kaynak/çeviri olayları eşleşmedi.");
Console.WriteLine("OpenAI ve Gemini canlı metin olayları doğrulandı.");

var partialLive = new StreamingTranscript();
partialLive.AppendOriginal("Hello.", TimeSpan.FromSeconds(1));
partialLive.AppendTranslation("Merha");
if (partialLive.Segments.Count != 1 || partialLive.Segments[0].Translation != "Merha")
    throw new Exception("Parçalı çeviri aynı kaynak cümle satırında görünmedi.");
partialLive.AppendTranslation("ba.");
if (partialLive.Segments.Single().Translation != "Merhaba.")
    throw new Exception("Parçalı çeviri tamamlandığında cümle kaydı bozuldu.");

var hybridOutput = new Dictionary<int, string>();
var hybrid = new HybridTranslator("test-only-key", "Türkçe", (prompt, _) => Task.FromResult(prompt.Contains("Hello") ? "Merhaba." : "Güle güle."));
hybrid.Translated += (index, value) => hybridOutput[index] = value;
var hybridSegments = new[] { new TranscriptSegment(TimeSpan.Zero, "Hello.", ""), new TranscriptSegment(TimeSpan.FromSeconds(3), "Goodbye.", "") };
hybrid.Observe(hybridSegments, 1);
await hybrid.CompleteAsync(hybridSegments);
if (hybridOutput.Count != 2 || hybridOutput[0] != "Merhaba." || hybridOutput[1] != "Güle güle.")
    throw new Exception("Hibrit canlı çeviri sırası veya kapanışta kalan cümle hatalı.");
Console.WriteLine("Hibrit canlı çevirinin sıralı ve son blok akışı doğrulandı.");

var archiveDirectory = Path.Combine(Path.GetTempPath(), "translator-archive-check-" + Guid.NewGuid().ToString("N"));
try
{
    var sessionRecord = new SessionRecord
    {
        StartedAt = DateTimeOffset.Now,
        Title = "Toplantı denemesi",
        EndedAt = DateTimeOffset.Now,
        Source = "Test WAV",
        Segments = [new TranscriptSegment(TimeSpan.FromSeconds(2), "Hello.", "Merhaba.")],
        Summaries = [new SummaryRecord { CreatedAt = DateTimeOffset.Now, Language = "Türkçe", Text = "Selamlama özeti" }]
    };
    ArchiveStore.SaveSession(archiveDirectory, sessionRecord);
    var loaded = ArchiveStore.LoadSessions(archiveDirectory).Single();
    if (loaded.Id != sessionRecord.Id || loaded.Title != "Toplantı denemesi" || loaded.Segments.Single().Translation != "Merhaba."
        || loaded.Summaries.Single().Text != "Selamlama özeti")
        throw new Exception("Konuşma ile özet aynı arşiv kaydında saklanamadı.");
    var copied = ArchiveStore.CopySession(archiveDirectory, loaded);
    if (copied.Id == loaded.Id || copied.Summaries.Single().Text != loaded.Summaries.Single().Text || ArchiveStore.LoadSessions(archiveDirectory).Count != 2)
        throw new Exception("Kayıt kopyası ayrı kimlikle ve özetiyle kaydedilmedi.");
    ArchiveStore.DeleteSession(archiveDirectory, copied.Id);
    if (ArchiveStore.LoadSessions(archiveDirectory).Count != 1)
        throw new Exception("Kopyalanan kayıt arşivden silinemedi.");
    Console.WriteLine("Zamanlı oturum ve bağlı özet arşiv döngüsü doğrulandı.");
}
finally { if (Directory.Exists(archiveDirectory)) Directory.Delete(archiveDirectory, true); }

var path = Path.Combine(Path.GetTempPath(), "translator-audio-check-" + Guid.NewGuid().ToString("N") + ".wav");
var samples = new byte[3200];
for (int i = 0; i < samples.Length; i++) samples[i] = (byte)(i % 251);
try
{
    using (var writer = new WaveRecorder(path)) writer.Write(samples);
    WaveFile.Validate(path);
    var result = new List<byte>();
    await foreach (var chunk in WaveFile.ReadPacedAsync(path, CancellationToken.None)) result.AddRange(chunk);
    if (!samples.SequenceEqual(result)) throw new Exception("Kaydedilen PCM verisi geri okununca farklı çıktı.");
    Console.WriteLine("WAV başlığı ve PCM verisi doğrulandı.");
}
finally { if (File.Exists(path)) File.Delete(path); }

if (args.Contains("--process-loopback"))
{
    await using var capture = new ApplicationAudioCapture();
    await capture.StartAsync(Environment.ProcessId);
    await Task.Delay(200);
    Console.WriteLine("Windows süreç sesi yakalama arayüzü açıldı.");
}

if (args.Contains("--deepseek-live"))
{
    var key = CredentialStore.ReadForProvider("DeepSeek");
    if (string.IsNullOrWhiteSpace(key))
        Console.WriteLine("DeepSeek anahtarı bu Windows oturumunun Kimlik Bilgileri deposunda bulunamadı.");
    else
    {
        var answer = await SummaryClient.GenerateAsync("DeepSeek", "deepseek-flash", key,
            "Yalnızca OK yaz.", 64, CancellationToken.None);
        Console.WriteLine("DeepSeek canlı istek başarılı; metin döndü (" + answer.Length + " karakter).");
    }
}

sealed class FakeSummaryHandler(string response) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Post || request.Content is null)
            throw new Exception("Özet API isteği POST değil.");
        var body = await request.Content.ReadAsStringAsync(cancellationToken);
        if (request.RequestUri?.Host == "api.deepseek.com"
            && !body.Contains("\"thinking\":{\"type\":\"disabled\"}"))
            throw new Exception("DeepSeek özetinde ekonomik düşünmesiz mod seçilmedi.");
        if (body.Length < 10 || !request.Headers.Contains("x-api-key")
            && !request.Headers.Contains("x-goog-api-key") && request.Headers.Authorization is null)
            throw new Exception("Özet isteğinde metin veya anahtar eksik.");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) };
    }
}

sealed class StaticJsonHandler(string response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization?.Scheme != "Bearer") throw new Exception("Faturalama isteğinde kimlik doğrulama yok.");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) });
    }
}
