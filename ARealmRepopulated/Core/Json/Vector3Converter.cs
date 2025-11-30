using FFXIVClientStructs.FFXIV.Common.Math;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ARealmRepopulated.Core.Json;
public class Vector3Converter : JsonConverter<Vector3>
{
    public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        var dictionary = new Dictionary<string, float>();
        var currentProperty = "";
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new Vector3(dictionary["x"], dictionary["y"], dictionary["z"]);
            }

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                currentProperty = reader.GetString() ?? "u";
            }

            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetSingle(out var val))
                    dictionary.Add(currentProperty.ToLowerInvariant(), val);
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                if (float.TryParse(reader.GetString(), out var val))
                    dictionary.Add(currentProperty.ToLowerInvariant(), val);
            }
        }

        return new Vector3();
    }

    public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteNumber("Z", value.Z);
        writer.WriteEndObject();
    }
}
