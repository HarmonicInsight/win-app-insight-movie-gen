namespace InsightMovie.Core;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public class Config
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InsightMovie");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private Dictionary<string, JsonElement> _data = new();

    public Config()
    {
        Load();
    }

    public void Load()
    {
        if (!File.Exists(ConfigPath))
        {
            _data = new Dictionary<string, JsonElement>();
            return;
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            _data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
                    ?? new Dictionary<string, JsonElement>();
        }
        catch
        {
            _data = new Dictionary<string, JsonElement>();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(_data, options);
        File.WriteAllText(ConfigPath, json);
    }

    public T? Get<T>(string key, T? defaultValue = default)
    {
        if (!_data.TryGetValue(key, out var element))
            return defaultValue;

        try
        {
            return element.Deserialize<T>();
        }
        catch
        {
            return defaultValue;
        }
    }

    public void Set<T>(string key, T value)
    {
        var element = JsonSerializer.SerializeToElement(value);
        _data[key] = element;
        Save();
    }

    public bool IsFirstRun
    {
        get => Get<bool>("is_first_run", true);
        set => Set("is_first_run", value);
    }

    public string? EngineUrl
    {
        get => Get<string?>("engine_url", null);
        set => Set("engine_url", value);
    }

    public string? EnginePath
    {
        get => Get<string?>("engine_path", null);
        set => Set("engine_path", value);
    }

    public int? DefaultSpeakerId
    {
        get => Get<int?>("default_speaker_id", null);
        set => Set("default_speaker_id", value);
    }

    public string? LicenseKey
    {
        get => Get<string?>("license_key", null);
        set => Set("license_key", value);
    }

    public void MarkSetupCompleted()
    {
        IsFirstRun = false;
    }

    public void ClearLicense()
    {
        LicenseKey = null;
    }
}
