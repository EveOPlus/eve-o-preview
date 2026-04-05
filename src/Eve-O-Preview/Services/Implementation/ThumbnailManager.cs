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

using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Mediator.Messages;
using EveOPreview.Mediator.Messages.Process;
using EveOPreview.Services.Interface;
using EveOPreview.View;
using Gma.System.MouseKeyHook;
using MediatR;
using Serilog;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;

namespace EveOPreview.Services
{
    sealed class ThumbnailManager : IThumbnailManager
    {
        #region Private constants
        private const int WINDOW_POSITION_THRESHOLD_LOW = -10_000;
        private const int WINDOW_POSITION_THRESHOLD_HIGH = 31_000;
        private const int WINDOW_SIZE_THRESHOLD = 10;
        private const int FORCED_REFRESH_CYCLE_THRESHOLD = 2;
        private const int DEFAULT_LOCATION_CHANGE_NOTIFICATION_DELAY = 2;

        private const string DEFAULT_CLIENT_TITLE = "EVE";
        #endregion

        #region Private fields
        private readonly IMediator _mediator;
        private readonly IProcessMonitor _processMonitor;
        private readonly IWindowManager _windowManager;
        private readonly IThumbnailConfiguration _configuration;
        private readonly DispatcherTimer _thumbnailUpdateTimer;
        private readonly IThumbnailViewFactory _thumbnailViewFactory;
        private readonly Dictionary<IntPtr, IThumbnailView> _thumbnailViews;
        private IKeyboardMouseEvents _keyboardMouseEvents;
        private readonly IHookService _hookService;
        private readonly IGlobalEvents _globalEvents;
        private readonly ILogger _logger;

        private (IntPtr Handle, string Title) _activeClient;
        private IntPtr _externalApplication;

        private readonly object _locationChangeNotificationSyncRoot;
        private (IntPtr Handle, string Title, string ActiveClient, Point Location, int Delay) _enqueuedLocationChangeNotification;

        private bool _ignoreViewEvents;
        private bool _isHoverEffectActive;

        private int _refreshCycleCount;
        private int _hideThumbnailsDelay;
        #endregion

        public ThumbnailManager(IMediator mediator, IThumbnailConfiguration configuration, IProcessMonitor processMonitor, IWindowManager windowManager, IThumbnailViewFactory factory, IKeyboardMouseEvents keyboardMouseEvents, IHookService hookService, IGlobalEvents globalEvents, ILogger logger)
        {
            this._mediator = mediator;
            this._processMonitor = processMonitor;
            this._windowManager = windowManager;
            this._configuration = configuration;
            this._thumbnailViewFactory = factory;
            this._keyboardMouseEvents = keyboardMouseEvents;
            _hookService = hookService;
            _globalEvents = globalEvents;
            _logger = logger;

            _logger.Verbose("ThumbnailManager: Constructor initializing. RefreshPeriod={RefreshPeriod}ms, ThumbnailSize={Width}x{Height}", 
                configuration.ThumbnailRefreshPeriod, configuration.ThumbnailSize.Width, configuration.ThumbnailSize.Height);

            this._activeClient = (IntPtr.Zero, ThumbnailManager.DEFAULT_CLIENT_TITLE);

            this.EnableViewEvents();
            this._isHoverEffectActive = false;

            this._refreshCycleCount = 0;
            this._locationChangeNotificationSyncRoot = new object();
            this._enqueuedLocationChangeNotification = (IntPtr.Zero, null, null, Point.Empty, -1);

            this._thumbnailViews = new Dictionary<IntPtr, IThumbnailView>();

            //  DispatcherTimer setup
            this._thumbnailUpdateTimer = new DispatcherTimer();
            this._thumbnailUpdateTimer.Tick += ThumbnailUpdateTimerTick;
            this._thumbnailUpdateTimer.Interval = new TimeSpan(0, 0, 0, 0, configuration.ThumbnailRefreshPeriod);

            this._hideThumbnailsDelay = this._configuration.HideThumbnailsDelay;

            this._globalEvents.CurrentProfileChanged += HandleCurrentProfileChanged;

            RegisterAllHotkeys();
            
            _logger.Verbose("ThumbnailManager: Constructor completed");
        }

        private void HandleCurrentProfileChanged(SelectedProfileChangedNotification obj)
        {
            _logger.Verbose("ThumbnailManager: Profile changed, re-registering hotkeys");
            RegisterAllHotkeys();
        }

        public IThumbnailView GetClientByTitle(string title)
        {
            _logger.Verbose("ThumbnailManager.GetClientByTitle: Looking up client by title: {Title}", title);
            var result = _thumbnailViews.FirstOrDefault(x => x.Value.Title == title).Value;
            _logger.Verbose("ThumbnailManager.GetClientByTitle: Found: {Result}", result != null ? "Yes" : "No");
            return result;
        }

        public IThumbnailView GetClientByPointer(IntPtr ptr)
        {
            _logger.Verbose("ThumbnailManager.GetClientByPointer: Looking up client by handle 0x{Handle:X}", ptr);
            var result = _thumbnailViews.FirstOrDefault(x => x.Key == ptr).Value;
            _logger.Verbose("ThumbnailManager.GetClientByPointer: Found: {Result}", result != null ? "Yes" : "No");
            return result;
        }

        public IThumbnailView GetActiveClient()
        {
            var result = GetClientByPointer(this._activeClient.Handle);
            _logger.Verbose("ThumbnailManager.GetActiveClient: Active client handle 0x{Handle:X}, Result: {Result}", this._activeClient.Handle, result?.Title ?? "(null)");
            return result;
        }

        public Dictionary<IntPtr, IThumbnailView> GetAllKnownClients()
        {
            _logger.Verbose("ThumbnailManager.GetAllKnownClients: Returning {Count} known clients", _thumbnailViews.Count);
            return _thumbnailViews;
        }

        public void SetActive(KeyValuePair<IntPtr, IThumbnailView> newClient)
        {
            string clientTitle = newClient.Value?.Title ?? "(NULL)";
            _logger.Verbose("ThumbnailManager.SetActive: Setting active client to {ClientTitle} (Handle: 0x{Handle:X})", clientTitle, newClient.Key);

            if (newClient.Value == null)
            {
                _logger.Warning("ThumbnailManager.SetActive: Cannot activate null client");
                return;
            }

            IThumbnailView previousActiveClient = this.GetActiveClient();
            if (previousActiveClient != null)
            {
                _logger.Verbose("ThumbnailManager.SetActive: Clearing border from previous active client: {PreviousClient}", previousActiveClient.Title);
                previousActiveClient.ClearBorder();
            }

            _logger.Verbose("ThumbnailManager.SetActive: Activating window for handle 0x{Handle:X}", newClient.Key);
            this._windowManager.ActivateWindow(newClient.Key);
            this.SwitchActiveClient(newClient.Key, newClient.Value.Title);

            _logger.Verbose("ThumbnailManager.SetActive: Setting highlight on active client");
            newClient.Value.SetHighlight();

            _logger.Verbose("ThumbnailManager.SetActive: Refreshing active client thumbnail");
            newClient.Value.Refresh(true);
            
            _logger.Verbose("ThumbnailManager.SetActive: Active client set successfully");
        }

        public void CycleNextClient(bool isForwards, SortedDictionary<int, string> cycleOrder)
        {
            string activeClientTitle = _activeClient.Title ?? "(LOGIN SCREEN)";
            _logger.Verbose("ThumbnailManager.CycleNextClient: Cycling clients. Direction={Direction}, ActiveClient={ActiveClient}", isForwards ? "Forward" : "Backward", activeClientTitle);

            string nextClientTitle = FindNextClientInCycleGroup(isForwards, activeClientTitle, cycleOrder);
            string nextNextClientTitle = FindNextClientInCycleGroup(isForwards, nextClientTitle, cycleOrder);

            _logger.Verbose("ThumbnailManager.CycleNextClient: Next={NextClient}, NextNext={NextNextClient}", nextClientTitle, nextNextClientTitle);

            KeyValuePair<IntPtr, IThumbnailView> nextClient = _thumbnailViews.FirstOrDefault(x => x.Value.Title == nextClientTitle);
            KeyValuePair<IntPtr, IThumbnailView> nextNextClient = _thumbnailViews.FirstOrDefault(x => x.Value.Title == nextNextClientTitle);

            if (nextClient.Value != null)
            {
                _logger.Verbose("ThumbnailManager.CycleNextClient: Updating CPU affinity. Active=0x{Active:X}, Next=0x{Next:X}, Prev=0x{Prev:X}",
                    _activeClient.Handle, nextClient.Key, IntPtr.Zero);
                _mediator.Send(new UpdateCpuAffinity(nextClient.Key, nextNextClient.Key, _activeClient.Handle));
            }

            SetActive(nextClient);
            this._windowManager.PredictUpcomingClient(nextNextClient.Key);
            
            _logger.Verbose("ThumbnailManager.CycleNextClient: Cycle completed");
        }

        private string FindNextClientInCycleGroup(bool isForwards, string findThisTitleFirst, SortedDictionary<int, string> cycleOrder)
        {
            // Remove all clients in the cycle group that are not running right now.
            var filteredTitles = cycleOrder.Where(co => _thumbnailViews.Any(tv => tv.Value.Title == co.Value));
            var orderedTitles = isForwards ? filteredTitles.OrderBy(x => x.Key).ToList() : filteredTitles.OrderByDescending(x => x.Key).ToList();
            var remainingClients = orderedTitles.SkipWhile(x => x.Value != findThisTitleFirst).Skip(1).ToList();
        
            return remainingClients.Any() ? remainingClients.First().Value : orderedTitles.FirstOrDefault().Value;
        }

        private List<KeyEventHandler> _trackedHotkeyDownDelegates = new List<KeyEventHandler>();
        private List<KeyEventHandler> _trackedHotkeyUpDelegates = new List<KeyEventHandler>();

        public void RegisterAllHotkeys()
        {
            var cycleGroups = this._configuration.CycleGroups;
            UnregisterExistingHotkeys();

            _logger.Verbose("ThumbnailManager.RegisterAllHotkeys: Registering all hotkeys for {GroupCount} cycle groups", cycleGroups.Count);

            foreach (var cycleGroup in cycleGroups)
            {
                RegisterCycleClientHotkey(cycleGroup);
            }

            RegisterGeneralHotkeys();
            
            _logger.Verbose("ThumbnailManager.RegisterAllHotkeys: Hotkey registration completed. Total tracked: Down={DownCount}, Up={UpCount}", 
                _trackedHotkeyDownDelegates.Count, _trackedHotkeyUpDelegates.Count);
        }

        private void UnregisterExistingHotkeys()
        {
            _logger.Verbose("ThumbnailManager.UnregisterExistingHotkeys: Unregistering {DownCount} down hotkeys and {UpCount} up hotkeys",
                _trackedHotkeyDownDelegates.Count, _trackedHotkeyUpDelegates.Count);

            foreach (var existingDown in _trackedHotkeyDownDelegates)
            {
                _keyboardMouseEvents.KeyDown -= existingDown;
            }
            _trackedHotkeyDownDelegates.Clear();

            foreach (var existingUp in _trackedHotkeyUpDelegates)
            {
                _keyboardMouseEvents.KeyUp -= existingUp;
            }
            _trackedHotkeyUpDelegates.Clear();
        }

        public void RegisterCycleClientHotkey(CycleGroup cycleGroup)
        {
            _logger.Verbose("ThumbnailManager.RegisterCycleClientHotkey: Registering cycle group: {Description}", cycleGroup.Description);
            RegisterCycleClientHotkey(cycleGroup.ForwardHotkeysParsedAndOrdered, true, cycleGroup.ClientsOrder);
            RegisterCycleClientHotkey(cycleGroup.BackwardHotkeysParsedAndOrdered, false, cycleGroup.ClientsOrder);
        }

        internal void RegisterCycleClientHotkey(List<Keys> keys, bool isForwards, SortedDictionary<int, string> cycleOrder)
        {
            _logger.Verbose("ThumbnailManager.RegisterCycleClientHotkey: Registering hotkeys. Direction={Direction}, KeyCount={KeyCount}", 
                isForwards ? "Forward" : "Backward", keys.Count);
            
            KeyEventHandler newDownDelegate = (sender, e) =>
            {
                try
                {
                    foreach (var hotkey in keys)
                    {
                        if (e.KeyData == hotkey)
                        {
                            _logger.Verbose("ThumbnailManager: Cycle hotkey down pressed. Direction={Direction}", isForwards ? "Forward" : "Backward");

                            if (this._windowManager.IsCurrentlySwitching)
                            {
                                _logger.Verbose("ThumbnailManager: Window switch in progress, ignoring hotkey");
                                return;
                            }

                            this.CycleNextClient(isForwards, cycleOrder);
                            e.Handled = true;
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "ThumbnailManager: Error while handling cycle hotkey down");
                }
            };

            _keyboardMouseEvents.KeyDown += newDownDelegate;
            _trackedHotkeyDownDelegates.Add(newDownDelegate);

            KeyEventHandler newUpDelegate = (sender, e) =>
            {
                try
                {
                    foreach (var hotkey in keys)
                    {
                        if (e.KeyCode == hotkey)
                        {
                            _logger.Verbose("ThumbnailManager: Cycle hotkey up. Direction={Direction}", isForwards ? "Forward" : "Backward");
                            e.Handled = true;
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "ThumbnailManager: Error while handling cycle hotkey up");
                }
            };

            _keyboardMouseEvents.KeyUp += newUpDelegate;
            _trackedHotkeyUpDelegates.Add(newUpDelegate);
        }

        public void RegisterGeneralHotkeys()
        {
            _logger.Verbose("ThumbnailManager.RegisterGeneralHotkeys: Registering general hotkeys (hide all, minimize all)");
            
            // Using the KeyUp for this one so it has less chance of impacting the flow of other more important hotkeys (like client cycling)
            KeyEventHandler newUpDelegate = (sender, e) =>
            {
                try
                {
                    if (e.KeyData == _configuration.ToggleHideActiveClientsHotkeyParsed)
                    {
                        _logger.Verbose("ThumbnailManager: Toggle hide all active clients hotkey pressed");
                        _mediator.Send(new ThumbnailToggleHideAll());
                        e.Handled = true;
                    }
                    else if (e.KeyData == _configuration.MinimizeAllClientsHotkeyParsed)
                    {
                        _logger.Verbose("ThumbnailManager: Minimize all clients hotkey pressed");
                        _mediator.Send(new MinimizeAllClients());
                        e.Handled = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "ThumbnailManager: Error handling general hotkey");
                }

            };

            _keyboardMouseEvents.KeyUp += newUpDelegate;
            _trackedHotkeyUpDelegates.Add(newUpDelegate);
        }

        public void Start()
        {
            _logger.Verbose("ThumbnailManager.Start: Starting thumbnail manager. RefreshPeriod={RefreshPeriod}ms", this._configuration.ThumbnailRefreshPeriod);
            this._thumbnailUpdateTimer.Start();

            this.RefreshThumbnails();
            _logger.Verbose("ThumbnailManager.Start: Service started successfully");
        }

        public void Stop()
        {
            _logger.Verbose("ThumbnailManager.Stop: Stopping thumbnail manager");
            this._thumbnailUpdateTimer.Stop();
            _logger.Verbose("ThumbnailManager.Stop: Service stopped");
        }

        private void ThumbnailUpdateTimerTick(object sender, EventArgs e)
        {
            _logger.Verbose("ThumbnailManager.ThumbnailUpdateTimerTick: Timer tick - updating thumbnails list and refreshing");
            this.UpdateThumbnailsList();
            this.RefreshThumbnails();
        }

        private async void UpdateThumbnailsList()
        {
            _logger.Verbose("ThumbnailManager.UpdateThumbnailsList: Updating thumbnails list");
            this._processMonitor.GetUpdatedProcesses(out ICollection<IProcessInfo> addedProcesses, out ICollection<IProcessInfo> updatedProcesses, out ICollection<IProcessInfo> removedProcesses);

            List<string> viewsAdded = new List<string>();
            List<string> viewsRemoved = new List<string>();

            _logger.Verbose("ThumbnailManager.UpdateThumbnailsList: Processing changes. Added={AddedCount}, Updated={UpdatedCount}, Removed={RemovedCount}",
                addedProcesses?.Count ?? 0, updatedProcesses?.Count ?? 0, removedProcesses?.Count ?? 0);

            foreach (IProcessInfo process in addedProcesses)
            {
                _logger.Verbose("ThumbnailManager.UpdateThumbnailsList: Creating thumbnail view for {Title} (PID={PID}, Handle=0x{Handle:X})", 
                    process.Title, process.ProcessId, process.MainWindowHandle);
                
                IThumbnailView view = this._thumbnailViewFactory.Create(process.MainWindowHandle, process.Title, this._configuration.ThumbnailSize);
                view.IsOverlayEnabled = this._configuration.ShowThumbnailOverlays;
                view.SetFrames(this._configuration.ShowThumbnailFrames);
                view.SetSizeLimitations(this._configuration.ThumbnailMinimumSize, this._configuration.ThumbnailMaximumSize);
                view.SetTopMost(this._configuration.ShowThumbnailsAlwaysOnTop);

                view.ThumbnailLocation = this.IsManageableThumbnail(view)
                                            ? this._configuration.GetThumbnailLocation(view.Title, this._activeClient.Title, view.ThumbnailLocation)
                                            : this._configuration.LoginThumbnailLocation;

                this._thumbnailViews.Add(view.Id, view);

                view.ThumbnailResized = this.ThumbnailViewResized;
                view.ThumbnailMoved = this.ThumbnailViewMoved;
                view.ThumbnailFocused = this.ThumbnailViewFocused;
                view.ThumbnailLostFocus = this.ThumbnailViewLostFocus;
                view.ThumbnailActivated = this.ThumbnailActivated;
                view.ThumbnailDeactivated = this.ThumbnailDeactivated;

                this.ApplyClientLayout(view.Id, view.Title);

                if (view.Title != ThumbnailManager.DEFAULT_CLIENT_TITLE)
                {
                    viewsAdded.Add(view.Title);
                }

                _ = _hookService.TryInstallHooksAsync(process);
            }

            foreach (IProcessInfo process in updatedProcesses)
            {
                this._thumbnailViews.TryGetValue(process.MainWindowHandle, out IThumbnailView view);

                if (view == null)
                {
                    _logger.Warning("ThumbnailManager.UpdateThumbnailsList: Updated process {Title} (PID={PID}) has no corresponding thumbnail view", process.Title, process.ProcessId);
                    continue;
                }

                if (process.Title != view.Title)
                {
                    _logger.Verbose("ThumbnailManager.UpdateThumbnailsList: Thumbnail title changed: {OldTitle} -> {NewTitle}", view.Title, process.Title);
                    viewsRemoved.Add(view.Title);
                    view.Title = process.Title;
                    viewsAdded.Add(view.Title);

                    this.ApplyClientLayout(view.Id, view.Title);
                }
            }

            foreach (IProcessInfo process in removedProcesses)
            {
                IThumbnailView view = this._thumbnailViews[process.MainWindowHandle];

                _logger.Verbose("ThumbnailManager.UpdateThumbnailsList: Removing thumbnail view: {Title} (Handle: 0x{Handle:X})", view.Title, view.Id);
                this._thumbnailViews.Remove(view.Id);
                if (view.Title != ThumbnailManager.DEFAULT_CLIENT_TITLE)
                {
                    viewsRemoved.Add(view.Title);
                }

                view.ThumbnailResized = null;
                view.ThumbnailMoved = null;
                view.ThumbnailFocused = null;
                view.ThumbnailLostFocus = null;
                view.ThumbnailActivated = null;

                view.Close();
            }

            if ((viewsAdded.Count > 0) || (viewsRemoved.Count > 0))
            {
                _logger.Verbose("ThumbnailManager.UpdateThumbnailsList: Publishing thumbnail list update. Added={AddedCount}, Removed={RemovedCount}", 
                    viewsAdded.Count, viewsRemoved.Count);
                await this._mediator.Publish(new ThumbnailListUpdated(viewsAdded, viewsRemoved));
            }
        }

        private void RefreshThumbnails()
        {
            _logger.Verbose("ThumbnailManager.RefreshThumbnails: Starting refresh cycle. CurrentCount={RefreshCount}, ThumbnailCount={ThumbnailCount}", 
                this._refreshCycleCount, this._thumbnailViews.Count);
            
            IntPtr foregroundWindowHandle = this._windowManager.GetForegroundWindowHandle();

            // The foreground window can be NULL in certain circumstances, such as when a window is losing activation.
            // It is safer to just skip this refresh round than to do something while the system state is undefined
            if (foregroundWindowHandle == IntPtr.Zero)
            {
                _logger.Verbose("ThumbnailManager.RefreshThumbnails: Foreground window is null, skipping refresh");
                return;
            }

            _logger.Verbose("ThumbnailManager.RefreshThumbnails: Foreground window handle: 0x{Handle:X}", foregroundWindowHandle);

            string foregroundWindowTitle = null;

            // Check if the foreground window handle is one of the known handles for client windows or their thumbnails
            bool isClientWindow = this.IsClientWindowActive(foregroundWindowHandle);
            bool isMainWindowActive = this.IsMainWindowActive(foregroundWindowHandle);

            _logger.Verbose("ThumbnailManager.RefreshThumbnails: Window state - IsClientWindow={IsClient}, IsMainWindow={IsMain}", isClientWindow, isMainWindowActive);

            if (foregroundWindowHandle == this._activeClient.Handle)
            {
                foregroundWindowTitle = this._activeClient.Title;
                _logger.Verbose("ThumbnailManager.RefreshThumbnails: Active client window is foreground: {Title}", foregroundWindowTitle);
            }
            else if (this._thumbnailViews.TryGetValue(foregroundWindowHandle, out IThumbnailView foregroundView))
            {
                // This code will work only on Alt+Tab switch between clients
                foregroundWindowTitle = foregroundView.Title;
                _logger.Verbose("ThumbnailManager.RefreshThumbnails: Thumbnail window is foreground: {Title}", foregroundWindowTitle);
            }
            else if (!isClientWindow)
            {
                _logger.Verbose("ThumbnailManager.RefreshThumbnails: External application activated: 0x{Handle:X}", foregroundWindowHandle);
                this._externalApplication = foregroundWindowHandle;
            }

            // No need to minimize EVE clients when switching out to non-EVE window (like thumbnail)
            if (!string.IsNullOrEmpty(foregroundWindowTitle))
            {
                this.SwitchActiveClient(foregroundWindowHandle, foregroundWindowTitle);
            }

            bool hideAllThumbnails = this._configuration.HideThumbnailsOnLostFocus && !(isClientWindow || isMainWindowActive);

            _logger.Verbose("ThumbnailManager.RefreshThumbnails: HideAllThumbnails={HideAll} (OnLostFocus={OnLostFocus})", hideAllThumbnails, this._configuration.HideThumbnailsOnLostFocus);

            // Wait for some time before hiding all previews
            if (hideAllThumbnails)
            {
                this._hideThumbnailsDelay--;
                _logger.Verbose("ThumbnailManager.RefreshThumbnails: Hide delay decremented to {Delay}", this._hideThumbnailsDelay);
                
                if (this._hideThumbnailsDelay > 0)
                {
                    hideAllThumbnails = false; // Postpone the 'hide all' operation
                    _logger.Verbose("ThumbnailManager.RefreshThumbnails: Postponing hide operation");
                }
                else
                {
                    this._hideThumbnailsDelay = 0; // Stop the counter
                    _logger.Information("ThumbnailManager.RefreshThumbnails: Hiding all thumbnails due to focus loss");
                }
            }
            else
            {
                this._hideThumbnailsDelay = this._configuration.HideThumbnailsDelay; // Reset the counter
            }

            this._refreshCycleCount++;

            bool forceRefresh;
            if (this._refreshCycleCount >= ThumbnailManager.FORCED_REFRESH_CYCLE_THRESHOLD)
            {
                this._refreshCycleCount = 0;
                forceRefresh = true;
                _logger.Verbose("ThumbnailManager.RefreshThumbnails: Force refresh triggered");
            }
            else
            {
                forceRefresh = false;
            }

            this.DisableViewEvents();

            // Snap thumbnail
            // No need to update Thumbnails while one of them is highlighted
            if ((!this._isHoverEffectActive) && this.TryDequeueLocationChange(out var locationChange))
            {
                _logger.Verbose("ThumbnailManager.RefreshThumbnails: Processing dequeued location change for {Title}", locationChange.Title);
                
                if ((locationChange.ActiveClient == this._activeClient.Title) && this._thumbnailViews.TryGetValue(locationChange.Handle, out var view))
                {
                    this.SnapThumbnailView(view);

                    this.RaiseThumbnailLocationUpdatedNotification(view.Title);
                }
                else
                {
                    this.RaiseThumbnailLocationUpdatedNotification(locationChange.Title);
                }
            }

            // Hide, show, resize and move
            int visibleCount = 0;
            int hiddenCount = 0;
            
            foreach (KeyValuePair<IntPtr, IThumbnailView> entry in this._thumbnailViews)
            {
                IThumbnailView view = entry.Value;

                if (hideAllThumbnails || this._configuration.IsThumbnailDisabled(view.Title))
                {
                    if (view.IsActive)
                    {
                        _logger.Verbose("ThumbnailManager.RefreshThumbnails: Hiding {Title} (HideAll={HideAll}, Disabled={IsDisabled})", 
                            view.Title, hideAllThumbnails, this._configuration.IsThumbnailDisabled(view.Title));
                        view.Hide();
                        hiddenCount++;
                    }
                    continue;
                }

                if (this._configuration.HideActiveClientThumbnail && (view.Id == this._activeClient.Handle))
                {
                    if (view.IsActive)
                    {
                        _logger.Verbose("ThumbnailManager.RefreshThumbnails: Hiding active client thumbnail {Title}", view.Title);
                        view.Hide();
                        hiddenCount++;
                    }
                    continue;
                }

                // No need to update Thumbnails while one of them is highlighted
                if (!this._isHoverEffectActive)
                {
                    // Do not even move thumbnails with default caption
                    if (this.IsManageableThumbnail(view))
                    {
                        view.ThumbnailLocation = this._configuration.GetThumbnailLocation(view.Title, this._activeClient.Title, view.ThumbnailLocation);
                    }

                    view.SetOpacity(this._configuration.ThumbnailOpacity);
                    view.SetTopMost(this._configuration.ShowThumbnailsAlwaysOnTop);
                }

                view.IsOverlayEnabled = this._configuration.ShowThumbnailOverlays;

                view.SetHighlight(
                    this._configuration.EnableActiveClientHighlight && (view.Id == this._activeClient.Handle), 
                    this._configuration.ActiveClientHighlightThickness);

                if (!view.IsActive)
                {
                    _logger.Verbose("ThumbnailManager.RefreshThumbnails: Showing {Title}", view.Title);
                    view.Show();
                    visibleCount++;
                }
                else
                {
                    view.Refresh(forceRefresh);
                    visibleCount++;
                }
            }

            this.EnableViewEvents();
            
            _logger.Verbose("ThumbnailManager.RefreshThumbnails: Refresh cycle complete. Visible={Visible}, Hidden={Hidden}, ForceRefresh={ForceRefresh}", 
                visibleCount, hiddenCount, forceRefresh);
        }

        public void UpdateThumbnailsSize()
        {
            _logger.Verbose("ThumbnailManager.UpdateThumbnailsSize: Updating thumbnail size to {Width}x{Height}", this._configuration.ThumbnailSize.Width, this._configuration.ThumbnailSize.Height);
            this.SetThumbnailsSize(this._configuration.ThumbnailSize);
        }

        private void SetThumbnailsSize(Size size)
        {
            _logger.Verbose("ThumbnailManager.SetThumbnailsSize: Setting size for {ThumbnailCount} thumbnails to {Width}x{Height}", this._thumbnailViews.Count, size.Width, size.Height);
            this.DisableViewEvents();

            foreach (KeyValuePair<IntPtr, IThumbnailView> entry in this._thumbnailViews)
            {
                entry.Value.ThumbnailSize = size;
                entry.Value.Refresh(false);
            }

            this.EnableViewEvents();
        }
        
        public void UpdateThumbnailFrames()
        {
            _logger.Verbose("ThumbnailManager.UpdateThumbnailFrames: Updating frames for {ThumbnailCount} thumbnails. ShowFrames={ShowFrames}", 
                this._thumbnailViews.Count, this._configuration.ShowThumbnailFrames);
            this.DisableViewEvents();

            foreach (KeyValuePair<IntPtr, IThumbnailView> entry in this._thumbnailViews)
            {
                entry.Value.SetFrames(this._configuration.ShowThumbnailFrames);
            }

            this.EnableViewEvents();
        }

        public void UpdateThumbnailTitleFont()
        {
            _logger.Verbose("ThumbnailManager.UpdateThumbnailTitleFont: Updating title font for {ThumbnailCount} thumbnails", this._thumbnailViews.Count);
            this.DisableViewEvents();

            foreach (KeyValuePair<IntPtr, IThumbnailView> entry in this._thumbnailViews)
            {
                entry.Value.TitleFontSettings = this._configuration.TitleFontSettings;
            }

            this.EnableViewEvents();
        }

        private void EnableViewEvents()
        {
            _logger.Verbose("ThumbnailManager: Enabling view events");
            this._ignoreViewEvents = false;
        }

        private void DisableViewEvents()
        {
            _logger.Verbose("ThumbnailManager: Disabling view events");
            this._ignoreViewEvents = true;
        }

        private void SwitchActiveClient(IntPtr foregroundClientHandle, string foregroundClientTitle)
        {
            if (this._activeClient.Handle == foregroundClientHandle)
            {
                _logger.Verbose("ThumbnailManager.SwitchActiveClient: Client already active, skipping");
                return;
            }

            _logger.Verbose("ThumbnailManager.SwitchActiveClient: Switching active client to {Title} (Handle: 0x{Handle:X})", foregroundClientTitle, foregroundClientHandle);

            if (this._configuration.MinimizeInactiveClients && !this._configuration.IsPriorityClient(this._activeClient.Title))
            {
                _logger.Verbose("ThumbnailManager.SwitchActiveClient: Minimizing previous active client {Title}", this._activeClient.Title);
                this._windowManager.MinimizeWindow(this._activeClient.Handle, false);
            }

            this._activeClient = (foregroundClientHandle, foregroundClientTitle);
        }

        private void ThumbnailViewFocused(IntPtr id)
        {
            if (this._isHoverEffectActive)
            {
                _logger.Verbose("ThumbnailManager.ThumbnailViewFocused: Hover already active, skipping");
                return;
            }

            _logger.Verbose("ThumbnailManager.ThumbnailViewFocused: Thumbnail focused (Handle: 0x{Handle:X})", id);
            this._isHoverEffectActive = true;

            IThumbnailView view = this._thumbnailViews[id];

            view.SetTopMost(true);
            view.SetOpacity(1.0);

            if (this._configuration.ThumbnailZoomEnabled)
            {
                _logger.Verbose("ThumbnailManager.ThumbnailViewFocused: Zooming in with factor {Factor}", this._configuration.ThumbnailZoomFactor);
                this.ThumbnailZoomIn(view);
            }
        }

        private void ThumbnailViewLostFocus(IntPtr id)
        {
            if (!this._isHoverEffectActive)
            {
                _logger.Verbose("ThumbnailManager.ThumbnailViewLostFocus: Hover not active, skipping");
                return;
            }

            _logger.Verbose("ThumbnailManager.ThumbnailViewLostFocus: Thumbnail lost focus (Handle: 0x{Handle:X})", id);
            IThumbnailView view = this._thumbnailViews[id];

            if (this._configuration.ThumbnailZoomEnabled)
            {
                this.ThumbnailZoomOut(view);
            }

            view.SetOpacity(this._configuration.ThumbnailOpacity);

            this._isHoverEffectActive = false;
        }

        private void ThumbnailActivated(IntPtr id)
        {
            _logger.Verbose("ThumbnailManager.ThumbnailActivated: Thumbnail activated (Handle: 0x{Handle:X})", id);
            IThumbnailView view = this._thumbnailViews[id];

            Task.Run(() =>
                {
                    this._mediator.Send(new UpdateCpuAffinity(view.Id, IntPtr.Zero, IntPtr.Zero));
                    this._windowManager.ActivateWindow(view.Id);
                })
                .ContinueWith((task) =>
                {
                    this.SwitchActiveClient(view.Id, view.Title);
                    this.UpdateClientLayouts();
                    this.RefreshThumbnails();
                }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void ThumbnailDeactivated(IntPtr id, bool switchOut)
        {
            _logger.Verbose("ThumbnailManager.ThumbnailDeactivated: Thumbnail deactivated (Handle: 0x{Handle:X}, SwitchOut={SwitchOut})", id, switchOut);
            
            if (switchOut)
            {
                _logger.Verbose("ThumbnailManager.ThumbnailDeactivated: Activating external application");
                this._windowManager.ActivateWindow(this._externalApplication);
            }
            else
            {
                if (!this._thumbnailViews.TryGetValue(id, out IThumbnailView view))
                {
                    _logger.Warning("ThumbnailManager.ThumbnailDeactivated: Thumbnail view not found for handle 0x{Handle:X}", id);
                    return;
                }

                _logger.Verbose("ThumbnailManager.ThumbnailDeactivated: Minimizing window {Title}", view.Title);
                this._windowManager.MinimizeWindow(view.Id, true);
                this.RefreshThumbnails();
            }
        }

        private async void ThumbnailViewResized(IntPtr id)
        {
            if (this._ignoreViewEvents)
            {
                _logger.Verbose("ThumbnailManager.ThumbnailViewResized: View events disabled, ignoring");
                return;
            }

            _logger.Verbose("ThumbnailManager.ThumbnailViewResized: Thumbnail resized (Handle: 0x{Handle:X})", id);
            IThumbnailView view = this._thumbnailViews[id];

            this.SetThumbnailsSize(view.ThumbnailSize);

            view.Refresh(false);

            await this._mediator.Publish(new ThumbnailActiveSizeUpdated(view.ThumbnailSize));
        }

        private void ThumbnailViewMoved(IntPtr id)
        {
            if (this._ignoreViewEvents)
            {
                _logger.Verbose("ThumbnailManager.ThumbnailViewMoved: View events disabled, ignoring");
                return;
            }

            _logger.Verbose("ThumbnailManager.ThumbnailViewMoved: Thumbnail moved (Handle: 0x{Handle:X})", id);
            IThumbnailView view = this._thumbnailViews[id];
            view.Refresh(false);
            this.EnqueueLocationChange(view);
        }

        // Checks whether currently active window belongs to an EVE client or its thumbnail
        private bool IsClientWindowActive(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                return false;
            }

            foreach (KeyValuePair<IntPtr, IThumbnailView> entry in this._thumbnailViews)
            {
                IThumbnailView view = entry.Value;

                if (view.IsKnownHandle(windowHandle))
                {
                    return true;
                }
            }

            return false;
        }

        // Check whether the currently active window belongs to EVE-O Preview itself
        private bool IsMainWindowActive(IntPtr windowHandle)
        {
            return (this._processMonitor.GetMainProcess().MainWindowHandle == windowHandle);
        }

        private void ThumbnailZoomIn(IThumbnailView view)
        {
            _logger.Verbose("ThumbnailManager.ThumbnailZoomIn: Zooming in on thumbnail {Title}", view.Title);
            this.DisableViewEvents();

            view.ZoomIn(ViewZoomAnchorConverter.Convert(this._configuration.ThumbnailZoomAnchor), this._configuration.ThumbnailZoomFactor);
            view.Refresh(false);

            this.EnableViewEvents();
        }

        private void ThumbnailZoomOut(IThumbnailView view)
        {
            _logger.Verbose("ThumbnailManager.ThumbnailZoomOut: Zooming out on thumbnail {Title}", view.Title);
            this.DisableViewEvents();

            view.ZoomOut();
            view.Refresh(false);

            this.EnableViewEvents();
        }

        private void SnapThumbnailView(IThumbnailView view)
        {
            if (!this._configuration.EnableThumbnailSnap)
            {
                _logger.Verbose("ThumbnailManager.SnapThumbnailView: Thumbnail snap disabled, skipping");
                return;
            }

            if (this._configuration.ShowThumbnailFrames)
            {
                _logger.Verbose("ThumbnailManager.SnapThumbnailView: Frames enabled, cannot snap borderless thumbnails");
                return;
            }

            _logger.Verbose("ThumbnailManager.SnapThumbnailView: Snapping thumbnail {Title} to nearby thumbnails", view.Title);
            
            int width = this._configuration.ThumbnailSize.Width;
            int height = this._configuration.ThumbnailSize.Height;

            int baseX = view.ThumbnailLocation.X;
            int baseY = view.ThumbnailLocation.Y;

            Point[] viewPoints = { new Point(baseX, baseY), new Point(baseX + width, baseY), new Point(baseX, baseY + height), new Point(baseX + width, baseY + height) };

            int thresholdX = Math.Max(20, width / 10);
            int thresholdY = Math.Max(20, height / 10);

            foreach (var entry in this._thumbnailViews)
            {
                IThumbnailView testView = entry.Value;

                if (view.Id == testView.Id)
                {
                    continue;
                }

                int testX = testView.ThumbnailLocation.X;
                int testY = testView.ThumbnailLocation.Y;

                Point[] testPoints = { new Point(testX, testY), new Point(testX + width, testY), new Point(testX, testY + height), new Point(testX + width, testY + height) };

                var delta = ThumbnailManager.TestViewPoints(viewPoints, testPoints, thresholdX, thresholdY);

                if ((delta.X == 0) && (delta.Y == 0))
                {
                    continue;
                }

                _logger.Verbose("ThumbnailManager.SnapThumbnailView: Snapped {Title} to {Target} with delta ({DeltaX},{DeltaY})", 
                    view.Title, testView.Title, delta.X, delta.Y);
                
                view.ThumbnailLocation = new Point(view.ThumbnailLocation.X + delta.X, view.ThumbnailLocation.Y + delta.Y);
                this._configuration.SetThumbnailLocation(view.Title, this._activeClient.Title, view.ThumbnailLocation);
                break;
            }
        }

        private static (int X, int Y) TestViewPoints(Point[] viewPoints, Point[] testPoints, int thresholdX, int thresholdY)
        {
            // Point combinations that we need to check
            // No need to check all 4x4 combinations
            (int ViewOffset, int TestOffset)[] testOffsets =
                                {   ( 0, 3 ), ( 0, 2 ), ( 1, 2 ),
                                    ( 0, 1 ), ( 0, 0 ), ( 1, 0 ),
                                    ( 2, 1 ), ( 2, 0 ), ( 3, 0 )};

            foreach (var testOffset in testOffsets)
            {
                Point viewPoint = viewPoints[testOffset.ViewOffset];
                Point testPoint = testPoints[testOffset.TestOffset];

                int deltaX = testPoint.X - viewPoint.X;
                int deltaY = testPoint.Y - viewPoint.Y;

                if ((Math.Abs(deltaX) <= thresholdX) && (Math.Abs(deltaY) <= thresholdY))
                {
                    return (deltaX, deltaY);
                }
            }

            return (0, 0);
        }

        private void ApplyClientLayout(IntPtr clientHandle, string clientTitle)
        {
            if (!this._configuration.EnableClientLayoutTracking)
            {
                _logger.Verbose("ThumbnailManager.ApplyClientLayout: Client layout tracking disabled, skipping");
                return;
            }

            // No need to apply layout for not yet logged-in clients
            if (clientTitle == ThumbnailManager.DEFAULT_CLIENT_TITLE)
            {
                _logger.Verbose("ThumbnailManager.ApplyClientLayout: Default client title, skipping layout");
                return;
            }

            ClientLayout clientLayout = this._configuration.GetClientLayout(clientTitle);

            if (clientLayout == null)
            {
                _logger.Verbose("ThumbnailManager.ApplyClientLayout: No layout found for {Title}", clientTitle);
                return;
            }

            if (clientLayout.IsMaximized)
            {
                _logger.Verbose("ThumbnailManager.ApplyClientLayout: Maximizing window for {Title}", clientTitle);
                this._windowManager.MaximizeWindow(clientHandle);
            }
            else
            {
                _logger.Verbose("ThumbnailManager.ApplyClientLayout: Restoring window for {Title} to ({X},{Y}) {Width}x{Height}", 
                    clientTitle, clientLayout.X, clientLayout.Y, clientLayout.Width, clientLayout.Height);
                this._windowManager.MoveWindow(clientHandle, clientLayout.X, clientLayout.Y, clientLayout.Width, clientLayout.Height);
            }
        }

        private void UpdateClientLayouts()
        {
            if (!this._configuration.EnableClientLayoutTracking)
            {
                _logger.Verbose("ThumbnailManager.UpdateClientLayouts: Client layout tracking disabled, skipping");
                return;
            }

            _logger.Verbose("ThumbnailManager.UpdateClientLayouts: Updating layouts for {ThumbnailCount} clients", this._thumbnailViews.Count);

            foreach (KeyValuePair<IntPtr, IThumbnailView> entry in this._thumbnailViews)
            {
                IThumbnailView view = entry.Value;

                // No need to save layout for not yet logged-in clients
                if (view.Title == ThumbnailManager.DEFAULT_CLIENT_TITLE)
                {
                    continue;
                }

                (int Left, int Top, int Right, int Bottom) position = this._windowManager.GetWindowPosition(view.Id);
                int width = Math.Abs(position.Right - position.Left);
                int height = Math.Abs(position.Bottom - position.Top);

                var isMaximized = this._windowManager.IsWindowMaximized(view.Id);

                if (!(isMaximized || this.IsValidWindowPosition(position.Left, position.Top, width, height)))
                {
                    _logger.Verbose("ThumbnailManager.UpdateClientLayouts: Invalid window position for {Title}, skipping", view.Title);
                    continue;
                }

                _logger.Verbose("ThumbnailManager.UpdateClientLayouts: Saved layout for {Title}: ({X},{Y}) {Width}x{Height}, Maximized={IsMaximized}",
                    view.Title, position.Left, position.Top, width, height, isMaximized);
                
                this._configuration.SetClientLayout(view.Title, new ClientLayout(position.Left, position.Top, width, height, isMaximized));
            }
        }

        private void EnqueueLocationChange(IThumbnailView view)
        {
            _logger.Verbose("ThumbnailManager.EnqueueLocationChange: Enqueueing location change for {Title} at ({X},{Y})", 
                view.Title, view.ThumbnailLocation.X, view.ThumbnailLocation.Y);
            
            string activeClientTitle = this._activeClient.Title;
            this._configuration.SetThumbnailLocation(view.Title, activeClientTitle, view.ThumbnailLocation);

            lock (this._locationChangeNotificationSyncRoot)
            {
                if (this._enqueuedLocationChangeNotification.Handle == IntPtr.Zero)
                {
                    this._enqueuedLocationChangeNotification = (view.Id, view.Title, activeClientTitle, view.ThumbnailLocation, ThumbnailManager.DEFAULT_LOCATION_CHANGE_NOTIFICATION_DELAY);
                    return;
                }

                if ((this._enqueuedLocationChangeNotification.Handle == view.Id) &&
                    (this._enqueuedLocationChangeNotification.ActiveClient == activeClientTitle))
                {
                    _logger.Verbose("ThumbnailManager.EnqueueLocationChange: Resetting delay for {Title}", view.Title);
                    this._enqueuedLocationChangeNotification.Delay = ThumbnailManager.DEFAULT_LOCATION_CHANGE_NOTIFICATION_DELAY;
                    return;
                }

                _logger.Verbose("ThumbnailManager.EnqueueLocationChange: Publishing previous queued notification for {Title}", 
                    this._enqueuedLocationChangeNotification.Title);
                this.RaiseThumbnailLocationUpdatedNotification(this._enqueuedLocationChangeNotification.Title);
                this._enqueuedLocationChangeNotification = (view.Id, view.Title, activeClientTitle, view.ThumbnailLocation, ThumbnailManager.DEFAULT_LOCATION_CHANGE_NOTIFICATION_DELAY);
            }
        }

        private bool TryDequeueLocationChange(out (IntPtr Handle, string Title, string ActiveClient, Point Location) change)
        {
            lock (this._locationChangeNotificationSyncRoot)
            {
                change = (IntPtr.Zero, null, null, Point.Empty);

                if (this._enqueuedLocationChangeNotification.Handle == IntPtr.Zero)
                {
                    return false;
                }

                this._enqueuedLocationChangeNotification.Delay--;

                if (this._enqueuedLocationChangeNotification.Delay > 0)
                {
                    return false;
                }

                change = (this._enqueuedLocationChangeNotification.Handle, this._enqueuedLocationChangeNotification.Title, this._enqueuedLocationChangeNotification.ActiveClient, this._enqueuedLocationChangeNotification.Location);
                _logger.Verbose("ThumbnailManager.TryDequeueLocationChange: Dequeueing location change for {Title}", change.Title);
                this._enqueuedLocationChangeNotification = (IntPtr.Zero, null, null, Point.Empty, -1);

                return true;
            }
        }

        private async void RaiseThumbnailLocationUpdatedNotification(string title)
        {
            if (string.IsNullOrEmpty(title) || (title == ThumbnailManager.DEFAULT_CLIENT_TITLE))
            {
                _logger.Verbose("ThumbnailManager.RaiseThumbnailLocationUpdatedNotification: Invalid title, skipping");
                return;
            }

            _logger.Verbose("ThumbnailManager.RaiseThumbnailLocationUpdatedNotification: Saving configuration");
            await this._mediator.Send(new SaveConfiguration());
        }

        private bool IsManageableThumbnail(IThumbnailView view)
        {
            bool result = view.Title != ThumbnailManager.DEFAULT_CLIENT_TITLE;
            _logger.Verbose("ThumbnailManager.IsManageableThumbnail: {Title} - Manageable={Result}", view.Title, result);
            return result;
        }

        private bool IsValidWindowPosition(int left, int top, int width, int height)
        {
            bool valid = (left > ThumbnailManager.WINDOW_POSITION_THRESHOLD_LOW) && (left < ThumbnailManager.WINDOW_POSITION_THRESHOLD_HIGH)
                    && (top > ThumbnailManager.WINDOW_POSITION_THRESHOLD_LOW) && (top < ThumbnailManager.WINDOW_POSITION_THRESHOLD_HIGH)
                    && (width > ThumbnailManager.WINDOW_SIZE_THRESHOLD) && (height > ThumbnailManager.WINDOW_SIZE_THRESHOLD);
            
            if (!valid)
            {
                _logger.Verbose("ThumbnailManager.IsValidWindowPosition: Invalid position ({Left},{Top}) {Width}x{Height}", left, top, width, height);
            }
            return valid;
        }
    }
}