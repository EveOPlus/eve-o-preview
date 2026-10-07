namespace EveOPreview.UI;

public sealed record StaticDataStatus(string Message, int? Build = null, bool Busy = false,
    double? Progress = null, long StoredBytes = 0, int Datasets = 0, long Records = 0)
{
    public string? MessageTemplate { get; init; }
    public object?[]? MessageArguments { get; init; }
    public string Localize(Func<string, string> translate) => MessageTemplate is null ? translate(Message)
        : string.Format(System.Globalization.CultureInfo.InvariantCulture, translate(MessageTemplate), MessageArguments ?? []);
}

public interface IWorkspaceStaticData
{
    StaticDataStatus ReadStaticData();
    event Action? StaticDataChanged;
    Task<CommandResult> UpdateStaticDataAsync();
    void CancelStaticDataUpdate();
    Task<CombatSimulationCatalog> ReadSimulationCatalogAsync();
}
