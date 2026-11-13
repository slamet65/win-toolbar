using System.Net.NetworkInformation;
using ManagedNativeWifi;

namespace WinToolbar.Network;

/// <summary>Membaca adapter WiFi yang sedang tersambung: nama jaringan dan counter byte.</summary>
public sealed class WifiMonitor : IDisposable
{
    private static readonly TimeSpan SsidRefresh = TimeSpan.FromSeconds(10);
    private static readonly Guid NetworkListManagerClsid = new("DCB00C01-570F-4A9B-8D69-199FDBA5723B");

    private string? _ssid;
    private string? _nicId;
    private DateTime _resolvedAt;
    private volatile bool _networkChanged = true;

    /// <summary>
    /// true bila nama jaringan tidak bisa dibaca dari WLAN API (biasanya izin Lokasi Windows mati)
    /// sehingga yang dipakai adalah nama profil jaringan.
    /// </summary>
    public bool SsidIsFallback { get; private set; }

    public WifiMonitor()
    {
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
    }

    public WifiSample? Read()
    {
        NetworkInterface? nic;
        try
        {
            nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(IsConnectedWifi);
        }
        catch (NetworkInformationException)
        {
            return null;
        }

        if (nic is null)
        {
            _ssid = null;
            return null;
        }

        var now = DateTime.UtcNow;
        if (_ssid is null || _nicId != nic.Id || _networkChanged || now - _resolvedAt > SsidRefresh)
        {
            _networkChanged = false;
            _ssid = ResolveSsid(nic);
            _nicId = nic.Id;
            _resolvedAt = now;
        }

        try
        {
            var stats = nic.GetIPStatistics();
            return new WifiSample(_ssid, stats.BytesReceived, stats.BytesSent);
        }
        catch (NetworkInformationException)
        {
            return null;
        }
    }

    // Adapter virtual (Wi-Fi Direct, hotspot) juga bertipe Wireless80211; yang punya gateway
    // adalah adapter yang benar-benar tersambung ke sebuah jaringan.
    private static bool IsConnectedWifi(NetworkInterface nic)
    {
        if (nic.NetworkInterfaceType != NetworkInterfaceType.Wireless80211) return false;
        if (nic.OperationalStatus != OperationalStatus.Up) return false;
        try
        {
            return nic.GetIPProperties().GatewayAddresses.Count > 0;
        }
        catch (NetworkInformationException)
        {
            return false;
        }
    }

    private string ResolveSsid(NetworkInterface nic)
    {
        try
        {
            var ssid = NativeWifi.EnumerateConnectedNetworkSsids().FirstOrDefault()?.ToString();
            if (!string.IsNullOrWhiteSpace(ssid))
            {
                SsidIsFallback = false;
                return ssid;
            }
        }
        catch (Exception)
        {
            // Akses WLAN API ditolak (izin Lokasi mati) atau layanan WLAN tidak jalan.
        }

        SsidIsFallback = true;
        return ProfileName(nic) ?? nic.Name;
    }

    // Nama profil jaringan dari Network List Manager; biasanya sama dengan SSID
    // (bisa berakhiran " 2" bila Windows pernah menganggapnya jaringan berbeda).
    private static string? ProfileName(NetworkInterface nic)
    {
        try
        {
            if (!Guid.TryParse(nic.Id, out var adapterId)) return null;
            var type = Type.GetTypeFromCLSID(NetworkListManagerClsid);
            if (type is null) return null;

            dynamic manager = Activator.CreateInstance(type)!;
            foreach (dynamic connection in manager.GetNetworkConnections())
            {
                if ((Guid)connection.GetAdapterId() != adapterId) continue;
                string name = connection.GetNetwork().GetName();
                return string.IsNullOrWhiteSpace(name) ? null : name;
            }
        }
        catch (Exception)
        {
            // COM tidak tersedia; pakai nama adapter.
        }
        return null;
    }

    private void OnNetworkChanged(object? sender, EventArgs e) => _networkChanged = true;

    public void Dispose()
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
    }
}
