using System.Diagnostics;
using System.Text.Json;
using EveOPreview.Services.Logs;
using EveOPreview.Services.StaticData;
using EveOPreview.UI;
using Serilog;

// Opt-in local replay. No source text, character names or paths in the output.
internal static class LogAudit
{
    public static int Run(string directory, string? staticDirectory)
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        using var data = staticDirectory is null ? null : new StaticDataService(staticDirectory, logger);
        if (staticDirectory is not null && data?.Build is null) throw new InvalidOperationException("Static data unavailable.");
        var catalog = EveLogCatalog.Load(data);
        var counts = new Dictionary<string, long>();
        var quantities = new Dictionary<string, decimal>();
        var categories = new Dictionary<string, long>();
        var headers = new Dictionary<string, int>();
        var players = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int files = 0, oversizedLines = 0, attributed = 0, languages = 0;
        long unresolvedMinedItems = 0;
        var clock = Stopwatch.StartNew();
        foreach (string folder in new[] { "Gamelogs", "Chatlogs", "Gamelog", "Chat-Local" }.Where(folder => Directory.Exists(Path.Combine(directory, folder))))
        foreach (string path in Directory.EnumerateFiles(Path.Combine(directory, folder), "*.txt"))
        {
            bool chat = folder is "Chatlogs" or "Chat-Local";
            if (chat && !EveLogParser.IsLocalFile(Path.GetFileName(path))) continue;
            files++;
            using var input = CompleteLogReader.OpenShared(path);
            string identity = CompleteLogReader.Identity(input);
            LogCursor? cursor = null;
            var header = new LogHeader();
            while (true)
            {
                var batch = CompleteLogReader.Read(input, identity, cursor);
                cursor = batch.Cursor; oversizedLines += batch.OversizedLines;
                foreach (var line in batch.Lines) header = EveLogParser.ReadHeader(header, line.Text);
                foreach (var line in batch.Lines)
                {
                    var parsed = EveLogParser.Parse(line.Text, header, chat, catalog, players);
                    if (parsed is null) continue;
                    string key = parsed.EventKind.ToString();
                    counts[key] = counts.GetValueOrDefault(key) + 1;
                    categories[parsed.Category] = categories.GetValueOrDefault(parsed.Category) + 1;
                    if (parsed.Quantity is { } quantity) quantities[key] = quantities.GetValueOrDefault(key) + quantity;
                    if (parsed.EventKind == LogEventKind.Mining && parsed.ItemTypeId is null) unresolvedMinedItems++;
                    if (parsed.EventKind is not (LogEventKind.Damage or LogEventKind.Repair)
                        && (parsed.Direction is not null || parsed.Amount != 0)) throw new InvalidOperationException("Non-combat event entered damage fields.");
                }
                if (!batch.More) break;
            }
            if (header.Listener is not null && !header.Ambiguous) attributed++;
            if (header.Language != LogLanguage.Automatic) languages++;
            string state = folder + ":" + (header.Ambiguous ? "conflicting-listeners" : header.Listener is null ? "missing-listener" : "attributed");
            headers[state] = headers.GetValueOrDefault(state) + 1;
        }
        Console.WriteLine(JsonSerializer.Serialize(new { files, attributed, languages, oversizedLines, counts, categories,
            quantities, headers, unresolvedMinedItems, staticBuild = data?.Build, elapsed = clock.Elapsed }, new JsonSerializerOptions { WriteIndented = true }));
        // Old/empty/conflicting files are diagnostic counts, not fabricated attribution.
        return oversizedLines == 0 ? 0 : 1;
    }
}
