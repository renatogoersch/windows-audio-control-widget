using System.Globalization;

namespace SlotMixer;

internal enum Language { Portuguese, English }

/// <summary>
/// Two languages, chosen from the Windows display language (Portuguese for pt-*, English for everything else) unless the
/// user picked one in the menu. The Portuguese text is the key: <c>L("Texto")</c> returns it as is in Portuguese and
/// looks it up in <see cref="Strings.English"/> otherwise, so the code reads the same as before and a missing
/// translation falls back to Portuguese (the tests make sure there is none).
/// </summary>
internal static class Loc
{
    static Language? s_language;

    public static Language Current => s_language ??= Detect(null);

    /// <summary>Applies the language saved in the settings ("auto", "pt" or "en").</summary>
    public static void Init(string? setting) => s_language = Detect(setting);

    /// <summary>SLOTMIXER_LANG (pt or en) wins over the setting, which wins over the Windows display language.</summary>
    public static Language Detect(string? setting)
    {
        var chosen = Environment.GetEnvironmentVariable("SLOTMIXER_LANG") ?? setting;
        switch (chosen?.Trim().ToLowerInvariant())
        {
            case "pt":
            case "pt-br":
                return Language.Portuguese;
            case "en":
                return Language.English;
        }

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("pt", StringComparison.OrdinalIgnoreCase)
            ? Language.Portuguese
            : Language.English;
    }

    /// <summary>The text in the current language; <paramref name="args"/> fill {0}, {1}… when given.</summary>
    public static string L(string portuguese, params object?[] args)
    {
        var text = Current == Language.English && Strings.English.TryGetValue(portuguese, out var english) ? english : portuguese;
        return args.Length == 0 ? text : string.Format(CultureInfo.CurrentCulture, text, args);
    }
}
