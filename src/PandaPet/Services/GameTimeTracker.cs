using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using PandaPet.Models;

namespace PandaPet.Services;

public sealed class GameTimeTracker
{
    private const int BreakGraceSeconds = 60;
    private const int RequiredBreakSeconds = 2 * 60;
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly DispatcherTimer _timer;
    private bool _limitRaisedToday;
    private int _sessionGameSeconds;
    private int _inactiveSeconds;
    private int _nextBreakAtSeconds;
    private bool _breakRequired;
    private bool _breakIgnoredRaised;
    private bool _breakIsDailyLimit;
    private int _activeSecondsAfterBreakDue;
    private int _breakInactiveSeconds;

    public GameTimeTracker(AppSettings settings, SettingsService settingsService)
    {
        _settings = settings;
        _settingsService = settingsService;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += (_, _) => TrackForegroundApplication();
        ResetForNewDayIfNeeded();
        ResetBreakCountdown();
    }

    public event Action<int>? GameLimitReached;
    public event Action<int>? GameBreakDue;
    public event Action<int, bool>? GameBreakIgnored;
    public event Action? GameBreakTaken;

    public void Start() => _timer.Start();

    public void Stop()
    {
        _timer.Stop();
        _settingsService.Save(_settings);
    }

    public void Refresh()
    {
        _limitRaisedToday = _settings.GameUsageSeconds >= _settings.GameDailyLimitMinutes * 60;
        ResetBreakCountdown();
    }

    private void TrackForegroundApplication()
    {
        ResetForNewDayIfNeeded();

        var processName = GetForegroundProcessName();
        if (processName is null || !IsConfiguredGame(processName))
        {
            _inactiveSeconds += (int)_timer.Interval.TotalSeconds;
            if (_breakRequired)
            {
                _breakInactiveSeconds += (int)_timer.Interval.TotalSeconds;
                if (_breakInactiveSeconds >= RequiredBreakSeconds)
                {
                    CompleteBreak();
                }
            }

            if (_inactiveSeconds >= 5 * 60)
            {
                _sessionGameSeconds = 0;
                _inactiveSeconds = 0;
                ResetBreakCountdown();
            }
            return;
        }

        _inactiveSeconds = 0;
        _breakInactiveSeconds = 0;
        _sessionGameSeconds += (int)_timer.Interval.TotalSeconds;
        _settings.GameUsageSeconds += (int)_timer.Interval.TotalSeconds;
        if (_settings.GameUsageSeconds % 60 == 0)
        {
            _settingsService.Save(_settings);
        }

        var limitSeconds = Math.Max(1, _settings.GameDailyLimitMinutes) * 60;
        if (_sessionGameSeconds >= _nextBreakAtSeconds)
        {
            RequireBreak(isDailyLimit: false);
            GameBreakDue?.Invoke(Math.Max(5, _settings.GameBreakIntervalMinutes));
            _nextBreakAtSeconds += Math.Max(5, _settings.GameBreakIntervalMinutes) * 60;
        }

        if (!_limitRaisedToday && _settings.GameUsageSeconds >= limitSeconds)
        {
            _limitRaisedToday = true;
            RequireBreak(isDailyLimit: true);
            GameLimitReached?.Invoke(_settings.GameDailyLimitMinutes);
        }

        if (_limitRaisedToday && !_breakRequired)
        {
            RequireBreak(isDailyLimit: true);
        }

        if (_breakRequired)
        {
            _activeSecondsAfterBreakDue += (int)_timer.Interval.TotalSeconds;
            if (!_breakIgnoredRaised && _activeSecondsAfterBreakDue >= BreakGraceSeconds)
            {
                _breakIgnoredRaised = true;
                GameBreakIgnored?.Invoke(
                    Math.Max(1, _settings.GameUsageSeconds / 60),
                    _breakIsDailyLimit);
            }
        }
    }

    private void RequireBreak(bool isDailyLimit)
    {
        if (!_breakRequired)
        {
            _breakRequired = true;
            _breakIgnoredRaised = false;
            _activeSecondsAfterBreakDue = 0;
            _breakInactiveSeconds = 0;
        }

        _breakIsDailyLimit |= isDailyLimit;
    }

    private void CompleteBreak()
    {
        _breakRequired = false;
        _breakIgnoredRaised = false;
        _breakIsDailyLimit = false;
        _activeSecondsAfterBreakDue = 0;
        _breakInactiveSeconds = 0;
        _sessionGameSeconds = 0;
        ResetBreakCountdown();
        GameBreakTaken?.Invoke();
    }

    private void ResetBreakCountdown()
    {
        var intervalSeconds = Math.Max(5, _settings.GameBreakIntervalMinutes) * 60;
        _nextBreakAtSeconds = ((_sessionGameSeconds / intervalSeconds) + 1) * intervalSeconds;
    }

    private bool IsConfiguredGame(string processName)
    {
        return _settings.GameProcessNames.Any(configured =>
            string.Equals(
                Path.GetFileNameWithoutExtension(configured.Trim()),
                processName,
                StringComparison.OrdinalIgnoreCase));
    }

    private void ResetForNewDayIfNeeded()
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        if (string.Equals(_settings.GameUsageDate, today, StringComparison.Ordinal))
        {
            return;
        }

        _settings.GameUsageDate = today;
        _settings.GameUsageSeconds = 0;
        _limitRaisedToday = false;
        _breakRequired = false;
        _breakIgnoredRaised = false;
        _breakIsDailyLimit = false;
        _activeSecondsAfterBreakDue = 0;
        _breakInactiveSeconds = 0;
        _settingsService.Save(_settings);
    }

    private static string? GetForegroundProcessName()
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        GetWindowThreadProcessId(handle, out var processId);
        if (processId == 0)
        {
            return null;
        }

        try
        {
            return Process.GetProcessById((int)processId).ProcessName;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
