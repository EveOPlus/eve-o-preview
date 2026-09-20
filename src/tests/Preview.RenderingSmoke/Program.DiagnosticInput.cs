using System.Diagnostics;
using System.Text.Json;
using EveOPreview.Services;
using EveOPreview.Services.Implementation;
using Serilog;

namespace EveOPreview.RenderingSmoke;

internal static partial class Program
{
    // Opt-in real desktop input, directed only to two guarded test windows. No EVE actions.
    private static int ValidateDiagnosticInput(string output)
    {
        const ushort key = 0x87; // F24, without modifiers.
        foreach (int modifier in new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C, (int)key })
            if ((Native.GetAsyncKeyState(modifier) & 0x8000) != 0) throw new InvalidOperationException("Release modifiers and F24 before this test.");
        using var logger = new LoggerConfiguration().CreateLogger();
        using var first = new Form { Text = "EVE-O diagnostic input recipient", Width = 400, Height = 120, KeyPreview = true };
        using var second = new Form { Text = "EVE-O diagnostic focus destination", Width = 400, Height = 120, KeyPreview = true };
        int firstDown = 0, firstUp = 0, secondDown = 0, secondUp = 0, actions = 0;
        first.KeyDown += (_, e) => { if (e.KeyCode == Keys.F24) firstDown++; };
        first.KeyUp += (_, e) => { if (e.KeyCode == Keys.F24) firstUp++; };
        second.KeyDown += (_, e) => { if (e.KeyCode == Keys.F24) secondDown++; };
        second.KeyUp += (_, e) => { if (e.KeyCode == Keys.F24) secondUp++; };
        nint original = Native.GetForegroundWindow();
        using var input = new WindowsHotkeyService(logger);
        var checks = new List<string>();
        void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException(check + $" (first {firstDown}/{firstUp}, second {secondDown}/{secondUp}, actions {actions})");
            checks.Add(check);
        }
        void FocusFirst()
        {
            Native.SetForegroundWindow(first.Handle);
            var wait = Stopwatch.StartNew();
            while (Native.GetForegroundWindow() != first.Handle && wait.ElapsedMilliseconds < 1000) Pump(TimeSpan.FromMilliseconds(5));
            Require(Native.GetForegroundWindow() == first.Handle, "Guarded recipient obtained foreground");
            firstDown = firstUp = secondDown = secondUp = actions = 0;
        }
        void Send(bool down)
        {
            nint foreground = Native.GetForegroundWindow();
            if (foreground != first.Handle && foreground != second.Handle) throw new InvalidOperationException("Focus left the test windows; no key sent.");
            Native.KeyTransition(key, down); Pump(TimeSpan.FromMilliseconds(60));
        }
        try
        {
            first.Show(); second.Show();
            foreach (bool release in new[] { false, true })
            foreach (bool passthrough in new[] { false, true })
            {
                input.Replace([new("F24", () => { actions++; Native.SetForegroundWindow(second.Handle); }, release)], HotkeyMode.Global, passthrough);
                Require(string.IsNullOrEmpty(input.RegistrationWarning), "Global hook installed");
                FocusFirst();
                Send(true);
                Require(actions == (release ? 0 : 1), $"{release}/{passthrough}: selected trigger edge respected on key-down");
                Require(firstDown == (passthrough ? 1 : 0) && secondDown == 0, $"{release}/{passthrough}: original key-down delivery respects passthrough");
                Send(false);
                Require(actions == 1, $"{release}/{passthrough}: exactly one action after key-up");
                Require(firstUp + secondUp == (passthrough ? 1 : 0), $"{release}/{passthrough}: key-up delivery respects passthrough");
                Require(Native.GetForegroundWindow() == second.Handle, "Hotkey action focused its destination");
            }
            input.Replace([new("F24", () => actions++)], HotkeyMode.Global, true);
            FocusFirst();
            var capture = input.CaptureAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
            Send(true); Send(false);
            while (!capture.IsCompleted) Pump(TimeSpan.FromMilliseconds(5));
            Require(capture.GetAwaiter().GetResult() == "F24" && actions == 0 && firstDown == 0 && firstUp == 0,
                "Recording remains suppressed with diagnostic passthrough enabled");
            Send(true); Send(false);
            Require(actions == 1 && firstDown == 1 && firstUp == 1, "Capture restores diagnostic passthrough");
            input.Replace([new("F24", () => actions++)], HotkeyMode.Global, false);
            FocusFirst(); Send(true); Send(false);
            Require(actions == 1 && firstDown == 0 && firstUp == 0, "Disabling diagnostics restores suppression");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "diagnostic-input.json"), JsonSerializer.Serialize(new { Passed = true, Checks = checks }, JsonOptions));
            Console.WriteLine($"Passed {checks.Count} native diagnostic input checks.");
            return 0;
        }
        finally
        {
            Native.KeyTransition(key, false);
            input.Replace([], HotkeyMode.Global);
            if (original != 0) Native.SetForegroundWindow(original);
        }
    }
}
