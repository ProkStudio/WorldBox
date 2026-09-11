using System.Text.Json;
using WorldBox.Core.Data;

namespace WorldBox.UI;

/// <summary>
/// Все видимые строки игры живут в data/strings.ru.json, в коде только ключи.
/// Если ключа нет, показывается сам ключ — так сразу видно, что забыли перевод.
/// </summary>
public static class Strings
{
    private static Dictionary<string, string> _map = new(StringComparer.Ordinal);

    public static bool Loaded { get; private set; }

    public static string? LoadedFrom { get; private set; }

    public static void Load(string fileName = "strings.ru.json")
    {
        string? path = DataPaths.Find(fileName);
        if (path == null)
        {
            Loaded = false;
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            Dictionary<string, string>? parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (parsed != null)
            {
                _map = new Dictionary<string, string>(parsed, StringComparer.Ordinal);
                Loaded = true;
                LoadedFrom = path;
            }
        }
        catch (JsonException)
        {
            Loaded = false;
        }
        catch (IOException)
        {
            Loaded = false;
        }
    }

    public static string Get(string key) => _map.TryGetValue(key, out string? value) ? value : key;
}
