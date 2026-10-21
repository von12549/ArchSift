using System.Globalization;
using System.Reflection;
using System.Text.Json;
using ArchSift.Contracts;

namespace ArchSift.Testing;

public sealed record TypedCaseArgument(string DeclaredType, string? RuntimeType, string? Value, string Kind);
public sealed record CaseIdentity(string Assembly, string Type, string Method, string[] ParameterTypes,
    TypedCaseArgument[] Arguments);
public sealed record CaseProjection(string ExecutionId, string XunitCaseId, CaseIdentity Identity);

/// <summary>A deliberately limited, lossless identity for the parameter types used by adoption tests.</summary>
public static class CaseIdentities
{
    public const string Marker = "ARCHSIFT_CASE_IDENTITY:";
    private static readonly JsonSerializerOptions Compact = new(JsonContract.Options) { WriteIndented = false };

    public static CaseIdentity Create(MethodInfo method, object?[] arguments)
    {
        if (method.IsGenericMethod || method.DeclaringType is null || method.DeclaringType.IsGenericType)
            throw new InvalidDataException("Generic test signatures are not supported by the case identity contract.");
        var parameters = method.GetParameters();
        if (parameters.Length != arguments.Length) throw new InvalidDataException("Incomplete test arguments.");
        if (parameters.Any(item => !Supported(item.ParameterType)))
            throw new InvalidDataException("Unsupported declared case parameter type.");
        return new(method.DeclaringType.Assembly.GetName().Name!, method.DeclaringType.FullName!, method.Name,
            parameters.Select(item => TypeId(item.ParameterType)).ToArray(),
            parameters.Select((item, index) => Argument(item.ParameterType, arguments[index])).ToArray());
    }

    public static string Serialize(CaseProjection projection) => JsonSerializer.Serialize(projection, Compact);
    public static string Key(CaseIdentity identity) => JsonSerializer.Serialize(identity, Compact);
    private static string TypeId(Type type) => type.AssemblyQualifiedName
        ?? throw new InvalidDataException("Type identity is unavailable.");

    private static TypedCaseArgument Argument(Type declared, object? value)
    {
        if (value is null ? declared.IsValueType : !declared.IsInstanceOfType(value))
            throw new InvalidDataException("Case argument does not match its declared type.");
        var type = value?.GetType();
        string? text = value switch
        {
            null => null,
            string item => item,
            Type item => TypeId(item),
            Enum item => Convert.ToString(Convert.ChangeType(item, Enum.GetUnderlyingType(type!), CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            bool item => item ? "true" : "false",
            char item => ((int)item).ToString(CultureInfo.InvariantCulture),
            byte or sbyte or short or ushort or int or uint or long or ulong or decimal =>
                ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture),
            float item when float.IsFinite(item) => item.ToString("R", CultureInfo.InvariantCulture),
            double item when double.IsFinite(item) => item.ToString("R", CultureInfo.InvariantCulture),
            _ => throw new InvalidDataException("Unsupported case parameter type: " + type?.FullName)
        };
        // RuntimeType for System.Type is stable across runtime implementation classes.
        var kind = value switch { null => "null", string => "string", Type => "type", Enum => "enum",
            bool => "boolean", char => "char", float or double => "float", _ => "number" };
        return new(TypeId(declared), value is Type ? TypeId(typeof(Type)) : type is null ? null : TypeId(type), text, kind);
    }

    public static CaseProjection? Read(string output)
    {
        var lines = output.Split('\n').Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith(Marker, StringComparison.Ordinal)).ToArray();
        if (lines.Length == 0) return null;
        if (lines.Length != 1) throw new InvalidDataException("Duplicate case projections.");
        var projection = JsonSerializer.Deserialize<CaseProjection>(lines[0][Marker.Length..], JsonContract.Options)
            ?? throw new InvalidDataException("Missing case projection.");
        var identity = projection.Identity;
        if (string.IsNullOrWhiteSpace(projection.ExecutionId) || string.IsNullOrWhiteSpace(projection.XunitCaseId) ||
            identity is null || string.IsNullOrWhiteSpace(identity.Assembly) || string.IsNullOrWhiteSpace(identity.Type) ||
            string.IsNullOrWhiteSpace(identity.Method) || identity.ParameterTypes is null || identity.Arguments is null ||
            identity.ParameterTypes.Length != identity.Arguments.Length ||
            identity.Arguments.Where((arg, index) => arg is null || arg.DeclaredType != identity.ParameterTypes[index] ||
                string.IsNullOrWhiteSpace(arg.DeclaredType) || !Valid(arg)).Any())
            throw new InvalidDataException("Incomplete case projection.");
        return projection;
    }

    private static bool Valid(TypedCaseArgument argument)
    {
        if (argument.Kind == "null") return argument.RuntimeType is null && argument.Value is null &&
            argument.DeclaredType.Split(',')[0] is "System.String" or "System.Type";
        if (argument.RuntimeType is null || argument.Value is null) return false;
        if (argument.RuntimeType != argument.DeclaredType) return false;
        var typeName = argument.RuntimeType.Split(',')[0];
        return argument.Kind switch
        {
            "string" => typeName == "System.String",
            "type" => typeName == "System.Type" && argument.Value.Contains(", ", StringComparison.Ordinal),
            "enum" => argument.RuntimeType == argument.DeclaredType &&
                System.Numerics.BigInteger.TryParse(argument.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) &&
                number.ToString(CultureInfo.InvariantCulture) == argument.Value,
            "boolean" => typeName == "System.Boolean" && argument.Value is "true" or "false",
            "char" => typeName == "System.Char" && ushort.TryParse(argument.Value, CultureInfo.InvariantCulture, out _),
            "float" => typeName is "System.Single" or "System.Double" &&
                double.TryParse(argument.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var floating) && double.IsFinite(floating),
            "number" => typeName is "System.Byte" or "System.SByte" or "System.Int16" or "System.UInt16" or
                "System.Int32" or "System.UInt32" or "System.Int64" or "System.UInt64" or "System.Decimal" &&
                decimal.TryParse(argument.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            _ => false
        };
    }

    private static bool Supported(Type type) => type.IsEnum || type == typeof(string) || type == typeof(Type) ||
        type == typeof(bool) || type == typeof(char) || type == typeof(byte) || type == typeof(sbyte) ||
        type == typeof(short) || type == typeof(ushort) || type == typeof(int) || type == typeof(uint) ||
        type == typeof(long) || type == typeof(ulong) || type == typeof(decimal) || type == typeof(float) || type == typeof(double);
}
