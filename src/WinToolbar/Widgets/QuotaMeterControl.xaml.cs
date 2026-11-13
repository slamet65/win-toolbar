using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinToolbar.Network;

namespace WinToolbar.Widgets;

public partial class QuotaMeterControl : UserControl
{
    public event EventHandler? HistoryRequested;

    public QuotaMeterControl()
    {
        InitializeComponent();
    }

    public void Update(Session? session, bool ssidIsFallback)
    {
        if (session is null)
        {
            SsidText.Text = "Tidak ada WiFi";
            UsagePanel.Visibility = Visibility.Collapsed;
            ToolTip = "Klik untuk melihat riwayat sesi";
            return;
        }

        SsidText.Text = session.Ssid;
        DownText.Text = Formatting.Bytes(session.BytesDown);
        UpText.Text = Formatting.Bytes(session.BytesUp);
        TotalText.Text = Formatting.Bytes(session.BytesTotal);
        DurationText.Text = Formatting.Duration(session.Duration);
        UsagePanel.Visibility = Visibility.Visible;
        ToolTip = ssidIsFallback
            ? "Nama WiFi diambil dari profil jaringan karena izin Lokasi Windows mati.\n" +
              "Aktifkan di Settings > Privacy & security > Location agar nama WiFi akurat.\n" +
              "Klik untuk melihat riwayat sesi."
            : "Pemakaian sejak tersambung ke WiFi ini. Klik untuk melihat riwayat sesi.";
    }

    private void OnClick(object sender, MouseButtonEventArgs e) =>
        HistoryRequested?.Invoke(this, EventArgs.Empty);
}
