using System.Windows;
using WinToolbar.Data;

namespace WinToolbar.Views;

public partial class HistoryWindow : Window
{
    private sealed record TotalRow(string Ssid, int Sessions, string Down, string Up, string Total);
    private sealed record SessionRow(string Ssid, string Started, string Duration, string Down, string Up, string Total);

    public HistoryWindow(SessionStore store)
    {
        InitializeComponent();

        TotalsList.ItemsSource = store.GetTotalsBySsid()
            .Select(t => new TotalRow(t.Ssid, t.SessionCount,
                Formatting.Bytes(t.BytesDown), Formatting.Bytes(t.BytesUp), Formatting.Bytes(t.BytesTotal)))
            .ToList();

        SessionsList.ItemsSource = store.GetSessions()
            .Select(s => new SessionRow(
                s.Ssid,
                s.StartedAt.ToLocalTime().ToString("dd MMM yyyy HH:mm"),
                Formatting.Duration(s.Duration) + (s.EndedAt is null ? " (aktif)" : ""),
                Formatting.Bytes(s.BytesDown), Formatting.Bytes(s.BytesUp), Formatting.Bytes(s.BytesTotal)))
            .ToList();
    }
}
