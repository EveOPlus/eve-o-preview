#nullable enable
using System;
using System.Threading;
using Microsoft.Data.Sqlite;

namespace EveOPreview.Services.StaticData;

public sealed partial class StaticDataDatabase
{
    // Derived locally from the full installed export, including every language.
    // Duplicate aliases are retained; lookup refuses ambiguous system identities.
    public long? FindSystem(string name)
    {
        using var command = _db.CreateCommand();
        command.CommandText = "SELECT id FROM system_names WHERE name=$name LIMIT 2";
        command.Parameters.AddWithValue("$name", name.ToUpperInvariant());
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        long id = reader.GetInt64(0);
        return reader.Read() ? null : id;
    }
    private static bool HasSystemNames(SqliteConnection db)
    {
        using var command = db.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='system_names'";
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }
    private static void UpgradeSystemNames(string path)
    {
        using (var check = Open(path, SqliteOpenMode.ReadOnly)) if (HasSystemNames(check)) return;
        using var db = Open(path, SqliteOpenMode.ReadWrite);
        using var transaction = db.BeginTransaction();
        if (HasSystemNames(db)) return;
        BuildSystemNames(db, transaction, CancellationToken.None);
        transaction.Commit();
    }
    private static void BuildSystemNames(SqliteConnection db, SqliteTransaction transaction, CancellationToken cancellation)
    {
        using var create = db.CreateCommand(); create.Transaction = transaction;
        create.CommandText = "CREATE TABLE system_names(name TEXT NOT NULL,id INTEGER NOT NULL,PRIMARY KEY(name,id)) WITHOUT ROWID";
        create.ExecuteNonQuery();
        using var insert = db.CreateCommand(); insert.Transaction = transaction;
        insert.CommandText = "INSERT OR IGNORE INTO system_names VALUES($name,$id)";
        insert.Parameters.Add("$name", SqliteType.Text); insert.Parameters.Add("$id", SqliteType.Integer); insert.Prepare();
        foreach (var record in ReadDataset(db, "mapSolarSystems"))
        using (record)
        {
            cancellation.ThrowIfCancellationRequested();
            var root = record.RootElement;
            if (!root.TryGetProperty("name", out var names)) continue;
            insert.Parameters["$id"].Value = root.GetProperty("_key").GetInt64();
            foreach (var alias in names.EnumerateObject())
            {
                insert.Parameters["$name"].Value = alias.Value.GetString()!.ToUpperInvariant();
                insert.ExecuteNonQuery();
            }
        }
    }
}
