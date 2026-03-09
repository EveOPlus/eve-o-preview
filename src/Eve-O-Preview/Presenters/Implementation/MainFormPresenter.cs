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
using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Configuration;
using EveOPreview.Mediator.Messages;
using EveOPreview.View;
using MediatR;

namespace EveOPreview.Presenters
{
    public class MainFormPresenter : Presenter<IMainFormView>, IMainFormPresenter, INotificationHandler<ThumbnailToggleHideAllChangedNotification>
    {
        #region Private constants
        private const string DISCORD_URL = @"https://discord.gg/HzQHBtTEcB";
        #endregion

        #region Private fields
        private readonly IMediator _mediator;
        private readonly IThumbnailConfiguration _configuration;
        private readonly IConfigurationStorage _configurationStorage;
        private readonly IDictionary<string, IThumbnailDescription> _descriptionsCache;
        private bool _suppressSizeNotifications;

        private bool _exitApplication;
        #endregion

        public MainFormPresenter(
            IApplicationController controller, 
            IMainFormView view, 
            IMediator mediator, 
            IThumbnailConfiguration configuration, 
            IConfigurationStorage configurationStorage)
            : base(controller, view)
        {
            this._mediator = mediator;
            this._configuration = configuration;
            this._configurationStorage = configurationStorage;

            this._descriptionsCache = new Dictionary<string, IThumbnailDescription>();

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
        }

        private CaptureNewHotkeyResponse SendCaptureNewHotkeyRequest(string currentKey)
        {
            return _mediator.Send(new CaptureNewHotkey(currentKey, 10000)).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private void Activate()
        {
            this._suppressSizeNotifications = true;
            this.LoadApplicationSettings();
            this.View.SetDocumentationUrl(MainFormPresenter.DISCORD_URL);
            this.View.SetVersionInfo(this.GetApplicationVersion());
            if (this._configuration.MinimizeToTray)
            {
                this.View.Minimize();
            }

            this._mediator.Send(new StartService());
            this._suppressSizeNotifications = false;
        }

        private void Minimize()
        {
            if (!this._configuration.MinimizeToTray)
            {
                return;
            }

            this.View.Hide();
        }

        private void Close(ViewCloseRequest request)
        {
            if (this._exitApplication || !this.View.MinimizeToTray)
            {
                this._mediator.Send(new StopService()).Wait();

                this._configurationStorage.Save();
                request.Allow = true;
                return;
            }

            request.Allow = false;
            this.View.Minimize();
        }

        private async void UpdateThumbnailsSize()
        {
            if (!this._suppressSizeNotifications)
            {
                this.SaveApplicationSettings();
                await this._mediator.Publish(new ThumbnailConfiguredSizeUpdated());
            }
        }

        private void LoadApplicationSettings()
        {
            this._configurationStorage.Load();

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
        }

        private async void SaveApplicationSettings()
        {
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
                this._configuration.ShowThumbnailFrames = this.View.ShowThumbnailFrames;
                await this._mediator.Publish(new ThumbnailFrameSettingsUpdated());
            }

            this._configuration.EnableActiveClientHighlight = this.View.EnableActiveClientHighlight;
            this._configuration.ActiveClientHighlightColor = this.View.ActiveClientHighlightColor;

            this._configuration.TitleFontSettings = this.View.TitleFontSettings;
            this._configuration.ToggleHideActiveClientsHotkey = this.View.ToggleHideAllActiveHotkey;
            this._configuration.MinimizeAllClientsHotkey = this.View.MinimizeAllClientsHotkey;
            
            this._configurationStorage.Save();

            this.View.RefreshZoomSettings();

            await this._mediator.Publish(new ThumbnailFontTitleSettingsUpdated());
            
            await this._mediator.Send(new RefreshHotkeys());
            await this._mediator.Send(new SaveConfiguration());
        }

        public void AddThumbnails(IList<string> thumbnailTitles)
        {
            IList<IThumbnailDescription> descriptions = new List<IThumbnailDescription>(thumbnailTitles.Count);

            lock (this._descriptionsCache)
            {
                foreach (string title in thumbnailTitles)
                {
                    IThumbnailDescription description = this.CreateThumbnailDescription(title);
                    this._descriptionsCache[title] = description;

                    descriptions.Add(description);
                }
            }

            this.View.AddThumbnails(descriptions);
        }

        public void RemoveThumbnails(IList<string> thumbnailTitles)
        {
            IList<IThumbnailDescription> descriptions = new List<IThumbnailDescription>(thumbnailTitles.Count);

            lock (this._descriptionsCache)
            {
                foreach (string title in thumbnailTitles)
                {
                    if (!this._descriptionsCache.TryGetValue(title, out IThumbnailDescription description))
                    {
                        continue;
                    }

                    this._descriptionsCache.Remove(title);
                    descriptions.Add(description);
                }
            }

            this.View.RemoveThumbnails(descriptions);
        }

        private IThumbnailDescription CreateThumbnailDescription(string title)
        {
            bool isDisabled = this._configuration.IsThumbnailDisabled(title);
            return new ThumbnailDescription(title, isDisabled);
        }

        private async void UpdateThumbnailState(String title)
        {
            if (this._descriptionsCache.TryGetValue(title, out IThumbnailDescription description))
            {
                this._configuration.ToggleThumbnail(title, description.IsDisabled);
            }
            
            await this._mediator.Send(new SaveConfiguration());
        }

        public void UpdateThumbnailSize(Size size)
        {
            this._suppressSizeNotifications = true;
            this.View.ThumbnailSize = size;
            this._suppressSizeNotifications = false;
        }

        public string GetClientDescriptionFromInputBox()
        {
            var input = new ClientNameInputBox();
            input.LoadKnownClients(_descriptionsCache.Keys.ToList());
            input.ShowDialog();

            return input.SelectedClientName;
        }

        private void OpenDocumentationLink()
        {
            // TODO Move out to a separate service / presenter / message handler
            ProcessStartInfo processStartInfo = new ProcessStartInfo(new Uri(MainFormPresenter.DISCORD_URL).AbsoluteUri);
            Process.Start(processStartInfo);
        }

        private string GetApplicationVersion()
        {
            Version version = System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        private void TriggerSetFpsLimiter()
        {
            this._mediator.Send(new SetFpsLimiter());
        }

        private void TriggerSetFpsLimiterEnabled()
        {
            this._mediator.Send(new SetFpsLimiterEnabled());
        }

        private void TriggerSetAudioSettings()
        {
            this._mediator.Send(new SetAudioSettings());
        }

        private void TriggerToggleHideAllActiveClients()
        {
            this._mediator.Send(new ThumbnailToggleHideAll());
        }

        private void TriggerMinimizeAllClientsHotkey()
        {
            this._mediator.Send(new MinimizeAllClients());
        }

        private void ExitApplication()
        {
            this._exitApplication = true;
            this.View.Close();
        }

        public Task Handle(ThumbnailToggleHideAllChangedNotification notification, CancellationToken cancellationToken)
        {
            this.View.UpdateThumbnailToggleHideAllStatus(notification.IsHidden);

            return Task.CompletedTask;
        }
    }
}