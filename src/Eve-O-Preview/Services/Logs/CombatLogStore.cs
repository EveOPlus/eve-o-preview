#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using EveOPreview.UI;
using Microsoft.Data.Sqlite;

namespace EveOPreview.Services.Logs;

public sealed record StoredLogFile(string Path, LogCursor Cursor, LogHeader Header);
public sealed record PositionedLogEntry(long Offset, ParsedLogEntry Entry);

/// <summary>One worker owns this connection. Entries, totals and offsets commit together.</summary>
public sealed partial class CombatLogStore : IDisposable
{
    private readonly SqliteConnection _database;
    public DateTimeOffset Since { get; private set; }
    public const int MaximumEntries = 200_000;

    public CombatLogStore(string path, DateTimeOffset now)
    {
        if (path != ":memory:") Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        _database = new(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, DefaultTimeout = 1 }.ToString());
        try
        {
            _database.Open();
            int version = Convert.ToInt32(Scalar("PRAGMA user_version;"), CultureInfo.InvariantCulture);
            if (version > 4)
                throw new InvalidDataException("The combat database was written by a newer version.");
            Execute("""
                PRAGMA journal_mode=WAL;
                PRAGMA synchronous=NORMAL;
                CREATE TABLE IF NOT EXISTS metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS sources (identity TEXT PRIMARY KEY, path TEXT NOT NULL, cursor TEXT NOT NULL, header TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS entries (
                    id INTEGER PRIMARY KEY, source TEXT NOT NULL, generation INTEGER NOT NULL, offset INTEGER NOT NULL,
                    timestamp INTEGER NOT NULL, character TEXT NOT NULL COLLATE NOCASE, category TEXT NOT NULL,
                    direction INTEGER, kind INTEGER NOT NULL, amount REAL NOT NULL, json TEXT NOT NULL,
                    UNIQUE(source,generation,offset));
                CREATE INDEX IF NOT EXISTS entries_time ON entries(timestamp);
                CREATE TABLE IF NOT EXISTS totals (
                    source TEXT NOT NULL, generation INTEGER NOT NULL, character TEXT NOT NULL COLLATE NOCASE,
                    direction INTEGER NOT NULL, kind INTEGER NOT NULL, amount REAL NOT NULL,
                    PRIMARY KEY(source,generation,character,direction,kind));
                CREATE TABLE IF NOT EXISTS locations (
                    source TEXT NOT NULL, character TEXT NOT NULL COLLATE NOCASE, timestamp INTEGER NOT NULL,
                    name TEXT NOT NULL, system INTEGER NOT NULL, PRIMARY KEY(source,character));
                """);
            var saved = Scalar("SELECT value FROM metadata WHERE key='since'") as string;
            Since = saved is null ? DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds()) : DateTimeOffset.Parse(saved, CultureInfo.InvariantCulture);
            if (saved is null) Execute("INSERT INTO metadata VALUES ('since',$value)", ("$value", Since.ToString("O")));
            InitializeActivity(version, now);
            DateTimeOffset? Cutoff(string key) => ScalarWithParameters("SELECT value FROM metadata WHERE key=$key", ("$key", key)) is string value
                ? DateTimeOffset.Parse(value, CultureInfo.InvariantCulture) : null;
            _damageSince = Cutoff("damage_since"); _repairSince = Cutoff("repairs_since"); _jumpsSince = Cutoff("jumps_since");
            InitializeDps(version, now);
            InitializeCharacterResets();
        }
        catch { _database.Dispose(); throw; }
    }

    public StoredLogFile? ReadFile(string identity)
    {
        using var command = Command("SELECT path,cursor,header FROM sources WHERE identity=$id", ("$id", identity));
        using var reader = command.ExecuteReader();
        return reader.Read() ? new(reader.GetString(0), JsonSerializer.Deserialize<LogCursor>(reader.GetString(1))!,
            JsonSerializer.Deserialize<LogHeader>(reader.GetString(2))!) : null;
    }

    /// <summary>Same schema and aggregation for temporary simulation, with no disk-backed test writes.</summary>
    public CombatLogStore CreateMemoryCopy()
    {
        var copy = new CombatLogStore(":memory:", Since);
        try
        {
            _database.BackupDatabase(copy._database); copy.ActivitySince = ActivitySince;
            copy._damageSince = _damageSince; copy._repairSince = _repairSince; copy._jumpsSince = _jumpsSince;
            copy._dpsSince = _dpsSince;
            foreach (var (character, resets) in _characterResets)
                foreach (var (scope, since) in resets) copy.RememberReset(character, scope, since);
            return copy;
        }
        catch { copy.Dispose(); throw; }
    }

    public IReadOnlyList<string> KnownPaths()
    {
        using var command = Command("SELECT path FROM sources");
        using var reader = command.ExecuteReader();
        var paths = new List<string>();
        while (reader.Read()) paths.Add(reader.GetString(0));
        return paths;
    }

    public void Commit(StoredLogFile file, IReadOnlyList<PositionedLogEntry> entries)
    {
        using var transaction = _database.BeginTransaction();
        try
        {
            var dpsChanges = new Dictionary<DpsStream, List<DpsPoint>>();
            long priorOffset = Convert.ToInt64(ScalarWithParameters("SELECT COALESCE(MAX(offset),-1) FROM activity_offsets WHERE source=$source AND generation=$generation",
                ("$source", file.Cursor.Identity), ("$generation", file.Cursor.Generation)), CultureInfo.InvariantCulture);
            if (file.Header.Ambiguous)
            {
                // A second listener makes attribution unsafe, including already-read
                // contributions. Per-source totals allow a complete retraction.
                Execute("DELETE FROM entries WHERE source=$id; DELETE FROM totals WHERE source=$id; DELETE FROM locations WHERE source=$id;", ("$id", file.Cursor.Identity));
                Execute("DELETE FROM activity_totals WHERE source=$id; DELETE FROM encounters WHERE source=$id; DELETE FROM system_visits WHERE source=$id;", ("$id", file.Cursor.Identity));
                RetractDps(file.Cursor.Identity);
            }
            else foreach (var positioned in entries)
            {
                // A committed high-water mark survives entry retention and reset.
                // Re-delivery of an old batch must not inflate durable statistics.
                if (positioned.Offset <= priorOffset) continue;
                var entry = positioned.Entry;
                long time = entry.Timestamp.ToUnixTimeMilliseconds();
                // Historical locations are useful at startup, but historical combat
                // must not silently inflate a newly started or reset measurement.
                if (entry.SolarSystem is not null)
                    Execute("""
                        INSERT INTO locations VALUES ($source,$character,$time,$name,$system)
                        ON CONFLICT(source,character) DO UPDATE SET timestamp=excluded.timestamp,name=excluded.name,system=excluded.system
                        WHERE excluded.timestamp >= locations.timestamp;
                        """, ("$source", file.Cursor.Identity), ("$character", entry.Character), ("$time", time),
                        ("$name", entry.SolarSystem), ("$system", entry.SolarSystemId!.Value));
                RecordVisit(file.Cursor.Identity, file.Cursor.Generation, positioned.Offset, entry);
                if (entry.Timestamp < CharacterCutoff(entry.Character, CombatResetScope.All, Since)) continue;
                if (entry.Direction.HasValue && entry.Timestamp < (entry.Effect == CombatEffect.Damage
                    ? CharacterCutoff(entry.Character, CombatResetScope.Damage, _damageSince ?? Since)
                    : CharacterCutoff(entry.Character, CombatResetScope.Repairs, _repairSince ?? Since))) continue;
                int inserted = Execute("""
                    INSERT OR IGNORE INTO entries(source,generation,offset,timestamp,character,category,direction,kind,amount,json)
                    VALUES($source,$generation,$offset,$time,$character,$category,$direction,$kind,$amount,$json)
                    """, ("$source", file.Cursor.Identity), ("$generation", file.Cursor.Generation), ("$offset", positioned.Offset),
                    ("$time", time), ("$character", entry.Character), ("$category", entry.Category),
                    ("$direction", entry.Direction is null || entry.Effect != CombatEffect.Damage ? DBNull.Value : (int)entry.Direction.Value),
                    ("$kind", (int)entry.Kind), ("$amount", entry.Amount), ("$json", JsonSerializer.Serialize(entry)));
                if (inserted > 0)
                {
                    RecordActivity(file.Cursor.Identity, file.Cursor.Generation, entry);
                    RecordDps(file.Cursor.Identity, file.Cursor.Generation, entry, dpsChanges);
                }
                if (inserted > 0 && entry.Direction.HasValue && entry.Effect == CombatEffect.Damage)
                    Execute("""
                        INSERT INTO totals VALUES ($source,$generation,$character,$direction,$kind,$amount)
                        ON CONFLICT(source,generation,character,direction,kind) DO UPDATE SET amount=totals.amount+excluded.amount
                        """, ("$source", file.Cursor.Identity), ("$generation", file.Cursor.Generation), ("$character", entry.Character),
                        ("$direction", (int)entry.Direction.Value), ("$kind", (int)entry.Kind), ("$amount", entry.Amount));
            }
            if (entries.Count > 0 && !file.Header.Ambiguous)
                Execute("""
                    INSERT INTO activity_offsets VALUES ($source,$generation,$offset)
                    ON CONFLICT(source,generation) DO UPDATE SET offset=MAX(activity_offsets.offset,excluded.offset)
                    """, ("$source", file.Cursor.Identity), ("$generation", file.Cursor.Generation), ("$offset", entries.Max(x => x.Offset)));
            UpdateDps(dpsChanges);
            Execute("""
                INSERT INTO sources VALUES ($id,$path,$cursor,$header)
                ON CONFLICT(identity) DO UPDATE SET path=excluded.path,cursor=excluded.cursor,header=excluded.header
                """, ("$id", file.Cursor.Identity), ("$path", file.Path), ("$cursor", JsonSerializer.Serialize(file.Cursor)),
                ("$header", JsonSerializer.Serialize(file.Header)));
            transaction.Commit();
            if (entries.Count > 0 || file.Header.Ambiguous) _activityCache = null;
        }
        catch { transaction.Rollback(); throw; }
    }

    public CombatLogSnapshot Snapshot(DateTimeOffset now, int windowSeconds, string status, string directory,
        IReadOnlyDictionary<string, long?> identities)
    {
        windowSeconds = Math.Clamp(windowSeconds, 1, 300);
        var values = new Dictionary<string, MutableCharacter>(StringComparer.OrdinalIgnoreCase);
        MutableCharacter Get(string name)
        {
            if (!values.TryGetValue(name, out var value)) values[name] = value = new(name);
            return value;
        }
        foreach (var identity in identities) Get(identity.Key);
        using (var command = Command("SELECT character,direction,kind,SUM(amount) FROM totals GROUP BY character,direction,kind"))
        using (var reader = command.ExecuteReader())
            // Older stores used kind=0 for unmatched labels. Keep every amount
            // under the current binary rule without replaying or resetting history.
            while (reader.Read()) Get(reader.GetString(0)).Total[reader.GetInt32(2) == 1 ? 1 : 2, reader.GetInt32(1)] += reader.GetDouble(3);
        using (var command = Command("""
            SELECT character,direction,kind,SUM(amount) FROM entries
            WHERE direction IS NOT NULL AND timestamp > $start AND timestamp <= $now GROUP BY character,direction,kind
            """, ("$start", now.AddSeconds(-windowSeconds).ToUnixTimeMilliseconds()), ("$now", now.ToUnixTimeMilliseconds())))
        using (var reader = command.ExecuteReader())
            while (reader.Read()) Get(reader.GetString(0)).Dps[reader.GetInt32(2) == 1 ? 1 : 2, reader.GetInt32(1)] += reader.GetDouble(3) / windowSeconds;
        // Repairs retain direction/type in JSON; their SQL direction is deliberately
        // null so they never enter damage totals. Read the entire indexed time window,
        // not the bounded recent-entry list, and reuse existing databases unchanged.
        using (var command = Command("""
            SELECT character,json_extract(json,'$.Effect'),json_extract(json,'$.Direction'),SUM(amount)
            FROM entries WHERE direction IS NULL AND amount>0 AND timestamp>$start AND timestamp<=$now
                AND json_extract(json,'$.Effect') IN (1,2,3) AND json_extract(json,'$.Direction') IN (0,1)
            GROUP BY character,json_extract(json,'$.Effect'),json_extract(json,'$.Direction')
            """, ("$start", now.AddSeconds(-windowSeconds).ToUnixTimeMilliseconds()), ("$now", now.ToUnixTimeMilliseconds())))
        using (var reader = command.ExecuteReader())
            while (reader.Read()) Get(reader.GetString(0)).RepairRates[reader.GetInt32(1), reader.GetInt32(2)] = reader.GetDouble(3) / windowSeconds;
        using (var command = Command("SELECT character,timestamp,name,system FROM locations ORDER BY timestamp"))
        using (var reader = command.ExecuteReader())
            while (reader.Read())
            {
                var value = Get(reader.GetString(0));
                value.LocationTime = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1));
                value.System = reader.GetString(2); value.SystemId = reader.GetInt64(3);
            }
        // One row per distinct composition, not per hit. Old JSON entries still
        // carry their single-type enum; missing evidence remains visibly unresolved.
        using (var command = Command("""
            SELECT DISTINCT character,COALESCE(json_extract(json,'$.DamageTypes'),0),COALESCE(json_extract(json,'$.DamageType'),0),kind
            FROM entries WHERE direction=0 AND amount>0 AND timestamp>$start AND timestamp<=$now
            """, ("$start", now.AddSeconds(-windowSeconds).ToUnixTimeMilliseconds()), ("$now", now.ToUnixTimeMilliseconds())))
        using (var reader = command.ExecuteReader())
            while (reader.Read())
            {
                var value = Get(reader.GetString(0));
                var types = (DamageTypes)reader.GetInt32(1);
                if (types == DamageTypes.None) types = new ParsedLogEntry(default, "", "", "", DamageType: (CombatDamageType)reader.GetInt32(2)).EffectiveDamageTypes;
                int kind = reader.GetInt32(3) == 1 ? 1 : 2;
                value.DamageTypes |= types; value.Unresolved |= types == DamageTypes.None;
                value.KindDamageTypes[kind] |= types; value.KindUnresolved[kind] |= types == DamageTypes.None;
            }
        using (var command = Command("SELECT character,MAX(timestamp) FROM entries GROUP BY character"))
        using (var reader = command.ExecuteReader())
            while (reader.Read()) Get(reader.GetString(0)).Last = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1));
        using (var command = Command("""
            SELECT character,MAX(timestamp),MAX(CASE WHEN kind<>1 THEN timestamp END) FROM entries
            WHERE direction=0 AND amount>0 AND timestamp > $before AND timestamp <= $now GROUP BY character
            """, ("$before", now.AddSeconds(-10).ToUnixTimeMilliseconds()), ("$now", now.ToUnixTimeMilliseconds())))
        using (var reader = command.ExecuteReader())
            while (reader.Read())
            {
                var value = Get(reader.GetString(0)); value.Incoming = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1));
                value.PlayerIncoming = reader.IsDBNull(2) ? null : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(2));
            }
        using (var command = Command("SELECT header,path FROM sources"))
        using (var reader = command.ExecuteReader())
            while (reader.Read())
            {
                var header = JsonSerializer.Deserialize<LogHeader>(reader.GetString(0))!;
                if (!header.Ambiguous && header.Listener is not null)
                {
                    var value = Get(header.Listener); value.Files++;
                    if (string.Equals(Path.GetFileName(Path.GetDirectoryName(reader.GetString(1))), "Gamelogs", StringComparison.OrdinalIgnoreCase)) value.GameFiles++;
                    else value.LocalFiles++;
                }
            }
        var recent = new List<ParsedLogEntry>();
        using (var command = Command("SELECT json FROM entries ORDER BY timestamp DESC,id DESC LIMIT 100"))
        using (var reader = command.ExecuteReader())
            while (reader.Read())
            {
                var entry = JsonSerializer.Deserialize<ParsedLogEntry>(reader.GetString(0))!;
                recent.Add(entry.Kind == CombatantKind.Npc ? entry : entry with { Kind = CombatantKind.Player });
            }
        long unrecognized = Convert.ToInt64(Scalar("SELECT COUNT(*) FROM entries WHERE category='combat' AND direction IS NULL"), CultureInfo.InvariantCulture);
        var activity = ReadActivity();
        return new(status, directory, Since, windowSeconds, values.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => new CharacterCombatSnapshot(x.Name, identities.GetValueOrDefault(x.Name), x.System, x.SystemId,
                x.LocationTime, x.Last, Enum.GetValues<CombatantKind>().Select(kind => new CombatCategory(kind,
                    new(x.Total[(int)kind, 0], x.Total[(int)kind, 1]), new(x.Dps[(int)kind, 0], x.Dps[(int)kind, 1]))
                    { IncomingDamageTypes = x.KindDamageTypes[(int)kind], HasUnresolvedIncomingDamage = x.KindUnresolved[(int)kind] }).ToArray(), x.Files, x.GameFiles, x.LocalFiles)
                    { Activity = activity.GetValueOrDefault(x.Name) ?? new(CharacterCutoff(x.Name, CombatResetScope.All, ActivitySince), 0, 0, [], [])
                        { AverageDpsSince = CharacterCutoff(x.Name, CombatResetScope.Damage, _dpsSince) },
                        RepairRates = new[] { CombatEffect.ShieldRepair, CombatEffect.ArmorRepair, CombatEffect.HullRepair }
                            .Select(effect => new RepairRate(effect, new(x.RepairRates[(int)effect, 0], x.RepairRates[(int)effect, 1]))).ToArray(),
                        LastIncomingDamageAt = x.Incoming, LastPlayerDamageAt = x.PlayerIncoming,
                        IncomingDamageTypes = x.DamageTypes, HasUnresolvedIncomingDamage = x.Unresolved }).ToArray(), recent, unrecognized);
    }

    public void Prune(DateTimeOffset now, int retentionDays)
    {
        Execute("DELETE FROM entries WHERE timestamp < $before", ("$before", now.AddDays(-retentionDays).ToUnixTimeMilliseconds()));
        Execute("DELETE FROM entries WHERE id IN (SELECT id FROM entries ORDER BY timestamp DESC,id DESC LIMIT -1 OFFSET $limit)", ("$limit", MaximumEntries));
    }

    /// <summary>Re-enrich only currently displayed damage after an SDE update.
    /// Amounts, classification, cursors and alert dispatch are deliberately unchanged.</summary>
    public void RefreshDamageMetadata(DateTimeOffset now, int windowSeconds, Func<ParsedLogEntry, ParsedLogEntry> enrich)
    {
        var entries = new List<(long Id, ParsedLogEntry Entry)>();
        using (var command = Command("SELECT id,json FROM entries WHERE direction IS NOT NULL AND timestamp>$start AND timestamp<=$now",
            ("$start", now.AddSeconds(-windowSeconds).ToUnixTimeMilliseconds()), ("$now", now.ToUnixTimeMilliseconds())))
        using (var reader = command.ExecuteReader())
            while (reader.Read()) entries.Add((reader.GetInt64(0), JsonSerializer.Deserialize<ParsedLogEntry>(reader.GetString(1))!));
        using var transaction = _database.BeginTransaction();
        foreach (var (id, entry) in entries)
        {
            var data = enrich(entry);
            var next = entry with { DamageType = data.DamageType, DamageTypes = data.DamageTypes, DamageEvidence = data.DamageEvidence,
                DamageSourceTypeId = data.DamageSourceTypeId, StaticDataBuild = data.StaticDataBuild, Platform = data.Platform, Weapon = data.Weapon };
            Execute("UPDATE entries SET json=$json WHERE id=$id", ("$json", JsonSerializer.Serialize(next)), ("$id", id));
        }
        transaction.Commit();
    }

    public void Reset(DateTimeOffset now, CombatResetScope scope = CombatResetScope.All, string? character = null)
    {
        if (character is not null) { ResetCharacter(now, scope, character); return; }
        if (!Enum.IsDefined(scope)) throw new ArgumentOutOfRangeException(nameof(scope));
        now = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds());
        using var transaction = _database.BeginTransaction();
        void Cutoff(string key) => Execute("INSERT OR REPLACE INTO metadata VALUES ($key,$since)", ("$key", key), ("$since", now.ToString("O")));
        if (scope is CombatResetScope.All)
        {
            Execute("DELETE FROM entries"); Cutoff("since"); Cutoff("activity_since");
            Execute("DELETE FROM character_resets");
        }
        if (scope is CombatResetScope.All or CombatResetScope.Damage)
        {
            Execute("DELETE FROM entries WHERE direction IS NOT NULL; DELETE FROM totals; DELETE FROM activity_totals WHERE effect=0; DELETE FROM encounters;");
            Execute("DELETE FROM dps_damage; DELETE FROM dps_streams;"); Cutoff("dps_since");
            Cutoff("damage_since");
        }
        if (scope is CombatResetScope.All or CombatResetScope.Repairs)
        {
            Execute("DELETE FROM entries WHERE json_extract(json,'$.Effect') IN (1,2,3); DELETE FROM activity_totals WHERE effect IN (1,2,3);");
            Cutoff("repairs_since");
        }
        if (scope is CombatResetScope.All or CombatResetScope.Jumps)
        {
            Execute("DELETE FROM system_visits;"); SeedVisitBaselines(); Cutoff("jumps_since");
        }
        transaction.Commit();
        if (scope is CombatResetScope.All) Since = ActivitySince = now;
        if (scope is CombatResetScope.All) _characterResets.Clear();
        if (scope is CombatResetScope.All or CombatResetScope.Damage) _damageSince = _dpsSince = now;
        if (scope is CombatResetScope.All or CombatResetScope.Repairs) _repairSince = now;
        if (scope is CombatResetScope.All or CombatResetScope.Jumps) _jumpsSince = now;
        _activityCache = null;
    }

    private sealed class MutableCharacter(string name)
    {
        public string Name = name;
        public double[,] Total = new double[3, 2], Dps = new double[3, 2];
        public double[,] RepairRates = new double[4, 2];
        public string? System;
        public long? SystemId;
        public DateTimeOffset? LocationTime, Last;
        public DateTimeOffset? Incoming, PlayerIncoming;
        public DamageTypes DamageTypes;
        public bool Unresolved;
        public DamageTypes[] KindDamageTypes = new DamageTypes[3];
        public bool[] KindUnresolved = new bool[3];
        public int Files, GameFiles, LocalFiles;
    }
    private SqliteCommand Command(string sql, params (string Key, object Value)[] parameters)
    {
        var command = _database.CreateCommand(); command.CommandText = sql;
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Key, parameter.Value);
        return command;
    }
    private int Execute(string sql, params (string Key, object Value)[] parameters)
    { using var command = Command(sql, parameters); return command.ExecuteNonQuery(); }
    private object? Scalar(string sql)
    { using var command = Command(sql); return command.ExecuteScalar(); }
    private object? ScalarWithParameters(string sql, params (string Key, object Value)[] parameters)
    { using var command = Command(sql, parameters); return command.ExecuteScalar(); }
    public void Dispose() => _database.Dispose();
}
