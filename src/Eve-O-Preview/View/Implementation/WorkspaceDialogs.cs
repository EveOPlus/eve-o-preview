using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using EveOPreview.Configuration.Implementation;
using EveOPreview.UI;
using DrawingColor = System.Drawing.Color;
using DrawingFontStyle = System.Drawing.FontStyle;

namespace EveOPreview.View;

/// <summary>Owned Avalonia dialogs. A cancelled dialog never changes the shared settings object.</summary>
internal static class WorkspaceDialogs
{
    public static async Task<FontSettings> PickFont(Window owner, FontSettings current, System.Collections.Generic.IReadOnlyList<string> families)
    {
        var text = Localizer(owner);
        var font = WorkspacePickers.FontFamily(families, current.Name, "font-family", text.Get("Font family"));
        var size = new NumericUpDown { Name = "dialog-font-size", Minimum = 1, Maximum = 200, Increment = 0.25m, Value = (decimal)current.Size, FormatString = "0.##" };
        var bold = Style("Bold", DrawingFontStyle.Bold);
        var italic = Style("Italic", DrawingFontStyle.Italic);
        var underline = Style("Underline", DrawingFontStyle.Underline);
        var strikeout = Style("Strikeout", DrawingFontStyle.Strikeout);
        var sample = new TextBlock { Text = "EVE - Sample Name", TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(0, 16) };
        void UpdateSample()
        {
            sample.FontFamily = new Avalonia.Media.FontFamily(font.SelectedItem?.ToString() ?? current.Name);
            sample.FontSize = (double)(size.Value ?? (decimal)current.Size) * 96 / 72;
            sample.FontWeight = bold.IsChecked == true ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal;
            sample.FontStyle = italic.IsChecked == true ? Avalonia.Media.FontStyle.Italic : Avalonia.Media.FontStyle.Normal;
            sample.TextDecorations = new Avalonia.Media.TextDecorationCollection();
            if (underline.IsChecked == true) sample.TextDecorations.AddRange(Avalonia.Media.TextDecorations.Underline);
            if (strikeout.IsChecked == true) sample.TextDecorations.AddRange(Avalonia.Media.TextDecorations.Strikethrough);
        }
        font.SelectionChanged += (_, _) => UpdateSample(); size.ValueChanged += (_, _) => UpdateSample();
        bold.IsCheckedChanged += (_, _) => UpdateSample(); italic.IsCheckedChanged += (_, _) => UpdateSample();
        underline.IsCheckedChanged += (_, _) => UpdateSample(); strikeout.IsCheckedChanged += (_, _) => UpdateSample();
        var body = new StackPanel { Spacing = 8, Children = { new TextBlock { Text = text.Get("Font family") }, font, new TextBlock { Text = text.Get("Title font size") + " (pt)" }, size,
            new WrapPanel { Children = { bold, italic, underline, strikeout } }, sample } };
        UpdateSample();
        var dialog = Create(text.Get("Title font family"), owner, new ScrollViewer { Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled }, 460, 400, out var accept);
        accept.Click += (_, _) => dialog.Close(new FontSettings
        {
            Name = font.SelectedItem?.ToString() ?? current.Name, Size = (float)(size.Value ?? (decimal)current.Size),
            Style = (bold.IsChecked == true ? DrawingFontStyle.Bold : 0) | (italic.IsChecked == true ? DrawingFontStyle.Italic : 0) |
                (underline.IsChecked == true ? DrawingFontStyle.Underline : 0) | (strikeout.IsChecked == true ? DrawingFontStyle.Strikeout : 0)
        });
        return await dialog.ShowDialog<FontSettings>(owner);
        CheckBox Style(string label, DrawingFontStyle style) => new() { Content = text.Get(label), Margin = new Thickness(0, 0, 12, 0), IsChecked = (current.Style & style) != 0 };
    }

    public static async Task<DrawingColor?> PickColor(Window owner, DrawingColor current)
    {
        var title = Localizer(owner).Get("Choose a color");
        var picker = WorkspacePickers.ExpandedColor(Avalonia.Media.Color.FromArgb(current.A, current.R, current.G, current.B), "dialog-color", title);
        var dialog = Create(title, owner, picker, 460, 560, out var accept);
        accept.Click += (_, _) => dialog.Close(picker.Color is { } c ? DrawingColor.FromArgb(c.A, c.R, c.G, c.B) : current);
        return await dialog.ShowDialog<DrawingColor?>(owner);
    }

    public static Task ShowError(Window owner, string message)
    {
        var body = new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var dialog = Create("EVE-O Preview", owner, body, 480, 200, out var accept);
        accept.Content = "OK";
        accept.Click += (_, _) => dialog.Close();
        if (owner != null) return dialog.ShowDialog(owner);
        var completed = new TaskCompletionSource();
        dialog.Closed += (_, _) => completed.TrySetResult();
        dialog.Show();
        return completed.Task;
    }

    private static Window Create(string title, Window owner, Control content, double width, double height, out Button accept)
    {
        var text = Localizer(owner);
        var palette = WorkspaceTheme.Get(owner is WorkspaceWindow workspace ? workspace.Backend.Read().Theme
            : owner?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark ? "Dark" : "Light");
        var dialog = new Window { Title = title, Width = width, Height = height, MinWidth = width, MinHeight = height,
            CanMinimize = false, CanMaximize = false, CanResize = true, ShowInTaskbar = owner == null,
            WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            RequestedThemeVariant = owner?.ActualThemeVariant, Background = WorkspaceTheme.Brush(palette.Background),
            Foreground = WorkspaceTheme.Brush(palette.Text), FlowDirection = text.RightToLeft ? Avalonia.Media.FlowDirection.RightToLeft : Avalonia.Media.FlowDirection.LeftToRight };
        accept = new Button { Content = text.Get("Apply"), IsDefault = true, MinWidth = 80 };
        var cancel = new Button { Content = text.Get("Cancel"), IsCancel = true, MinWidth = 80 };
        cancel.Click += (_, _) => dialog.Close();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8, Margin = new Thickness(0, 16, 0, 0), Children = { cancel, accept } };
        var panel = new DockPanel { Margin = new Thickness(20) };
        DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons); panel.Children.Add(content);
        dialog.Content = panel;
        dialog.KeyDown += (_, e) => { if (e.Key == Key.Escape) dialog.Close(); };
        return dialog;
    }

    private static WorkspaceLocalization Localizer(Window owner)
    {
        var snapshot = (owner as WorkspaceWindow)?.Backend.Read();
        return new WorkspaceLocalization(snapshot?.Theme == "Legacy" ? "en" : snapshot?.UiLanguage ?? "auto");
    }
}
