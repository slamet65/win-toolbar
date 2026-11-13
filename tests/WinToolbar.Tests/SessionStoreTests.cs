using System.Globalization;
using WinToolbar.Data;
using WinToolbar.Network;

namespace WinToolbar.Tests;

public sealed class SessionStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"wintoolbar-test-{Guid.NewGuid():N}.db");
    private readonly SessionStore _store;
    private static readonly DateTime Start = new(2026, 11, 13, 8, 0, 0, DateTimeKind.Utc);

    public SessionStoreTests()
    {
        _store = new SessionStore(_path);
    }

    public void Dispose() => File.Delete(_path);

    private Session Add(string ssid, DateTime start, long down, long up, bool ended)
    {
        var session = new Session { Ssid = ssid, StartedAt = start, LastUpdated = start };
        session.Id = _store.Insert(session);
        session.BytesDown = down;
        session.BytesUp = up;
        session.LastUpdated = start.AddMinutes(10);
        if (ended) session.EndedAt = session.LastUpdated;
        _store.Update(session);
        return session;
    }

    [Fact]
    public void Round_trips_a_session()
    {
        Add("Rumah", Start, 1234, 56, ended: true);

        var loaded = Assert.Single(_store.GetSessions());
        Assert.Equal("Rumah", loaded.Ssid);
        Assert.Equal(Start, loaded.StartedAt);
        Assert.Equal(Start.AddMinutes(10), loaded.EndedAt);
        Assert.Equal(1234, loaded.BytesDown);
        Assert.Equal(56, loaded.BytesUp);
    }

    [Fact]
    public void Sessions_are_listed_newest_first()
    {
        Add("Lama", Start, 1, 1, ended: true);
        Add("Baru", Start.AddHours(1), 1, 1, ended: true);

        Assert.Equal(["Baru", "Lama"], _store.GetSessions().Select(s => s.Ssid));
    }

    [Fact]
    public void Dangling_sessions_are_closed_at_their_last_update()
    {
        Add("Rumah", Start, 100, 10, ended: false);

        _store.CloseDanglingSessions();

        var loaded = Assert.Single(_store.GetSessions());
        Assert.Equal(Start.AddMinutes(10), loaded.EndedAt);
    }

    [Fact]
    public void Totals_are_grouped_by_ssid_largest_first()
    {
        Add("Rumah", Start, 100, 10, ended: true);
        Add("Hotspot", Start.AddHours(1), 5000, 500, ended: true);
        Add("Rumah", Start.AddHours(2), 200, 20, ended: true);

        var totals = _store.GetTotalsBySsid();

        Assert.Equal(2, totals.Count);
        Assert.Equal(new SsidTotal("Hotspot", 1, 5000, 500), totals[0]);
        Assert.Equal(new SsidTotal("Rumah", 2, 300, 30), totals[1]);
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1536, "2 KB")]
    [InlineData(1_572_864, "1.50 MB")]
    [InlineData(52_428_800, "50.0 MB")]
    [InlineData(140_509_184, "134 MB")]
    [InlineData(1_342_177_280, "1.25 GB")]
    public void Formats_bytes(long bytes, string expected)
    {
        Assert.Equal(expected, Formatting.Bytes(bytes, CultureInfo.InvariantCulture));
    }
}
