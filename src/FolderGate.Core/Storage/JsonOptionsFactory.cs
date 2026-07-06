using System.Text.Json;
using System.Text.Json.Serialization;

namespace FolderGate.Core.Storage;

internal static class JsonOptionsFactory
{
    public static JsonSerializerOptions Create(bool indented = true)
    {
        JsonSerializerOptions options = new()
        {
            WriteIndented = indented,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
