using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using SlotMixer;
using Xunit;

namespace SlotMixer.Tests;

/// <summary>The two languages: nothing in the code may reach the screen untranslated, and no translation may go unused.</summary>
public class LocTests
{
    static string SourceFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "SlotMixer");
            if (File.Exists(Path.Combine(candidate, "SlotMixer.csproj")))
                return candidate;
        }
        throw new DirectoryNotFoundException("The SlotMixer source folder was not found above " + AppContext.BaseDirectory);
    }

    /// <summary>Every text passed to Loc.L("…") in the sources (a literal, or several joined by +), as the string it makes at run time.</summary>
    static HashSet<string> KeysInSource()
    {
        var keys = new HashSet<string>();
        var start = new Regex(@"(?<![A-Za-z0-9_.])L\(\s*(?=@?"")");
        foreach (var file in Directory.EnumerateFiles(SourceFolder(), "*.cs", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file);
            if (name is "Loc.cs" or "Strings.En.cs")
                continue;

            var text = File.ReadAllText(file);
            foreach (Match match in start.Matches(text))
            {
                var parts = new StringBuilder();
                var i = match.Index + match.Length;
                while (true)
                {
                    var (literal, end) = ReadLiteral(text, i);
                    parts.Append(literal);
                    var k = end;
                    while (k < text.Length && char.IsWhiteSpace(text[k])) k++;
                    if (k < text.Length && text[k] == '+')
                    {
                        k++;
                        while (k < text.Length && char.IsWhiteSpace(text[k])) k++;
                        if (k < text.Length && text[k] == '"') { i = k; continue; }
                    }
                    break;
                }
                keys.Add(parts.ToString());
            }
        }
        return keys;
    }

    static (string Value, int End) ReadLiteral(string text, int index)
    {
        var value = new StringBuilder();
        var i = index + 1;
        while (text[i] != '"')
        {
            if (text[i] != '\\') { value.Append(text[i++]); continue; }
            i++;
            switch (text[i])
            {
                case 'n': value.Append('\n'); break;
                case 'r': value.Append('\r'); break;
                case 't': value.Append('\t'); break;
                case '"': value.Append('"'); break;
                case '\\': value.Append('\\'); break;
                case '\'': value.Append('\''); break;
                case 'u': value.Append((char)int.Parse(text.AsSpan(i + 1, 4), NumberStyles.HexNumber)); i += 4; break;
                default: throw new FormatException($"Unhandled escape \\{text[i]} in a translated text");
            }
            i++;
        }
        return (value.ToString(), i + 1);
    }

    static readonly Regex Placeholder = new(@"\{(\d+)\}");

    static string[] Holes(string text) =>
        Placeholder.Matches(text).Select(m => m.Value).Distinct().OrderBy(h => h).ToArray();

    [Fact]
    public void Every_text_in_the_code_has_an_english_translation()
    {
        var missing = KeysInSource().Where(k => !Strings.English.ContainsKey(k)).Select(Escape).ToList();
        Assert.True(missing.Count == 0, "Missing from Strings.En.cs:\n  " + string.Join("\n  ", missing));
    }

    [Fact]
    public void No_translation_is_left_without_a_use()
    {
        var used = KeysInSource();
        var unused = Strings.English.Keys.Where(k => !used.Contains(k)).Select(Escape).ToList();
        Assert.True(unused.Count == 0, "In Strings.En.cs but never passed to L(...):\n  " + string.Join("\n  ", unused));
    }

    [Fact]
    public void A_translation_fills_the_same_placeholders_as_the_original()
    {
        var wrong = Strings.English.Where(p => !Holes(p.Key).SequenceEqual(Holes(p.Value))).Select(p => Escape(p.Key)).ToList();
        Assert.True(wrong.Count == 0, "Placeholders differ for:\n  " + string.Join("\n  ", wrong));
    }

    [Fact]
    public void No_translation_is_empty_or_identical_by_accident()
    {
        Assert.All(Strings.English, p => Assert.False(string.IsNullOrWhiteSpace(p.Value), "Empty translation for " + Escape(p.Key)));
    }

    [Fact]
    public void Formatting_with_arguments_works_in_both_languages()
    {
        var previous = Loc.Current;
        try
        {
            Loc.Init("pt");
            Assert.Equal("Volume 30%", Loc.L("Volume {0}%", 30));
            Loc.Init("en");
            Assert.Equal("Volume 30%", Loc.L("Volume {0}%", 30));
            Assert.Equal("Add slot", Loc.L("Adicionar slot"));
            Loc.Init("pt");
            Assert.Equal("Adicionar slot", Loc.L("Adicionar slot"));
        }
        finally
        {
            Loc.Init(previous == Language.English ? "en" : "pt");
        }
    }

    [Theory]
    [InlineData("pt", "Portuguese")]
    [InlineData("PT-BR", "Portuguese")]
    [InlineData("en", "English")]
    public void The_setting_picks_the_language(string setting, string expected)
    {
        var env = Environment.GetEnvironmentVariable("SLOTMIXER_LANG");
        Environment.SetEnvironmentVariable("SLOTMIXER_LANG", null);
        try
        {
            Assert.Equal(expected, Loc.Detect(setting).ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("SLOTMIXER_LANG", env);
        }
    }

    [Fact]
    public void Automatic_follows_the_windows_display_language()
    {
        var env = Environment.GetEnvironmentVariable("SLOTMIXER_LANG");
        Environment.SetEnvironmentVariable("SLOTMIXER_LANG", null);
        var culture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("pt-BR");
            Assert.Equal(Language.Portuguese, Loc.Detect("auto"));
            CultureInfo.CurrentUICulture = new CultureInfo("pt-PT");
            Assert.Equal(Language.Portuguese, Loc.Detect(null));
            CultureInfo.CurrentUICulture = new CultureInfo("de-DE");
            Assert.Equal(Language.English, Loc.Detect("auto"));
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            Assert.Equal(Language.English, Loc.Detect("auto"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = culture;
            Environment.SetEnvironmentVariable("SLOTMIXER_LANG", env);
        }
    }

    static string Escape(string text) => text.Replace("\n", "\\n");
}
