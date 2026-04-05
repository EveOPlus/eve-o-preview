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
using System.Runtime.InteropServices;
using EveOPreview.Services.Interop;
using Serilog;

namespace EveOPreview.Services.Implementation
{
    class DwmThumbnail : IDwmThumbnail
    {
        #region Private fields
        private readonly IWindowManager _windowManager;
        private readonly ILogger _logger;
        private IntPtr _handle;
        private DWM_THUMBNAIL_PROPERTIES _properties;
        #endregion

        public DwmThumbnail(IWindowManager windowManager, ILogger logger)
        {
            this._windowManager = windowManager;
            _logger = logger;
            this._handle = IntPtr.Zero;
        }

        public void Register(IntPtr destination, IntPtr source)
        {
            _logger.Verbose("Registering DWM thumbnail: Destination=0x{Destination:X}, Source=0x{Source:X}", destination, source);
            
            this._properties = new DWM_THUMBNAIL_PROPERTIES();
            this._properties.dwFlags = DWM_TNP_CONSTANTS.DWM_TNP_VISIBLE
                                      + DWM_TNP_CONSTANTS.DWM_TNP_OPACITY
                                      + DWM_TNP_CONSTANTS.DWM_TNP_RECTDESTINATION
                                      + DWM_TNP_CONSTANTS.DWM_TNP_SOURCECLIENTAREAONLY;
            this._properties.opacity = 255;
            this._properties.fVisible = true;
            this._properties.fSourceClientAreaOnly = true;

            if (!this._windowManager.IsCompositionEnabled)
            {
                _logger.Verbose("DWM composition not enabled, skipping thumbnail registration");
                return;
            }

            try
            {
                this._handle = DwmNativeMethods.DwmRegisterThumbnail(destination, source);
                _logger.Verbose("DWM thumbnail registered successfully: Handle=0x{Handle:X}", this._handle);
            }
            catch (ArgumentException ex)
            {
                _logger.Warning(ex, "DWM thumbnail registration failed: Source window may be closed. Destination=0x{Destination:X}, Source=0x{Source:X}", destination, source);
                this._handle = IntPtr.Zero;
            }
            catch (COMException ex)
            {
                _logger.Warning(ex, "DWM thumbnail registration failed: DWM unavailable (possibly user account switch)");
                this._handle = IntPtr.Zero;
            }
        }

        public void Unregister()
        {
            if ((!this._windowManager.IsCompositionEnabled) || (this._handle == IntPtr.Zero))
            {
                _logger.Verbose("DWM thumbnail unregister: Skipped (Composition={IsEnabled}, Handle={Handle})", this._windowManager.IsCompositionEnabled, this._handle);
                return;
            }

            try
            {
                _logger.Verbose("Unregistering DWM thumbnail: Handle=0x{Handle:X}", this._handle);
                DwmNativeMethods.DwmUnregisterThumbnail(this._handle);
            }
            catch (ArgumentException ex)
            {
                _logger.Warning(ex, "Error unregistering DWM thumbnail");
            }
            catch (COMException ex)
            {
                _logger.Warning(ex, "DWM unavailable while unregistering thumbnail");
            }
        }

        public void Move(int left, int top, int right, int bottom)
        {
            this._properties.rcDestination = new RECT(left, top, right, bottom);
        }

        public void Update()
        {
            if ((!this._windowManager.IsCompositionEnabled) || (this._handle == IntPtr.Zero))
            {
                return;
            }

            try
            {
                DwmNativeMethods.DwmUpdateThumbnailProperties(this._handle, this._properties);
            }
            catch (ArgumentException ex)
            {
                _logger.Verbose(ex, "DWM thumbnail update failed: Source window may have been closed. Handle=0x{Handle:X}", this._handle);
            }
            catch (COMException ex)
            {
                _logger.Warning(ex, "DWM thumbnail update failed: DWM unavailable");
            }
        }
    }
}
