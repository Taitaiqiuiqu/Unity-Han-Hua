using System.Text.RegularExpressions;
using System.Linq;

namespace OneClickChineseMod.Core;

public static class GameTextFilter
{
    private static readonly Regex ControlPatternRegex = new(
        @"\[(A|B|X|Y|LB|RB|LT|RT|L3|R3|START|SELECT|MENU|OPTIONS|HOME)\]" +
        @"|(?<=^|[\s\p{P}])(A|B|X|Y|LB|RB|LT|RT|L3|R3)(?=$|[\s\p{P}])" +
        @"|(?<=^|[\s\p{P}])(Ctrl|Alt|Shift|Esc|Tab|Enter|Space|Backspace|Delete)" +
        @"(?=$|[\s\p{P}\+])" +
        @"|(?<=^|[\s\p{P}])(Home|End|PgUp|PgDn)(?=$|[\s\p{P}])" +
        @"|(?<=^|[\s\p{P}])F[1-9]\d?(?=$|[\s\p{P}])" +
        @"|[←↑↓→↖↗↘↙⟵⟶⟷]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<char> DirectionChars = new()
    {
        '←', '↑', '↓', '→', '↖', '↗', '↘', '↙', '⟵', '⟶', '⟷'
    };

    public static (string FilteredText, Dictionary<string, string> PlaceholderMap) Filter(string text)
    {
        if (string.IsNullOrEmpty(text))
            return (text, new Dictionary<string, string>());

        var map = new Dictionary<string, string>();
        var counter = 0;

        var filtered = ControlPatternRegex.Replace(text, match =>
        {
            var original = match.Value;
            var placeholder = $"{{K{++counter}}}";
            map[placeholder] = original;
            return placeholder;
        });

        return (filtered, map);
    }

    public static string Restore(string text, Dictionary<string, string> placeholderMap)
    {
        if (string.IsNullOrEmpty(text) || placeholderMap.Count == 0)
            return text;

        foreach (var kvp in placeholderMap.OrderByDescending(k => k.Key.Length))
        {
            text = text.Replace(kvp.Key, kvp.Value);
        }

        return text;
    }

    public static bool ContainsOnlyControlChars(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var stripped = ControlPatternRegex.Replace(text, "").Trim();
        return string.IsNullOrEmpty(stripped);
    }
}
