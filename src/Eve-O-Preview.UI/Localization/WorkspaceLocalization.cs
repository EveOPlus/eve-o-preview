using System.Globalization;
using System.Text.Json;

namespace EveOPreview.UI;

/// <summary>Offline UI catalogs; independent of data parsing and platform culture.</summary>
public sealed class WorkspaceLocalization
{
    public sealed record Language(string Code, string NativeName);
    public static IReadOnlyList<Language> Languages { get; } = Array.AsReadOnly(new[]
    {
        new Language("en", "English"), new Language("ar", "العربية"),
        new Language("de", "Deutsch"), new Language("es", "Español"),
        new Language("fr", "Français"), new Language("hi", "हिन्दी"),
        new Language("id", "Bahasa Indonesia"), new Language("it", "Italiano"),
        new Language("ja", "日本語"), new Language("ko", "한국어"),
        new Language("nl", "Nederlands"), new Language("pl", "Polski"),
        new Language("pt-BR", "Português (Brasil)"), new Language("ru", "Русский"),
        new Language("tr", "Türkçe"), new Language("uk", "Українська"),
        new Language("zh-Hans", "简体中文"), new Language("zh-Hant", "繁體中文")
    });

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> Catalogs = new(() =>
        Languages.ToDictionary(x => x.Code, x => ReadCatalog(x.Code), StringComparer.OrdinalIgnoreCase));
    public string Code { get; }
    public bool RightToLeft => CultureInfo.GetCultureInfo(Code).TextInfo.IsRightToLeft;
    public WorkspaceLocalization(string? preference = "auto") => Code = Resolve(preference);

    private static IReadOnlyDictionary<string, string> ReadCatalog(string code)
    {
        using var stream = typeof(WorkspaceLocalization).Assembly.GetManifestResourceStream($"EveOPreview.UI.Localization.{code}.json")
            ?? throw new InvalidOperationException($"Missing translation catalog: {code}");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }

    public static string NormalizePreference(string? preference) => string.IsNullOrWhiteSpace(preference)
        || preference.Equals("auto", StringComparison.OrdinalIgnoreCase) ? "auto" : Resolve(preference);

    public static string Resolve(string? preference)
    {
        var name = string.IsNullOrWhiteSpace(preference) || preference.Equals("auto", StringComparison.OrdinalIgnoreCase)
            ? CultureInfo.CurrentUICulture.Name : preference;
        try
        {
            var culture = CultureInfo.GetCultureInfo(name);
            if (culture.Name is "zh-TW" or "zh-HK" or "zh-MO") return "zh-Hant";
            while (culture.Name.Length > 0)
            {
                var found = Languages.FirstOrDefault(x => x.Code.Equals(culture.Name, StringComparison.OrdinalIgnoreCase));
                if (found is not null) return found.Code;
                if (culture.Name == "zh") return "zh-Hans";
                if (culture.Name == "pt") return "pt-BR";
                culture = culture.Parent;
            }
        }
        catch (CultureNotFoundException) { }
        return "en";
    }

    public string Get(string english) => Catalogs.Value[Code].TryGetValue(english, out var value)
        && !string.IsNullOrWhiteSpace(value) ? value : english;
    public bool Contains(string text, string query) => CultureInfo.GetCultureInfo(Code).CompareInfo
        .IndexOf(text, query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
    public string Format(FormattableString message) => string.Format(CultureInfo.GetCultureInfo(Code), Get(message.Format), message.GetArguments());
}
