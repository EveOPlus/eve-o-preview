using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using EveOPreview.Configuration;
using EveOPreview.Services.Interop;


namespace EveOPreview.Services.Implementation
{
	class DwmThumbnail : IDwmThumbnail
	{
		#region Private fields
		private readonly IWindowManager _windowManager;
        private IThumbnailConfiguration _config;
        private IntPtr _handle;
		private DWM_THUMBNAIL_PROPERTIES _properties;
		#endregion

		public DwmThumbnail(IWindowManager windowManager, IThumbnailConfiguration config)
		{
			this._windowManager = windowManager;
			this._handle = IntPtr.Zero;
			this._config = config; 

		}


        [DllImport("user32.dll")]
		static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        private string GetWindowTitle(IntPtr handle)
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);

            if (GetWindowText(handle, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return null;
        }

        public void Register(IntPtr destination, IntPtr source)
		{
			Rectangle cc = new Rectangle();
            String clientTitle = this.GetWindowTitle(source);
            if (clientTitle != null)
			{
				cc = this._config.GetClientCrop(clientTitle);
			}
            if (cc.IsEmpty)
            {
                cc = this._config.GetClientCrop("*");
            }

            this._properties = new DWM_THUMBNAIL_PROPERTIES();
            this._properties.dwFlags = DWM_TNP_CONSTANTS.DWM_TNP_VISIBLE
									   + DWM_TNP_CONSTANTS.DWM_TNP_OPACITY
                                       + DWM_TNP_CONSTANTS.DWM_TNP_RECTDESTINATION;
            this._properties.opacity = 255;
            this._properties.fVisible = true;

            if (cc.IsEmpty)
			{
                this._properties.dwFlags += DWM_TNP_CONSTANTS.DWM_TNP_SOURCECLIENTAREAONLY;
                this._properties.fSourceClientAreaOnly = true;
			}
			else
            {
                this._properties.dwFlags += DWM_TNP_CONSTANTS.DWM_TNP_RECTSOURCE;
                this._properties.rcSource = new RECT(cc.X, cc.Y, cc.Right, cc.Bottom);
            }

            if (!this._windowManager.IsCompositionEnabled)
			{
				return;
			}

			try
			{
				this._handle = DwmNativeMethods.DwmRegisterThumbnail(destination, source);
			}
			catch (ArgumentException)
			{
				// This exception is raised if the source client is already closed
				// Can happen on a really slow CPU's that the window is still being
				// listed in the process list yet it already cannot be used as
				// a thumbnail source
				this._handle = IntPtr.Zero;
			}
			catch (COMException)
			{
				// This exception is raised if DWM is suddenly not available
				// (f.e. when switching between Windows user accounts)
				this._handle = IntPtr.Zero;
			}
		}

		public void Unregister()
		{
			if ((!this._windowManager.IsCompositionEnabled) || (this._handle == IntPtr.Zero))
			{
				return;
			}

			try
			{
				DwmNativeMethods.DwmUnregisterThumbnail(this._handle);
			}
			catch (ArgumentException)
			{
			}
			catch (COMException)
			{
				// This exception is raised when DWM is not available for some reason
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
			catch (ArgumentException)
			{
				// This exception will be thrown if the EVE client disappears while this method is running
			}
			catch (COMException)
			{
				// This exception is raised when DWM is not available for some reason
			}
		}
	}
}
