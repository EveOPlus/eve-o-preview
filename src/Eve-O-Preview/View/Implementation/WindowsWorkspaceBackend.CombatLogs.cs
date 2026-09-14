using System;
using System.Threading.Tasks;
using EveOPreview.UI;

namespace EveOPreview.View;

public sealed partial class WindowsWorkspaceBackend
{
    private readonly IWorkspaceStaticData _staticData;
    public StaticDataStatus ReadStaticData() => _staticData?.ReadStaticData() ?? new("Static data service unavailable.");
    public event Action StaticDataChanged
    {
        add { if (_staticData is not null) _staticData.StaticDataChanged += value; }
        remove { if (_staticData is not null) _staticData.StaticDataChanged -= value; }
    }
    public Task<CommandResult> UpdateStaticDataAsync() => _staticData?.UpdateStaticDataAsync()
        ?? Task.FromResult(CommandResult.Error("Static data service unavailable."));
    public void CancelStaticDataUpdate() => _staticData?.CancelStaticDataUpdate();
    public Task<CombatSimulationCatalog> ReadSimulationCatalogAsync() => _staticData?.ReadSimulationCatalogAsync()
        ?? Task.FromResult(CombatSimulationCatalog.Unavailable("Static data service unavailable."));
    private readonly IWorkspaceCombatLogs _combatLogs;
    public CombatLogSettings ReadLogSettings() => _combatLogs?.ReadLogSettings() ?? new();
    public CombatLogSnapshot ReadLogs() => _combatLogs?.ReadLogs()
        ?? new("Log service unavailable", "", DateTimeOffset.UtcNow, 10, [], []);
    public event Action LogsChanged
    {
        add { if (_combatLogs is not null) _combatLogs.LogsChanged += value; }
        remove { if (_combatLogs is not null) _combatLogs.LogsChanged -= value; }
    }
    public Task<CommandResult> SaveLogSettingsAsync(CombatLogSettings settings) => _combatLogs?.SaveLogSettingsAsync(settings)
        ?? Task.FromResult(CommandResult.Error("Log service unavailable"));
    public Task<CommandResult> ResetCombatAsync() => _combatLogs?.ResetCombatAsync()
        ?? Task.FromResult(CommandResult.Error("Log service unavailable"));
    public Task<CommandResult> ResetCombatAsync(CombatResetScope scope, string character = null) => _combatLogs?.ResetCombatAsync(scope, character)
        ?? Task.FromResult(CommandResult.Error("Log service unavailable"));
    public Task<CommandResult> RescanLogsAsync() => _combatLogs?.RescanLogsAsync()
        ?? Task.FromResult(CommandResult.Error("Log service unavailable"));
    public Task<CommandResult> SimulateLogEventAsync(CombatSimulation simulation) => _combatLogs?.SimulateLogEventAsync(simulation)
        ?? Task.FromResult(CommandResult.Error("Log service unavailable"));
}
