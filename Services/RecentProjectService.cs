using System.Text.Json;

namespace MemoryLab.Services;

public sealed class RecentProjectService
{
    private const int MaxRecent = 10;

    private readonly string _settingsFile;

    public RecentProjectService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "MemoryLab");

        Directory.CreateDirectory(folder);

        _settingsFile = Path.Combine(
            folder,
            "recent-projects.json");
    }

    public IReadOnlyList<string> Load()
    {
        try
        {
            if (!File.Exists(_settingsFile))
                return Array.Empty<string>();

            var json = File.ReadAllText(_settingsFile);

            var items = JsonSerializer.Deserialize<List<string>>(json)
                ?? new List<string>();

            return items
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxRecent)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public void Add(string filePath)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath);

            var items = Load()
                .Where(x => !string.Equals(
                    x,
                    fullPath,
                    StringComparison.OrdinalIgnoreCase))
                .Prepend(fullPath)
                .Take(MaxRecent)
                .ToList();

            File.WriteAllText(
                _settingsFile,
                JsonSerializer.Serialize(
                    items,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
        }
        catch
        {
        }
    }
}
