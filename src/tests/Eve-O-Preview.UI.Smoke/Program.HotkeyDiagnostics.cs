using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace EveOPreview.UI.Smoke;

internal static partial class Program
{
    private static int CheckHotkeyDiagnostics(string output)
    {
        foreach (string theme in new[] { "Dark", "Light" })
        {
            var backend = new SmokeBackend();
            backend.ExecuteAsync(new("theme", Value: theme)).GetAwaiter().GetResult();
            using var view = new WorkspaceView(backend);
            var window = new Window { Width = 784, Height = 720, Content = view };
            window.Show(); Flush();
            bool HasDiagnostic() => view.GetVisualDescendants().Any(c => c.Name == "setting-DiagnosticHotkeyPassthrough");
            try
            {
                var search = FindControl<TextBox>(view, "search-settings");
                search.Text = "passthrough"; Flush();
                Require(!HasDiagnostic(), "Search must not expose locked diagnostics.");
                view.Navigate("Switching"); Flush(); Click(view, "switching-global");
                Require(!HasDiagnostic(), "Diagnostics must be hidden initially.");
                var trigger = FindControl<ComboBox>(view, "setting-GlobalHotkeyTrigger");
                Require((string?)trigger.SelectedItem == "KeyDown", "Key-down must be the initial trigger.");
                trigger.SelectedItem = "KeyUp"; Flush(); Click(view, "apply-GlobalHotkeyTrigger");
                Require(backend.Read().Settings["GlobalHotkeyTrigger"] == "KeyUp", "Trigger timing must use the settings backend.");
                Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-hotkey-neutral.png"));

                FindControl<ComboBox>(view, "profile-picker").SelectedIndex = 0; Flush();
                Require((string?)FindControl<ComboBox>(view, "setting-GlobalHotkeyTrigger").SelectedItem == "KeyDown", "Another profile must have its own trigger.");
                FindControl<ComboBox>(view, "setting-HotkeyInputMethod").SelectedItem = "Windows"; Flush(); Click(view, "apply-HotkeyInputMethod");
                Require(!view.GetVisualDescendants().Any(c => c.Name == "setting-GlobalHotkeyTrigger"), "The native profile must hide trigger timing.");
                FindControl<ComboBox>(view, "profile-picker").SelectedIndex = 1; Flush();
                Require((string?)FindControl<ComboBox>(view, "setting-HotkeyInputMethod").SelectedItem == "Global", "Returning to the original profile must restore its method.");
                Require((string?)FindControl<ComboBox>(view, "setting-GlobalHotkeyTrigger").SelectedItem == "KeyUp", "Returning to the original profile must restore its trigger.");
                FindControl<ComboBox>(view, "profile-picker").SelectedIndex = 0; Flush();
                Require((string?)FindControl<ComboBox>(view, "setting-HotkeyInputMethod").SelectedItem == "Windows", "The other profile must retain native hotkeys.");
                FindControl<ComboBox>(view, "profile-picker").SelectedIndex = 1; Flush();

                void Toggle()
                {
                    var combo = FindControl<ComboBox>(view, "setting-HotkeyInputMethod");
                    combo.IsDropDownOpen = true; Flush();
                    combo.IsDropDownOpen = false; Flush();
                }
                for (int i = 0; i < 3; i++) Toggle();
                // Simulate an idle gap without slowing the complete visual suite.
                typeof(WorkspaceView).GetField("_hotkeyDiagnosticLastToggle", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(view, Stopwatch.GetTimestamp() - Stopwatch.Frequency * 3);
                for (int i = 0; i < 9; i++) Toggle();
                Require(!HasDiagnostic(), "Nine rapid cycles after an idle gap must not unlock diagnostics.");
                Toggle();
                Require(HasDiagnostic(), "The tenth complete rapid cycle must reveal diagnostics.");
                var diagnostic = FindControl<ToggleSwitch>(view, "setting-DiagnosticHotkeyPassthrough");
                Require(diagnostic.IsChecked != true, "Unlocking must not enable passthrough.");
                diagnostic.IsChecked = true; Flush();
                Require(backend.Commands.Last().Target == "DiagnosticHotkeyPassthrough" && backend.Commands.Last().Value == "True", "Diagnostic toggle must reach the backend.");
                Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-hotkey-diagnostics.png"));

                var input = FindControl<ComboBox>(view, "setting-HotkeyInputMethod");
                input.SelectedItem = "Windows"; Flush(); Click(view, "apply-HotkeyInputMethod");
                Require(!view.GetVisualDescendants().Any(c => c.Name == "setting-GlobalHotkeyTrigger"), "Native hotkeys must not expose key-up timing.");
                Require(!FindControl<ToggleSwitch>(view, "setting-DiagnosticHotkeyPassthrough").IsEffectivelyEnabled, "Native hotkeys must disable diagnostic passthrough.");
                search = FindControl<TextBox>(view, "search-settings");
                search.Text = "passthrough"; Flush();
                Require(HasDiagnostic() && !FindControl<ToggleSwitch>(view, "setting-DiagnosticHotkeyPassthrough").IsEffectivelyEnabled, "Unlocked search must respect the selected input method.");
                backend.ExecuteAsync(new("theme", Value: "Legacy")).GetAwaiter().GetResult();
                view.RefreshFromBackend(); view.Navigate("CycleGroups"); Flush();
                Require(!HasDiagnostic(), "Legacy must not expose diagnostics even after unlock.");
            }
            finally { window.Close(); }
        }
        return 4 + CheckHotkeyLocalization(output);
    }

    private static int CheckHotkeyLocalization(string output)
    {
        foreach (var language in WorkspaceLocalization.Languages)
        {
            var backend = new SmokeBackend();
            backend.ExecuteAsync(new("language", Value: language.Code)).GetAwaiter().GetResult();
            using var view = new WorkspaceView(backend);
            var window = new Window { Width = 784, Height = 720, Content = view };
            window.Show(); Flush();
            try
            {
                var localizer = new WorkspaceLocalization(language.Code);
                FindControl<TextBox>(view, "search-settings").Text = localizer.Get("Hotkey method"); Flush();
                Require(view.GetVisualDescendants().Any(c => c.Name == "setting-HotkeyInputMethod"), "Localized hotkey search failed: " + language.Code);
                view.Navigate("Switching"); Flush(); Click(view, "switching-global");
                // Gesture behavior is tested above; expose the diagnostic row for translation/layout checks.
                typeof(WorkspaceView).GetField("_hotkeyDiagnosticsUnlocked", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(view, true);
                view.RefreshFromBackend(); Flush();
                var input = FindControl<ComboBox>(view, "setting-HotkeyInputMethod");
                var trigger = FindControl<ComboBox>(view, "setting-GlobalHotkeyTrigger");
                Require((string?)input.SelectedItem == "Global" && (string?)trigger.SelectedItem == "KeyDown", "Localization changed stored choice values.");
                foreach (string label in new[] { "Hotkey method", "Hotkey trigger", "Global input", "Key down", "Diagnostic key passthrough" })
                    Require(view.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == localizer.Get(label)), "Missing translated hotkey label: " + language.Code + ": " + label);
                Capture(window, Path.Combine(output, "language-" + language.Code + "-hotkeys.png"));
                trigger.SelectedItem = "KeyUp"; Flush();
                Require(view.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == localizer.Get("Key up")), "Missing translated release choice.");
                input.SelectedItem = "Windows"; Flush();
                Require(view.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == localizer.Get("Windows hotkeys")), "Missing translated native choice.");
            }
            finally { window.Close(); }
        }
        Console.WriteLine("PASS: localized hotkey controls, diagnostic labels, search and stable choice values in all 18 languages.");
        return WorkspaceLocalization.Languages.Count;
    }
}
