using WinToolbar.Network;

namespace WinToolbar.Tests;

public class UsageTrackerTests
{
    private sealed class FakeStore : ISessionStore
    {
        public List<Session> Inserted { get; } = [];
        public int Updates { get; private set; }

        public long Insert(Session session)
        {
            Inserted.Add(session);
            return Inserted.Count;
        }

        public void Update(Session session) => Updates++;
    }

    private readonly FakeStore _store = new();
    private DateTime _now = new(2026, 11, 13, 8, 0, 0, DateTimeKind.Utc);
    private readonly UsageTracker _tracker;

    public UsageTrackerTests()
    {
        _tracker = new UsageTracker(_store, () => _now);
    }

    private void Tick(WifiSample? sample, int seconds = 1)
    {
        _now = _now.AddSeconds(seconds);
        _tracker.Process(sample);
    }

    [Fact]
    public void First_sample_starts_session_at_zero()
    {
        Tick(new WifiSample("Rumah", 5_000_000, 1_000_000));

        Assert.NotNull(_tracker.Current);
        Assert.Equal("Rumah", _tracker.Current.Ssid);
        Assert.Equal(0, _tracker.Current.BytesTotal);
        Assert.Single(_store.Inserted);
    }

    [Fact]
    public void Deltas_accumulate()
    {
        Tick(new WifiSample("Rumah", 1000, 100));
        Tick(new WifiSample("Rumah", 1500, 150));
        Tick(new WifiSample("Rumah", 4000, 400));

        Assert.Equal(3000, _tracker.Current!.BytesDown);
        Assert.Equal(300, _tracker.Current.BytesUp);
        Assert.Equal(TimeSpan.FromSeconds(2), _tracker.Current.Duration);
    }

    [Fact]
    public void Counter_reset_counts_from_zero_instead_of_going_negative()
    {
        Tick(new WifiSample("Rumah", 1000, 100));
        Tick(new WifiSample("Rumah", 2000, 200));
        Tick(new WifiSample("Rumah", 300, 30));

        Assert.Equal(1300, _tracker.Current!.BytesDown);
        Assert.Equal(130, _tracker.Current.BytesUp);
    }

    [Fact]
    public void Ssid_change_closes_session_and_starts_new_one()
    {
        Tick(new WifiSample("Rumah", 1000, 0));
        Tick(new WifiSample("Rumah", 3000, 0));
        var first = _tracker.Current!;

        Tick(new WifiSample("Hotspot HP", 9000, 0));

        Assert.NotNull(first.EndedAt);
        Assert.Equal(2000, first.BytesDown);
        Assert.Equal("Hotspot HP", _tracker.Current!.Ssid);
        Assert.Equal(0, _tracker.Current.BytesDown);
        Assert.Equal(2, _store.Inserted.Count);
    }

    [Fact]
    public void Disconnect_ends_session_and_reconnect_starts_fresh()
    {
        Tick(new WifiSample("Rumah", 1000, 0));
        Tick(new WifiSample("Rumah", 2000, 0));
        var first = _tracker.Current!;

        Tick(null);
        Assert.Null(_tracker.Current);
        Assert.Equal(_now, first.EndedAt);

        Tick(new WifiSample("Rumah", 2500, 0));
        Assert.NotSame(first, _tracker.Current);
        Assert.Equal(0, _tracker.Current!.BytesDown);
    }

    [Fact]
    public void Speed_is_bytes_per_second_between_the_last_two_samples()
    {
        Tick(new WifiSample("Rumah", 1000, 100));
        Assert.Equal(0, _tracker.SpeedDown);

        Tick(new WifiSample("Rumah", 5000, 500), seconds: 2);

        Assert.Equal(2000, _tracker.SpeedDown);
        Assert.Equal(200, _tracker.SpeedUp);
    }

    [Fact]
    public void Speed_resets_when_wifi_disconnects_or_changes()
    {
        Tick(new WifiSample("Rumah", 0, 0));
        Tick(new WifiSample("Rumah", 9000, 900));

        Tick(new WifiSample("Hotspot HP", 50_000, 5000));
        Assert.Equal(0, _tracker.SpeedDown);

        Tick(new WifiSample("Hotspot HP", 51_000, 5100));
        Tick(null);
        Assert.Equal(0, _tracker.SpeedDown);
        Assert.Equal(0, _tracker.SpeedUp);
    }

    [Fact]
    public void Active_session_is_saved_periodically()
    {
        Tick(new WifiSample("Rumah", 0, 0));
        for (var i = 1; i <= 14; i++) Tick(new WifiSample("Rumah", i * 10, 0));
        Assert.Equal(0, _store.Updates);

        Tick(new WifiSample("Rumah", 150, 0));
        Assert.Equal(1, _store.Updates);
    }
}
