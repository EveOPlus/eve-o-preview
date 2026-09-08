using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace EveOPreview.UI.Smoke;

internal static partial class Program
{
    private static int CheckLocalization(Window window, WorkspaceView view, SmokeBackend backend, string output)
    {
        int renders = 0;
        window.Width = 1180; window.Height = 820;
        backend.ExecuteAsync(new("theme", Value: "Dark")).GetAwaiter().GetResult(); Flush();
        view.NavigatePreviewTab("Titles"); Flush();
        FindControl<TextBox>(view, "preview-character-title").Text = "EVE - Appearance"; Flush();
        FindControl<AutoCompleteBox>(view, "setting-TitleFontName").Text = "Arial"; Flush();
        Require(view.HasUnappliedEdits, "Prepare a real unapplied font edit for language switching.");
        foreach (var language in WorkspaceLocalization.Languages)
        {
            view.Navigate("Appearance"); Flush();
            var picker = FindControl<ComboBox>(view, "ui-language");
            picker.SelectedItem = picker.ItemsSource!.Cast<WorkspaceLocalization.Language>().Single(x => x.Code == language.Code); Flush();
            Require(backend.Read().UiLanguage == language.Code, "Language selector must execute a backend command.");
            Require(view.HasUnappliedEdits, "Language switching discarded an unapplied edit.");
            var localizer = new WorkspaceLocalization(language.Code);
            Require(view.FlowDirection == (localizer.RightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight), "Incorrect language direction.");
            Require(view.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == localizer.Get("Language")), "Language heading was not translated.");
            window.Width = 784; window.Height = 581; Flush();
            Capture(window, Path.Combine(output, "language-" + language.Code + "-appearance.png")); renders++;
            window.Width = 1180; window.Height = 820; Flush();
            view.NavigatePreviewTab("Titles"); Flush();
            Require(FindControl<AutoCompleteBox>(view, "setting-TitleFontName").Text == "Arial", "Language switching lost the font draft.");
            Require(FindControl<TextBox>(view, "preview-character-title").Text == "EVE - Appearance", "Language switching translated the sample character name.");
            Require(FindControl<Control>(view, "title-preview").FlowDirection == FlowDirection.LeftToRight, "RTL must not mirror native preview geometry.");
            view.Navigate("Overview"); Flush();
            FindControl<TextBox>(view, "search-settings").Text = localizer.Get("Preview opacity"); Flush();
            Require(view.GetVisualDescendants().OfType<Control>().Any(c => c.Name == "setting-ThumbnailOpacity"), "Localized setting search failed for " + language.Code);
            if (language.Code is "de" or "ar" or "ja" or "hi")
            {
                window.Width = 784; window.Height = 581; Flush();
                foreach (var page in ModernPages)
                {
                    view.Navigate(page); Flush();
                    Capture(window, Path.Combine(output, "language-" + language.Code + "-" + page + ".png")); renders++;
                }
            }
        }
        backend.ExecuteAsync(new("profile-switch", "default")).GetAwaiter().GetResult(); Flush();
        Require(backend.Read().UiLanguage == "zh-Hant", "Profile switching changed global language.");
        backend.ExecuteAsync(new("theme", Value: "Legacy")).GetAwaiter().GetResult(); Flush();
        view.Navigate("Appearance"); Flush();
        Require(!view.GetVisualDescendants().Any(c => c.Name == "ui-language"), "Legacy must not gain a language control.");
        Require(view.FlowDirection == FlowDirection.LeftToRight, "Legacy direction must remain unchanged.");
        Console.WriteLine("PASS: all 18 languages; translated search; raw names; draft retention; global preference; RTL preview geometry; Legacy isolation.");
        return renders;
    }
}
