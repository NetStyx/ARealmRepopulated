using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace ARealmRepopulated.Core.Json;

/// <summary>
/// https://stackoverflow.com/a/79819855
/// </summary>
public static class DefaultValueModifier {

    public static void Instance(JsonTypeInfo typeInfo) {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
            return;

        foreach (var propertyInfo in typeInfo.Properties) {
            if (!TryGetAttribute<DefaultValueAttribute>(propertyInfo, out var defaultValueAttribute))
                continue;

            if (typeInfo.Options.DefaultIgnoreCondition != JsonIgnoreCondition.WhenWritingDefault) {

                if (!TryGetAttribute<JsonIgnoreAttribute>(propertyInfo, out var ignoreAttribute))
                    continue;

                if (ignoreAttribute.Condition != JsonIgnoreCondition.WhenWritingDefault)
                    continue;

                propertyInfo.ShouldSerialize = (_, value) => !Object.Equals(value, defaultValueAttribute.Value);
            }
        }
    }

    private static bool TryGetAttribute<T>(JsonPropertyInfo jsonPropertyInfo, [NotNullWhen(true)] out T? attribute) where T : Attribute {
        var attributes = jsonPropertyInfo.AttributeProvider?.GetCustomAttributes(typeof(T), true);
        if (attributes != null && attributes.Length > 0) {
            attribute = (T)attributes[0];
            return true;
        }
        attribute = null;
        return false;
    }

}
