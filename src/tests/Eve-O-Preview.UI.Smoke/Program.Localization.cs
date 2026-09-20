using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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
        FindControl<ComboBox>(view, "setting-TitleFontName").SelectedItem = "Arial"; Flush();
        Require(view.HasUnappliedEdits, "Prepare a real unapplied font edit for language switching.");
        var titleFont = FindControl<ComboBox>(view, "setting-TitleFontName"); titleFont.IsDropDownOpen = true; Flush();
        Capture(window, Path.Combine(output, "title-font-dropdown.png")); renders++;
        titleFont.IsDropDownOpen = false;
        var titleColour = FindControl<ColorPicker>(view, "pick-TitleFontForeColor");
        string savedColour = backend.Read().Settings["TitleFontForeColor"];
        titleColour.Color = Color.Parse("#66AAFF"); Flush();
        Require(FindControl<TextBox>(view, "setting-TitleFontForeColor").Text == "#66AAFF" && backend.Read().Settings["TitleFontForeColor"] == savedColour,
            "Title colour selection must update the draft and preview without saving before Apply.");
        Require(titleColour.PaletteColors!.Count() == 24 && titleColour.IsComponentSliderVisible && titleColour.ColorModel == ColorModel.Rgba,
            "Title colours must use the shared palette and RGB controls.");
        titleColour.BringIntoView(); Flush();
        var point = titleColour.TranslatePoint(new Point(titleColour.Bounds.Width / 2, titleColour.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left); Flush();
        titleColour.SelectedIndex = 2; Flush();
        Capture(window, Path.Combine(output, "title-colour-rgb-popup.png")); renders++;
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None); window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None); Flush();
        FindControl<TextBox>(view, "setting-TitleFontForeColor").Text = savedColour; Flush();
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
            string expectedCredit = language.Code switch
            {
                "en" => "",
                "zh-Hans" => localizer.Format($"Thanks to {"Rangeen (冉吉)"} for assistance with translations. New automated translations may be added over time."),
                _ => localizer.Get("This language is automatically translated. Some wording may be inaccurate or unnatural.")
            };
            var credit = FindControl<TextBlock>(view, "ui-language-credit");
            Require(credit.Text == expectedCredit && credit.IsVisible == (language.Code != "en") && credit.TextWrapping == TextWrapping.Wrap,
                "Language credit must identify contributors or automated translations and wrap at narrow widths.");
            Require(credit.FlowDirection == view.FlowDirection, "Translation credit must follow the language direction.");
            window.Width = 784; window.Height = 581; Flush();
            Capture(window, Path.Combine(output, "language-" + language.Code + "-appearance.png")); renders++;
            window.Width = 1180; window.Height = 820; Flush();
            view.NavigatePreviewTab("Titles"); Flush();
            Require(FindControl<ComboBox>(view, "setting-TitleFontName").SelectedItem as string == "Arial", "Language switching lost the font draft.");
            Require(FindControl<TextBox>(view, "preview-character-title").Text == "EVE - Appearance", "Language switching translated the sample character name.");
            Require(FindControl<Control>(view, "title-preview").FlowDirection == FlowDirection.LeftToRight, "RTL must not mirror native preview geometry.");
            view.Navigate("Overview"); Flush();
            FindControl<TextBox>(view, "search-settings").Text = localizer.Get("Preview opacity"); Flush();
            Require(view.GetVisualDescendants().OfType<Control>().Any(c => c.Name == "setting-ThumbnailOpacity"), "Localized setting search failed for " + language.Code);
            if (language.Code is "de" or "ar" or "ja" or "hi" or "zh-Hans" or "zh-Hant")
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
        view.Navigate("Appearance"); Flush();
        var automaticPicker = FindControl<ComboBox>(view, "ui-language");
        automaticPicker.SelectedItem = automaticPicker.ItemsSource!.Cast<WorkspaceLocalization.Language>().Single(x => x.Code == "auto"); Flush();
        var automatic = new WorkspaceLocalization("auto");
        var automaticNotice = FindControl<TextBlock>(view, "ui-language-credit");
        string automaticCredit = automaticNotice.Text!;
        Require(automaticNotice.IsVisible == (automatic.Code != "en"), "Automatic English must hide the translation notice.");
        Require(backend.Read().UiLanguage == "auto" && automaticCredit == (automatic.Code switch
        {
            "en" => "",
            "zh-Hans" => automatic.Format($"Thanks to {"Rangeen (冉吉)"} for assistance with translations. New automated translations may be added over time."),
            _ => automatic.Get("This language is automatically translated. Some wording may be inaccurate or unnatural.")
        }), "Automatic language must show the resolved language's translation credit.");
        backend.ExecuteAsync(new("theme", Value: "Legacy")).GetAwaiter().GetResult(); Flush();
        view.Navigate("Appearance"); Flush();
        Require(!view.GetVisualDescendants().Any(c => c.Name == "ui-language"), "Legacy must not gain a language control.");
        Require(!view.GetVisualDescendants().Any(c => c.Name == "ui-language-credit"), "Legacy must not gain a translation notice.");
        Require(view.FlowDirection == FlowDirection.LeftToRight, "Legacy direction must remain unchanged.");
        Console.WriteLine("PASS: all 18 languages; translated search; raw names; draft retention; global preference; RTL preview geometry; Legacy isolation.");
        return renders;
    }
}
