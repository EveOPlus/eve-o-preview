using System;
using System.IO;
using EveOPreview.Tests.Checks;
using Xunit.Runner.InProc.SystemConsole;

namespace EveOPreview.Tests;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Normal launches, including Test Explorer, use the xUnit runner.
        if (args.Length != 3 || args[0] != "--private-desktop")
            return ConsoleRunner.Run(args).GetAwaiter().GetResult();

        // The xUnit window tests launch this isolated STA worker automatically.
        using var output = new StreamWriter(args[2]) { AutoFlush = true };
        Console.SetOut(output);
        Console.SetError(output);
        try
        {
            if (args[1] == "feature-controls") FeatureAvailabilityTests.CheckControls();
            else if (args[1] == "custom-audio-ui") CustomAudioTests.CheckUi();
            else if (args[1].StartsWith("settings-", StringComparison.Ordinal)) SettingsIntegrationTests.RunScenario(args[1][9..]);
            else if (args[1].StartsWith("live-", StringComparison.Ordinal)) LiveThumbnailTests.RunScenario(args[1][5..]);
            else ThumbnailZOrderTests.RunScenario(args[1]);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
