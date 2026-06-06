using System.Text.Json.Nodes;

namespace ARealmRepopulated.Core.Json;

public static class JsonExtensions {

    public static int GetInt(this JsonNode node) {
        ArgumentNullException.ThrowIfNull(node);

        if (node is not JsonValue jsonValue) {
            throw new ArgumentException("Node must be a JSON value", nameof(node));
        }
        if (jsonValue.TryGetValue<int>(out var intValue)) {
            return intValue;
        }
        if (jsonValue.TryGetValue<string>(out var stringValue) && int.TryParse(stringValue, out intValue)) {
            return intValue;
        }
        throw new ArgumentException("Unable to parse JSON node as integer", nameof(node));
    }

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

    public static byte GetByte(this JsonNode node) {
        ArgumentNullException.ThrowIfNull(node);

        if (node is not JsonValue jsonValue) {
            throw new ArgumentException("Node must be a JSON value", nameof(node));
        }
        if (jsonValue.TryGetValue<byte>(out var byteValue)) {
            return byteValue;
        }
        if (jsonValue.TryGetValue<int>(out var intValue) && intValue >= byte.MinValue && intValue <= byte.MaxValue) {
            return (byte)intValue;
        }
        throw new ArgumentException("Unable to parse JSON node as byte", nameof(node));
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

}
