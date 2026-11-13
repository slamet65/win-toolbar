using System.Windows;
using WinToolbar.AppBar;
using WinToolbar.Startup;

namespace WinToolbar;

public partial class BarWindow : Window
{
    private const double BarHeight = 28;

    private readonly AppBarManager _appBar;

    public event EventHandler? HistoryRequested;

    public BarWindow()
    {
        InitializeComponent();
        Width = SystemParameters.PrimaryScreenWidth;
        Height = BarHeight;

        _appBar = new AppBarManager(this, BarHeight);
        QuotaMeter.HistoryRequested += (_, _) => HistoryRequested?.Invoke(this, EventArgs.Empty);
        ContextMenu!.Opened += (_, _) => AutoStartItem.IsChecked = AutoStart.IsEnabled;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _appBar.Register();
    }

    protected override void OnClosed(EventArgs e)
    {
        _appBar.Dispose();
        base.OnClosed(e);
    }

    private void OnHistoryClick(object sender, RoutedEventArgs e) =>
        HistoryRequested?.Invoke(this, EventArgs.Empty);

    private void OnAutoStartClick(object sender, RoutedEventArgs e) =>
        AutoStart.Set(AutoStartItem.IsChecked);

    private void OnExitClick(object sender, RoutedEventArgs e) =>
        Application.Current.Shutdown();
}
