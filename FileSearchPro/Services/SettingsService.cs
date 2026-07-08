using System.IO;
using System.Text.Json;
using FileSearchPro.Models;

namespace FileSearchPro.Services;

public class SettingsService
{
    private readonly string _settingsDir;
    private readonly string _configPath;

    public SettingsService()
    {
        _settingsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings");
        _configPath = Path.Combine(_settingsDir, "config.json");
        Directory.CreateDirectory(_settingsDir);
    }

    public SearchConfig LoadConfig()
    {
        if (!File.Exists(_configPath))
            return new SearchConfig();

        var json = File.ReadAllText(_configPath);
        return JsonSerializer.Deserialize<SearchConfig>(json) ?? new SearchConfig();
    }

    public void SaveConfig(SearchConfig config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configPath, json);
    }
}
