using System.Windows;
using PandaPet.Models;
using PandaPet.Services;

namespace PandaPet;

public partial class ParentAccessWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly bool _isSetup;

    public ParentAccessWindow(AppSettings settings, SettingsService settingsService)
    {
        InitializeComponent();
        _settings = settings;
        _settingsService = settingsService;
        _isSetup = string.IsNullOrWhiteSpace(settings.ParentPinHash)
            || string.IsNullOrWhiteSpace(settings.ParentPinSalt);

        if (_isSetup)
        {
            HeadingText.Text = "Создайте родительский PIN";
            DescriptionText.Text = "PIN защитит расписание, игровые лимиты и список программ от случайных изменений.";
            ConfirmationPanel.Visibility = Visibility.Visible;
            ContinueButton.Content = "Создать PIN";
        }

        Loaded += (_, _) => PinBox.Focus();
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        var pin = PinBox.Password.Trim();
        if (!IsFourDigitPin(pin))
        {
            ErrorText.Text = "PIN должен состоять ровно из четырёх цифр.";
            PinBox.Focus();
            return;
        }

        if (_isSetup)
        {
            if (!string.Equals(pin, ConfirmPinBox.Password.Trim(), StringComparison.Ordinal))
            {
                ErrorText.Text = "PIN-коды не совпадают.";
                ConfirmPinBox.Focus();
                return;
            }

            var credentials = ParentPinService.Create(pin);
            _settings.ParentPinSalt = credentials.Salt;
            _settings.ParentPinHash = credentials.Hash;
            _settingsService.Save(_settings);
            DialogResult = true;
            return;
        }

        if (!ParentPinService.Verify(pin, _settings.ParentPinSalt, _settings.ParentPinHash))
        {
            ErrorText.Text = "Неверный PIN.";
            PinBox.Clear();
            PinBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private static bool IsFourDigitPin(string pin) =>
        pin.Length == 4 && pin.All(char.IsDigit);

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
