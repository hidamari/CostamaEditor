using System.IO;
using System.Text.Json;

namespace CostamaEditor.Services;

public sealed class AppSettings
{
    public string Language { get; set; } = "zh-Hant";
    public List<string> ItemDbSources { get; set; } = new();
    public List<string> ItemInfoSources { get; set; } = new();
    public List<string> GrfSources { get; set; } = new();
}

public sealed class SettingsStore
{
    private readonly string _path = Path.Combine(AppContext.BaseDirectory, "settings.json");
    private readonly string _legacyPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Auriga", "CostamaEditor", "settings.json");

    public AppSettings Load()
    {
        if (File.Exists(_path))
            return TryLoad(_path) ?? CreateDefaults();

        var legacySettings = TryLoad(_legacyPath);
        var settings = legacySettings ?? CreateDefaults();
        try
        {
            Save(settings);
            if (legacySettings is not null)
                File.Delete(_legacyPath);
        }
        catch
        {
            // Keep the editor usable if settings cannot be moved to the executable folder.
        }
        return settings;
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static AppSettings? TryLoad(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path))
                : null;
        }
        catch
        {
            // A damaged preference file should not prevent opening the editor.
            return null;
        }
    }

    private static AppSettings CreateDefaults()
    {
        return new AppSettings();
    }
}
