using System.Text;
using System.Text.Json;
using ArchSift.Contracts;

namespace ArchSift.Core;

public sealed record LoadedRuleset(Ruleset Ruleset, RulesetIdentity Identity);
public sealed record RuleBundle(LoadedRuleset[] Documents)
{
    public Rule[] Rules => Documents.SelectMany(document => document.Ruleset.Rules).ToArray();
    public RuleException[] Exceptions => Documents.SelectMany(document => document.Ruleset.Exceptions).ToArray();
}

public static class RuleLoader
{
    public const int MaximumJsonBytes = 4 * 1024 * 1024;

    public static LoadedRuleset Load(string path)
    {
        var full = Path.GetFullPath(path);
        var bytes = File.ReadAllBytes(full);
        var rules = Parse(bytes);
        return new(rules, new(rules.Id, rules.Version, ContentHash.Bytes(bytes), full));
    }

    public static Ruleset Parse(byte[] bytes)
    {
        if (bytes.Length > MaximumJsonBytes) throw new ConfigurationException("Ruleset exceeds 4 MiB limit.");
        try
        {
            var offset = bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0;
            using var document = JsonDocument.Parse(bytes.AsMemory(offset), new JsonDocumentOptions { MaxDepth = 64 });
            SchemaValidation.Validate(document.RootElement, "ruleset");
            var ruleset = document.RootElement.Deserialize<Ruleset>(JsonContract.Options)!;
            RuleSemantics.Validate(ruleset);
            return ruleset;
        }
        catch (JsonException error) { throw new ConfigurationException("Invalid ruleset JSON: " + error.Message); }
    }

    public static RuleBundle Compose(IEnumerable<LoadedRuleset> documents)
    {
        var bundle = new RuleBundle(documents.ToArray());
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in bundle.Rules)
            if (!ids.Add(rule.Id)) throw new ConfigurationException("Duplicate rule ID: " + rule.Id);
        var exceptionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var exception in bundle.Exceptions)
        {
            if (!exceptionIds.Add(exception.Id)) throw new ConfigurationException("Duplicate exception ID: " + exception.Id);
            if (!ids.Contains(exception.RuleId)) throw new ConfigurationException("Exception references missing rule: " + exception.RuleId);
        }
        var rules = bundle.Rules;
        for (var i = 0; i < rules.Length; i++)
        for (var j = i + 1; j < rules.Length; j++)
        {
            var left = rules[i];
            var right = rules[j];
            if (!left.Enabled || !right.Enabled || left.Type != right.Type ||
                left.Scope.Kind != right.Scope.Kind || left.Scope.Match != right.Scope.Match || left.Scope.Value != right.Scope.Value) continue;
            if (left.Type == "target-framework")
            {
                var a = left.Parameters.GetProperty("allowedFrameworks").EnumerateArray().Select(x => x.GetString()!);
                var b = right.Parameters.GetProperty("allowedFrameworks").EnumerateArray().Select(x => x.GetString()!);
                if (!a.Intersect(b, StringComparer.Ordinal).Any())
                    throw new ConfigurationException($"Conflicting TFM rules: {left.Id}, {right.Id}");
            }
            if (left.Type == "naming" &&
                left.Parameters.GetProperty("subjectKind").GetString() == right.Parameters.GetProperty("subjectKind").GetString())
            {
                var a = RuleSemantics.NameSelector(left);
                var b = RuleSemantics.NameSelector(right);
                if (!GlobIntersection.CanOverlap(a, b))
                    throw new ConfigurationException($"Conflicting naming rules: {left.Id}, {right.Id}");
            }
        }
        return bundle;
    }
}

public static class RuleSemantics
{
    public static void Validate(Ruleset ruleset)
    {
        NonBlank(ruleset.Id, "ruleset ID");
        NonBlank(ruleset.Version, "ruleset version");
        foreach (var rule in ruleset.Rules)
        {
            NonBlank(rule.Id, "rule ID");
            NonBlank(rule.Reason, "rule reason");
            ValidateSelector(rule.Scope);
            var expected = rule.Type == "type-dependency" ? new[] { "type", "namespace" }
                : rule.Type == "naming" ? new[] { rule.Parameters.GetProperty("subjectKind").GetString()! }
                : ["project"];
            if (!expected.Contains(rule.Scope.Kind, StringComparer.Ordinal))
                throw new ConfigurationException("Scope kind does not match rule type: " + rule.Id);
            foreach (var p in rule.Parameters.EnumerateObject())
            {
                if (p.Name is "source" or "target" or "forbiddenTarget")
                    ValidateSelector(p.Value.Deserialize<Selector>(JsonContract.Options)!);
                if (p.Name == "allowedTargets")
                {
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var item in p.Value.EnumerateArray())
                    {
                        var selector = item.Deserialize<Selector>(JsonContract.Options)!;
                        ValidateSelector(selector);
                        if (selector.Kind != "project")
                            throw new ConfigurationException("allowedTargets requires project selectors.");
                        var key = string.Join('\0', selector.Kind, selector.Match, selector.Value, selector.AllowEmpty);
                        if (!seen.Add(key)) throw new ConfigurationException("Duplicate value in allowedTargets");
                    }
                }
                if (p.Name is "allowedFrameworks" or "forbiddenPackageIds" or "allowedPackageIds")
                {
                    var seen = new HashSet<string>(p.Name is "forbiddenPackageIds" or "allowedPackageIds"
                        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
                    foreach (var item in p.Value.EnumerateArray())
                    {
                        var text = item.GetString()!;
                        NonBlank(text, p.Name);
                        if (!seen.Add(text)) throw new ConfigurationException("Duplicate value in " + p.Name);
                    }
                }
            }
            if (rule.Type == "naming") ValidateSelector(NameSelector(rule));
        }
        foreach (var exception in ruleset.Exceptions)
        {
            NonBlank(exception.Id, "exception ID");
            NonBlank(exception.RuleId, "exception rule ID");
            NonBlank(exception.Reason, "exception reason");
            ValidateSelector(exception.Scope);
        }
    }

    public static Selector NameSelector(Rule rule)
    {
        var name = rule.Parameters.GetProperty("requiredName");
        return new Selector { Kind = rule.Parameters.GetProperty("subjectKind").GetString()!,
            Match = name.GetProperty("match").GetString()!, Value = name.GetProperty("value").GetString()! };
    }

    private static void NonBlank(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ConfigurationException(label + " must not be whitespace.");
    }

    public static void ValidateSelector(Selector selector)
    {
        NonBlank(selector.Value, "selector");
        if (selector.Value.Length > 256) throw new ConfigurationException("Selector exceeds 256 characters.");
        if (selector.Match == "glob" && selector.Value.IndexOfAny(['[', ']', '{', '}', '(', ')', '|', '^', '$', '\\']) >= 0)
            throw new ConfigurationException("Only * and ? glob syntax is supported; paths additionally support **.");
        if (selector.Kind is "project" or "source-file")
        {
            if (selector.Value.Contains('\\') || selector.Value.Contains(':') || selector.Value.StartsWith('/') ||
                selector.Value.Split('/').Any(part => part is "." or ".." or ""))
                throw new ConfigurationException("Path selectors must use root-relative slash paths without traversal.");
        }
    }
}

public static class GlobIntersection
{
    public static bool CanOverlap(Selector a, Selector b)
    {
        if (a.Match == "exact") return SelectorMatcher.Matches(b, a.Value);
        if (b.Match == "exact") return SelectorMatcher.Matches(a, b.Value);
        var path = a.Kind is "project" or "source-file";
        var seen = new HashSet<(int, int)>();
        var pending = new Queue<(int, int)>();
        pending.Enqueue((0, 0));
        while (pending.TryDequeue(out var state))
        {
            if (!seen.Add(state)) continue;
            var (i, j) = state;
            if (i == a.Value.Length && j == b.Value.Length) return true;
            foreach (var x in Epsilon(a.Value, i)) pending.Enqueue((x, j));
            foreach (var y in Epsilon(b.Value, j)) pending.Enqueue((i, y));
            if (i == a.Value.Length || j == b.Value.Length) continue;
            var xToken = Token(a.Value, i);
            var yToken = Token(b.Value, j);
            if (Compatible(xToken, yToken)) pending.Enqueue((xToken.Next, yToken.Next));
        }
        return false;

        IEnumerable<int> Epsilon(string pattern, int index)
        {
            if (index == pattern.Length || pattern[index] != '*') yield break;
            var next = index + (path && index + 1 < pattern.Length && pattern[index + 1] == '*' ? 2 : 1);
            yield return next;
            if (next == index + 2 && next < pattern.Length && pattern[next] == '/') yield return next + 1;
        }

        (char Literal, bool Any, bool Slash, int Next) Token(string pattern, int index)
        {
            var c = pattern[index];
            if (c == '*') return ('\0', true, !path || (index + 1 < pattern.Length && pattern[index + 1] == '*'), index);
            if (c == '?') return ('\0', true, !path, index + 1);
            return (c, false, c == '/', index + 1);
        }

        static bool Compatible((char Literal, bool Any, bool Slash, int Next) x, (char Literal, bool Any, bool Slash, int Next) y)
        {
            if (!x.Any && !y.Any) return x.Literal == y.Literal;
            if (x.Any && y.Any) return true;
            var literal = x.Any ? y.Literal : x.Literal;
            var wildcard = x.Any ? x : y;
            return literal != '/' || wildcard.Slash;
        }
    }
}
