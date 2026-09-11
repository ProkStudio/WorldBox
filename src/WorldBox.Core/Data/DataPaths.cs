namespace WorldBox.Core.Data;

/// <summary>
/// Поиск папки data рядом с exe или выше по дереву исходников.
/// Вызывается только при старте. В тике симуляции обращений к файлам нет.
/// </summary>
public static class DataPaths
{
    /// <summary>Полный путь к файлу внутри data или null, если файл не найден.</summary>
    public static string? Find(string fileName)
    {
        string baseDirectory = AppContext.BaseDirectory;
        string next = Path.Combine(baseDirectory, "data", fileName);
        if (File.Exists(next))
        {
            return next;
        }

        var directory = new DirectoryInfo(baseDirectory);
        for (int depth = 0; depth < 8 && directory != null; depth++)
        {
            string candidate = Path.Combine(directory.FullName, "data", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
