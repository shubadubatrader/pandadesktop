namespace PandaPet.Models;

public sealed class AppSettings
{
    public int? SettingsVersion { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public string ParentPinSalt { get; set; } = string.Empty;
    public string ParentPinHash { get; set; } = string.Empty;
    public int StarCount { get; set; }
    public int GameDailyLimitMinutes { get; set; } = 60;
    public int GameBreakIntervalMinutes { get; set; } = 30;
    public List<string> GameProcessNames { get; set; } =
    [
        "MinecraftLauncher",
        "javaw",
        "RobloxPlayerBeta",
        "steam"
    ];
    public List<ReminderItem> Reminders { get; set; } = [];
    public string GameUsageDate { get; set; } = string.Empty;
    public int GameUsageSeconds { get; set; }

    public static AppSettings CreateDefaults() => new()
    {
        SettingsVersion = 2,
        Reminders =
        [
            new ReminderItem
            {
                Id = "backpack",
                Title = "Пора собрать рюкзак 🎒",
                Time = "20:00",
                Enabled = true,
                AllowedDays =
                [
                    DayOfWeek.Sunday,
                    DayOfWeek.Monday,
                    DayOfWeek.Tuesday,
                    DayOfWeek.Wednesday,
                    DayOfWeek.Thursday
                ]
            },
            new ReminderItem
            {
                Id = "teeth",
                Title = "Пора почистить зубы 🪥",
                Time = "21:30",
                Enabled = true,
                AllowedDays = Enum.GetValues<DayOfWeek>().ToList()
            }
        ]
    };
}
