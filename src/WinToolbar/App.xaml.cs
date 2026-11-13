using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using WinToolbar.Data;
using WinToolbar.Network;
using WinToolbar.Views;

namespace WinToolbar;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private SessionStore? _store;
    private UsageTracker? _tracker;
    private WifiMonitor? _monitor;
    private DispatcherTimer? _timer;
    private BarWindow? _bar;
    private HistoryWindow? _history;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(initiallyOwned: true, @"Local\WinToolbar.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        _store = new SessionStore(SessionStore.DefaultPath);
        _store.CloseDanglingSessions();
        _tracker = new UsageTracker(_store, () => DateTime.UtcNow);
        _monitor = new WifiMonitor();

        _bar = new BarWindow();
        _bar.HistoryRequested += (_, _) => ShowHistory();
        _bar.Show();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Tick();

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SessionEnding += (_, _) => _tracker?.EndSession();
    }

    private void Tick()
    {
        if (_tracker is null || _monitor is null || _bar is null) return;
        _tracker.Process(_monitor.Read());
        _bar.QuotaMeter.Update(_tracker.Current, _tracker.SpeedDown, _tracker.SpeedUp, _monitor.SsidIsFallback);
    }

    // Sleep mengakhiri sesi; saat bangun, tick berikutnya membuka sesi baru.
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.Mode == PowerModes.Suspend)
            {
                _timer?.Stop();
                _tracker?.EndSession();
            }
            else if (e.Mode == PowerModes.Resume)
            {
                _timer?.Start();
            }
        });
    }

    private void ShowHistory()
    {
        if (_store is null) return;
        _tracker?.Flush();

        _history?.Close();
        _history = new HistoryWindow(_store);
        _history.Closed += (_, _) => _history = null;
        _history.Show();
        _history.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _timer?.Stop();
        _tracker?.EndSession();
        _monitor?.Dispose();
        _bar?.Close();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
