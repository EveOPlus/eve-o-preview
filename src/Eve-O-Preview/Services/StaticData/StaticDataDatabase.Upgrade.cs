#nullable enable
using System;
using System.IO;
using System.Threading;
using Microsoft.Data.Sqlite;

namespace EveOPreview.Services.StaticData;

public sealed partial class StaticDataDatabase
{
    // Independent of the complete-record schema and FC build. Revision 1 had no marker.
    public const int CombatIndexVersion = 2;
    private static int ReadCombatIndexVersion(SqliteConnection db)
    {
        using var command = db.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='combat_index'";
        if (Convert.ToInt32(command.ExecuteScalar()) == 0) return 1;
        command.CommandText = "SELECT version FROM combat_index";
        return Convert.ToInt32(command.ExecuteScalar());
    }
    private static void WriteCombatIndexVersion(SqliteConnection db, SqliteTransaction transaction)
    {
        using var command = db.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "CREATE TABLE IF NOT EXISTS combat_index(version INTEGER NOT NULL); DELETE FROM combat_index; INSERT INTO combat_index VALUES($version)";
        command.Parameters.AddWithValue("$version", CombatIndexVersion); command.ExecuteNonQuery();
    }
    /// <summary>Atomic, local-only derived-index upgrade. Raw records, SDE manifest and
    /// app history/settings are untouched. Cancellation/failure rolls back the entire index.</summary>
    public static bool UpgradeCombatIndex(string path, CancellationToken cancellation = default)
    {
        using (var check = Open(path, SqliteOpenMode.ReadOnly))
        {
            using var manifest = check.CreateCommand(); manifest.CommandText = "SELECT build FROM manifest WHERE schema=2";
            if (manifest.ExecuteScalar() is null) throw new InvalidDataException("Incomplete static data.");
            if (ReadCombatIndexVersion(check) >= CombatIndexVersion) return false;
        }
        using var db = Open(path, SqliteOpenMode.ReadWrite);
        using var transaction = db.BeginTransaction();
        // The write transaction serializes concurrent startup/catalog readers.
        if (ReadCombatIndexVersion(db) >= CombatIndexVersion) return false;
        cancellation.ThrowIfCancellationRequested();
        using var clear = db.CreateCommand(); clear.Transaction = transaction;
        clear.CommandText = "DELETE FROM names; DELETE FROM combat"; clear.ExecuteNonQuery();
        BuildCombatIndex(db, transaction, cancellation);
        WriteCombatIndexVersion(db, transaction);
        cancellation.ThrowIfCancellationRequested(); transaction.Commit(); return true;
    }
}
