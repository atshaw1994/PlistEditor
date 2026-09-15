using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PlistEditor.Models;

namespace PlistEditor.Services;

public static class SettingsService
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PlistEditor"
    );

    private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Global application settings instance.
    /// </summary>
    public static AppSettings Current { get; } = LoadSettings();

    public static AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load settings: {ex.Message}");
        }

        return new AppSettings();
    }

    public static void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);

            string json = JsonSerializer.Serialize(Current, JsonOptions);

            string tempFile = Path.Combine(SettingsFolder, $"{Guid.NewGuid()}.tmp");
            File.WriteAllText(tempFile, json);
            File.Move(tempFile, SettingsFile, overwrite: true);

            Debug.WriteLine($"Successfully saved settings to: {SettingsFile}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
}