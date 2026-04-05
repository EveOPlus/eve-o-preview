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

using EveOPreview.Services.Interface;
using EveOPreview.Services.Interop;
using System;
using System.Drawing;
using System.Net;
using System.Runtime.InteropServices;
using EveOPreview.Helper;
using Serilog;

namespace EveOPreview.Services.Implementation
{
    public class WindowManager : IWindowManager
    {
        private readonly IHookService _hookService;
        private readonly ILogger _logger;

        #region Private constants
        private const int WINDOW_SIZE_THRESHOLD = 300;
        #endregion
        
        public WindowManager(IHookService hookService, ILogger logger)
        {
            _hookService = hookService;
            _logger = logger;

            // Composition is always enabled for Windows 8+
            this.IsCompositionEnabled = 
                ((Environment.OSVersion.Version.Major == 6) && (Environment.OSVersion.Version.Minor >= 2)) // Win 8 and Win 8.1
                || (Environment.OSVersion.Version.Major >= 10) // Win 10
                || DwmNativeMethods.DwmIsCompositionEnabled(); // In case of Win 7 an API call is requiredWin 7
            
            _logger.Information("WindowManager initialized: CompositionEnabled={IsCompositionEnabled}, OS={OSVersion}", 
                this.IsCompositionEnabled, Environment.OSVersion.VersionString);
        }

        public bool IsCompositionEnabled { get; }
        
        /// <summary>
        /// Track the state if the manager is part way through switching a client at the moment.
        /// </summary>
        public bool IsCurrentlySwitching { get; set; } = false;

        public IntPtr GetForegroundWindowHandle()
        {
            var handle = User32NativeMethods.GetForegroundWindow();
            _logger.Verbose("WindowManager.GetForegroundWindowHandle: Result=0x{Handle:X}", handle);
            return handle;
        }

        public void MakeApiCallsToSetForegroundAndFocus(IntPtr handle)
        {
            _logger.Verbose("WindowManager.MakeApiCallsToSetForegroundAndFocus: Setting foreground and focus for 0x{Handle:X}", handle);
            bool success = User32NativeMethods.SetForegroundWindow(handle);
            User32NativeMethods.SetFocus(handle);

            if (!success)
            {
                _logger.Verbose("WindowManager.MakeApiCallsToSetForegroundAndFocus: SetForegroundWindow failed, using SwitchToThisWindow");
                User32NativeMethods.SwitchToThisWindow(handle, false);
            }
        }

        public void ActivateWindow(IntPtr handle)
        {
            _logger.Verbose("WindowManager.ActivateWindow: Activating window 0x{Handle:X}", handle);

            _ = _hookService.TellEveClientFocusIsComingAsync(handle);

            try
            {
                IsCurrentlySwitching = true;
                MakeApiCallsToSetForegroundAndFocus(handle);

                int style = User32NativeMethods.GetWindowLong(handle, InteropConstants.GWL_STYLE);

                if ((style & InteropConstants.WS_MINIMIZE) == InteropConstants.WS_MINIMIZE)
                {
                    _logger.Verbose("WindowManager.ActivateWindow: Window 0x{Handle:X} was minimized, restoring", handle);
                    User32NativeMethods.ShowWindowAsync(handle, InteropConstants.SW_RESTORE);
                }
                
                _logger.Verbose("WindowManager.ActivateWindow: Window 0x{Handle:X} activated successfully", handle);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "WindowManager.ActivateWindow: Error activating window 0x{Handle:X}", handle);
            }
            finally
            {
                IsCurrentlySwitching = false;
            }
        }

        public void MinimizeWindow(IntPtr handle, bool enableAnimation)
        {
            _logger.Verbose("WindowManager.MinimizeWindow: Minimizing 0x{Handle:X}, Animation={EnableAnimation}", handle, enableAnimation);
            
            if (enableAnimation)
            {
                _logger.Verbose("WindowManager.MinimizeWindow: Using animated minimize");
                User32NativeMethods.SendMessage(handle, InteropConstants.WM_SYSCOMMAND, InteropConstants.SC_MINIMIZE, 0);
            }
            else
            {
                _logger.Verbose("WindowManager.MinimizeWindow: Using non-animated minimize");
                WINDOWPLACEMENT param = new WINDOWPLACEMENT();
                param.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
                User32NativeMethods.GetWindowPlacement(handle, ref param);
                param.showCmd = WINDOWPLACEMENT.SW_MINIMIZE;
                User32NativeMethods.SetWindowPlacement(handle, ref param);
            }
        }

        public void MoveWindow(IntPtr handle, int left, int top, int width, int height)
        {
            _logger.Verbose("WindowManager.MoveWindow: Moving 0x{Handle:X} to ({Left},{Top}) size {Width}x{Height}", handle, left, top, width, height);
            User32NativeMethods.MoveWindow(handle, left, top, width, height, true);
        }

        public void MaximizeWindow(IntPtr handle)
        {
            _logger.Verbose("WindowManager.MaximizeWindow: Maximizing 0x{Handle:X}", handle);
            User32NativeMethods.ShowWindowAsync(handle, InteropConstants.SW_SHOWMAXIMIZED);
        }

        public (int Left, int Top, int Right, int Bottom) GetWindowPosition(IntPtr handle)
        {
            User32NativeMethods.GetWindowRect(handle, out RECT windowRectangle);
            _logger.Verbose("WindowManager.GetWindowPosition: Handle=0x{Handle:X} Position=({Left},{Top},{Right},{Bottom})", 
                handle, windowRectangle.Left, windowRectangle.Top, windowRectangle.Right, windowRectangle.Bottom);

            return (windowRectangle.Left, windowRectangle.Top, windowRectangle.Right, windowRectangle.Bottom);
        }

        public bool IsWindowMaximized(IntPtr handle)
        {
            bool result = User32NativeMethods.IsZoomed(handle);
            _logger.Verbose("WindowManager.IsWindowMaximized: Handle=0x{Handle:X} Result={IsMaximized}", handle, result);
            return result;
        }

        public bool IsWindowMinimized(IntPtr handle)
        {
            bool result = User32NativeMethods.IsIconic(handle);
            _logger.Verbose("WindowManager.IsWindowMinimized: Handle=0x{Handle:X} Result={IsMinimized}", handle, result);
            return result;
        }

        public IDwmThumbnail GetLiveThumbnail(IntPtr destination, IntPtr source)
        {
            _logger.Verbose("WindowManager.GetLiveThumbnail: Creating live thumbnail. Destination=0x{Destination:X}, Source=0x{Source:X}", destination, source);
            IDwmThumbnail thumbnail = new DwmThumbnail(this, _logger);
            thumbnail.Register(destination, source);

            return thumbnail;
        }

        public Image GetStaticThumbnail(IntPtr source)
        {
            _logger.Verbose("WindowManager.GetStaticThumbnail: Capturing static thumbnail for 0x{Handle:X}", source);
            
            var sourceContext = User32NativeMethods.GetDC(source);

            User32NativeMethods.GetClientRect(source, out RECT windowRect);

            var width = windowRect.Right - windowRect.Left;
            var height = windowRect.Bottom - windowRect.Top;

            _logger.Verbose("WindowManager.GetStaticThumbnail: Window size {Width}x{Height}", width, height);

            // Check if there is anything to make thumbnail of
            if ((width < WINDOW_SIZE_THRESHOLD) || (height < WINDOW_SIZE_THRESHOLD))
            {
                _logger.Verbose("WindowManager.GetStaticThumbnail: Window too small for thumbnail (threshold={Threshold})", WINDOW_SIZE_THRESHOLD);
                return null;
            }

            var destContext = Gdi32NativeMethods.CreateCompatibleDC(sourceContext);
            var bitmap = Gdi32NativeMethods.CreateCompatibleBitmap(sourceContext, width, height);

            var oldBitmap = Gdi32NativeMethods.SelectObject(destContext, bitmap);
            Gdi32NativeMethods.BitBlt(destContext, 0, 0, width, height, sourceContext, 0, 0, Gdi32NativeMethods.SRCCOPY);
            Gdi32NativeMethods.SelectObject(destContext, oldBitmap);
            Gdi32NativeMethods.DeleteDC(destContext);
            User32NativeMethods.ReleaseDC(source, sourceContext);

            Image image = Image.FromHbitmap(bitmap);
            Gdi32NativeMethods.DeleteObject(bitmap);

            _logger.Verbose("WindowManager.GetStaticThumbnail: Static thumbnail captured successfully");
            return image;
        }

        public void PredictUpcomingClient(IntPtr upcomingHandle)
        {
            if (upcomingHandle == IntPtr.Zero)
            {
                _logger.Verbose("WindowManager.PredictUpcomingClient: Handle is null, skipping prediction");
                return;
            }

            _logger.Verbose("WindowManager.PredictUpcomingClient: Predicting upcoming client 0x{Handle:X}", upcomingHandle);
            _hookService.TellEveClientFocusIsMaybeComingSoonAsync(upcomingHandle);
        }
    }
}
