#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using EveOPreview.UI;
using Microsoft.Data.Sqlite;

namespace EveOPreview.Services.StaticData;

public sealed record StaticCombatItem(long Id, bool Npc, WeaponPlatform Platform, DamageTypes Damage,
    long MissileId = 0, bool HasTurret = false, bool HasMissile = false, int Build = 0);

/// <summary>Full SDE records, compressed in small indexed blocks for random access. No server,
/// extracted directory tree, or whole-export in-memory representation is required.</summary>
public sealed partial class StaticDataDatabase : IDisposable
{
    private readonly SqliteConnection _db;
    public int Build { get; }
    public string FilePath { get; }
    public StaticDataDatabase(string path)
    {
        FilePath = path;
        UpgradeCombatIndex(path);
        _db = Open(path, SqliteOpenMode.ReadOnly);
        try
        {
            using var command = _db.CreateCommand();
            command.CommandText = "SELECT build FROM manifest WHERE schema=2";
            Build = Convert.ToInt32(command.ExecuteScalar() ?? throw new InvalidDataException("Incomplete static data."), CultureInfo.InvariantCulture);
        }
        catch { _db.Dispose(); throw; }
    }
    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = false }.ToString());
        try
        {
            db.Open(); using var command = db.CreateCommand();
            command.CommandText = "PRAGMA cache_size=-4096; PRAGMA mmap_size=0;"; command.ExecuteNonQuery(); return db;
        }
        catch { db.Dispose(); throw; }
    }
    public JsonDocument? ReadRecord(string dataset, string key) => ReadRecord(_db, dataset, key);
    private static JsonDocument? ReadRecord(SqliteConnection db, string dataset, string key)
    {
        using var command = db.CreateCommand(); command.CommandText = "SELECT b.data,r.offset,r.length FROM records r JOIN blocks b ON b.id=r.block WHERE r.dataset=$dataset AND r.key=$key";
        command.Parameters.AddWithValue("$dataset", dataset); command.Parameters.AddWithValue("$key", key);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        byte[] block = Decode((byte[])reader[0]);
        return JsonDocument.Parse(block.AsMemory(reader.GetInt32(1), reader.GetInt32(2)));
    }
    private static byte[] Decode(byte[] bytes)
    {
        using var input = new MemoryStream(bytes, false); using var zip = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream(); zip.CopyTo(output); return output.ToArray();
    }
    private static byte[] Encode(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var zip = new BrotliStream(output, CompressionLevel.Optimal, true)) zip.Write(bytes);
        return output.ToArray();
    }
    public StaticCombatItem? FindItem(string name)
    {
        using var command = _db.CreateCommand();
        command.CommandText = """
            SELECT c.id,c.npc,c.platform,c.damage,c.missile,c.turret,c.launcher FROM names n
            JOIN combat c ON c.id=n.id WHERE n.name=$name
            """;
        command.Parameters.AddWithValue("$name", name.ToUpperInvariant());
        using var reader = command.ExecuteReader();
        StaticCombatItem? item = null;
        while (reader.Read())
        {
            var next = new StaticCombatItem(reader.GetInt64(0), reader.GetBoolean(1), (WeaponPlatform)reader.GetInt32(2),
                (DamageTypes)reader.GetInt32(3), reader.GetInt64(4), reader.GetBoolean(5), reader.GetBoolean(6));
            if (item is null) item = next;
            else
            {
                // Localised aliases can identify multiple types. Only keep facts on
                // which every match agrees; never silently pick the last type ID.
                item = item with { Id = 0, Npc = item.Npc && next.Npc,
                    Platform = item.Platform == next.Platform ? item.Platform : WeaponPlatform.Unknown,
                    Damage = item.Damage == next.Damage ? item.Damage : DamageTypes.None,
                    MissileId = item.MissileId == next.MissileId ? item.MissileId : 0,
                    HasTurret = item.HasTurret || next.HasTurret, HasMissile = item.HasMissile && next.HasMissile };
            }
        }
        return item is null ? null : item with { Build = Build };
    }
    public static (int Datasets, long Records) Import(string archive, string destination, int build,
        CancellationToken cancellation, Action<double, FormattableString>? progress = null)
    {
        using var source = ZipFile.OpenRead(archive);
        var files = source.Entries.Where(x => x.FullName.EndsWith(".jsonl", StringComparison.Ordinal)).ToArray();
        if (files.Length == 0 || files.Sum(x => x.Length) > 8L * 1024 * 1024 * 1024)
            throw new InvalidDataException("Invalid static export size.");
        using var db = Open(destination, SqliteOpenMode.ReadWriteCreate);
        using var schema = db.CreateCommand();
        schema.CommandText = """
            PRAGMA journal_mode=DELETE; PRAGMA synchronous=NORMAL;
            CREATE TABLE records(dataset TEXT NOT NULL,key TEXT NOT NULL,block INTEGER NOT NULL,offset INTEGER NOT NULL,length INTEGER NOT NULL,PRIMARY KEY(dataset,key)) WITHOUT ROWID;
            CREATE TABLE blocks(id INTEGER PRIMARY KEY,dataset TEXT NOT NULL,data BLOB NOT NULL);
            CREATE INDEX blocks_dataset ON blocks(dataset);
            CREATE TABLE names(name TEXT NOT NULL,id INTEGER NOT NULL,PRIMARY KEY(name,id)) WITHOUT ROWID;
            CREATE TABLE combat(id INTEGER PRIMARY KEY,npc INTEGER,platform INTEGER,damage INTEGER,missile INTEGER,turret INTEGER,launcher INTEGER);
            CREATE TABLE manifest(schema INTEGER,build INTEGER,datasets INTEGER,records INTEGER);
            """; schema.ExecuteNonQuery();
        long records = 0, processed = 0, total = files.Sum(x => x.Length);
        using var transaction = db.BeginTransaction();
        using var insert = db.CreateCommand(); insert.Transaction = transaction;
        insert.CommandText = "INSERT INTO records VALUES($dataset,$key,$block,$offset,$length)";
        var dataset = insert.Parameters.Add("$dataset", SqliteType.Text); var key = insert.Parameters.Add("$key", SqliteType.Text);
        var blockId = insert.Parameters.Add("$block", SqliteType.Integer);
        var offset = insert.Parameters.Add("$offset", SqliteType.Integer); var length = insert.Parameters.Add("$length", SqliteType.Integer); insert.Prepare();
        using var blockInsert = db.CreateCommand(); blockInsert.Transaction = transaction;
        blockInsert.CommandText = "INSERT INTO blocks VALUES($id,$dataset,$data)";
        blockInsert.Parameters.Add("$id", SqliteType.Integer); blockInsert.Parameters.Add("$dataset", SqliteType.Text);
        blockInsert.Parameters.Add("$data", SqliteType.Blob); blockInsert.Prepare();
        long nextBlock = 0;
        using var buffer = new MemoryStream();
        var positions = new List<(string Key, int Offset, int Length)>();
        void FlushBlock()
        {
            if (positions.Count == 0) return;
            blockInsert.Parameters["$id"].Value = ++nextBlock; blockInsert.Parameters["$dataset"].Value = dataset.Value;
            blockInsert.Parameters["$data"].Value = Encode(buffer.ToArray()); blockInsert.ExecuteNonQuery();
            blockId.Value = nextBlock;
            foreach (var position in positions)
            { key.Value = position.Key; offset.Value = position.Offset; length.Value = position.Length; insert.ExecuteNonQuery(); }
            positions.Clear(); buffer.SetLength(0);
        }
        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();
            // Names are database keys, never extraction paths. Reject nested/unexpected
            // members rather than writing anything chosen by an archive entry.
            if (file.Name != file.FullName) throw new InvalidDataException("Unexpected static export member.");
            dataset.Value = Path.GetFileNameWithoutExtension(file.Name);
            using var stream = file.Open(); using var reader = new StreamReader(stream, Encoding.UTF8, true);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                cancellation.ThrowIfCancellationRequested();
                if (line.Length > 16 * 1024 * 1024) throw new InvalidDataException("Oversized static record.");
                if (string.IsNullOrWhiteSpace(line)) continue;
                using var json = JsonDocument.Parse(line); var entry = json.RootElement;
                byte[] bytes = Encoding.UTF8.GetBytes(line);
                if (buffer.Length + bytes.Length > 65536) FlushBlock();
                positions.Add((entry.GetProperty("_key").ToString(), (int)buffer.Length, bytes.Length));
                buffer.Write(bytes); buffer.WriteByte((byte)'\n'); records++;
            }
            FlushBlock();
            processed += file.Length; progress?.Invoke(0.8 * processed / Math.Max(1, total), $"Importing {dataset.Value}");
        }
        foreach (string required in new[] { "types", "groups", "typeDogma", "dogmaAttributes", "_sde" })
        {
            using var check = db.CreateCommand(); check.Transaction = transaction;
            check.CommandText = "SELECT COUNT(*) FROM records WHERE dataset=$name"; check.Parameters.AddWithValue("$name", required);
            if ((long)check.ExecuteScalar()! == 0) throw new InvalidDataException("Required static dataset is missing: " + required);
        }
        using (var meta = ReadRecord(db, "_sde", "sde"))
            if (meta is null || meta.RootElement.GetProperty("buildNumber").GetInt32() != build)
                throw new InvalidDataException("Static export build does not match its download.");
        progress?.Invoke(0.82, $"Indexing item names and damage attributes");
        BuildCombatIndex(db, transaction, cancellation);
        WriteCombatIndexVersion(db, transaction);
        using var finish = db.CreateCommand(); finish.Transaction = transaction;
        finish.CommandText = "INSERT INTO manifest VALUES(2,$build,$datasets,$records)";
        finish.Parameters.AddWithValue("$build", build); finish.Parameters.AddWithValue("$datasets", files.Length); finish.Parameters.AddWithValue("$records", records);
        finish.ExecuteNonQuery(); cancellation.ThrowIfCancellationRequested(); transaction.Commit();
        return (files.Length, records);
    }
    private static void BuildCombatIndex(SqliteConnection db, SqliteTransaction transaction, CancellationToken cancellation)
    {
        var groups = new Dictionary<int, (int Category, string Name)>();
        foreach (var record in ReadDataset(db, "groups"))
            using (record) groups[record.RootElement.GetProperty("_key").GetInt32()] =
                (record.RootElement.GetProperty("categoryID").GetInt32(), EnglishName(record.RootElement));
        var dogma = new Dictionary<long, (DamageTypes Damage, long Missile, long TurretId, bool Turret, bool Launcher)>();
        var itemEffects = new Dictionary<long, HashSet<int>>();
        var fighterDamage = new Dictionary<long, DamageTypes>();
        foreach (var record in ReadDataset(db, "typeDogma"))
        {
            using (record)
            {
                cancellation.ThrowIfCancellationRequested(); var root = record.RootElement;
                DamageTypes damage = 0; long missile = 0, turretId = 0; bool turret = false, launcher = false;
                var values = new Dictionary<int, double>(); var effectIds = new HashSet<int>();
                if (root.TryGetProperty("dogmaAttributes", out var attributes))
                    foreach (var attr in attributes.EnumerateArray())
                    {
                        double value = attr.GetProperty("value").GetDouble();
                        values[attr.GetProperty("attributeID").GetInt32()] = value;
                        if (value <= 0) continue;
                        switch (attr.GetProperty("attributeID").GetInt32())
                        {
                            case 114: damage |= DamageTypes.EM; break; case 118: damage |= DamageTypes.Thermal; break;
                            case 117: damage |= DamageTypes.Kinetic; break; case 116: damage |= DamageTypes.Explosive; break;
                            case 507: missile = (long)value; break; case 245: turretId = (long)value; break;
                        }
                    }
                if (root.TryGetProperty("dogmaEffects", out var effects))
                    foreach (var effect in effects.EnumerateArray())
                    { int id = effect.GetProperty("effectID").GetInt32(); effectIds.Add(id); turret |= id == 10; launcher |= id == 569; }
                long typeId = root.GetProperty("_key").GetInt64();
                dogma[typeId] = (damage, missile, turretId, turret, launcher);
                itemEffects[typeId] = effectIds;
                // Bomb-launch abilities have a separate charge; a fighter label does
                // not tell us which attack fired. Leave those compositions unresolved.
                fighterDamage[typeId] = values.GetValueOrDefault(2324) > 0 ? DamageTypes.None : WeaponPlatformClassifier.FighterDamage(values);
            }
        }
        using var insert = db.CreateCommand(); insert.Transaction = transaction;
        insert.CommandText = "INSERT INTO combat VALUES($id,$npc,$platform,$damage,$missile,$turret,$launcher)";
        foreach (string name in new[] { "id", "npc", "platform", "damage", "missile", "turret", "launcher" }) insert.Parameters.Add("$" + name, SqliteType.Integer);
        insert.Prepare();
        using var alias = db.CreateCommand(); alias.Transaction = transaction;
        alias.CommandText = "INSERT OR IGNORE INTO names VALUES($name,$id)";
        alias.Parameters.Add("$name", SqliteType.Text); alias.Parameters.Add("$id", SqliteType.Integer); alias.Prepare();
        foreach (var record in ReadDataset(db, "types"))
        {
            using (record)
            {
                cancellation.ThrowIfCancellationRequested(); var root = record.RootElement;
                long id = root.GetProperty("_key").GetInt64(); int groupId = root.GetProperty("groupID").GetInt32(); var group = groups[groupId];
                var d = dogma.GetValueOrDefault(id);
                if (group.Category == 87) d.Damage = fighterDamage.GetValueOrDefault(id);
                var platform = WeaponPlatformClassifier.Classify(groupId, group.Category, group.Name, PlatformName(db, root), d.Damage, itemEffects.GetValueOrDefault(id) ?? []);
                bool npc = group.Category == 11;
                // Only a missile-only NPC can use its missile's attributes without a
                // named missile in the log. Mixed turret/missile NPCs use their guns.
                if (npc && !d.Turret && d.Launcher && d.Missile > 0)
                {
                    d.Damage = dogma.GetValueOrDefault(d.Missile).Damage; platform = WeaponPlatform.Missile;
                    using var missileType = ReadRecord(db, "types", d.Missile.ToString(CultureInfo.InvariantCulture));
                    if (missileType is not null)
                    {
                        int missileGroupId = missileType.RootElement.GetProperty("groupID").GetInt32(); var missileGroup = groups[missileGroupId];
                        platform = WeaponPlatformClassifier.Classify(missileGroupId, missileGroup.Category, missileGroup.Name, EnglishName(missileType.RootElement), d.Damage, itemEffects.GetValueOrDefault(d.Missile) ?? []);
                    }
                }
                else if (npc && d.TurretId > 0)
                {
                    using var gun = ReadRecord(db, "types", d.TurretId.ToString(CultureInfo.InvariantCulture));
                    if (gun is not null && groups.TryGetValue(gun.RootElement.GetProperty("groupID").GetInt32(), out var gunGroup))
                        platform = WeaponPlatformClassifier.Classify(gun.RootElement.GetProperty("groupID").GetInt32(), gunGroup.Category, gunGroup.Name, PlatformName(db, gun.RootElement), dogma.GetValueOrDefault(d.TurretId).Damage, itemEffects.GetValueOrDefault(d.TurretId) ?? []);
                }
                // NPC superweapon attributes are separate attacks; never union them
                // into a normal gun hit just because they coexist on the NPC type.
                insert.Parameters["$id"].Value = id; insert.Parameters["$npc"].Value = npc;
                insert.Parameters["$platform"].Value = (int)platform; insert.Parameters["$damage"].Value = (int)d.Damage;
                insert.Parameters["$missile"].Value = d.Missile; insert.Parameters["$turret"].Value = d.Turret; insert.Parameters["$launcher"].Value = d.Launcher;
                insert.ExecuteNonQuery();
                foreach (var name in root.GetProperty("name").EnumerateObject())
                { alias.Parameters["$name"].Value = name.Value.GetString()!.ToUpperInvariant(); alias.Parameters["$id"].Value = id; alias.ExecuteNonQuery(); }
            }
        }
    }
    private static IEnumerable<JsonDocument> ReadDataset(SqliteConnection db, string dataset)
    {
        using var command = db.CreateCommand(); command.CommandText = "SELECT data FROM blocks WHERE dataset=$dataset ORDER BY id";
        command.Parameters.AddWithValue("$dataset", dataset); using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            using var input = new MemoryStream(Decode((byte[])reader[0]), false); using var lines = new StreamReader(input, Encoding.UTF8);
            while (lines.ReadLine() is { } line) yield return JsonDocument.Parse(line);
        }
    }
    private static string EnglishName(JsonElement value) => value.TryGetProperty("name", out var names) && names.TryGetProperty("en", out var name) ? name.GetString() ?? "" : "";
    private static string PlatformName(SqliteConnection db, JsonElement type)
    {
        string name = EnglishName(type); int group = (int)Number(type, "groupID");
        if (group is not (53 or 55 or 74 or 426 or 430 or 449)) return name;
        long parent = (long)Number(type, "variationParentTypeID");
        // Named/faction turrets can omit their family in the English label.
        // Follow only explicit same-group SDE ancestry, bounded against malformed cycles.
        for (int depth = 0; parent > 0 && depth < 8; depth++)
        {
            using var record = ReadRecord(db, "types", parent.ToString(CultureInfo.InvariantCulture));
            if (record is null || Number(record.RootElement, "groupID") != group) break;
            name = EnglishName(record.RootElement);
            long next = (long)Number(record.RootElement, "variationParentTypeID");
            if (next == parent) break;
            parent = next;
        }
        return name;
    }
    public void Dispose() => _db.Dispose();
}
