using System.IO;
using System.Text.Json;
using PandaPet.Models;

namespace PandaPet.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string SettingsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PandaPet");

    public string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return AppSettings.CreateDefaults();
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                ?? AppSettings.CreateDefaults();

            if (settings.Reminders.Count == 0)
            {
                settings.Reminders = AppSettings.CreateDefaults().Reminders;
            }

            ApplyMigrations(settings);
            ApplyScheduleDefaults(settings);

            return settings;
        }
        catch (JsonException)
        {
            return AppSettings.CreateDefaults();
        }
        catch (IOException)
        {
            return AppSettings.CreateDefaults();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var temporaryPath = SettingsPath + ".tmp";
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, SettingsPath, overwrite: true);
    }

    private static void ApplyScheduleDefaults(AppSettings settings)
    {
        var defaults = AppSettings.CreateDefaults();
        foreach (var reminder in settings.Reminders)
        {
            if (reminder.AllowedDays.Count > 0)
            {
                continue;
            }

            reminder.AllowedDays = defaults.Reminders
                .FirstOrDefault(item => item.Id == reminder.Id)?.AllowedDays.ToList()
                ?? Enum.GetValues<DayOfWeek>().ToList();
        }
    }

    private static void ApplyMigrations(AppSettings settings)
    {
        if (settings.SettingsVersion.GetValueOrDefault() < 2)
        {
            if (!settings.GameProcessNames.Contains("steam", StringComparer.OrdinalIgnoreCase))
            {
                settings.GameProcessNames.Add("steam");
            }

            settings.SettingsVersion = 2;
        }
    }
}
