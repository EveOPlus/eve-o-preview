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
using System.Windows.Forms;
using System.ComponentModel;

namespace EveOPreview.UI.Hotkeys
{
    class HotkeyHandler : IMessageFilter, IDisposable
    {
        private static int _currentId;
        private const int MAX_ID = 0xBFFF;

        #region Private fields
        private readonly int _hotkeyId;
        private readonly IntPtr _hotkeyTarget;
        #endregion

        public HotkeyHandler(IntPtr target, Keys hotkey)
        {
            this._hotkeyId = HotkeyHandler._currentId;
            HotkeyHandler._currentId = (HotkeyHandler._currentId + 1) & HotkeyHandler.MAX_ID;

            this._hotkeyTarget = target;

            // Assign properties
            this.IsRegistered = false;

            this.KeyCode = hotkey;
        }

        public void Dispose()
        {
            this.Unregister();
            GC.SuppressFinalize(this);
        }

        ~HotkeyHandler()
        {
            // Unregister the hotkey if necessary
            this.Unregister();
        }

        public bool IsRegistered { get; private set; }

        public Keys KeyCode { get; private set; }

        public event HandledEventHandler Pressed;

        public bool CanRegister()
        {
            // Attempt to register
            if (this.Register())
            {
                // Unregister and say we managed it
                this.Unregister();
                return true;
            }

            return false;
        }

        public bool Register()
        {
            // Check that we have not registered
            if (this.IsRegistered)
            {
                return false;
            }

            if (this.KeyCode == Keys.None)
            {
                return false;
            }

            // Remove all modifiers from the 'main' hotkey
            uint key = (uint)this.KeyCode & (~(uint)Keys.Alt) & (~(uint)Keys.Control) & (~(uint)Keys.Shift);

            // Get unmanaged version of the modifiers code
            uint modifiers = (this.KeyCode.HasFlag(Keys.Alt) ? HotkeyHandlerNativeMethods.MOD_ALT : 0)
                             | (this.KeyCode.HasFlag(Keys.Control) ? HotkeyHandlerNativeMethods.MOD_CONTROL : 0)
                             | (this.KeyCode.HasFlag(Keys.Shift) ? HotkeyHandlerNativeMethods.MOD_SHIFT : 0);

            // Register the hotkey
            if (!HotkeyHandlerNativeMethods.RegisterHotKey(this._hotkeyTarget, this._hotkeyId, modifiers, key))
            {
                return false;
            }

            Application.AddMessageFilter(this);

            this.IsRegistered = true;

            // We successfully registered
            return true;
        }

        public void Unregister()
        {
            // Check that we have registered
            if (!this.IsRegistered)
            {
                return;
            }

            this.IsRegistered = false;

            Application.RemoveMessageFilter(this);

            // Clean up after ourselves
            HotkeyHandlerNativeMethods.UnregisterHotKey(this._hotkeyTarget, this._hotkeyId);
        }

        #region IMessageFilter
        public bool PreFilterMessage(ref Message message)
        {
            return this.IsRegistered
                    && (message.Msg == HotkeyHandlerNativeMethods.WM_HOTKEY)
                    && (message.WParam.ToInt32() == this._hotkeyId)
                    && this.OnPressed();
        }
        #endregion

        private bool OnPressed()
        {
            // Fire the event if we can
            HandledEventArgs handledEventArgs = new HandledEventArgs(false);
            this.Pressed?.Invoke(this, handledEventArgs);

            // Return whether we handled the event or not
            return handledEventArgs.Handled;
        }
    }
}