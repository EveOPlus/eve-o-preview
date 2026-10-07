using Avalonia.Threading;

namespace EveOPreview.UI;

public sealed partial class WorkspaceView
{
    private bool _staticDataPromptShown;

    private void OfferStaticDataDownload()
    {
        if (_disposed || _theme.Legacy || _staticDataPromptShown || _confirmation is not null
            || _backend is not IWorkspaceStaticData data || _backend is not IWorkspaceCombatLogs logs
            || !logs.ReadLogSettings().Enabled && _pageId != "Dps" || data.ReadStaticData() is { Build: not null } or { Busy: true }) return;
        _staticDataPromptShown = true;
        Confirm("Download FC static data?", "Download game data for item lookups and combat simulation? Logs and basic augments work without it. You can download it later in Augments → Data setup.",
            "Download", async () =>
            {
                if (_modules.FirstOrDefault(x => x.Id == "Dps")?.Drafts is CombatLogDrafts drafts) drafts.Tab = 2;
                Navigate("Dps");
                var result = await data.UpdateStaticDataAsync();
                if (_disposed) return;
                _message = result.Localize(L);
                UpdateHeader();
            });
    }
}
