using ArchSift.Contracts;

namespace ArchSift.Core;

public static class SelectorMatcher
{
    public static bool Matches(Selector selector, string candidate)
    {
        if (selector.Match == "exact") return string.Equals(selector.Value, candidate, StringComparison.Ordinal);
        var pattern = selector.Value;
        var path = selector.Kind is "project" or "source-file";
        var table = new bool[pattern.Length + 1, candidate.Length + 1];
        table[pattern.Length, candidate.Length] = true;
        for (var p = pattern.Length - 1; p >= 0; p--)
        for (var c = candidate.Length; c >= 0; c--)
        {
            bool matched;
            if (pattern[p] == '*')
            {
                var doubleStar = path && p + 1 < pattern.Length && pattern[p + 1] == '*';
                var next = p + (doubleStar ? 2 : 1);
                matched = (doubleStar && next < pattern.Length && pattern[next] == '/' && table[next + 1, c])
                    || table[next, c]
                    || (c < candidate.Length && (!path || doubleStar || candidate[c] != '/') && table[p, c + 1]);
            }
            else matched = c < candidate.Length &&
                ((pattern[p] == '?' && (!path || candidate[c] != '/')) || pattern[p] == candidate[c]) && table[p + 1, c + 1];
            table[p, c] = matched;
        }
        return table[0, 0];
    }
}
