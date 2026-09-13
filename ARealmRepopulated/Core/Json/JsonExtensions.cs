using FFXIVClientStructs.FFXIV.Common.Math;
using System.Globalization;
using System.Text.Json.Nodes;

namespace ARealmRepopulated.Core.Json;

public static class JsonExtensions {

    public static string GetString(this JsonNode node) {
        ArgumentNullException.ThrowIfNull(node);

        if (node is not JsonValue jsonValue) {
            throw new ArgumentException("Node must be a JSON value", nameof(node));
        }
        if (jsonValue.TryGetValue<string>(out var stringValue)) {
            return stringValue;
        }
        if (jsonValue.TryGetValue<int>(out var intValue)) {
            return intValue.ToString();
        }
        return jsonValue.ToString();
    }

    public static T GetEnum<T>(this JsonNode node) where T : struct, Enum {
        ArgumentNullException.ThrowIfNull(node);

        if (node is not JsonValue jsonValue) {
            throw new ArgumentException("Node must be a JSON value", nameof(node));
        }

        if (jsonValue.TryGetValue<string>(out var stringValue)) {
            if (Enum.TryParse<T>(stringValue, ignoreCase: true, out var result)) {
                return result;
            }

            if (int.TryParse(stringValue, out var numericValue)) {
                return (T)Enum.ToObject(typeof(T), numericValue);
            }

            throw new ArgumentException($"Unable to parse '{stringValue}' as enum {typeof(T).Name}");
        }

        if (jsonValue.TryGetValue<int>(out var intValue)) {
            return (T)Enum.ToObject(typeof(T), intValue);
        }

        if (jsonValue.TryGetValue<long>(out var longValue)) {
            return (T)Enum.ToObject(typeof(T), longValue);
        }

        if (jsonValue.TryGetValue<byte>(out var byteValue)) {
            return (T)Enum.ToObject(typeof(T), byteValue);
        }

        throw new ArgumentException($"Unable to parse JSON node as enum {typeof(T).Name}");
    }

    /// The tolerant counterparts below return null for a missing or unreadable entry instead of
    /// throwing, so one absent key in an imported file no longer costs the whole file.

    public static byte? GetByteOrNull(this JsonNode? node) {
        var value = node.GetDoubleOrNull();
        if (value == null || value < byte.MinValue || value > byte.MaxValue)
            return null;

        return (byte)value.Value;
    }

    public static ushort? GetUShortOrNull(this JsonNode? node) {
        var value = node.GetDoubleOrNull();
        if (value == null || value < ushort.MinValue || value > ushort.MaxValue)
            return null;

        return (ushort)value.Value;
    }

    public static int? GetIntOrNull(this JsonNode? node) {
        var value = node.GetDoubleOrNull();
        if (value == null || value < int.MinValue || value > int.MaxValue)
            return null;

        return (int)value.Value;
    }

    public static float? GetFloatOrNull(this JsonNode? node) {
        var value = node.GetDoubleOrNull();
        if (value == null)
            return null;

        return (float)value.Value;
    }

    public static bool? GetBoolOrNull(this JsonNode? node) {
        if (node is not JsonValue jsonValue)
            return null;

        if (jsonValue.TryGetValue<bool>(out var boolValue))
            return boolValue;

        if (jsonValue.TryGetValue<string>(out var stringValue) && bool.TryParse(stringValue, out boolValue))
            return boolValue;

        return null;
    }

    public static string? GetStringOrNull(this JsonNode? node) {
        if (node is not JsonValue jsonValue)
            return null;

        return jsonValue.TryGetValue<string>(out var stringValue) ? stringValue : null;
    }

    public static Vector3? GetVector3OrNull(this JsonNode? node) {
        var components = node.GetComponentsOrNull(3);
        if (components == null)
            return null;

        return new Vector3(components[0], components[1], components[2]);
    }

    public static Vector4? GetVector4OrNull(this JsonNode? node) {
        var components = node.GetComponentsOrNull(4);
        if (components == null)
            return null;

        return new Vector4(components[0], components[1], components[2], components[3]);
    }

    private static double? GetDoubleOrNull(this JsonNode? node) {
        if (node is not JsonValue jsonValue)
            return null;

        if (jsonValue.TryGetValue<double>(out var doubleValue))
            return doubleValue;

        if (jsonValue.TryGetValue<string>(out var stringValue)
            && double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out doubleValue)) {
            return doubleValue;
        }

        return null;
    }

    private static float[]? GetComponentsOrNull(this JsonNode? node, int count) {

        if (node is not JsonValue value || !value.TryGetValue<string>(out var text))
            return null;

        var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != count)
            return null;

        var components = new float[count];
        for (var i = 0; i < count; i++) {
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out components[i]))
                return null;
        }

        return components;
    }

}
