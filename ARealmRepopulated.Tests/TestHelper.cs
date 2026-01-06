using System;
using System.IO;
using System.Linq;

namespace ARealmRepopulated.Tests;

internal static class TestHelper {

    public static string ReadEmbeddedResource(string fileName) {
        var assembly = typeof(TestHelper).Assembly;
        var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(f => f.EndsWith(fileName))
            ?? throw new InvalidOperationException($"Resource {fileName} not found");
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Could not find embedded resource: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

}
