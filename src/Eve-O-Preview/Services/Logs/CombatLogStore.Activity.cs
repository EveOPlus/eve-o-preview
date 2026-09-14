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
    private DateTimeOffset ActivitySince;
    private DateTimeOffset? _damageSince, _repairSince, _jumpsSince;
    private Dictionary<string, CharacterActivityStats>? _activityCache;

    private void InitializeActivity(int version, DateTimeOffset now)
    {
        using var transaction = _database.BeginTransaction();
        Execute("""
            CREATE TABLE IF NOT EXISTS activity_totals (
                source TEXT NOT NULL,generation INTEGER NOT NULL,character TEXT NOT NULL COLLATE NOCASE,
                direction INTEGER NOT NULL,kind INTEGER NOT NULL,effect INTEGER NOT NULL,
                hits INTEGER NOT NULL,amount REAL NOT NULL,largest REAL NOT NULL,
                PRIMARY KEY(source,generation,character,direction,kind,effect));
            CREATE TABLE IF NOT EXISTS encounters (
                source TEXT NOT NULL,generation INTEGER NOT NULL,character TEXT NOT NULL COLLATE NOCASE,
                direction INTEGER NOT NULL,kind INTEGER NOT NULL,name TEXT NOT NULL COLLATE NOCASE,
                PRIMARY KEY(source,generation,character,direction,kind,name));
            CREATE TABLE IF NOT EXISTS system_visits (
                source TEXT NOT NULL,generation INTEGER NOT NULL,offset INTEGER NOT NULL,
                character TEXT NOT NULL COLLATE NOCASE,timestamp INTEGER NOT NULL,name TEXT NOT NULL,system INTEGER NOT NULL,
                PRIMARY KEY(source,generation,offset));
            CREATE INDEX IF NOT EXISTS visits_character_time ON system_visits(character,timestamp);
            CREATE TABLE IF NOT EXISTS activity_offsets (source TEXT NOT NULL,generation INTEGER NOT NULL,offset INTEGER NOT NULL,
                PRIMARY KEY(source,generation));
            """);
        if (version < 2)
        {
            // Old damage totals can outlive their entries. Backfill only the retained
            // interval and expose its start separately; never invent lost hit/target counts.
            object? earliest = Scalar("SELECT MIN(timestamp) FROM entries");
            bool oldTotals = Convert.ToInt64(Scalar("SELECT COUNT(*) FROM totals"), CultureInfo.InvariantCulture) > 0;
            ActivitySince = earliest is long time ? DateTimeOffset.FromUnixTimeMilliseconds(time)
                : oldTotals ? now : Since;
            if (ActivitySince < Since) ActivitySince = Since;
            Execute("INSERT OR REPLACE INTO metadata VALUES ('activity_since',$since)", ("$since", ActivitySince.ToString("O")));
            var retained = new List<(string Source, long Generation, long Offset, ParsedLogEntry Entry)>();
            using (var command = Command("SELECT source,generation,offset,json FROM entries ORDER BY timestamp,id"))
            using (var reader = command.ExecuteReader())
                while (reader.Read()) retained.Add((reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2),
                    JsonSerializer.Deserialize<ParsedLogEntry>(reader.GetString(3))!));
            foreach (var entry in retained)
            {
                RecordActivity(entry.Source, entry.Generation, entry.Entry);
                RecordVisit(entry.Source, entry.Generation, entry.Offset, entry.Entry);
            }
            Execute("""
                INSERT OR IGNORE INTO activity_offsets SELECT source,generation,MAX(offset) FROM entries GROUP BY source,generation;
                INSERT INTO activity_offsets SELECT identity,json_extract(cursor,'$.Generation'),json_extract(cursor,'$.Offset')-1 FROM sources WHERE true
                ON CONFLICT(source,generation) DO UPDATE SET offset=MAX(activity_offsets.offset,excluded.offset);
                """);
            SeedVisitBaselines();
            Execute("PRAGMA user_version=2");
        }
        else ActivitySince = DateTimeOffset.Parse((string)Scalar("SELECT value FROM metadata WHERE key='activity_since'")!, CultureInfo.InvariantCulture);
        transaction.Commit();
    }

    private void SeedVisitBaselines(string? character = null) => Execute("""
        INSERT OR IGNORE INTO system_visits(source,generation,offset,character,timestamp,name,system)
        SELECT source,-1,-1,character,timestamp,name,system FROM locations WHERE $character IS NULL OR character=$character;
        """, ("$character", (object?)character ?? DBNull.Value));

    private void RecordVisit(string source, long generation, long offset, ParsedLogEntry entry)
    {
        if (entry.SolarSystem is null || entry.SolarSystemId is not long system) return;
        // Retain one pre-measurement observation per source. Later visits are small,
        // durable evidence so delayed files, rollover and source retraction can be
        // ordered correctly without confusing discovery order with travel order.
        bool baseline = entry.Timestamp < CharacterCutoff(entry.Character, CombatResetScope.Jumps, _jumpsSince ?? ActivitySince);
        Execute("""
            INSERT INTO system_visits VALUES ($source,$generation,$offset,$character,$time,$name,$system)
            ON CONFLICT(source,generation,offset) DO UPDATE SET timestamp=excluded.timestamp,name=excluded.name,system=excluded.system
            WHERE excluded.timestamp>system_visits.timestamp;
            """, ("$source", source), ("$generation", baseline ? -1 : generation), ("$offset", baseline ? -1 : offset),
            ("$character", entry.Character), ("$time", entry.Timestamp.ToUnixTimeMilliseconds()), ("$name", entry.SolarSystem), ("$system", system));
    }

    private void RecordActivity(string source, long generation, ParsedLogEntry entry)
    {
        if (entry.Timestamp < ActivitySince || entry.Direction is null || entry.Amount <= 0) return;
        int kind = entry.Kind == CombatantKind.Npc ? 1 : 2;
        Execute("""
            INSERT INTO activity_totals VALUES ($source,$generation,$character,$direction,$kind,$effect,1,$amount,$amount)
            ON CONFLICT(source,generation,character,direction,kind,effect) DO UPDATE SET
                hits=activity_totals.hits+1,amount=activity_totals.amount+excluded.amount,largest=MAX(activity_totals.largest,excluded.largest);
            """, ("$source", source), ("$generation", generation), ("$character", entry.Character),
            ("$direction", (int)entry.Direction.Value), ("$kind", kind), ("$effect", (int)entry.Effect), ("$amount", entry.Amount));
        if (entry.Effect == CombatEffect.Damage && !string.IsNullOrWhiteSpace(entry.Counterparty))
            Execute("INSERT OR IGNORE INTO encounters VALUES ($source,$generation,$character,$direction,$kind,$name)",
                ("$source", source), ("$generation", generation), ("$character", entry.Character),
                ("$direction", (int)entry.Direction.Value), ("$kind", kind), ("$name", entry.Counterparty.Trim()));
    }

    private Dictionary<string, CharacterActivityStats> ReadActivity()
    {
        if (_activityCache is not null) return _activityCache;
        var values = new Dictionary<string, ActivityCounters>(StringComparer.OrdinalIgnoreCase);
        ActivityCounters Get(string name)
        { if (!values.TryGetValue(name, out var value)) values[name] = value = new(); return value; }
        using (var command = Command("SELECT character,direction,kind,effect,SUM(hits),SUM(amount),MAX(largest) FROM activity_totals GROUP BY character,direction,kind,effect"))
        using (var reader = command.ExecuteReader())
            while (reader.Read())
            {
                var value = Get(reader.GetString(0)); int direction = reader.GetInt32(1), kind = reader.GetInt32(2), effect = reader.GetInt32(3);
                if (effect == (int)CombatEffect.Damage)
                { value.Hits[kind, direction] = reader.GetDouble(4); value.Largest[kind, direction] = reader.GetDouble(6); }
                else if (effect is >= 1 and <= 3)
                { value.Repairs[effect, direction] += reader.GetDouble(5); value.RepairCount[effect, direction] += reader.GetDouble(4); }
            }
        using (var command = Command("SELECT character,direction,kind,COUNT(DISTINCT name) FROM encounters GROUP BY character,direction,kind"))
        using (var reader = command.ExecuteReader())
            while (reader.Read()) Get(reader.GetString(0)).Unique[reader.GetInt32(2), reader.GetInt32(1)] = reader.GetInt64(3);
        using (var command = Command("SELECT character,direction,kind,json_extract(state,'$.Damage'),json_extract(state,'$.Seconds') FROM dps_streams"))
        using (var reader = command.ExecuteReader())
            while (reader.Read())
            {
                var value = Get(reader.GetString(0)); int direction = reader.GetInt32(1), kind = reader.GetInt32(2);
                double seconds = reader.GetDouble(4);
                value.DpsSeconds[kind, direction] = seconds;
                value.AverageDps[kind, direction] = seconds > 0 ? reader.GetDouble(3) / seconds : 0;
            }
        // Same-system reconnects/duplicate Local files are one observation. A first
        // known system is not a jump. Conflicting systems in the same log second
        // are omitted rather than assigning an invented ordering.
        using (var command = Command("""
            WITH cutoffs AS (
                SELECT character,MAX($since,MAX(since)) AS cutoff FROM character_resets
                WHERE scope IN (0,3) GROUP BY character
            ), observations AS (
                SELECT v.character,v.timestamp,MIN(v.system) AS system,COALESCE(c.cutoff,$since) AS cutoff
                FROM system_visits v LEFT JOIN cutoffs c ON c.character=v.character
                GROUP BY v.character,v.timestamp HAVING COUNT(DISTINCT v.system)=1
            ), baseline AS (
                SELECT *,ROW_NUMBER() OVER (PARTITION BY character ORDER BY timestamp DESC) AS rank
                FROM observations WHERE timestamp<cutoff
            ), period AS (
                SELECT character,timestamp,system,cutoff FROM observations WHERE timestamp>=cutoff
                UNION ALL SELECT character,timestamp,system,cutoff FROM baseline WHERE rank=1
            ), ordered AS (
                SELECT *,LAG(system) OVER (PARTITION BY character ORDER BY timestamp) AS previous FROM period
            ) SELECT character,COUNT(DISTINCT system),SUM(CASE WHEN previous IS NOT NULL AND previous<>system AND timestamp>=cutoff THEN 1 ELSE 0 END)
              FROM ordered GROUP BY character;
            """, ("$since", (_jumpsSince ?? ActivitySince).ToUnixTimeMilliseconds())))
        using (var reader = command.ExecuteReader())
            while (reader.Read()) { var value = Get(reader.GetString(0)); value.Systems = reader.GetInt64(1); value.Jumps = reader.GetInt64(2); }
        return _activityCache = values.ToDictionary(x => x.Key, x => new CharacterActivityStats(CharacterCutoff(x.Key, CombatResetScope.All, ActivitySince), x.Value.Jumps, x.Value.Systems,
            Enum.GetValues<CombatantKind>().Select(kind => new CombatActivityCategory(kind,
                new(x.Value.Hits[(int)kind, 0], x.Value.Hits[(int)kind, 1]), new(x.Value.Largest[(int)kind, 0], x.Value.Largest[(int)kind, 1]),
                x.Value.Unique[(int)kind, 1], x.Value.Unique[(int)kind, 0])
                { AverageDps = new(x.Value.AverageDps[(int)kind, 0], x.Value.AverageDps[(int)kind, 1]),
                    DpsSampleSeconds = new(x.Value.DpsSeconds[(int)kind, 0], x.Value.DpsSeconds[(int)kind, 1]) }).ToArray(),
            Enum.GetValues<CombatEffect>().Where(effect => effect != CombatEffect.Damage).Select(effect => new RepairActivity(effect,
                new(x.Value.Repairs[(int)effect, 0], x.Value.Repairs[(int)effect, 1]),
                new(x.Value.RepairCount[(int)effect, 0], x.Value.RepairCount[(int)effect, 1]))).ToArray())
                { AverageDpsSince = CharacterCutoff(x.Key, CombatResetScope.Damage, _dpsSince) }, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ActivityCounters
    {
        public long Jumps, Systems;
        public double[,] Hits = new double[3, 2], Largest = new double[3, 2], Repairs = new double[4, 2], RepairCount = new double[4, 2];
        public double[,] AverageDps = new double[3, 2], DpsSeconds = new double[3, 2];
        public long[,] Unique = new long[3, 2];
    }
}
