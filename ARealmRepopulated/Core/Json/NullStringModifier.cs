using System.Text.Json.Serialization.Metadata;

namespace ARealmRepopulated.Core.Json;
public static class NullStringModifier
{
    public static void Instance(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
            return;

        foreach (var jsonPropertyInfo in typeInfo.Properties)
        {
            if (jsonPropertyInfo.PropertyType == typeof(string))
            {
                jsonPropertyInfo.ShouldSerialize = static (obj, value) =>
                    !string.IsNullOrWhiteSpace((string)value!);
            }
        }
    }
}
