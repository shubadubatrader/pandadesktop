using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PandaPet.Models;
using PandaPet.Services;

namespace PandaPet;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly ReminderScheduler _reminderScheduler;
    private readonly GameTimeTracker _gameTimeTracker;
    private readonly DispatcherTimer _speechTimer;
    private readonly DispatcherTimer _singleClickTimer;
    private readonly ImageSource _idlePandaSource;
    private readonly BitmapSource[] _headFrames;
    private readonly BitmapSource[] _pawFrames;
    private readonly BitmapSource _sadPandaSource;
    private DispatcherTimer? _spriteAnimationTimer;
    private ReminderItem? _currentReminder;
    private bool _allowClose;
    private bool _isExiting;
    private bool _isCollapsed;
    private bool _isSadAboutGame;
    private double _expandedLeft;

    public MainWindow(AppSettings settings, SettingsService settingsService)
    {
        InitializeComponent();
        _settings = settings;
        _settingsService = settingsService;
        _headFrames = LoadSpriteFrames("panda-head-sheet-v4.png", 4, 4);
        _pawFrames = LoadSpriteFrames("panda-paws-sheet-v4.png", 4, 4);
        _sadPandaSource = LoadSpriteFrames("panda-actions-sheet-v2.png", 3, 3)[8];
        _idlePandaSource = _headFrames[0];
        ResetFrameLayers(_idlePandaSource);

        _reminderScheduler = new ReminderScheduler(_settings, _settingsService);
        _reminderScheduler.ReminderDue += ShowReminder;

        _gameTimeTracker = new GameTimeTracker(_settings, _settingsService);
        _gameTimeTracker.GameLimitReached += minutes => Dispatcher.Invoke(() =>
        {
            ShowSpeech(
                "Пора немного отдохнуть 🎮",
                $"{DisplayName}, ты играешь уже около {minutes} минут. Давай разомнёмся и дадим глазам отдохнуть.",
                showActions: false,
                autoHide: TimeSpan.FromSeconds(18));
        });
        _gameTimeTracker.GameBreakDue += minutes => Dispatcher.Invoke(() =>
        {
            ShowSpeech(
                "Время игрового перерыва 🌿",
                $"{DisplayName}, прошло ещё {minutes} минут активной игры. Отойдём от экрана, потянемся и попьём воды?",
                showActions: false,
                autoHide: TimeSpan.FromSeconds(20));
        });
        _gameTimeTracker.GameBreakIgnored += (_, dailyLimitReached) => Dispatcher.Invoke(() =>
            ShowSadGameMood(dailyLimitReached));
        _gameTimeTracker.GameBreakTaken += () => Dispatcher.Invoke(ShowHappyAfterBreak);

        _speechTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _speechTimer.Tick += (_, _) => HideSpeech();

        _singleClickTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(GetDoubleClickTime() + 40)
        };
        _singleClickTimer.Tick += (_, _) =>
        {
            _singleClickTimer.Stop();
            PerformRandomInteraction();
        };
    }

    private string DisplayName => string.IsNullOrWhiteSpace(_settings.ChildName)
        ? "друг"
        : _settings.ChildName.Trim();

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PositionAboveTaskbarAndSlideIn();
        ((Storyboard)FindResource("IdleAnimation")).Begin(this, true);
        _reminderScheduler.Start();
        _gameTimeTracker.Start();
    }

    private void PositionAboveTaskbarAndSlideIn()
    {
        var workArea = SystemParameters.WorkArea;
        _expandedLeft = workArea.Right - ActualWidth - 12;
        Top = Math.Max(workArea.Top + 8, workArea.Bottom - ActualHeight - 8);
        Left = workArea.Right + 8;

        var slide = new DoubleAnimation
        {
            From = Left,
            To = _expandedLeft,
            Duration = TimeSpan.FromMilliseconds(620),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        slide.Completed += (_, _) => Left = _expandedLeft;
        BeginAnimation(LeftProperty, slide);
    }

    public void OpenSettings(bool isFirstRun = false)
    {
        var window = new SettingsWindow(_settings, _settingsService, isFirstRun)
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            _reminderScheduler.Refresh();
            _gameTimeTracker.Refresh();
            ShowSpeech(
                $"Приятно познакомиться, {DisplayName}!",
                "Я буду мягко напоминать о важных делах и перерывах.",
                showActions: false,
                autoHide: TimeSpan.FromSeconds(8));
        }
    }

    private void ShowReminder(ReminderItem reminder)
    {
        Dispatcher.Invoke(() =>
        {
            _currentReminder = reminder;
            ShowSpeech(
                reminder.Title,
                $"{DisplayName}, это маленькое напоминание. Отметь дело, когда закончишь.",
                showActions: true,
                autoHide: null);
        });
    }

    private void ShowSpeech(string title, string detail, bool showActions, TimeSpan? autoHide)
    {
        if (_isCollapsed)
        {
            ExpandFromEdge();
        }

        SpeechTitle.Text = title;
        SpeechDetail.Text = detail;
        ReminderActions.Visibility = showActions ? Visibility.Visible : Visibility.Collapsed;
        SpeechCard.Visibility = Visibility.Visible;

        _speechTimer.Stop();
        if (autoHide.HasValue)
        {
            _speechTimer.Interval = autoHide.Value;
            _speechTimer.Start();
        }
    }

    private void HideSpeech()
    {
        _speechTimer.Stop();
        SpeechCard.Visibility = Visibility.Collapsed;
        _currentReminder = null;
    }

    private void CompleteReminder_Click(object sender, RoutedEventArgs e)
    {
        if (_currentReminder is not null)
        {
            _reminderScheduler.Complete(_currentReminder);
            _settings.StarCount++;
            _settingsService.Save(_settings);
        }

        _currentReminder = null;
        ShowSpeech(
            "Отличная работа! ⭐",
            $"Молодец, {DisplayName}! У тебя уже {_settings.StarCount} звёзд.",
            showActions: false,
            autoHide: TimeSpan.FromSeconds(7));
    }

    private void SnoozeReminder_Click(object sender, RoutedEventArgs e)
    {
        if (_currentReminder is not null)
        {
            _reminderScheduler.Snooze(_currentReminder, TimeSpan.FromMinutes(5));
        }

        ShowSpeech(
            "Хорошо, напомню позже",
            "Вернусь с этим делом через пять минут.",
            showActions: false,
            autoHide: TimeSpan.FromSeconds(5));
    }

    private void Greeting_Click(object sender, RoutedEventArgs e) => ShowGreeting();

    private void ShowGreeting()
    {
        ShowSpeech(
            $"Привет, {DisplayName}! 👋",
            "Я рядом. Давай сегодня всё успеем и не забудем отдыхать.",
            showActions: false,
            autoHide: TimeSpan.FromSeconds(8));
    }

    private void TestReminder_Click(object sender, RoutedEventArgs e)
    {
        var reminder = _settings.Reminders.FirstOrDefault(r => r.Enabled)
            ?? AppSettings.CreateDefaults().Reminders[0];
        ShowReminder(reminder);
    }

    private void Panda_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            _singleClickTimer.Stop();
            CollapseToEdge();
            e.Handled = true;
            return;
        }

        _singleClickTimer.Stop();
        _singleClickTimer.Start();
        e.Handled = true;
    }

    private void PerformRandomInteraction()
    {
        if (_isSadAboutGame)
        {
            ResetFrameLayers(_sadPandaSource);
            ShowSpeech(
                "Я всё ещё жду перерыв 😟",
                $"{DisplayName}, отойди от игры хотя бы на две минуты. Когда перерыв будет настоящим, я снова повеселею.",
                showActions: false,
                autoHide: TimeSpan.FromSeconds(12));
            return;
        }

        var phrases = PhraseLibrary.Create(DisplayName);

        var interaction = Random.Shared.Next(phrases.Count);
        PlayInteractionAnimation(interaction % 4);
        PlaySpriteAnimation(interaction % 4);
        var (title, detail) = phrases[interaction];

        ShowSpeech(title, detail, showActions: false, autoHide: TimeSpan.FromSeconds(9));
    }

    private static BitmapSource[] LoadSpriteFrames(string assetName, int columns, int rows)
    {
        var sheet = new BitmapImage();
        sheet.BeginInit();
        sheet.CacheOption = BitmapCacheOption.OnLoad;
        sheet.UriSource = new Uri($"pack://application:,,,/Assets/{assetName}", UriKind.Absolute);
        sheet.EndInit();
        sheet.Freeze();

        if (sheet.PixelWidth % columns != 0 || sheet.PixelHeight % rows != 0)
        {
            throw new InvalidOperationException(
                $"Лист {assetName} нельзя ровно разделить на сетку {columns}x{rows}.");
        }

        var frameWidth = sheet.PixelWidth / columns;
        var frameHeight = sheet.PixelHeight / rows;
        var frames = Enumerable.Range(0, columns * rows)
            .Select(index => new CroppedBitmap(
                sheet,
                new Int32Rect(
                    (index % columns) * frameWidth,
                    (index / columns) * frameHeight,
                    frameWidth,
                    frameHeight)))
            .ToArray();

        foreach (var frame in frames)
        {
            frame.Freeze();
        }

        return frames;
    }

    private void PlaySpriteAnimation(int animationKind)
    {
        _spriteAnimationTimer?.Stop();
        var sequence = (animationKind % 4) switch
        {
            0 or 2 => _pawFrames.Append(_pawFrames[0]).ToArray(),
            _ => _headFrames.Append(_headFrames[0]).ToArray()
        };
        var frameIndex = 0;
        ResetFrameLayers(sequence[frameIndex]);

        _spriteAnimationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(136) };
        _spriteAnimationTimer.Tick += (_, _) =>
        {
            frameIndex++;
            if (frameIndex >= sequence.Length)
            {
                _spriteAnimationTimer.Stop();
                _spriteAnimationTimer = null;
                DisplayFrame(_idlePandaSource);
                return;
            }

            DisplayFrame(sequence[frameIndex]);
        };
        _spriteAnimationTimer.Start();
    }

    private void DisplayFrame(ImageSource source)
    {
        PandaImage.Source = source;
        PandaImage.Opacity = 1;
    }

    private void ResetFrameLayers(ImageSource source)
    {
        DisplayFrame(source);
    }

    private void StopSpriteAnimation()
    {
        _spriteAnimationTimer?.Stop();
        _spriteAnimationTimer = null;
        ResetFrameLayers(_isSadAboutGame ? _sadPandaSource : _idlePandaSource);
    }

    private void PlayInteractionAnimation(int animationKind)
    {
        // Положение персонажа всегда остаётся закреплено по центру.
        // Выразительность дают промежуточные спрайты головы и лап, а не сдвиг всего изображения.
        PandaBob.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
        PandaBob.X = 0;
        PandaRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
        PandaRotate.Angle = 0;

        var easing = new SineEase { EasingMode = EasingMode.EaseInOut };
        var scaleTo = animationKind % 2 == 0 ? 1.018 : 0.985;
        PandaScale.BeginAnimation(
            System.Windows.Media.ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1, scaleTo, TimeSpan.FromMilliseconds(960))
            {
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(1),
                FillBehavior = FillBehavior.Stop,
                EasingFunction = easing
            });
    }

    private void ShowSadGameMood(bool dailyLimitReached)
    {
        _isSadAboutGame = true;
        _spriteAnimationTimer?.Stop();
        _spriteAnimationTimer = null;
        ((Storyboard)FindResource("IdleAnimation")).Stop(this);
        ResetFrameLayers(_sadPandaSource);

        PandaRotate.BeginAnimation(
            System.Windows.Media.RotateTransform.AngleProperty,
            new DoubleAnimation(-1.5, 1.5, TimeSpan.FromSeconds(2.2))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });

        var detail = dailyLimitReached
            ? $"{DisplayName}, дневной лимит игры уже закончился, а игра всё ещё идёт. Сохранись и отойди от экрана хотя бы на две минуты."
            : $"{DisplayName}, после напоминания игра продолжается. Мне станет веселее, когда ты сделаешь настоящий двухминутный перерыв.";
        ShowSpeech(
            "Панда расстроилась 😟",
            detail,
            showActions: false,
            autoHide: TimeSpan.FromSeconds(25));
    }

    private void ShowHappyAfterBreak()
    {
        _isSadAboutGame = false;
        PandaRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
        PandaRotate.Angle = 0;
        ResetFrameLayers(_idlePandaSource);
        ((Storyboard)FindResource("IdleAnimation")).Begin(this, true);
        ShowSpeech(
            "Перерыв засчитан! 🌿",
            $"Спасибо, {DisplayName}! Две минуты вне игры прошли — я снова в хорошем настроении.",
            showActions: false,
            autoHide: TimeSpan.FromSeconds(9));
    }

    private void CollapseToEdge()
    {
        if (_isCollapsed || _isExiting)
        {
            return;
        }

        _isCollapsed = true;
        HideSpeech();
        StopSpriteAnimation();
        ((Storyboard)FindResource("IdleAnimation")).Stop(this);
        PandaVisual.Visibility = Visibility.Collapsed;
        SettingsButton.Visibility = Visibility.Collapsed;
        CompactTab.Visibility = Visibility.Visible;

        var slide = new DoubleAnimation
        {
            To = SystemParameters.WorkArea.Right - 84,
            Duration = TimeSpan.FromMilliseconds(430),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
            FillBehavior = FillBehavior.Stop
        };
        slide.Completed += (_, _) => Left = SystemParameters.WorkArea.Right - 84;
        BeginAnimation(LeftProperty, slide);
    }

    private void ExpandFromEdge()
    {
        if (!_isCollapsed || _isExiting)
        {
            return;
        }

        _isCollapsed = false;
        var slide = new DoubleAnimation
        {
            To = _expandedLeft,
            Duration = TimeSpan.FromMilliseconds(500),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        slide.Completed += (_, _) =>
        {
            Left = _expandedLeft;
            CompactTab.Visibility = Visibility.Collapsed;
            PandaVisual.Visibility = Visibility.Visible;
            SettingsButton.Visibility = Visibility.Visible;
            if (_isSadAboutGame)
            {
                ResetFrameLayers(_sadPandaSource);
            }
            else
            {
                ((Storyboard)FindResource("IdleAnimation")).Begin(this, true);
            }
        };
        BeginAnimation(LeftProperty, slide);
    }

    private void CompactTab_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ExpandFromEdge();
        e.Handled = true;
    }

    private void Panda_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        PandaVisual.ContextMenu!.IsOpen = true;
        e.Handled = true;
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var parentAccess = new ParentAccessWindow(_settings, _settingsService)
        {
            Owner = this
        };

        if (parentAccess.ShowDialog() == true)
        {
            OpenSettings();
        }
    }

    private void HideSpeech_Click(object sender, RoutedEventArgs e) => HideSpeech();

    private void Exit_Click(object sender, RoutedEventArgs e) => BeginExitAnimation();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            _reminderScheduler.Stop();
            _gameTimeTracker.Stop();
            return;
        }

        e.Cancel = true;
        BeginExitAnimation();
    }

    private void BeginExitAnimation()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        SpeechCard.Visibility = Visibility.Collapsed;
        var slide = new DoubleAnimation
        {
            To = SystemParameters.WorkArea.Right + 12,
            Duration = TimeSpan.FromMilliseconds(420),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        slide.Completed += (_, _) =>
        {
            _allowClose = true;
            Close();
        };
        BeginAnimation(LeftProperty, slide);
    }

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();
}
