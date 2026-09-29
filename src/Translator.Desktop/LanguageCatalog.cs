using System.Globalization;

namespace Translator.Desktop;

internal sealed record LanguageOption(string Code, string Name)
{
    public override string ToString()
    {
        if (string.IsNullOrEmpty(Code)) return UiLocalizer.T(Name);
        var translated = UiLocalizer.T(Name);
        if (UiLocalizer.Language != "tr" && translated == Name)
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(Code);
                translated = UiLocalizer.Language == "en" ? culture.EnglishName : culture.NativeName;
            }
            catch (CultureNotFoundException) { }
        }
        return $"{translated} ({Code})";
    }
}

internal static class LanguageCatalog
{
    // Provider lists: https://soniox.com/docs/translation/supported-languages
    // https://ai.google.dev/gemini-api/docs/live-api/live-translate#supported-languages
    private const string SonioxCodes = "af sq ar az eu be bn bs bg ca zh hr cs da nl en et fi fr gl de el gu he hi hu id it ja kn kk ko lv lt mk ms ml mr no fa pl pt pa ro ru sr sk sl es sw sv tl ta te th tr uk ur vi cy";
    private const string GeminiCodes = "af ak sq am ar hy az eu be bn bg my ca zh-Hans zh-Hant hr cs da nl en et fil fi fr gl ka de el gu ha he hi hu is id it ja jv kn kk km rw ko lo lv lt mk ms ml mr mn ne no nb fa pl pt-BR pt-PT pa ro ru sd si sk sl sr es su sw sv ta te th tr uk ur uz vi zu";

    private static readonly Dictionary<string, string> TurkishNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tr"] = "Türkçe", ["en"] = "İngilizce", ["de"] = "Almanca", ["fr"] = "Fransızca",
        ["es"] = "İspanyolca", ["it"] = "İtalyanca", ["pt"] = "Portekizce", ["ru"] = "Rusça",
        ["ar"] = "Arapça", ["zh"] = "Çince", ["zh-Hans"] = "Çince (Basitleştirilmiş)",
        ["zh-Hant"] = "Çince (Geleneksel)", ["ja"] = "Japonca", ["ko"] = "Korece"
    };

    public static LanguageOption Auto { get; } = new("", "Otomatik algıla");

    public static IReadOnlyList<LanguageOption> Live(string provider, bool source)
    {
        // OpenAI does not publish a closed Realtime Translation language table; offer
        // standard language codes and let the service validate the selected target.
        var codes = provider switch
        {
            LiveProviders.Soniox or LiveProviders.Hybrid => SonioxCodes.Split(' '),
            LiveProviders.Gemini => GeminiCodes.Split(' '),
            _ => AllCultureCodes()
        };
        var languages = MakeOptions(codes);
        return source ? [Auto, .. languages] : languages;
    }

    public static IReadOnlyList<LanguageOption> Summary() => MakeOptions(AllCultureCodes());

    public static LanguageOption? Find(IEnumerable<LanguageOption> options, string? code) =>
        options.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));

    public static LanguageOption? Resolve(IEnumerable<LanguageOption> options, string? text, object? selected)
    {
        var value = (text ?? "").Trim();
        if (selected is LanguageOption option && (value == option.ToString() || value == option.Name || value == option.Code)) return option;
        return options.FirstOrDefault(x => value.Equals(x.ToString(), StringComparison.CurrentCultureIgnoreCase)
            || value.Equals(x.Name, StringComparison.CurrentCultureIgnoreCase)
            || (x.Code.Length > 0 && value.Equals(x.Code, StringComparison.OrdinalIgnoreCase)));
    }

    private static string[] AllCultureCodes() => CultureInfo.GetCultures(CultureTypes.NeutralCultures)
        .Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 15)
        .Concat(SonioxCodes.Split(' ')).Concat(GeminiCodes.Split(' '))
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static IReadOnlyList<LanguageOption> MakeOptions(IEnumerable<string> codes) => codes
        .Distinct(StringComparer.OrdinalIgnoreCase).Select(code => new LanguageOption(code, Name(code)))
        .OrderBy(x => x.Name, StringComparer.Create(new CultureInfo("tr-TR"), true)).ToArray();

    private static string Name(string code)
    {
        if (TurkishNames.TryGetValue(code, out var name)) return name;
        try { return CultureInfo.GetCultureInfo(code).EnglishName.Split('(')[0].Trim(); }
        catch (CultureNotFoundException) { return code; }
    }
}
