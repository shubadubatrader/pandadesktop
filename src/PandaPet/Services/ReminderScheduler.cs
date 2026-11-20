using System.Globalization;
using System.Windows.Threading;
using PandaPet.Models;

namespace PandaPet.Services;

public sealed class ReminderScheduler
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<string, DateTime> _snoozedUntil = [];
    private readonly HashSet<string> _triggeredKeys = [];

    public ReminderScheduler(AppSettings settings, SettingsService settingsService)
    {
        _settings = settings;
        _settingsService = settingsService;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _timer.Tick += (_, _) => CheckReminders();
    }

    public event Action<ReminderItem>? ReminderDue;

    public void Start()
    {
        _timer.Start();
        CheckReminders();
    }

    public void Stop() => _timer.Stop();

    public void Refresh()
    {
        _triggeredKeys.Clear();
        CheckReminders();
    }

    public void Snooze(ReminderItem reminder, TimeSpan delay)
    {
        _snoozedUntil[reminder.Id] = DateTime.Now.Add(delay);
    }

    public void Complete(ReminderItem reminder)
    {
        reminder.LastCompletedDate = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        _snoozedUntil.Remove(reminder.Id);
        _settingsService.Save(_settings);
    }

    private void CheckReminders()
    {
        var now = DateTime.Now;
        foreach (var reminder in _settings.Reminders.Where(item => item.Enabled))
        {
            if (reminder.AllowedDays.Count > 0 && !reminder.AllowedDays.Contains(now.DayOfWeek))
            {
                continue;
            }

            var todayKey = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (string.Equals(reminder.LastCompletedDate, todayKey, StringComparison.Ordinal))
            {
                continue;
            }

            if (_snoozedUntil.TryGetValue(reminder.Id, out var snoozedUntil))
            {
                if (now < snoozedUntil)
                {
                    continue;
                }

                _snoozedUntil.Remove(reminder.Id);
                ReminderDue?.Invoke(reminder);
                continue;
            }

            if (!TimeOnly.TryParseExact(reminder.Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            {
                continue;
            }

            var triggerTime = DateTime.Today.Add(time.ToTimeSpan());
            var triggerKey = $"{reminder.Id}:{now:yyyy-MM-dd}";
            var isDue = now >= triggerTime;
            if (isDue && _triggeredKeys.Add(triggerKey))
            {
                ReminderDue?.Invoke(reminder);
            }
        }
    }
}
