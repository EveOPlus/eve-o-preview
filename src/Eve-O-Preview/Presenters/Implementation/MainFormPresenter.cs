//Eve-O Preview Plus is a program designed to deliver quality of life tooling. Primarily but not limited to enabling rapid window foreground and focus changes for the online game Eve Online.
//Copyright (C) 2026  Aura Asuna
//
//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.
//
//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.
//
//You should have received a copy of the GNU General Public License
//along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Interface;
using EveOPreview.Configuration.Model;
using EveOPreview.Mediator.Messages;
using EveOPreview.Mediator.Messages.Process;
using EveOPreview.Services.Interface;
using EveOPreview.View;
using MediatR;
using Serilog;

namespace EveOPreview.Presenters
{
    public class MainFormPresenter : Presenter<IMainFormView>, IMainFormPresenter
    {
        #region Private constants
        private const string DISCORD_URL = @"https://discord.gg/HzQHBtTEcB";
        #endregion

        #region Private fields
        private readonly IMediator _mediator;
        private readonly IThumbnailConfiguration _configuration;
        private readonly IConfigurationStorage _configurationStorage;
        private readonly IGlobalEvents _globalEvents;
        private readonly IProfileManager _profileManager;
        private readonly IDictionary<string, IThumbnailDescription> _descriptionsCache;
        private readonly ILogger _logger;
        private bool _suppressSizeNotifications;

        private bool _exitApplication;
        #endregion

        public MainFormPresenter(
            IApplicationController controller, 
            IMainFormView view, 
            IMediator mediator, 
            IThumbnailConfiguration configuration, 
            IConfigurationStorage configurationStorage,
            IGlobalEvents globalEvents,
            IProfileManager profileManager,
            ILogger logger)
            : base(controller, view)
        {
            this._mediator = mediator;
            this._configuration = configuration;
            this._configurationStorage = configurationStorage;
            _globalEvents = globalEvents;
            _profileManager = profileManager;
            _logger = logger;
            
            _logger.Verbose("MainFormPresenter: Constructor initializing");
            
            _globalEvents.CurrentProfileChanged += HandleSelectedProfileChangedNotification;
            _globalEvents.ProfileListChanged += HandleProfileListChangedNotification;

            this._descriptionsCache = new Dictionary<string, IThumbnailDescription>();
            lock (this._descriptionsCache)
            {
                this._descriptionsCache.Clear();
            }

            this._suppressSizeNotifications = false;
            this._exitApplication = false;

            this.View.FormActivated = this.Activate;
            this.View.FormMinimized = this.Minimize;
            this.View.FormCloseRequested = this.Close;
            this.View.ApplicationSettingsChanged = this.SaveApplicationSettings;
            this.View.ThumbnailsSizeChanged = this.UpdateThumbnailsSize;
            this.View.ThumbnailStateChanged = this.UpdateThumbnailState;
            this.View.DocumentationLinkActivated = this.OpenDocumentationLink;
            this.View.ApplicationExitRequested = this.ExitApplication;
            this.View.GetClientNameFromInput = this.GetClientDescriptionFromInputBox;
            this.View.CaptureNewHotkey = this.SendCaptureNewHotkeyRequest;
            this.View.FpsLimiterChanged = this.TriggerSetFpsLimiter;
            this.View.FpsLimiterEnabledChanged = this.TriggerSetFpsLimiterEnabled;
            this.View.AudioSettingsChanged = this.TriggerSetAudioSettings;
            this.View.ToggleHideAllActiveClients = this.TriggerToggleHideAllActiveClients;
            this.View.MinimizeAllClients = this.TriggerMinimizeAllClientsHotkey;
            this.View.SwitchToProfile = this.ActionSwitchToNewProfile;
            this.View.CloneCurrentProfile = this.ActionCloneCurrentProfile;
            this.View.DeleteCurrentProfile = this.ActionDeleteCurrentProfile;
            this.View.RenameCurrentProfile = this.RenameCurrentProfile;

            var currentProfile = _mediator.Send(new GetCurrentProfileLocation()).Result;
            _logger.Verbose("MainFormPresenter: Current profile retrieved: {ProfilePath}", currentProfile?.FullPath ?? "(null)");
            _mediator.Send(new ChangeSelectedProfile(currentProfile)).GetAwaiter().GetResult();
            _profileManager.RefreshProfileLocations();
            _logger.Verbose("MainFormPresenter: Constructor completed");
        }

        private void RenameCurrentProfile(string newProfileName)
        {
            _logger.Verbose("MainFormPresenter.RenameCurrentProfile: Renaming profile to {NewName}", newProfileName);
            _mediator.Send(new RenameCurrentProfile(newProfileName));
        }

        private void ActionDeleteCurrentProfile()
        {
            _logger.Verbose("MainFormPresenter.ActionDeleteCurrentProfile: Deleting current profile");
            _mediator.Send(new DeleteCurrentProfile());

        }

        private void ActionCloneCurrentProfile()
        {
            _logger.Verbose("MainFormPresenter.ActionCloneCurrentProfile: Cloning current profile");
            _mediator.Send(new CloneCurrentProfile());
        }

        public void HandleSelectedProfileChangedNotification(SelectedProfileChangedNotification notification)
        {
            _logger.Verbose("MainFormPresenter.HandleSelectedProfileChangedNotification: Profile changed notification received");
            ReloadApplicationSettings();
        }

        private void ActionSwitchToNewProfile(ProfileLocation newProfileLocation)
        {
            _logger.Verbose("MainFormPresenter.ActionSwitchToNewProfile: Switching to profile {ProfilePath}", newProfileLocation?.FullPath ?? "(null)");
            _mediator.Send(new ChangeSelectedProfile(newProfileLocation));
        }

        public void HandleThumbnailToggleHideAllChangedNotification(ThumbnailToggleHideAllChangedNotification notification)
        {
            _logger.Verbose("MainFormPresenter.HandleThumbnailToggleHideAllChanged: IsHidden={IsHidden}", notification.IsHidden);
            this.View.UpdateThumbnailToggleHideAllStatus(notification.IsHidden);
        }

        public void HandleProfileListChangedNotification(ProfileListChangedNotification notification)
        {
            _logger.Verbose("MainFormPresenter.HandleProfileListChanged: Updating profile list in view");
            this.View.UpdateProfileList(notification.NewProfileLocations);
        }

        private CaptureNewHotkeyResponse SendCaptureNewHotkeyRequest(string currentKey)
        {
            _logger.Verbose("MainFormPresenter.SendCaptureNewHotkeyRequest: Capturing hotkey. Current: {CurrentKey}", currentKey);
            var response = _mediator.Send(new CaptureNewHotkey(currentKey, 10000)).ConfigureAwait(false).GetAwaiter().GetResult();
            _logger.Verbose("MainFormPresenter.SendCaptureNewHotkeyRequest: Hotkey capture result: Valid={IsValid}, Captured={KeyString}", response.IsValid, response.KeyString);
            return response;
        }

        private void Activate()
        {
            _logger.Verbose("MainFormPresenter.Activate: Activating main form");
            this._suppressSizeNotifications = true;
            this.LoadApplicationSettings();
            this.View.SetDocumentationUrl(MainFormPresenter.DISCORD_URL);
            this.View.SetVersionInfo(this.GetApplicationVersion());
            if (this._configuration.MinimizeToTray)
            {
                _logger.Verbose("MainFormPresenter.Activate: Minimizing to tray on startup");
                this.View.Minimize();
            }

            _logger.Verbose("MainFormPresenter.Activate: Starting service");
            this._mediator.Send(new StartService());
            this._suppressSizeNotifications = false;
            _logger.Verbose("MainFormPresenter.Activate: Activation complete");
        }

        private void Minimize()
        {
            if (!this._configuration.MinimizeToTray)
            {
                _logger.Verbose("MainFormPresenter.Minimize: MinimizeToTray disabled, skipping minimize");
                return;
            }

            _logger.Verbose("MainFormPresenter.Minimize: Minimizing to tray");
            this.View.Hide();
        }

        private void Close(ViewCloseRequest request)
        {
            _logger.Verbose("MainFormPresenter.Close: Close requested. ExitApplication={ExitApplication}, MinimizeToTray={MinimizeToTray}", this._exitApplication, this.View.MinimizeToTray);
            
            if (this._exitApplication || !this.View.MinimizeToTray)
            {
                _logger.Verbose("MainFormPresenter.Close: Performing full application shutdown");
                // we are closing so be careful not to block on the UI main thread
                Task.Run(() => this._mediator.Send(new StopService())).GetAwaiter().GetResult();

                this._configurationStorage.Save();
                request.Allow = true;
                _logger.Verbose("MainFormPresenter.Close: Application shutdown complete");
                return;
            }

            _logger.Verbose("MainFormPresenter.Close: Minimizing instead of closing");
            request.Allow = false;
            this.View.Minimize();
        }

        private async void UpdateThumbnailsSize()
        {
            if (!this._suppressSizeNotifications)
            {
                _logger.Verbose("MainFormPresenter.UpdateThumbnailsSize: Thumbnail size changed, saving settings");
                this.SaveApplicationSettings();
                await this._mediator.Publish(new ThumbnailConfiguredSizeUpdated());
            }
        }

        private void LoadApplicationSettings()
        {
            _logger.Verbose("MainFormPresenter.LoadApplicationSettings: Loading configuration from storage");
            this._configurationStorage.Load();
            ReloadApplicationSettings();
            _logger.Verbose("MainFormPresenter.LoadApplicationSettings: Configuration loaded and reloaded to UI");
        }

        private void ReloadApplicationSettings()
        {
            _logger.Verbose("MainFormPresenter.ReloadApplicationSettings: Reloading all settings to UI components");
            this.View.CycleGroups = this._configuration.CycleGroups;

            this.View.MinimizeToTray = this._configuration.MinimizeToTray;

            this.View.ThumbnailOpacity = this._configuration.ThumbnailOpacity;

            this.View.EnableClientLayoutTracking = this._configuration.EnableClientLayoutTracking;
            this.View.HideActiveClientThumbnail = this._configuration.HideActiveClientThumbnail;
            this.View.MinimizeInactiveClients = this._configuration.MinimizeInactiveClients;
            this.View.ShowThumbnailsAlwaysOnTop = this._configuration.ShowThumbnailsAlwaysOnTop;
            this.View.HideThumbnailsOnLostFocus = this._configuration.HideThumbnailsOnLostFocus;
            this.View.EnablePerClientThumbnailLayouts = this._configuration.EnablePerClientThumbnailLayouts;

            this.View.SetThumbnailSizeLimitations(this._configuration.ThumbnailMinimumSize, this._configuration.ThumbnailMaximumSize);
            this.View.ThumbnailSize = this._configuration.ThumbnailSize;

            this.View.EnableThumbnailZoom = this._configuration.ThumbnailZoomEnabled;
            this.View.ThumbnailZoomFactor = this._configuration.ThumbnailZoomFactor;
            this.View.ThumbnailZoomAnchor = ViewZoomAnchorConverter.Convert(this._configuration.ThumbnailZoomAnchor);

            this.View.ShowThumbnailOverlays = this._configuration.ShowThumbnailOverlays;
            this.View.ShowThumbnailFrames = this._configuration.ShowThumbnailFrames;
            this.View.EnableActiveClientHighlight = this._configuration.EnableActiveClientHighlight;
            this.View.ActiveClientHighlightColor = this._configuration.ActiveClientHighlightColor;
            this.View.TitleFontSettings = this._configuration.TitleFontSettings;
            this.View.ToggleHideAllActiveHotkey = this._configuration.ToggleHideActiveClientsHotkey;
            this.View.MinimizeAllClientsHotkey = this._configuration.MinimizeAllClientsHotkey;
            this.View.IsPremium = this._configuration.IsPremium;

            this.View.FpsLimiterSettings = this._configuration.FpsLimiterSettings;
            this.View.AudioMuteSettings = this._configuration.AudioMuteSettings;
            this.View.LoadedProfileName = this._configurationStorage.CurrentProfile.FriendlyName;
            this.View.EnableAutomaticCpuAffinity = this._configuration.EnableAutomaticCpuAffinity;
        }

        private async void SaveApplicationSettings()
        {
            _logger.Verbose("MainFormPresenter.SaveApplicationSettings: Saving all application settings");
            this._configuration.CycleGroups = this.View.CycleGroups;

            this._configuration.MinimizeToTray = this.View.MinimizeToTray;

            this._configuration.ThumbnailOpacity = (float)this.View.ThumbnailOpacity;

            this._configuration.EnableClientLayoutTracking = this.View.EnableClientLayoutTracking;
            this._configuration.HideActiveClientThumbnail = this.View.HideActiveClientThumbnail;
            this._configuration.MinimizeInactiveClients = this.View.MinimizeInactiveClients;
            this._configuration.ShowThumbnailsAlwaysOnTop = this.View.ShowThumbnailsAlwaysOnTop;
            this._configuration.HideThumbnailsOnLostFocus = this.View.HideThumbnailsOnLostFocus;
            this._configuration.EnablePerClientThumbnailLayouts = this.View.EnablePerClientThumbnailLayouts;

            this._configuration.ThumbnailSize = this.View.ThumbnailSize;

            this._configuration.ThumbnailZoomEnabled = this.View.EnableThumbnailZoom;
            this._configuration.ThumbnailZoomFactor = this.View.ThumbnailZoomFactor;
            this._configuration.ThumbnailZoomAnchor = ViewZoomAnchorConverter.Convert(this.View.ThumbnailZoomAnchor);

            this._configuration.ShowThumbnailOverlays = this.View.ShowThumbnailOverlays;
            if (this._configuration.ShowThumbnailFrames != this.View.ShowThumbnailFrames)
            {
                _logger.Verbose("MainFormPresenter.SaveApplicationSettings: Thumbnail frame settings changed");
                this._configuration.ShowThumbnailFrames = this.View.ShowThumbnailFrames;
                await this._mediator.Publish(new ThumbnailFrameSettingsUpdated());
            }

            this._configuration.EnableActiveClientHighlight = this.View.EnableActiveClientHighlight;
            this._configuration.ActiveClientHighlightColor = this.View.ActiveClientHighlightColor;

            this._configuration.TitleFontSettings = this.View.TitleFontSettings;
            this._configuration.ToggleHideActiveClientsHotkey = this.View.ToggleHideAllActiveHotkey;
            this._configuration.MinimizeAllClientsHotkey = this.View.MinimizeAllClientsHotkey;

            this._configuration.EnableAutomaticCpuAffinity = this.View.EnableAutomaticCpuAffinity;

            //this._configurationStorage.Save();

            this.View.RefreshZoomSettings();

            await this._mediator.Publish(new ThumbnailFontTitleSettingsUpdated());
            
            await this._mediator.Send(new RefreshHotkeys());
            await this._mediator.Send(new SaveConfiguration());

            if (!this._configuration.EnableAutomaticCpuAffinity)
            {
                _logger.Verbose("MainFormPresenter.SaveApplicationSettings: CPU affinity disabled, resetting all");
                await this._mediator.Send(new ResetAllCpuAffinity());
            }
            
            _logger.Verbose("MainFormPresenter.SaveApplicationSettings: Settings saved complete");
        }

        public void AddThumbnails(IList<string> thumbnailTitles)
        {
            _logger.Verbose("MainFormPresenter.AddThumbnails: Adding {ThumbnailCount} thumbnails", thumbnailTitles.Count);
            IList<IThumbnailDescription> descriptions = new List<IThumbnailDescription>(thumbnailTitles.Count);

            lock (this._descriptionsCache)
            {
                foreach (string title in thumbnailTitles)
                {
                    IThumbnailDescription description = this.CreateThumbnailDescription(title);
                    this._descriptionsCache[title] = description;

                    descriptions.Add(description);
                    _logger.Verbose("MainFormPresenter.AddThumbnails: Added {Title} (IsDisabled={IsDisabled})", title, description.IsDisabled);
                }
            }

            this.View.AddThumbnails(descriptions);
        }

        public void RemoveThumbnails(IList<string> thumbnailTitles)
        {
            _logger.Verbose("MainFormPresenter.RemoveThumbnails: Removing {ThumbnailCount} thumbnails", thumbnailTitles.Count);
            IList<IThumbnailDescription> descriptions = new List<IThumbnailDescription>(thumbnailTitles.Count);

            lock (this._descriptionsCache)
            {
                foreach (string title in thumbnailTitles)
                {
                    if (!this._descriptionsCache.TryGetValue(title, out IThumbnailDescription description))
                    {
                        _logger.Warning("MainFormPresenter.RemoveThumbnails: Thumbnail {Title} not found in cache", title);
                        continue;
                    }

                    this._descriptionsCache.Remove(title);
                    descriptions.Add(description);
                    _logger.Verbose("MainFormPresenter.RemoveThumbnails: Removed {Title}", title);
                }
            }

            this.View.RemoveThumbnails(descriptions);
        }

        private IThumbnailDescription CreateThumbnailDescription(string title)
        {
            bool isDisabled = this._configuration.IsThumbnailDisabled(title);
            _logger.Verbose("MainFormPresenter.CreateThumbnailDescription: {Title} (IsDisabled={IsDisabled})", title, isDisabled);
            return new ThumbnailDescription(title, isDisabled);
        }

        private async void UpdateThumbnailState(String title)
        {
            _logger.Verbose("MainFormPresenter.UpdateThumbnailState: Updating state for {Title}", title);
            bool exists;
            IThumbnailDescription description;

            lock (_descriptionsCache)
            {
                exists = this._descriptionsCache.TryGetValue(title, out description);
            }
            
            if (exists)
            {
                _logger.Verbose("MainFormPresenter.UpdateThumbnailState: {Title} disabled state toggled to {IsDisabled}", title, description.IsDisabled);
                this._configuration.ToggleThumbnail(title, description.IsDisabled);
            }
            else
            {
                _logger.Warning("MainFormPresenter.UpdateThumbnailState: {Title} not found in cache", title);
            }

            await this._mediator.Send(new SaveConfiguration());
        }

        public void UpdateThumbnailSize(Size size)
        {
            _logger.Verbose("MainFormPresenter.UpdateThumbnailSize: Setting size to {Width}x{Height}", size.Width, size.Height);
            this._suppressSizeNotifications = true;
            this.View.ThumbnailSize = size;
            this._suppressSizeNotifications = false;
        }

        public string GetClientDescriptionFromInputBox()
        {
            _logger.Verbose("MainFormPresenter.GetClientDescriptionFromInputBox: Opening client selection dialog");
            var input = new ClientNameInputBox();
            lock (_descriptionsCache)
            {
                input.LoadKnownClients(_descriptionsCache.Keys.ToList());
            }

            input.ShowDialog();

            _logger.Verbose("MainFormPresenter.GetClientDescriptionFromInputBox: User selected {SelectedClient}", input.SelectedClientName ?? "(cancelled)");
            return input.SelectedClientName;
        }

        private void OpenDocumentationLink()
        {
            _logger.Verbose("MainFormPresenter.OpenDocumentationLink: Opening Discord documentation link");
            // TODO Move out to a separate service / presenter / message handler
            ProcessStartInfo processStartInfo = new ProcessStartInfo(new Uri(MainFormPresenter.DISCORD_URL).AbsoluteUri);
            Process.Start(processStartInfo);
        }

        private string GetApplicationVersion()
        {
            var version = System.Windows.Forms.Application.ProductVersion;
            _logger.Verbose("MainFormPresenter.GetApplicationVersion: {Version}", version);
            return version;
        }

        private void TriggerSetFpsLimiter()
        {
            _logger.Verbose("MainFormPresenter.TriggerSetFpsLimiter: Triggering FPS limiter update");
            this._mediator.Send(new SetFpsLimiter());
        }

        private void TriggerSetFpsLimiterEnabled()
        {
            _logger.Verbose("MainFormPresenter.TriggerSetFpsLimiterEnabled: Toggling FPS limiter enabled state");
            this._mediator.Send(new SetFpsLimiterEnabled());
        }

        private void TriggerSetAudioSettings()
        {
            _logger.Verbose("MainFormPresenter.TriggerSetAudioSettings: Triggering audio settings update");
            this._mediator.Send(new SetAudioSettings());
        }

        private void TriggerToggleHideAllActiveClients()
        {
            _logger.Verbose("MainFormPresenter.TriggerToggleHideAllActiveClients: Toggling hide all active clients");
            this._mediator.Send(new ThumbnailToggleHideAll());
        }

        private void TriggerMinimizeAllClientsHotkey()
        {
            _logger.Verbose("MainFormPresenter.TriggerMinimizeAllClientsHotkey: Minimizing all client windows");
            this._mediator.Send(new MinimizeAllClients());
        }

        private void ExitApplication()
        {
            _logger.Verbose("MainFormPresenter.ExitApplication: User requested application exit");
            this._exitApplication = true;
            this.View.Close();
        }
    }
}