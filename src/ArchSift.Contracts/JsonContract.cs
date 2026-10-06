using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchSift.Contracts;

public static class JsonContract
{
    public static JsonSerializerOptions Options { get; } = Create();
    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = true,
            MaxDepth = 64
        };
        options.Converters.Add(new IntegerConverter());
        return options;
    }

    private sealed class IntegerConverter : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            var value = reader.GetDecimal();
            if (value != decimal.Truncate(value) || value < int.MinValue || value > int.MaxValue)
                throw new JsonException("Integer is out of range.");
            return (int)value;
        }
        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(value);
    }
}
