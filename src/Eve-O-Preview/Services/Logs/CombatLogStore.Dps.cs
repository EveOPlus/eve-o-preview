#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using EveOPreview.UI;

namespace EveOPreview.Services.Logs;

public sealed partial class CombatLogStore
{
    private DateTimeOffset _dpsSince;
    private const long DpsEdgeMilliseconds = 10_000;
    // A firing gap is not automatically idle: artillery and other slow weapons
    // regularly exceed the ten-second edge exclusion. Logs do not expose fitted
    // cycle times, so use a separate, conservative inactivity threshold.
    private const long DpsIdleMilliseconds = 60_000;
    private readonly record struct DpsStream(string Character, int Direction, int Kind);
    private sealed record DpsPoint(long Timestamp, double Amount);

    // Keep a short pending tail: it cannot enter the average until another hit
    // establishes that it was at least ten seconds before the end of combat.
    // Damage/Seconds are lifetime, time-weighted sums of accepted middle sections.
    private sealed class DpsAccumulator
    {
        public long? First { get; set; }
        public long Last { get; set; }
        public long? Previous { get; set; }
        public double Damage { get; set; }
        public double Seconds { get; set; }
        public List<DpsPoint> Pending { get; set; } = [];

        public void Add(DpsPoint point)
        {
            if (First is null || point.Timestamp - Last > DpsIdleMilliseconds)
            {
                First = Last = point.Timestamp;
                Previous = null;
                Pending.Clear(); // The previous run's final ten seconds never qualify.
            }
            long start = First.Value + DpsEdgeMilliseconds;
            Last = point.Timestamp;
            if (Pending.Count > 0 && Pending[^1].Timestamp == point.Timestamp)
                Pending[^1] = point with { Amount = Pending[^1].Amount + point.Amount };
            else Pending.Add(point);
            int accepted = 0;
            foreach (var sample in Pending)
            {
                if (sample.Timestamp > Last - DpsEdgeMilliseconds) break;
                // Use complete inter-hit intervals inside the valid middle. A
                // partial interval between slow volleys would produce a spurious
                // zero or inflate the rate depending on where the edge landed.
                if (Previous is long previous && previous >= start && sample.Timestamp > previous)
                {
                    Damage += sample.Amount;
                    Seconds += (sample.Timestamp - previous) / 1000d;
                }
                Previous = sample.Timestamp;
                accepted++;
            }
            if (accepted > 0) Pending.RemoveRange(0, accepted);
        }
    }

    private void InitializeDps(int version, DateTimeOffset now)
    {
        using var transaction = _database.BeginTransaction();
        Execute("""
            CREATE TABLE IF NOT EXISTS dps_damage (
                source TEXT NOT NULL,generation INTEGER NOT NULL,character TEXT NOT NULL COLLATE NOCASE,
                direction INTEGER NOT NULL,kind INTEGER NOT NULL,timestamp INTEGER NOT NULL,amount REAL NOT NULL,
                PRIMARY KEY(source,generation,character,direction,kind,timestamp));
            CREATE INDEX IF NOT EXISTS dps_damage_stream ON dps_damage(character,direction,kind,timestamp);
            CREATE TABLE IF NOT EXISTS dps_streams (
                character TEXT NOT NULL COLLATE NOCASE,direction INTEGER NOT NULL,kind INTEGER NOT NULL,state TEXT NOT NULL,
                PRIMARY KEY(character,direction,kind));
            """);
        if (version < 3)
        {
            // Old totals may outlive retained entries. Rebuild only available
            // timestamp evidence and expose its own measurement start.
            Execute("DELETE FROM dps_damage; DELETE FROM dps_streams;");
            Execute("""
                INSERT INTO dps_damage
                SELECT source,generation,character,direction,CASE WHEN kind=1 THEN 1 ELSE 2 END,timestamp,SUM(amount)
                FROM entries WHERE direction IS NOT NULL AND amount>0 AND timestamp>=$since
                GROUP BY source,generation,character,direction,CASE WHEN kind=1 THEN 1 ELSE 2 END,timestamp;
                """, ("$since", (_damageSince ?? Since).ToUnixTimeMilliseconds()));
            var earliest = Scalar("SELECT MIN(timestamp) FROM dps_damage");
            _dpsSince = earliest is long time ? DateTimeOffset.FromUnixTimeMilliseconds(time)
                : version == 0 ? Since : now;
            Execute("INSERT OR REPLACE INTO metadata VALUES ('dps_since',$since)", ("$since", _dpsSince.ToString("O")));
            foreach (var stream in DpsStreams("SELECT DISTINCT character,direction,kind FROM dps_damage"))
                RebuildDps(stream);
            Execute("PRAGMA user_version=3");
        }
        else _dpsSince = DateTimeOffset.Parse((string)Scalar("SELECT value FROM metadata WHERE key='dps_since'")!, CultureInfo.InvariantCulture);
        transaction.Commit();
    }

    private List<DpsStream> DpsStreams(string sql, params (string Key, object Value)[] parameters)
    {
        var streams = new List<DpsStream>();
        using var command = Command(sql, parameters); using var reader = command.ExecuteReader();
        while (reader.Read()) streams.Add(new(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2)));
        return streams;
    }

    private void RecordDps(string source, long generation, ParsedLogEntry entry, Dictionary<DpsStream, List<DpsPoint>> changed)
    {
        if (entry.Effect != CombatEffect.Damage || entry.Direction is null || entry.Amount <= 0
            || entry.Timestamp < CharacterCutoff(entry.Character, CombatResetScope.Damage, _dpsSince)) return;
        var stream = new DpsStream(entry.Character.ToUpperInvariant(), (int)entry.Direction.Value, entry.Kind == CombatantKind.Npc ? 1 : 2);
        var point = new DpsPoint(entry.Timestamp.ToUnixTimeMilliseconds(), entry.Amount);
        Execute("""
            INSERT INTO dps_damage VALUES ($source,$generation,$character,$direction,$kind,$time,$amount)
            ON CONFLICT(source,generation,character,direction,kind,timestamp) DO UPDATE SET amount=dps_damage.amount+excluded.amount;
            """, ("$source", source), ("$generation", generation), ("$character", stream.Character), ("$direction", stream.Direction),
            ("$kind", stream.Kind), ("$time", point.Timestamp), ("$amount", point.Amount));
        if (!changed.TryGetValue(stream, out var points)) changed[stream] = points = [];
        points.Add(point);
    }

    private void UpdateDps(Dictionary<DpsStream, List<DpsPoint>> changed)
    {
        foreach (var (stream, points) in changed)
        {
            string? saved = ScalarWithParameters("SELECT state FROM dps_streams WHERE character=$character AND direction=$direction AND kind=$kind",
                ("$character", stream.Character), ("$direction", stream.Direction), ("$kind", stream.Kind)) as string;
            var state = saved is null ? new DpsAccumulator() : JsonSerializer.Deserialize<DpsAccumulator>(saved)!;
            // Files can arrive out of order or overlap across rotation. Recompute
            // only this stream when chronology changes; normal live commits append
            // to the short tail and never rescan historical samples.
            if (state.First is not null && points.Any(x => x.Timestamp < state.Last)) RebuildDps(stream);
            else
            {
                foreach (var point in points.OrderBy(x => x.Timestamp)) state.Add(point);
                SaveDps(stream, state);
            }
        }
    }

    private void RebuildDps(DpsStream stream)
    {
        var state = new DpsAccumulator();
        using (var command = Command("""
            SELECT timestamp,SUM(amount) FROM dps_damage
            WHERE character=$character AND direction=$direction AND kind=$kind GROUP BY timestamp ORDER BY timestamp;
            """, ("$character", stream.Character), ("$direction", stream.Direction), ("$kind", stream.Kind)))
        using (var reader = command.ExecuteReader())
            while (reader.Read()) state.Add(new(reader.GetInt64(0), reader.GetDouble(1)));
        SaveDps(stream, state);
    }

    private void SaveDps(DpsStream stream, DpsAccumulator state) => Execute("""
        INSERT INTO dps_streams VALUES ($character,$direction,$kind,$state)
        ON CONFLICT(character,direction,kind) DO UPDATE SET state=excluded.state;
        """, ("$character", stream.Character), ("$direction", stream.Direction), ("$kind", stream.Kind), ("$state", JsonSerializer.Serialize(state)));

    private void RetractDps(string source)
    {
        var affected = DpsStreams("SELECT DISTINCT character,direction,kind FROM dps_damage WHERE source=$source", ("$source", source));
        Execute("DELETE FROM dps_damage WHERE source=$source", ("$source", source));
        foreach (var stream in affected) RebuildDps(stream);
    }
}
