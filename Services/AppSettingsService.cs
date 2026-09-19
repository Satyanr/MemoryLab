using System.Text.Json;
using MemoryLab.Models;

namespace MemoryLab.Services;

public sealed class AppSettingsService
{
    private readonly string _settingsFile;

    public AppSettingsService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "MemoryLab");

        Directory.CreateDirectory(folder);

        _settingsFile = Path.Combine(
            folder,
            "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsFile))
                return new AppSettings();

            var json = File.ReadAllText(_settingsFile);
            var settings =
                JsonSerializer.Deserialize<AppSettings>(json)
                ?? new AppSettings();

            Normalize(settings);
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Normalize(settings);

        File.WriteAllText(
            _settingsFile,
            JsonSerializer.Serialize(
                settings,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
    }

    private static void Normalize(AppSettings settings)
    {
        settings.AddressRefreshMs =
            Math.Clamp(settings.AddressRefreshMs, 100, 5000);

        settings.MemoryViewerRefreshMs =
            Math.Clamp(settings.MemoryViewerRefreshMs, 100, 5000);

        settings.ScanResultLimit =
            Math.Clamp(settings.ScanResultLimit, 10_000, 1_000_000);
    }
}
