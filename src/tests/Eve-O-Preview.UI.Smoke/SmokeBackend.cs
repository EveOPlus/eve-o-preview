using EveOPreview.UI;

namespace EveOPreview.UI.Smoke;

// Representative data only; this executable cannot discover clients, inject Robin,
// write profiles, access credentials, or change native windows.
internal sealed class SmokeBackend : IWorkspaceBackend, IWorkspacePortraitProvider
{
    private readonly Dictionary<string, HashSet<string>> _skips = new();
    private readonly TaskCompletionSource<byte[]?> _portrait = new();
    public List<long> PortraitRequests { get; } = new();
    public Task<byte[]?> GetCharacterPortraitAsync(long characterId)
    {
        PortraitRequests.Add(characterId);
        return _portrait.Task;
    }
    public void CompletePortrait()
    {
        using var resource = typeof(SmokeBackend).Assembly.GetManifestResourceStream("Eve-O-Preview.UI.Smoke.Fixtures.aura-asuna.jpg")!;
        using var bytes = new MemoryStream();
        resource.CopyTo(bytes);
        _portrait.SetResult(bytes.ToArray());
    }

    private WorkspaceSnapshot _snapshot = new(
        "Multibox fleet", "10.1.0.13", "Light", false,
        new Dictionary<string, string>
        {
            ["MinimizeToTray"] = "true",
            ["ProfileAccentColor"] = "#6D9FFF",
            ["ThumbnailRefreshPeriod"] = "500",
            ["HideDelaySeconds"] = "1",
            ["ThumbnailMinimumWidth"] = "192",
            ["ThumbnailMinimumHeight"] = "108",
            ["ThumbnailMaximumWidth"] = "960",
            ["ThumbnailMaximumHeight"] = "540",
            ["LoginThumbnailLeft"] = "0",
            ["LoginThumbnailTop"] = "0",
            ["EnableCompatibilityMode"] = "false",
            ["ThumbnailOpacity"] = "95",
            ["ShowThumbnailsAlwaysOnTop"] = "true",
            ["ThumbnailWidth"] = "320",
            ["ThumbnailHeight"] = "180",
            ["EnableThumbnailSnap"] = "true",
            ["EnableThumbnailZoom"] = "true",
            ["ThumbnailZoomFactor"] = "3",
            ["ThumbnailZoomAnchor"] = "SE",
            ["ShowThumbnailOverlays"] = "true",
            ["ShowThumbnailFrames"] = "true",
            ["EnableActiveClientHighlight"] = "true",
            ["ActiveClientHighlightColor"] = "#72C9FF",
            ["ActiveClientHighlightThickness"] = "3",
            ["TitleFontName"] = "Segoe UI",
            ["TitleFontSize"] = "11",
            ["TitleFontForeColor"] = "#FFFFFF",
            ["TitleFontOutlineColor"] = "#000000",
            ["TitleFontStyle"] = "Bold",
            ["TitleFontOutlineWidth"] = "1",
            ["TitleFontOffsetLeft"] = "6",
            ["TitleFontOffsetTop"] = "6",
            ["CycleSkipIndicatorStyle"] = "Circle with slash",
            ["CycleSkipIndicatorColor"] = "#FF0000",
            ["FpsEnabled"] = "true",
            ["FpsFocused"] = "144",
            ["FpsBackground"] = "30",
            ["FpsPredictingFocus"] = "60",
            ["EnableAutomaticCpuAffinity"] = "true",
            ["AudioMuteJumpGateTunnel"] = "true",
            ["AudioCustomMutedEventIds"] = "12345, 67890",
            ["ToggleHideAllActiveHotkey"] = "Control + Alt + H",
            ["MinimizeAllClientsHotkey"] = "Control + Alt + M"
        },
        new ClientItem[]
        {
            new("EVE - Aura Asuna", true),
            new("EVE - Sera Voss", true),
            new("EVE - Kaelen Orin", false),
            new("EVE - A character with a deliberately long name", true)
        },
        new ProfileItem[]
        {
            new("default", "Default", true),
            new("fleet", "Multibox fleet", false),
            new("exploration", "Exploration and scouting", false)
        },
        new CycleGroupItem[]
        {
            new(0, "Combat wing", new[] { "EVE - Aura Asuna", "EVE - Sera Voss", "EVE - Kaelen Orin" },
                new[] { "Control + Tab", "" }, new[] { "Control + Shift + Tab", "" }),
            new(1, "Scouts and support", new[] { "EVE - A character with a deliberately long name" },
                new[] { "Alt + Tab", "" }, new[] { "", "" })
        });

    public event Action? Changed;
    private readonly Dictionary<string, string> _profileAccents = new()
    {
        ["Multibox fleet"] = "#6D9FFF",
        ["Default"] = "",
        ["Exploration and scouting"] = "#F2B56E"
    };
    public List<WorkspaceCommand> Commands { get; } = new();
    public WorkspaceSnapshot Read() => _snapshot with { CycleGroups = _snapshot.CycleGroups.Select(g => g with
        { SkippedClients = g.Clients.Where(title => _skips.GetValueOrDefault(_snapshot.ProfileName)?.Contains(title) == true).ToArray() }).ToArray() };

    public void PrepareCycleOrder()
    {
        _skips.Clear();
        var clients = new[] { "EVE - Aura Asuna", "EVE - Sera Voss", "EVE - Kaelen Orin" }
            .Concat(Enumerable.Range(4, 12).Select(i => "EVE - Reserve pilot " + i)).ToArray();
        _snapshot = _snapshot with { CycleGroups = _snapshot.CycleGroups.Select(g => g with { Clients = g.Id == 0 ? clients : new[] { "EVE - Aura Asuna" } }).ToArray() };
        Changed?.Invoke();
    }

    public Task<CommandResult> ExecuteAsync(WorkspaceCommand command)
    {
        Commands.Add(command);
        switch (command.Action)
        {
            case "thumbnail-menu-move":
                var menuOrder = ThumbnailMenuActions.Normalize(_snapshot.ThumbnailMenuOrder).ToList();
                if (!ThumbnailMenuActions.CanMove(menuOrder, command.Target, command.Position ?? -1)) return Task.FromResult(CommandResult.Error("Keep actions first and last."));
                menuOrder.Remove(command.Target);
                menuOrder.Insert(command.Position!.Value, command.Target);
                _snapshot = _snapshot with { ThumbnailMenuOrder = menuOrder };
                break;
            case "thumbnail-menu-reset":
                _snapshot = _snapshot with { ThumbnailMenuOrder = ThumbnailMenuActions.DefaultOrder };
                break;
            case "thumbnail-menu-divider-add":
                var divided = ThumbnailMenuActions.Normalize(_snapshot.ThumbnailMenuOrder).ToList();
                divided.Insert(divided.IndexOf(command.Target) + 1, "divider:" + Guid.NewGuid().ToString("N"));
                _snapshot = _snapshot with { ThumbnailMenuOrder = divided };
                break;
            case "thumbnail-menu-divider-remove":
                _snapshot = _snapshot with { ThumbnailMenuOrder = ThumbnailMenuActions.Normalize(_snapshot.ThumbnailMenuOrder).Where(id => id != command.Target).ToArray() };
                break;
            case "thumbnail-menu-theme":
                _snapshot = _snapshot with { ThumbnailMenuTheme = command.Value };
                break;
            case "client-cycle-skip":
                if (!_skips.TryGetValue(_snapshot.ProfileName, out var skips)) _skips[_snapshot.ProfileName] = skips = new(StringComparer.Ordinal);
                if (bool.Parse(command.Value)) skips.Add(command.Target); else skips.Remove(command.Target);
                break;
            case "group-client-move": case "group-client-up": case "group-client-down": case "group-client-remove":
                var group = _snapshot.CycleGroups.Single(g => g.Id.ToString() == command.Target);
                var members = group.Clients.ToList();
                int source = members.IndexOf(command.Value);
                if (command.Action == "group-client-remove") members.RemoveAt(source);
                else
                {
                    int destination = command.Position ?? (source + (command.Action == "group-client-up" ? -1 : 1));
                    if (destination >= 0 && destination < members.Count) { members.RemoveAt(source); members.Insert(destination, command.Value); }
                }
                _snapshot = _snapshot with { CycleGroups = _snapshot.CycleGroups.Select(g => g.Id == group.Id ? g with { Clients = members } : g).ToArray() };
                break;
            case "theme":
                _snapshot = _snapshot with { Theme = command.Value };
                break;
            case "setting":
                var settings = new Dictionary<string, string>(_snapshot.Settings)
                {
                    [command.Target] = command.Value
                };
                _snapshot = _snapshot with { Settings = settings };
                if (command.Target == "ProfileAccentColor") _profileAccents[_snapshot.ProfileName] = command.Value;
                break;
            case "preview-size-limits":
                var resized = new Dictionary<string, string>(_snapshot.Settings);
                foreach (var entry in command.Settings!) resized[entry.Key] = entry.Value;
                _snapshot = _snapshot with { Settings = resized };
                break;
            case "client-preferences":
                var preferences = (_snapshot.ClientPreferences ?? []).Where(entry => entry.Title != command.Target).ToList();
                preferences.Add(new(command.Target, bool.Parse(command.Settings!["Priority"]), command.Value));
                _snapshot = _snapshot with { ClientPreferences = preferences };
                break;
            case "toggle-all":
                _snapshot = _snapshot with { AllPreviewsHidden = !_snapshot.AllPreviewsHidden };
                break;
            case "client-visible":
                _snapshot = _snapshot with
                {
                    Clients = _snapshot.Clients.Select(client => client.Title == command.Target
                        ? client with { PreviewVisible = bool.Parse(command.Value) } : client).ToArray()
                };
                break;
            case "profile-switch":
                string profileName = _snapshot.Profiles.Single(profile => profile.Id == command.Target).Name;
                _snapshot = _snapshot with
                {
                    ProfileName = profileName,
                    Settings = new Dictionary<string, string>(_snapshot.Settings)
                    {
                        ["ProfileAccentColor"] = _profileAccents[profileName]
                    }
                };
                break;
        }
        Changed?.Invoke();
        return Task.FromResult(CommandResult.Ok());
    }
}
