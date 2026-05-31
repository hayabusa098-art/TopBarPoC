using System;
using System.IO;
using System.Text.Json;

namespace TopBarPoC;

internal sealed record AppSettings
{
    public string MonitorMode { get; init; } = "all";
}

internal static class SettingsService
{
    internal static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TopBarPoC", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented               = true,
    };

    // Returns default settings on missing file, invalid JSON, or unrecognized monitorMode.
    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new();
            var s = JsonSerializer.Deserialize<AppSettings>(
                File.ReadAllText(SettingsPath), JsonOptions);
            if (s is not null)
                s = s with { MonitorMode = s.MonitorMode.ToLowerInvariant() };
            return s is { MonitorMode: "all" or "primary" } ? s : new();
        }
        catch { return new(); }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
