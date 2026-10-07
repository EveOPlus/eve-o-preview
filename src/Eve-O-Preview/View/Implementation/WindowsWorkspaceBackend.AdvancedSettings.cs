using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using EveOPreview.Mediator.Messages;
using EveOPreview.UI;

namespace EveOPreview.View;

public sealed partial class WindowsWorkspaceBackend
{
    private static readonly string[] PreviewSizeLimitKeys =
        ["ThumbnailMinimumWidth", "ThumbnailMinimumHeight", "ThumbnailMaximumWidth", "ThumbnailMaximumHeight"];

    private async Task<CommandResult> ApplyPreviewSizeLimits(IReadOnlyDictionary<string, string> values)
    {
        foreach (string key in PreviewSizeLimitKeys)
        {
            if (values == null || !values.TryGetValue(key, out string value)) return CommandResult.Error("Enter all four preview size limits.");
            if (SettingCatalog.Validate(SettingCatalog.Find(key), value) is string error) return CommandResult.Error(error);
        }
        var minimum = new Size(Integer(values[PreviewSizeLimitKeys[0]]), Integer(values[PreviewSizeLimitKeys[1]]));
        var maximum = new Size(Integer(values[PreviewSizeLimitKeys[2]]), Integer(values[PreviewSizeLimitKeys[3]]));
        if (minimum.Width > maximum.Width || minimum.Height > maximum.Height)
            return CommandResult.Error("Minimum dimensions cannot exceed the maximum dimensions.");
        var oldMinimum = _configuration.ThumbnailMinimumSize;
        var oldMaximum = _configuration.ThumbnailMaximumSize;
        var oldSize = _view.ThumbnailSize;
        void Update(Size min, Size max, Size size)
        {
            _configuration.ThumbnailMinimumSize = MinimumThumbnailSize = min;
            _configuration.ThumbnailMaximumSize = MaximumThumbnailSize = max;
            _configuration.ThumbnailSize = size;
            _view.SetThumbnailSizeLimitations(min, max);
            _view.ThumbnailSize = size;
        }
        var resized = new Size(Math.Clamp(oldSize.Width, minimum.Width, maximum.Width), Math.Clamp(oldSize.Height, minimum.Height, maximum.Height));
        try
        {
            Update(minimum, maximum, resized);
            await _mediator.Send(new SaveConfiguration());
        }
        catch { Update(oldMinimum, oldMaximum, oldSize); throw; }
        await _mediator.Publish(new ThumbnailRuntimeSettingsUpdated());
        return CommandResult.Ok(resized == oldSize ? "Resize limits saved and applied" : "Resize limits applied; preview size adjusted to fit");
    }

    private async Task<CommandResult> ApplyClientPreferences(WorkspaceCommand command)
    {
        string title = command.Target;
        if (!_clients.ContainsKey(title) && !_configuration.PerClientActiveClientHighlightColor.ContainsKey(title)
            && !(_configuration.GetKnownClientTitles() ?? []).Contains(title) && !(_configuration.GetPriorityClientTitles() ?? []).Contains(title))
        {
            title = title.Trim();
            if (title.Length == 0 || title == "EVE -") return CommandResult.Error("Enter a character name after EVE -.");
            if (title != "EVE" && !title.StartsWith("EVE - ", StringComparison.Ordinal)) title = "EVE - " + title;
        }
        if (string.IsNullOrWhiteSpace(title) || title == "EVE - " || title.Any(char.IsControl))
            return CommandResult.Error("Choose a character or enter an offline character name.");
        if (command.Settings == null || !command.Settings.TryGetValue("Priority", out var value) || !bool.TryParse(value, out bool priority))
            return CommandResult.Error("Choose whether this character stays open when switching.");
        if (command.Value.Length > 0 && SettingCatalog.Validate(SettingCatalog.Find("ActiveClientHighlightColor"), command.Value) is string error)
            return CommandResult.Error(error);
        bool hadColor = _configuration.PerClientActiveClientHighlightColor.TryGetValue(title, out var oldColor);
        bool wasPriority = _configuration.IsPriorityClient(title);
        try
        {
            _configuration.SetPriorityClient(title, priority);
            if (command.Value.Length == 0) _configuration.PerClientActiveClientHighlightColor.Remove(title);
            else _configuration.PerClientActiveClientHighlightColor[title] = ParseColor(command.Value);
            await _mediator.Send(new SaveConfiguration());
        }
        catch
        {
            _configuration.SetPriorityClient(title, wasPriority);
            if (hadColor) _configuration.PerClientActiveClientHighlightColor[title] = oldColor;
            else _configuration.PerClientActiveClientHighlightColor.Remove(title);
            throw;
        }
        await _mediator.Publish(new ThumbnailRuntimeSettingsUpdated());
        return CommandResult.OkFormat($"Character settings saved for {title}");
    }
}
