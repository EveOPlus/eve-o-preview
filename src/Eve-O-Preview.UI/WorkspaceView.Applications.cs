using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace EveOPreview.UI;

public sealed partial class WorkspaceView
{
    private void AddApplicationPreviews()
    {
        var rows = new StackPanel { Spacing = 10 };
        rows.Children.Add(Text("Other applications", 14, _theme.Text, true));
        foreach (string name in _snapshot.PreviewApplications ?? [])
        {
            var row = new Grid { ColumnDefinitions = new("*,Auto") };
            row.Children.Add(RawText(name + ".exe", 12, _theme.Text));
            var remove = CommandButton("Remove", new("application-remove", name), "remove-application-" + name);
            Grid.SetColumn(remove, 1); row.Children.Add(remove); rows.Children.Add(row);
        }
        var selection = new StackPanel { Spacing = 8 };
        var choose = ActionButton("Add application", async () =>
        {
            var applications = await ((IWorkspaceApplications)_backend).GetPreviewApplicationsAsync();
            if (_disposed) return;
            selection.Children.Clear();
            if (applications.Count == 0)
            {
                selection.Children.Add(Text("No other application windows found.", 12, _theme.Muted));
                return;
            }
            var picker = new ComboBox { Name = "preview-application-picker", ItemsSource = applications,
                PlaceholderText = L("Add application"), IsTextSearchEnabled = true, MaxDropDownHeight = 260,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemTemplate = new FuncDataTemplate<PreviewApplication>((application, _) => new TextBlock
                    { Text = application?.ToString(), TextTrimming = TextTrimming.CharacterEllipsis }) };
            AutomationProperties.SetName(picker, L("Search applications"));
            ScrollViewer.SetVerticalScrollBarVisibility(picker, Avalonia.Controls.Primitives.ScrollBarVisibility.Visible);
            ScrollViewer.SetHorizontalScrollBarVisibility(picker, Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
            ScrollViewer.SetAllowAutoHide(picker, false);
            selection.Children.Add(picker);
            var add = ActionButton("Add", async () =>
            {
                if (picker.SelectedItem is PreviewApplication application)
                    await Run(new("application-add", application.ProcessName));
            }, "add-preview-application", true);
            add.IsEnabled = false;
            picker.SelectionChanged += (_, _) => add.IsEnabled = picker.SelectedItem is PreviewApplication;
            selection.Children.Add(add);
            selection.Children.Add(Text("Includes all running instances of this application. Selection is saved in this profile.", 11, _theme.Muted));
        }, "choose-preview-application");
        rows.Children.Add(choose); rows.Children.Add(selection);
        _page.Children.Add(Card(rows));
    }
}
