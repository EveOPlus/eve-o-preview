using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace EveOPreview.UI;

public sealed partial class CombatLogView
{
    private Action? _staticDataChanged;
    private Control BuildStaticData()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Text("FC static data", 16, true));
        var label = Text("Static data unavailable.", 12); label.Name = "logs-static-status"; panel.Children.Add(label);
        if (_backend is not IWorkspaceStaticData data) return panel;
        var progress = new ProgressBar { Name = "logs-static-progress", Minimum = 0, Maximum = 1, Height = 5, IsVisible = false };
        panel.Children.Add(progress);
        var buttons = new WrapPanel();
        var download = new Button { Name = "logs-static-download", Content = L("Download / update"), Padding = new(12, 7), Margin = new(0, 0, 8, 0) };
        var cancel = new Button { Name = "logs-static-cancel", Content = L("Cancel download"), Padding = new(12, 7), IsVisible = false };
        // This operation has its own busy state so cancellation and all other
        // thumbnail settings remain usable throughout a large download/import.
        download.Click += async (_, _) =>
        {
            download.IsEnabled = false;
            try { var result = await data.UpdateStaticDataAsync(); if (!_disposed) Report(result); }
            catch { if (!_disposed) Report(CommandResult.Error("The static data update failed. Please retry.")); }
            finally { if (!_disposed) Refresh(); }
        };
        cancel.Click += (_, _) => data.CancelStaticDataUpdate();
        buttons.Children.Add(download); buttons.Children.Add(cancel); panel.Children.Add(buttons);
        void Refresh()
        {
            if (_disposed) return;
            var status = data.ReadStaticData();
            label.Text = status.Localize(L) + (status.Build is { } build ? "\n" + F($"Build {build} · {status.Datasets} datasets · {status.Records:N0} records · {status.StoredBytes / 1048576d:N0} MB on disk") : "");
            download.IsEnabled = !status.Busy; cancel.IsVisible = status.Busy;
            progress.IsVisible = status.Busy; progress.IsIndeterminate = status.Progress is null; progress.Value = status.Progress ?? 0;
            _refreshSimulationCatalog?.Invoke();
        }
        _staticDataChanged = () => Dispatcher.UIThread.Post(Refresh);
        data.StaticDataChanged += _staticDataChanged; Refresh(); return panel;
    }
}
