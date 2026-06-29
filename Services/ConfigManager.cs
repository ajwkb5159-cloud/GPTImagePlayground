using System.Diagnostics;
using System.Text.Json;
using ImageGenerator.Models;

namespace ImageGenerator.Services;

internal class ConfigManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _configPath;

    public ConfigManager()
    {
        var exeDir = AppContext.BaseDirectory;
        _configPath = Path.Combine(exeDir, "appsettings.json");
    }

    public AppConfig Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (config != null) return config;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ConfigManager] Failed to load config: {ex.Message}");
        }

        return new AppConfig();
    }

    public void Save(AppConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ConfigManager] Failed to save config: {ex.Message}");
        }
    }
}
