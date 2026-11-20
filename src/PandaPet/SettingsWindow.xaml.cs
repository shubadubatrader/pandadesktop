using System.Globalization;
using System.Windows;
using PandaPet.Models;
using PandaPet.Services;

namespace PandaPet;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;

    public SettingsWindow(AppSettings settings, SettingsService settingsService, bool isFirstRun)
    {
        InitializeComponent();
        _settings = settings;
        _settingsService = settingsService;

        if (isFirstRun)
        {
            HeadingText.Text = "Давайте познакомимся";
            IntroText.Text = "Введите имя ребёнка и выберите удобное время. Эти данные хранятся только на компьютере.";
            CancelButton.Visibility = Visibility.Collapsed;
        }

        ParentPinCard.Visibility = isFirstRun
            || string.IsNullOrWhiteSpace(settings.ParentPinHash)
            || string.IsNullOrWhiteSpace(settings.ParentPinSalt)
            ? Visibility.Visible
            : Visibility.Collapsed;

        PopulateFields();
        Loaded += (_, _) => ChildNameBox.Focus();
    }

    private void PopulateFields()
    {
        var defaults = AppSettings.CreateDefaults();
        var backpack = _settings.Reminders.FirstOrDefault(item => item.Id == "backpack")
            ?? defaults.Reminders.First(item => item.Id == "backpack");
        var teeth = _settings.Reminders.FirstOrDefault(item => item.Id == "teeth")
            ?? defaults.Reminders.First(item => item.Id == "teeth");

        ChildNameBox.Text = _settings.ChildName;
        BackpackEnabledBox.IsChecked = backpack.Enabled;
        BackpackTimeBox.Text = backpack.Time;
        TeethEnabledBox.IsChecked = teeth.Enabled;
        TeethTimeBox.Text = "21:30";
        GameLimitBox.Text = _settings.GameDailyLimitMinutes.ToString(CultureInfo.InvariantCulture);
        GameBreakIntervalBox.Text = _settings.GameBreakIntervalMinutes.ToString(CultureInfo.InvariantCulture);
        GameProcessesBox.Text = string.Join(Environment.NewLine, _settings.GameProcessNames);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        var childName = ChildNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(childName))
        {
            ErrorText.Text = "Введите имя ребёнка.";
            ChildNameBox.Focus();
            return;
        }

        if (ParentPinCard.Visibility == Visibility.Visible)
        {
            var pin = ParentPinBox.Password.Trim();
            if (pin.Length != 4 || !pin.All(char.IsDigit))
            {
                ErrorText.Text = "Родительский PIN должен состоять ровно из четырёх цифр.";
                ParentPinBox.Focus();
                return;
            }

            if (!string.Equals(pin, ParentPinConfirmBox.Password.Trim(), StringComparison.Ordinal))
            {
                ErrorText.Text = "Родительские PIN-коды не совпадают.";
                ParentPinConfirmBox.Focus();
                return;
            }

            var credentials = ParentPinService.Create(pin);
            _settings.ParentPinSalt = credentials.Salt;
            _settings.ParentPinHash = credentials.Hash;
        }

        if (!IsValidTime(BackpackTimeBox.Text) || !IsValidTime(TeethTimeBox.Text))
        {
            ErrorText.Text = "Время нужно указать в формате ЧЧ:ММ, например 20:00.";
            return;
        }

        var backpackTime = TimeOnly.ParseExact(BackpackTimeBox.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture);
        if (backpackTime < new TimeOnly(20, 0))
        {
            ErrorText.Text = "Напоминание о рюкзаке можно поставить не раньше 20:00.";
            BackpackTimeBox.Focus();
            return;
        }

        if (!int.TryParse(GameLimitBox.Text, out var gameLimit) || gameLimit is < 5 or > 1440)
        {
            ErrorText.Text = "Игровое время должно быть от 5 до 1440 минут.";
            return;
        }

        if (!int.TryParse(GameBreakIntervalBox.Text, out var breakInterval) || breakInterval is < 5 or > 240)
        {
            ErrorText.Text = "Интервал перерывов должен быть от 5 до 240 минут.";
            GameBreakIntervalBox.Focus();
            return;
        }

        _settings.ChildName = childName;
        _settings.GameDailyLimitMinutes = gameLimit;
        _settings.GameBreakIntervalMinutes = breakInterval;
        _settings.GameProcessNames = GameProcessesBox.Text
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        UpdateReminder(
            "backpack",
            "Пора собрать рюкзак 🎒",
            BackpackTimeBox.Text,
            BackpackEnabledBox.IsChecked == true,
            [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday]);
        UpdateReminder(
            "teeth",
            "Пора почистить зубы 🪥",
            "21:30",
            TeethEnabledBox.IsChecked == true,
            Enum.GetValues<DayOfWeek>().ToList());

        _settingsService.Save(_settings);
        DialogResult = true;
    }

    private void UpdateReminder(string id, string title, string time, bool enabled, List<DayOfWeek> allowedDays)
    {
        var reminder = _settings.Reminders.FirstOrDefault(item => item.Id == id);
        if (reminder is null)
        {
            reminder = new ReminderItem { Id = id };
            _settings.Reminders.Add(reminder);
        }

        reminder.Title = title;
        reminder.Time = time;
        reminder.Enabled = enabled;
        reminder.AllowedDays = allowedDays;
    }

    private static bool IsValidTime(string value) =>
        TimeOnly.TryParseExact(value.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
