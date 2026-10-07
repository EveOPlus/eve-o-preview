#nullable enable
using System;
using System.Collections.Generic;
using EveOPreview.UI;

namespace EveOPreview.Services.Logs;

public sealed partial class CombatLogStore
{
    private readonly Dictionary<string, Dictionary<CombatResetScope, DateTimeOffset>> _characterResets = new(StringComparer.OrdinalIgnoreCase);

    private void InitializeCharacterResets()
    {
        using var transaction = _database.BeginTransaction();
        Execute("""
            CREATE TABLE IF NOT EXISTS character_resets (
                character TEXT NOT NULL COLLATE NOCASE,scope INTEGER NOT NULL,since INTEGER NOT NULL,
                PRIMARY KEY(character,scope));
            PRAGMA user_version=4;
            """);
        using (var command = Command("SELECT character,scope,since FROM character_resets"))
        using (var reader = command.ExecuteReader())
            while (reader.Read()) RememberReset(reader.GetString(0), (CombatResetScope)reader.GetInt32(1), DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(2)));
        transaction.Commit();
    }

    private void RememberReset(string character, CombatResetScope scope, DateTimeOffset since)
    {
        if (!_characterResets.TryGetValue(character, out var resets)) _characterResets[character] = resets = new();
        resets[scope] = since;
    }

    private DateTimeOffset CharacterCutoff(string character, CombatResetScope scope, DateTimeOffset baseline)
    {
        if (!_characterResets.TryGetValue(character, out var resets)) return baseline;
        if (resets.TryGetValue(CombatResetScope.All, out var all) && all > baseline) baseline = all;
        return resets.TryGetValue(scope, out var selected) && selected > baseline ? selected : baseline;
    }

    private void ResetCharacter(DateTimeOffset now, CombatResetScope scope, string character)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(character);
        using var transaction = _database.BeginTransaction();
        void Delete(string table, string condition = "") => Execute("DELETE FROM " + table + " WHERE character=$character" + condition, ("$character", character));
        if (scope is CombatResetScope.All) Delete("entries");
        if (scope is CombatResetScope.All or CombatResetScope.Damage)
        {
            Delete("entries", " AND direction IS NOT NULL"); Delete("totals"); Delete("activity_totals", " AND effect=0");
            Delete("encounters"); Delete("dps_damage"); Delete("dps_streams");
        }
        if (scope is CombatResetScope.All or CombatResetScope.Repairs)
        {
            Delete("entries", " AND json_extract(json,'$.Effect') IN (1,2,3)");
            Delete("activity_totals", " AND effect IN (1,2,3)");
        }
        if (scope is CombatResetScope.All or CombatResetScope.Jumps)
        {
            Delete("system_visits"); SeedVisitBaselines(character);
        }
        Execute("INSERT OR REPLACE INTO character_resets VALUES ($character,$scope,$since)",
            ("$character", character), ("$scope", (int)scope), ("$since", now.ToUnixTimeMilliseconds()));
        transaction.Commit();
        // Publish the new cutoff only after the same transaction as its counters.
        RememberReset(character, scope, now); _activityCache = null;
    }
}
