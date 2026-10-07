using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EveOPreview.Services.Logs;
using EveOPreview.UI;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class CombatLogFixtureTests
{
    private sealed record Expected(string File, int Entries, string[] NpcAliases,
        double IncomingDamage, double OutgoingDamage, double IncomingRepair, double OutgoingRepair);

    [Theory]
    [InlineData("autocannon.txt")]
    [InlineData("repairs.txt")]
    [InlineData("artillery.txt")]
    public void AnonymizedCombatLogsReplayWithoutExternalData(string file)
    {
        string fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var expected = JsonSerializer.Deserialize<Expected[]>(File.ReadAllText(Path.Combine(fixtures, "log-expectations.json")))!.Single(x => x.File == file);
        var catalog = EveLogCatalog.Load();
        foreach (var npc in expected.NpcAliases) catalog.NpcNames.Add(npc);
        using var input = CompleteLogReader.OpenShared(Path.Combine(fixtures, file));
        var batch = CompleteLogReader.Read(input, CompleteLogReader.Identity(input), null);
        Assert.False(batch.More); Assert.Equal(0, batch.OversizedLines);
        var header = new LogHeader();
        foreach (var line in batch.Lines) header = EveLogParser.ReadHeader(header, line.Text);
        Assert.Equal("Sample Observer", header.Listener); Assert.False(header.Ambiguous);
        var parsed = batch.Lines.Select(line => (line.Offset, Entry: EveLogParser.Parse(line.Text, header, false, catalog, new HashSet<string>())))
            .Where(x => x.Entry?.Direction is not null).Select(x => new PositionedLogEntry(x.Offset, x.Entry!)).ToArray();
        Assert.Equal(expected.Entries, parsed.Length);
        Assert.All(parsed, x =>
        {
            Assert.StartsWith("Sample ", x.Entry.Counterparty!);
            Assert.DoesNotContain("showinfo:", x.Entry.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(2026, x.Entry.Timestamp.Year);
        });
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = parsed.Max(x => x.Entry.Timestamp).AddMinutes(1);
        using var store = new CombatLogStore(":memory:", start);
        var source = new StoredLogFile(file, batch.Cursor, header);
        store.Commit(source, parsed); store.Commit(source, parsed);
        CharacterCombatSnapshot Read() => Assert.Single(store.Snapshot(end, 10, "", "", new Dictionary<string, long?>()).Characters);
        var character = Read();
        Assert.Equal(expected.IncomingDamage, character.Categories.Sum(x => x.Total.Incoming));
        Assert.Equal(expected.OutgoingDamage, character.Categories.Sum(x => x.Total.Outgoing));
        Assert.Equal(expected.IncomingRepair, character.Activity!.Repairs.Sum(x => x.Total.Incoming));
        Assert.Equal(expected.OutgoingRepair, character.Activity.Repairs.Sum(x => x.Total.Outgoing));
        Assert.Equal(parsed.Count(x => x.Entry.Effect != CombatEffect.Damage && x.Entry.Amount > 0), character.Activity.Repairs.Sum(x => x.Count.Incoming + x.Count.Outgoing));
        Assert.All(character.Categories, x => Assert.Equal(new DamageFigures(0, 0), x.Dps));
        if (file == "artillery.txt") Assert.All(character.Activity.Combat, x => Assert.Equal(new DamageFigures(0, 0), x.DpsSampleSeconds));
        else Assert.True(character.Activity.Combat.Sum(x => x.DpsSampleSeconds.Incoming + x.DpsSampleSeconds.Outgoing) > 0);
        string before = JsonSerializer.Serialize(character.Activity);
        store.Prune(end.AddDays(10), 1); store.Commit(source, parsed);
        Assert.Equal(before, JsonSerializer.Serialize(Read().Activity));
        store.Reset(end, CombatResetScope.All, "Sample Observer");
        store.Commit(source with { Cursor = batch.Cursor with { Identity = "late-copy" } }, parsed);
        Assert.Equal(0, Read().Categories.Sum(x => x.Total.Incoming + x.Total.Outgoing));
    }
}
