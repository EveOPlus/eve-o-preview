using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Configuration.Interface;
using EveOPreview.Configuration.Model;
using EveOPreview.Mediator.Messages;
using EveOPreview.Mediator.Messages.Process;
using EveOPreview.UI;
using MediatR;
using Serilog;

namespace EveOPreview.View;

/// <summary>Maps portable workspace commands to the existing presenter, profile and native services.</summary>
public sealed partial class WindowsWorkspaceBackend : IWorkspaceBackend, IWorkspacePreviewRenderer, IWorkspacePreviewCapture, IWorkspacePortraitProvider, IDisposable
{
    private readonly IWorkspacePreviewCapture _previewCapture;
    public Task<WorkspaceClientStill> CapturePreviewStillAsync(string preferredTitle) =>
        _previewCapture?.CapturePreviewStillAsync(preferredTitle) ?? Task.FromResult<WorkspaceClientStill>(null);
    private readonly IWorkspacePortraitProvider _portraits;
    public Task<byte[]> GetCharacterPortraitAsync(long characterId) => _portraits.GetCharacterPortraitAsync(characterId);

    private readonly WindowsWorkspacePreviewRenderer _previewRenderer = new();
    public IReadOnlyList<string> FontFamilies => _previewRenderer.FontFamilies;
    public WorkspacePreviewImage RenderPreview(WorkspacePreviewRequest request) => _previewRenderer.RenderPreview(request);
    private readonly IMainFormView _view;
    private readonly IAsyncSettingsView _commits;
    private readonly IMediator _mediator;
    private readonly IConfigurationStorage _storage;
    private readonly IThumbnailConfiguration _configuration;
    private readonly IProfileManager _profiles;
    private readonly ApplicationPreferences _preferences;
    private readonly ILogger _logger;
    private readonly Dictionary<string, IThumbnailDescription> _clients = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Func<object> Read, Action<string> Write)> _settings = new();
    public event Action Changed;
    public bool IsBusy { get; private set; }
    public string Version { get; set; } = "";
    public string DocumentationUrl { get; set; } = "https://github.com/EveOPlus/eve-o-preview";
    public const string DiscordUrl = "https://discord.gg/HzQHBtTEcB";
    public Size MinimumThumbnailSize { get; set; } = new(192, 108);
    public Size MaximumThumbnailSize { get; set; } = new(960, 540);

    public WindowsWorkspaceBackend(IMainFormView view, IAsyncSettingsView commits, IMediator mediator,
        IConfigurationStorage storage, IThumbnailConfiguration configuration, IProfileManager profiles,
        ApplicationPreferences preferences, ILogger logger, IWorkspacePortraitProvider portraits, IWorkspacePreviewCapture previewCapture = null)
    {
        _view = view; _commits = commits; _mediator = mediator; _storage = storage;
        _configuration = configuration; _profiles = profiles; _preferences = preferences; _logger = logger;
        _portraits = portraits;
        _previewCapture = previewCapture;
        preferences.Changed += NotifyChanged;
        configuration.CycleSkipChanged += NotifyChanged;
        BuildSettings();
    }

    public void Dispose()
    {
        _preferences.Changed -= NotifyChanged;
        _configuration.CycleSkipChanged -= NotifyChanged;
    }
    public void NotifyChanged() => Changed?.Invoke();
    public void AddClients(IList<IThumbnailDescription> clients) { foreach (var client in clients) _clients[client.Title] = client; }
    public void RemoveClients(IList<IThumbnailDescription> clients) { foreach (var client in clients) _clients.Remove(client.Title); }

    public WorkspaceSnapshot Read()
    {
        var values = _settings.ToDictionary(x => x.Key, x => Format(x.Value.Read()));
        values["ToggleHideAllActiveHotkey"] = _view.ToggleHideAllActiveHotkey ?? "";
        values["MinimizeAllClientsHotkey"] = _view.MinimizeAllClientsHotkey ?? "";
        values["ThumbnailMinimumWidth"] = Format(MinimumThumbnailSize.Width);
        values["ThumbnailMinimumHeight"] = Format(MinimumThumbnailSize.Height);
        values["ThumbnailMaximumWidth"] = Format(MaximumThumbnailSize.Width);
        values["ThumbnailMaximumHeight"] = Format(MaximumThumbnailSize.Height);
        return new(_storage.CurrentProfile?.FriendlyName ?? _view.LoadedProfileName, Version, _preferences.Theme,
            _configuration.IsTemporarilyHidingAllThumbnails, values,
            _clients.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Select(x => new ClientItem(x, !_configuration.IsThumbnailIndividuallyDisabled(x))).ToArray(),
            (_profiles.ProfileLocations ?? []).Select(x => new ProfileItem(x.FullPath, x.FriendlyName,
                x.FriendlyName.Equals("Default", StringComparison.OrdinalIgnoreCase))).ToArray(),
            (_view.CycleGroups ?? []).Select((x, i) => new CycleGroupItem(i, x.Description ?? "",
                x.ClientsOrder.Values.ToArray(), x.ForwardHotkeys.ToArray(), x.BackwardHotkeys.ToArray(),
                x.ClientsOrder.Values.Where(_configuration.IsClientCycleSkipped).ToArray())).ToArray(),
            _preferences.ThumbnailMenuOrder, _preferences.ThumbnailMenuTheme, _configuration.GetKnownClientTitles()?.ToArray(),
            _clients.Keys.Concat(_configuration.GetKnownClientTitles() ?? [])
                .Concat(_configuration.GetPriorityClientTitles() ?? []).Concat(_configuration.PerClientActiveClientHighlightColor.Keys)
                .Distinct(StringComparer.Ordinal).OrderBy(title => title, StringComparer.OrdinalIgnoreCase)
                .Select(title => new ClientPreferenceItem(title, _configuration.IsPriorityClient(title),
                    _configuration.PerClientActiveClientHighlightColor.TryGetValue(title, out var color) ? Format(color) : "")).ToArray());
    }

    public async Task<CommandResult> ExecuteAsync(WorkspaceCommand command)
    {
        // UI, profile and hotkey edits must not race each other, including reentrant capture callbacks.
        if (IsBusy) return CommandResult.Error("Wait for the current operation to finish.");
        IsBusy = true;
        try
        {
            switch (command.Action)
            {
                case "setting": return await ApplySetting(command.Target, command.Value);
                case "preview-size-limits": return await ApplyPreviewSizeLimits(command.Settings);
                case "client-preferences": return await ApplyClientPreferences(command);
                case "theme": _preferences.SetTheme(command.Value); return CommandResult.Ok("Theme saved");
                case "thumbnail-menu-move":
                    var menuOrder = _preferences.ThumbnailMenuOrder.ToList();
                    if (command.Position is not int position || !ThumbnailMenuActions.CanMove(menuOrder, command.Target, position))
                        return CommandResult.Error("Keep an action first and last; place dividers between them.");
                    menuOrder.Remove(command.Target);
                    menuOrder.Insert(position, command.Target);
                    _preferences.SetThumbnailMenuOrder(menuOrder);
                    return CommandResult.Ok("Thumbnail menu order saved for all profiles");
                case "thumbnail-menu-reset":
                    _preferences.SetThumbnailMenuOrder(ThumbnailMenuActions.DefaultOrder);
                    return CommandResult.Ok("Default thumbnail menu order restored");
                case "thumbnail-menu-divider-add":
                    var divided = _preferences.ThumbnailMenuOrder.ToList();
                    int after = divided.IndexOf(command.Target);
                    if (after < 0 || after >= divided.Count - 1 || ThumbnailMenuActions.IsDivider(command.Target) || ThumbnailMenuActions.IsDivider(divided[after + 1]))
                        return CommandResult.Error("Insert a divider between two actions.");
                    divided.Insert(after + 1, "divider:" + Guid.NewGuid().ToString("N"));
                    _preferences.SetThumbnailMenuOrder(divided);
                    return CommandResult.Ok("Divider added");
                case "thumbnail-menu-divider-remove":
                    var remaining = _preferences.ThumbnailMenuOrder.ToList();
                    if (!ThumbnailMenuActions.IsDivider(command.Target) || !remaining.Remove(command.Target))
                        return CommandResult.Error("Choose a divider to remove.");
                    _preferences.SetThumbnailMenuOrder(remaining);
                    return CommandResult.Ok("Divider removed");
                case "thumbnail-menu-theme":
                    _preferences.SetThumbnailMenuTheme(command.Value);
                    return CommandResult.Ok("Thumbnail menu theme saved for all profiles");
                case "font-picker": return await PickFont();
                case "color-picker": return await PickColor(command.Target);
                case "toggle-all": await _mediator.Send(new ThumbnailToggleHideAll()); break;
                case "minimize-all": await _mediator.Send(new MinimizeAllClients()); break;
                case "client-visible":
                    if (!_clients.TryGetValue(command.Target, out var client)) return CommandResult.Error("This client is no longer running.");
                    if (!bool.TryParse(command.Value, out bool visible)) return CommandResult.Error("Choose a valid visibility state.");
                    client.IsDisabled = !visible;
                    _configuration.ToggleThumbnail(command.Target, !visible);
                    await _mediator.Send(new SaveConfiguration());
                    break;
                case "profile-switch":
                    var profile = _profiles.ProfileLocations.FirstOrDefault(x => x.FullPath == command.Target);
                    if (profile == null) return CommandResult.Error("That profile is no longer available.");
                    await _mediator.Send(new ChangeSelectedProfile(profile));
                    if (_storage.CurrentProfile?.FullPath != profile.FullPath) return CommandResult.Error("The profile could not be loaded. Your current profile is still active.");
                    break;
                case "profile-clone":
                    int count = _profiles.ProfileLocations.Count;
                    await _mediator.Send(new CloneCurrentProfile());
                    if (_profiles.ProfileLocations.Count <= count) return CommandResult.Error("The profile could not be duplicated. Check the log and folder permissions.");
                    return CommandResult.Ok("Profile duplicated. Select the copy to edit it.");
                case "profile-rename":
                    if (IsDefaultProfile()) return CommandResult.Error("Duplicate Default before renaming it.");
                    if (!ProfileManager.IsValidProfileName(command.Value)) return CommandResult.Error("Enter a valid profile name without reserved names, trailing spaces or filename characters.");
                    if (_profiles.ProfileLocations.Any(x => x.FullPath != _storage.CurrentProfile.FullPath && x.FriendlyName.Equals(command.Value, StringComparison.OrdinalIgnoreCase)))
                        return CommandResult.Error("A profile with that name already exists.");
                    await _mediator.Send(new RenameCurrentProfile(command.Value));
                    if (_storage.CurrentProfile.FriendlyName != command.Value) return CommandResult.Error("The profile could not be renamed. Check the log and folder permissions.");
                    break;
                case "profile-delete":
                    if (IsDefaultProfile()) return CommandResult.Error("The Default profile cannot be deleted.");
                    string oldPath = _storage.CurrentProfile.FullPath;
                    await _mediator.Send(new DeleteCurrentProfile());
                    if (_profiles.ProfileLocations.Any(x => x.FullPath == oldPath)) return CommandResult.Error("The profile could not be deleted. Check the log and folder permissions.");
                    break;
                case "group-add":
                    int suffix = 1;
                    while (_view.CycleGroups.Any(x => x.Description == "Cycle group " + suffix)) suffix++;
                    _view.CycleGroups.Add(new CycleGroup { Description = "Cycle group " + suffix });
                    await Commit(); break;
                case "group-delete":
                    _view.CycleGroups.Remove(GetGroup(command.Target));
                    await Commit(); break;
                case "group-rename":
                    if (string.IsNullOrWhiteSpace(command.Value)) return CommandResult.Error("Enter a name for the cycle group.");
                    var renamedGroup = GetGroup(command.Target);
                    if (_view.CycleGroups.Any(x => x != renamedGroup && string.Equals(x.Description, command.Value.Trim(), StringComparison.OrdinalIgnoreCase)))
                        return CommandResult.Error("A cycle group with that name already exists.");
                    renamedGroup.Description = command.Value.Trim();
                    await Commit(); break;
                case "group-client-add": case "group-client-remove": case "group-client-up": case "group-client-down": case "group-client-move":
                    EditGroupClient(command); await Commit(); break;
                case "client-cycle-skip":
                    if (!_clients.ContainsKey(command.Target) && !_view.CycleGroups.Any(g => g.ClientsOrder.ContainsValue(command.Target)))
                        return CommandResult.Error("That character is no longer available.");
                    if (!bool.TryParse(command.Value, out bool skipped)) return CommandResult.Error("Choose skip or resume.");
                    await _mediator.Send(new SetClientCycleSkipped(command.Target, skipped));
                    return CommandResult.Ok(skipped ? "Character skipped in all groups for this session" : "Character resumed in all groups");
                case "hotkey-capture": case "hotkey-clear": return await EditHotkey(command);
                case "documentation": Process.Start(new ProcessStartInfo(DocumentationUrl) { UseShellExecute = true }); break;
                case "discord": Process.Start(new ProcessStartInfo(DiscordUrl) { UseShellExecute = true }); break;
                case "exit":
                    IsBusy = false;
                    _view.ApplicationExitRequested?.Invoke();
                    return CommandResult.Ok("Closing EVE-O Preview");
                default: return CommandResult.Error("This action is not available.");
            }
            return CommandResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Workspace command {Action} failed", command.Action);
            return CommandResult.Error("The change could not be completed: " + ex.GetBaseException().Message);
        }
        finally { IsBusy = false; NotifyChanged(); }
    }

    private bool IsDefaultProfile() => string.Equals(_storage.CurrentProfile?.FriendlyName, "Default", StringComparison.OrdinalIgnoreCase);

    private async Task<CommandResult> PickFont()
    {
        var current = _view.TitleFontSettings;
        using var font = new Font(current.Name, current.Size, current.Style);
        using var dialog = new System.Windows.Forms.FontDialog
        {
            Font = font, FontMustExist = true, MinSize = 1, MaxSize = 200,
            ShowEffects = true, ShowColor = false
        };
        if (dialog.ShowDialog(_view as System.Windows.Forms.IWin32Window) != System.Windows.Forms.DialogResult.OK)
            return CommandResult.Cancelled("Font selection cancelled");
        current.Name = dialog.Font.FontFamily.Name;
        current.Size = dialog.Font.Size;
        current.Style = dialog.Font.Style;
        _view.TitleFontSettings = current;
        await Commit();
        return CommandResult.Ok("Title font saved");
    }

    private async Task<CommandResult> PickColor(string key)
    {
        if (key is not ("ActiveClientHighlightColor" or "TitleFontForeColor" or "TitleFontOutlineColor" or "ProfileAccentColor"))
            return CommandResult.Error("Choose a title, highlight or profile color.");
        string current = Format(_settings[key].Read());
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            Color = string.IsNullOrEmpty(current) ? Color.CornflowerBlue : ParseColor(current),
            FullOpen = true, AnyColor = true
        };
        if (dialog.ShowDialog(_view as System.Windows.Forms.IWin32Window) != System.Windows.Forms.DialogResult.OK)
            return CommandResult.Cancelled("Color selection cancelled");
        return await ApplySetting(key, Format(dialog.Color));
    }
    private Task Commit() => _commits.CommitSettingsAsync?.Invoke() ?? throw new InvalidOperationException("The settings presenter is not ready.");

    private async Task<CommandResult> ApplySetting(string key, string value)
    {
        if (!_settings.TryGetValue(key, out var binding)) return CommandResult.Error("This setting is not available.");
        var definition = SettingCatalog.Find(key);
        if (definition == null) return CommandResult.Error("This setting has no validation rule.");
        if (key == "ThumbnailWidth") definition = definition with { Minimum = MinimumThumbnailSize.Width, Maximum = MaximumThumbnailSize.Width };
        if (key == "ThumbnailHeight") definition = definition with { Minimum = MinimumThumbnailSize.Height, Maximum = MaximumThumbnailSize.Height };
        string error = SettingCatalog.Validate(definition, value);
        if (error != null) return CommandResult.Error(error);
        if (definition.Kind == SettingKind.Toggle && !bool.TryParse(value, out _)) return CommandResult.Error("Choose on or off.");
        if (definition.Kind == SettingKind.Choice && definition.Options?.Contains(value) != true) return CommandResult.Error("Choose an available option.");
        if (PreviewSizeLimitKeys.Contains(key))
        {
            var limits = PreviewSizeLimitKeys.ToDictionary(name => name, name => Format(_settings[name].Read()));
            limits[key] = value;
            return await ApplyPreviewSizeLimits(limits);
        }
        // Explicit Apply also retries a failed disk/native update even if the in-memory value matches.
        string previous = Format(binding.Read());
        binding.Write(value);
        if (definition.Page == "AdvancedPreview")
        {
            try { await _mediator.Send(new SaveConfiguration()); }
            catch { binding.Write(previous); throw; }
            await _mediator.Publish(new ThumbnailRuntimeSettingsUpdated());
            return CommandResult.Ok("Preview settings saved and applied");
        }
        if (key == "ProfileAccentColor")
        {
            await _mediator.Send(new SaveConfiguration());
            return CommandResult.Ok("Profile accent saved");
        }
        if (key is "ThumbnailWidth" or "ThumbnailHeight")
            await (_commits.CommitSizeAsync?.Invoke() ?? throw new InvalidOperationException("The settings presenter is not ready."));
        else await Commit();
        if (key == "FpsEnabled") await _mediator.Send(new SetFpsLimiterEnabled());
        else if (key.StartsWith("Fps", StringComparison.Ordinal)) await _mediator.Send(new SetFpsLimiter());
        else if (key.StartsWith("Audio", StringComparison.Ordinal)) await _mediator.Send(new SetAudioSettings());
        return CommandResult.Ok("Settings saved; client update requested");
    }

    private CycleGroup GetGroup(string id)
    {
        if (!int.TryParse(id, out int index) || index < 0 || index >= _view.CycleGroups.Count)
            throw new ArgumentException("That cycle group is no longer available.");
        return _view.CycleGroups[index];
    }

    private void EditGroupClient(WorkspaceCommand command)
    {
        var group = GetGroup(command.Target);
        var entries = group.ClientsOrder.ToList();
        int index = entries.FindIndex(x => x.Value == command.Value);
        if (command.Action == "group-client-add")
        {
            string title = command.Value.Trim();
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Enter a character name or select a running client.");
            // Preserve any known full title, and apply the existing EVE character prefix only to offline names.
            if (!_clients.ContainsKey(title) && title != "EVE" && !title.StartsWith("EVE - ", StringComparison.Ordinal)) title = "EVE - " + title;
            if (entries.Any(x => x.Value == title)) throw new ArgumentException("That character is already in the group.");
            entries.Add(new(0, title));
        }
        else
        {
            if (index < 0) throw new ArgumentException("Select a character in this group.");
            if (command.Action == "group-client-remove") entries.RemoveAt(index);
            else if (command.Action == "group-client-move")
            {
                if (command.Position is not int position || position < 0 || position >= entries.Count)
                    throw new ArgumentException("Choose a position in this group's character order.");
                var entry = entries[index];
                entries.RemoveAt(index);
                entries.Insert(position, entry);
            }
            else
            {
                int next = index + (command.Action == "group-client-up" ? -1 : 1);
                if (next < 0 || next >= entries.Count) return;
                (entries[index], entries[next]) = (entries[next], entries[index]);
            }
        }
        group.ClientsOrder.Clear();
        for (int i = 0; i < entries.Count; i++) group.ClientsOrder[i] = entries[i].Value;
    }

    private async Task<CommandResult> EditHotkey(WorkspaceCommand command)
    {
        string current;
        Action<string> set;
        if (command.Target is "ToggleHideAllActiveHotkey" or "MinimizeAllClientsHotkey")
        {
            bool hide = command.Target == "ToggleHideAllActiveHotkey";
            current = hide ? _view.ToggleHideAllActiveHotkey : _view.MinimizeAllClientsHotkey;
            set = value => { if (hide) _view.ToggleHideAllActiveHotkey = value; else _view.MinimizeAllClientsHotkey = value; };
        }
        else
        {
            var parts = command.Target.Split(':');
            if (parts.Length != 4 || parts[0] != "group" || parts[2] is not ("forward" or "backward") ||
                !int.TryParse(parts[3], out int slot) || slot < 0 || slot > 31) return CommandResult.Error("Select a valid hotkey slot.");
            var group = GetGroup(parts[1]);
            var keys = parts[2] == "forward" ? group.ForwardHotkeys : group.BackwardHotkeys;
            current = keys.ElementAtOrDefault(slot) ?? "";
            set = value => { while (keys.Count <= slot) keys.Add(""); keys[slot] = value; };
        }
        string replacement = "";
        if (command.Action == "hotkey-capture")
        {
            // The existing handler pumps Windows messages. Yield first so the Recording status paints.
            await Task.Yield();
            var captured = await _mediator.Send(new CaptureNewHotkey(current ?? "", 10000));
            if (!captured.IsValid) return CommandResult.Error(captured.ErrorMessage ?? "No shortcut was captured.");
            if (captured.KeysCaptured == System.Windows.Forms.Keys.None) return CommandResult.Ok("Recording cancelled; shortcut unchanged");
            replacement = captured.KeyString ?? "";
        }
        set(replacement);
        await Commit();
        return CommandResult.Ok(command.Action == "hotkey-clear" ? "Shortcut cleared" : "Shortcut saved");
    }

    private static string Format(object value) => value switch
    {
        null => "", Color c => $"#{c.R:X2}{c.G:X2}{c.B:X2}",
        bool b => b ? "true" : "false", IFormattable f => f.ToString(null, CultureInfo.InvariantCulture), _ => value.ToString()
    };
    private static double Number(string value) => double.Parse(value, CultureInfo.InvariantCulture);
    private static int Integer(string value) => checked((int)Number(value));
    private static Color ParseColor(string value) => ColorTranslator.FromHtml(value);

    private void BuildSettings()
    {
        // Explicit bindings only. Never populate a profile from a UI snapshot:
        // layout dictionaries and shared nested objects must survive edits.
        Bind("EnableThumbnailSnap", () => _configuration.EnableThumbnailSnap, value => _configuration.EnableThumbnailSnap = bool.Parse(value));
        Bind("EnableCompatibilityMode", () => _configuration.EnableCompatibilityMode, value => _configuration.EnableCompatibilityMode = bool.Parse(value));
        Bind("ThumbnailRefreshPeriod", () => _configuration.ThumbnailRefreshPeriod, value => _configuration.ThumbnailRefreshPeriod = Integer(value));
        Bind("HideDelaySeconds", () => _configuration.HideThumbnailsDelay * (_configuration.ThumbnailRefreshPeriod / 1000.0),
            value => _configuration.HideThumbnailsDelay = checked((int)Math.Ceiling(Number(value) * 1000 / _configuration.ThumbnailRefreshPeriod)));
        Bind("ThumbnailMinimumWidth", () => _configuration.ThumbnailMinimumSize.Width, _ => { });
        Bind("ThumbnailMinimumHeight", () => _configuration.ThumbnailMinimumSize.Height, _ => { });
        Bind("ThumbnailMaximumWidth", () => _configuration.ThumbnailMaximumSize.Width, _ => { });
        Bind("ThumbnailMaximumHeight", () => _configuration.ThumbnailMaximumSize.Height, _ => { });
        Bind("LoginThumbnailLeft", () => _configuration.LoginThumbnailLocation.X, value => _configuration.LoginThumbnailLocation = new(Integer(value), _configuration.LoginThumbnailLocation.Y));
        Bind("LoginThumbnailTop", () => _configuration.LoginThumbnailLocation.Y, value => _configuration.LoginThumbnailLocation = new(_configuration.LoginThumbnailLocation.X, Integer(value)));
        foreach (string key in new[] { "MinimizeToTray", "EnableClientLayoutTracking", "HideActiveClientThumbnail",
            "MinimizeInactiveClients", "ShowThumbnailsAlwaysOnTop", "HideThumbnailsOnLostFocus",
            "EnablePerClientThumbnailLayouts", "EnableThumbnailZoom", "ShowThumbnailOverlays", "ShowThumbnailFrames",
            "EnableActiveClientHighlight", "EnableAutomaticCpuAffinity" })
        {
            var property = typeof(IMainFormView).GetProperty(key);
            Bind(key, () => property.GetValue(_view), value => property.SetValue(_view, bool.Parse(value)));
        }
        Bind("ThumbnailWidth", () => _view.ThumbnailSize.Width, x => _view.ThumbnailSize = new(Integer(x), _view.ThumbnailSize.Height));
        Bind("ThumbnailHeight", () => _view.ThumbnailSize.Height, x => _view.ThumbnailSize = new(_view.ThumbnailSize.Width, Integer(x)));
        Bind("ThumbnailOpacity", () => Math.Round(_view.ThumbnailOpacity * 100), x => _view.ThumbnailOpacity = Number(x) / 100);
        Bind("ThumbnailZoomFactor", () => _view.ThumbnailZoomFactor, x => _view.ThumbnailZoomFactor = Integer(x));
        Bind("ThumbnailZoomAnchor", () => _view.ThumbnailZoomAnchor, x => _view.ThumbnailZoomAnchor = Enum.Parse<ViewZoomAnchor>(x));
        Bind("ActiveClientHighlightColor", () => _view.ActiveClientHighlightColor, x => _view.ActiveClientHighlightColor = ParseColor(x));
        Bind("ActiveClientHighlightThickness", () => _configuration.ActiveClientHighlightThickness, x => _configuration.ActiveClientHighlightThickness = Integer(x));
        Bind("TitleFontName", () => _view.TitleFontSettings.Name, x =>
        {
            using var font = new Font(x, _view.TitleFontSettings.Size);
            if (!font.FontFamily.Name.Equals(x, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("That font family is not installed. Choose an installed font.");
            _view.TitleFontSettings.Name = x;
        });
        Bind("TitleFontSize", () => _view.TitleFontSettings.Size, x => _view.TitleFontSettings.Size = (float)Number(x));
        Bind("TitleFontStyle", () => _view.TitleFontSettings.Style, x => _view.TitleFontSettings.Style = Enum.Parse<FontStyle>(x));
        Bind("TitleFontForeColor", () => _view.TitleFontSettings.ForeColor, x => _view.TitleFontSettings.ForeColor = ParseColor(x));
        Bind("TitleFontOutlineColor", () => _view.TitleFontSettings.OutlineColor, x => _view.TitleFontSettings.OutlineColor = ParseColor(x));
        Bind("TitleFontOutlineWidth", () => _view.TitleFontSettings.OutlineWidth, x => _view.TitleFontSettings.OutlineWidth = (float)Number(x));
        Bind("TitleFontOffsetLeft", () => _view.TitleFontSettings.PositionOffsetFromLeft, x => _view.TitleFontSettings.PositionOffsetFromLeft = Integer(x));
        Bind("TitleFontOffsetTop", () => _view.TitleFontSettings.PositionOffsetFromTop, x => _view.TitleFontSettings.PositionOffsetFromTop = Integer(x));
        Bind("CycleSkipIndicatorStyle", () => _configuration.CycleSkipIndicatorStyle, x => _configuration.CycleSkipIndicatorStyle = x);
        Bind("CycleSkipIndicatorColor", () => _configuration.CycleSkipIndicatorColor, x => _configuration.CycleSkipIndicatorColor = ParseColor(x));
        Bind("FpsEnabled", () => _view.FpsLimiterSettings.IsEnabled, x => _view.FpsLimiterSettings.IsEnabled = bool.Parse(x));
        Bind("FpsFocused", () => _view.FpsLimiterSettings.FpsFocused, x => _view.FpsLimiterSettings.FpsFocused = Integer(x));
        Bind("FpsBackground", () => _view.FpsLimiterSettings.FpsBackground, x => _view.FpsLimiterSettings.FpsBackground = Integer(x));
        Bind("FpsPredictingFocus", () => _view.FpsLimiterSettings.FpsPredictingFocus, x => _view.FpsLimiterSettings.FpsPredictingFocus = Integer(x));
        Bind("AudioMuteJumpGateTunnel", () => _view.AudioMuteSettings.MuteJumpGateTunnel, x => _view.AudioMuteSettings.MuteJumpGateTunnel = bool.Parse(x));
        Bind("AudioMuteLocationBanner", () => _view.AudioMuteSettings.MuteLocationBanner, x => _view.AudioMuteSettings.MuteLocationBanner = bool.Parse(x));
        Bind("AudioCustomMutedEventIds", () => string.Join(", ", _view.AudioMuteSettings.CustomMutedEventIds), x =>
        {
            if (!AudioMuteSettings.TryParseCustomMutedEventIds(x, out var ids)) throw new ArgumentException("Use comma-separated unsigned decimal audio IDs.");
            _view.AudioMuteSettings.CustomMutedEventIds = ids;
        });
        Bind("ProfileAccentColor", () => _configuration.UiAccentColor, x => _configuration.UiAccentColor = x);
    }

    private void Bind(string key, Func<object> read, Action<string> write) => _settings.Add(key, (read, write));
}
