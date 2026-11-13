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

    public void Update(Session? session, double speedDown, double speedUp, bool ssidIsFallback)
    {
        if (session is null)
        {
            SsidText.Text = "Tidak ada WiFi";
            UsagePanel.Visibility = Visibility.Collapsed;
            ToolTip = "Klik untuk melihat riwayat sesi";
            return;
        }

        SsidText.Text = session.Ssid;
        SpeedDownText.Text = Formatting.Speed(speedDown);
        SpeedUpText.Text = Formatting.Speed(speedUp);
        DownText.Text = Formatting.Bytes(session.BytesDown);
        UpText.Text = Formatting.Bytes(session.BytesUp);
        TotalText.Text = Formatting.Bytes(session.BytesTotal);
        DurationText.Text = Formatting.Duration(session.Duration);
        UsagePanel.Visibility = Visibility.Visible;
        ToolTip = ssidIsFallback
            ? "Nama WiFi diambil dari profil jaringan karena izin Lokasi Windows mati.\n" +
              "Aktifkan di Settings > Privacy & security > Location agar nama WiFi akurat.\n" +
              "Klik untuk melihat riwayat sesi."
            : "Kecepatan saat ini, lalu pemakaian sejak tersambung ke WiFi ini.\nKlik untuk melihat riwayat sesi.";
    }

    private void OnClick(object sender, MouseButtonEventArgs e) =>
        HistoryRequested?.Invoke(this, EventArgs.Empty);
}
