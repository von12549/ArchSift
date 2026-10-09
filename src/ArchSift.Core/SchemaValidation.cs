using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArchSift.Core;

/// <summary>Evaluates the draft-07 keywords used by the fixed bundled schemas. No remote schemas are loaded.</summary>
public static class SchemaValidation
{
    private static readonly string[] Names = ["ruleset", "config", "report", "assembly-manifest", "comparison", "library", "chain", "chain-diagnostic", "chain-summary"];

    public static void Validate(JsonElement value, string name)
    {
        if (!Names.Contains(name, StringComparer.Ordinal))
            throw new ConfigurationException("Unknown built-in schema.");
        var assembly = typeof(SchemaValidation).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith("." + name + ".schema.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var schema = JsonDocument.Parse(stream);
        ValidateDocument(value, schema.RootElement);
    }

    public static void ValidateDocument(JsonElement value, JsonElement schema)
    {
        CheckDuplicateKeys(value, "$");
        var errors = Evaluate(value, schema, schema, "$", 0);
        if (errors.Count > 0)
            throw new ConfigurationException(string.Join("\n", errors.Take(20)));
    }

    private static void CheckDuplicateKeys(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in value.EnumerateObject())
            {
                if (!seen.Add(p.Name)) throw new ConfigurationException($"Duplicate JSON property: {path}.{p.Name}");
                CheckDuplicateKeys(p.Value, path + "." + p.Name);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray()) CheckDuplicateKeys(item, path + "[]");
        }
    }

    private static List<string> Evaluate(JsonElement value, JsonElement schema, JsonElement root, string path, int depth)
    {
        if (depth > 128) return [path + ": schema recursion limit."];
        if (schema.ValueKind == JsonValueKind.True) return [];
        if (schema.ValueKind == JsonValueKind.False) return [path + ": disallowed."];
        var errors = new List<string>();
        if (schema.TryGetProperty("$ref", out var reference))
        {
            var pointer = reference.GetString()!;
            if (!pointer.StartsWith("#/", StringComparison.Ordinal))
                return [path + ": non-local schema reference rejected."];
            var target = root;
            foreach (var part in pointer[2..].Split('/'))
                target = target.GetProperty(part.Replace("~1", "/").Replace("~0", "~"));
            return Evaluate(value, target, root, path, depth + 1);
        }
        if (schema.TryGetProperty("allOf", out var all))
            foreach (var branch in all.EnumerateArray()) errors.AddRange(Evaluate(value, branch, root, path, depth + 1));
        foreach (var keyword in new[] { "oneOf", "anyOf" })
        {
            if (!schema.TryGetProperty(keyword, out var options)) continue;
            var count = options.EnumerateArray().Count(branch => Evaluate(value, branch, root, path, depth + 1).Count == 0);
            if (count == 0 || (keyword == "oneOf" && count != 1))
                errors.Add(path + ": " + keyword + " did not select a valid schema branch.");
        }
        if (schema.TryGetProperty("const", out var constant) && !JsonElement.DeepEquals(value, constant))
            errors.Add(path + ": invalid constant.");
        if (schema.TryGetProperty("enum", out var values) && !values.EnumerateArray().Any(candidate => JsonElement.DeepEquals(value, candidate)))
            errors.Add(path + ": value not in enum.");
        if (schema.TryGetProperty("type", out var type))
        {
            var matches = type.ValueKind == JsonValueKind.Array
                ? type.EnumerateArray().Any(t => HasType(value, t.GetString()!))
                : HasType(value, type.GetString()!);
            if (!matches) return [path + ": invalid JSON type."];
        }
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (schema.TryGetProperty("required", out var required))
                foreach (var field in required.EnumerateArray())
                    if (!value.TryGetProperty(field.GetString()!, out _)) errors.Add(path + ": missing " + field.GetString());
            foreach (var field in value.EnumerateObject())
            {
                if (schema.TryGetProperty("properties", out var properties) && properties.TryGetProperty(field.Name, out var child))
                    errors.AddRange(Evaluate(field.Value, child, root, path + "." + field.Name, depth + 1));
                else if (schema.TryGetProperty("additionalProperties", out var additional))
                    errors.AddRange(Evaluate(field.Value, additional, root, path + "." + field.Name, depth + 1));
            }
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            if (schema.TryGetProperty("minItems", out var minimum) && value.GetArrayLength() < minimum.GetInt32())
                errors.Add(path + ": too few items.");
            if (schema.TryGetProperty("items", out var itemSchema))
                foreach (var item in value.EnumerateArray()) errors.AddRange(Evaluate(item, itemSchema, root, path + "[]", depth + 1));
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()!;
            if (schema.TryGetProperty("minLength", out var length) && text.EnumerateRunes().Count() < length.GetInt32())
                errors.Add(path + ": string too short.");
            if (schema.TryGetProperty("pattern", out var pattern) &&
                !Regex.IsMatch(text, pattern.GetString()!, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
                errors.Add(path + ": invalid format.");
        }
        if (value.ValueKind == JsonValueKind.Number && schema.TryGetProperty("minimum", out var min) &&
            value.GetDecimal() < min.GetDecimal()) errors.Add(path + ": below minimum.");
        return errors;
    }

    private static bool HasType(JsonElement value, string type) => type switch
    {
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        "string" => value.ValueKind == JsonValueKind.String,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => value.ValueKind == JsonValueKind.Null,
        "number" => value.ValueKind == JsonValueKind.Number,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var n) && n == decimal.Truncate(n),
        _ => throw new ConfigurationException("Unsupported internal schema type.")
    };
}
