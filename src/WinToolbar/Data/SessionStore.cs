using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using WinToolbar.Network;

namespace WinToolbar.Data;

public sealed record SsidTotal(string Ssid, int SessionCount, long BytesDown, long BytesUp)
{
    public long BytesTotal => BytesDown + BytesUp;
}

public sealed class SessionStore : ISessionStore
{
    private readonly string _connectionString;

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinToolbar", "data.db");

    public SessionStore(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString();
        Execute("""
            CREATE TABLE IF NOT EXISTS sessions (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                ssid         TEXT    NOT NULL,
                started_at   TEXT    NOT NULL,
                ended_at     TEXT,
                last_updated TEXT    NOT NULL,
                bytes_down   INTEGER NOT NULL DEFAULT 0,
                bytes_up     INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS ix_sessions_started ON sessions(started_at);
            """);
    }

    public long Insert(Session session)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sessions (ssid, started_at, ended_at, last_updated, bytes_down, bytes_up)
            VALUES ($ssid, $started, $ended, $updated, $down, $up);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$ssid", session.Ssid);
        cmd.Parameters.AddWithValue("$started", ToText(session.StartedAt));
        AddMutable(cmd, session);
        return (long)cmd.ExecuteScalar()!;
    }

    public void Update(Session session)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE sessions
            SET ended_at = $ended, last_updated = $updated, bytes_down = $down, bytes_up = $up
            WHERE id = $id;
            """;
        cmd.Parameters.AddWithValue("$id", session.Id);
        AddMutable(cmd, session);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Menutup sesi yang masih terbuka dari run sebelumnya (laptop mati mendadak, aplikasi crash).</summary>
    public void CloseDanglingSessions() =>
        Execute("UPDATE sessions SET ended_at = last_updated WHERE ended_at IS NULL;");

    public List<Session> GetSessions(int limit = 500)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, ssid, started_at, ended_at, last_updated, bytes_down, bytes_up
            FROM sessions ORDER BY started_at DESC, id DESC LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$limit", limit);

        var result = new List<Session>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Session
            {
                Id = reader.GetInt64(0),
                Ssid = reader.GetString(1),
                StartedAt = FromText(reader.GetString(2)),
                EndedAt = reader.IsDBNull(3) ? null : FromText(reader.GetString(3)),
                LastUpdated = FromText(reader.GetString(4)),
                BytesDown = reader.GetInt64(5),
                BytesUp = reader.GetInt64(6),
            });
        }
        return result;
    }

    public List<SsidTotal> GetTotalsBySsid()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT ssid, COUNT(*), SUM(bytes_down), SUM(bytes_up)
            FROM sessions GROUP BY ssid ORDER BY SUM(bytes_down) + SUM(bytes_up) DESC;
            """;

        var result = new List<SsidTotal>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(new SsidTotal(reader.GetString(0), reader.GetInt32(1), reader.GetInt64(2), reader.GetInt64(3)));
        return result;
    }

    private static void AddMutable(SqliteCommand cmd, Session session)
    {
        cmd.Parameters.AddWithValue("$ended", session.EndedAt is { } ended ? ToText(ended) : DBNull.Value);
        cmd.Parameters.AddWithValue("$updated", ToText(session.LastUpdated));
        cmd.Parameters.AddWithValue("$down", session.BytesDown);
        cmd.Parameters.AddWithValue("$up", session.BytesUp);
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private void Execute(string sql)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static string ToText(DateTime value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTime FromText(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
