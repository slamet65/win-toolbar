namespace WinToolbar.Network;

/// <summary>Satu pembacaan adapter WiFi: nama jaringan dan counter byte kumulatif adapter.</summary>
public sealed record WifiSample(string Ssid, long BytesDown, long BytesUp);

public sealed class Session
{
    public long Id { get; set; }
    public required string Ssid { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? EndedAt { get; set; }
    public DateTime LastUpdated { get; set; }
    public long BytesDown { get; set; }
    public long BytesUp { get; set; }

    public long BytesTotal => BytesDown + BytesUp;
    public TimeSpan Duration => (EndedAt ?? LastUpdated) - StartedAt;
}

public interface ISessionStore
{
    long Insert(Session session);
    void Update(Session session);
}

/// <summary>
/// Mengubah deretan pembacaan adapter menjadi sesi pemakaian.
/// Sesi baru dimulai tiap kali tersambung ke sebuah WiFi dan berakhir saat putus atau pindah WiFi.
/// </summary>
public sealed class UsageTracker(ISessionStore store, Func<DateTime> now)
{
    public static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(15);

    private long _lastDown;
    private long _lastUp;
    private DateTime _lastFlush;

    public Session? Current { get; private set; }

    /// <param name="sample">null berarti tidak ada WiFi yang tersambung.</param>
    public void Process(WifiSample? sample)
    {
        if (sample is null)
        {
            EndSession();
            return;
        }

        var time = now();
        if (Current is null || Current.Ssid != sample.Ssid)
        {
            EndSession();
            Current = new Session { Ssid = sample.Ssid, StartedAt = time, LastUpdated = time };
            Current.Id = store.Insert(Current);
            _lastFlush = time;
        }
        else
        {
            Current.BytesDown += Delta(sample.BytesDown, _lastDown);
            Current.BytesUp += Delta(sample.BytesUp, _lastUp);
            Current.LastUpdated = time;
            if (time - _lastFlush >= FlushInterval)
                Flush();
        }

        _lastDown = sample.BytesDown;
        _lastUp = sample.BytesUp;
    }

    public void Flush()
    {
        if (Current is null) return;
        store.Update(Current);
        _lastFlush = now();
    }

    public void EndSession()
    {
        if (Current is null) return;
        var time = now();
        Current.LastUpdated = time;
        Current.EndedAt = time;
        store.Update(Current);
        Current = null;
    }

    // Counter adapter bisa kembali ke nol (driver reset, adapter dimatikan lalu dinyalakan).
    // Dalam kasus itu nilai sekarang adalah pemakaian sejak reset.
    private static long Delta(long current, long previous) =>
        current >= previous ? current - previous : current;
}
