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

using EveOPreview.Services;
using System;
using System.Drawing;
using System.Windows.Forms;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Preview;
using EveOPreview.View.Rendering;
using System.Collections.Generic;
using System.Linq;

namespace EveOPreview.View
{
    public partial class ThumbnailOverlay : Form
    {
        #region Private fields
        private readonly Action<object, MouseEventArgs> _areaClickAction;
        private bool _titleEnabled = true;
        private bool _cycleSkipped;
        private OverlayRendererKind _rendererKind;
        private IOverlayRenderer _renderer;
        private OverlayScene _scene = new();
        private double _overlayOpacity = 1;
        private Font _ownedFont;
        private bool _creatingHandle;
        private bool _recreateForCompatibility;
        private CompatibilityDamageTint _compatibilityTint;
        #endregion

        public ThumbnailOverlay(Form owner, Action<object, MouseEventArgs> areaClickAction)
            : this(owner, areaClickAction, OverlayRendererKind.Legacy) { }

        public ThumbnailOverlay(Form owner, Action<object, MouseEventArgs> areaClickAction, OverlayRendererKind rendererKind)
        {
            _rendererKind = rendererKind;
            this.Owner = owner;
            this._areaClickAction = areaClickAction;

            InitializeComponent();
            OverlayLabel.MouseUp += OverlayArea_Click;
            if (_rendererKind == OverlayRendererKind.NativeComposition)
            {
                TransparencyKey = Color.Empty;
                BackColor = Color.Black;
                foreach (Control child in Controls) child.Visible = false;
            }
        }

        public OverlayRendererKind RendererKind => _rendererKind;
        internal OverlayScene Scene => _scene;
        public OverlayCapabilities GraphicsCapabilities => _renderer?.Capabilities
            ?? (OverlayCapabilities.Title | OverlayCapabilities.CycleMarker);

        // Form.Opacity creates a layered HWND, incompatible with the native composition target.
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public new double Opacity
        {
            get => _overlayOpacity;
            set
            {
                _overlayOpacity = Math.Clamp(value, 0, 1);
                if (_rendererKind == OverlayRendererKind.Legacy) base.Opacity = _overlayOpacity;
                else Render(renderer => renderer.SetOpacity(_overlayOpacity));
                UpdateCompatibilityTint();
            }
        }

        public void SetStats(IReadOnlyList<OverlayStat> stats, OverlayStatsStyle style = null)
        {
            var next = stats?.Take(8).ToArray() ?? Array.Empty<OverlayStat>();
            style ??= _scene.StatsStyle;
            if (_scene.Stats.SequenceEqual(next) && _scene.StatsStyle == style) return;
            _scene = _scene with { Stats = next, StatsStyle = style };
            OverlayLabel.Visible = _rendererKind == OverlayRendererKind.Legacy && UseCompatibilityLabel && (_titleEnabled || _cycleSkipped);
            Render(renderer => renderer.SetScene(_scene));
            if (_rendererKind == OverlayRendererKind.Legacy) Invalidate();
        }

        public void SetSubtitle(string text, uint color, SubtitlePlacement placement = SubtitlePlacement.Below, float? fontSize = null)
        {
            if (_scene.Subtitle == text && _scene.SubtitleColor == color && _scene.SubtitlePlacement == placement && _scene.SubtitleFontSize == fontSize) return;
            _scene = _scene with { Subtitle = text, SubtitleColor = color, SubtitlePlacement = placement, SubtitleFontSize = fontSize };
            OverlayLabel.Visible = _rendererKind == OverlayRendererKind.Legacy && UseCompatibilityLabel && (_titleEnabled || _cycleSkipped);
            Render(renderer => renderer.SetScene(_scene));
            if (_rendererKind == OverlayRendererKind.Legacy) Invalidate();
        }

        public void SetTitleColor(uint? color) => SetDamageFlash(color, _scene.DamageTint, _scene.DamageFlashIntensity);
        public void SetTitlePosition(OverlayPosition position)
        {
            if (_scene.TitlePosition == position) return;
            _scene = _scene with { TitlePosition = position };
            OverlayLabel.Visible = _rendererKind == OverlayRendererKind.Legacy && UseCompatibilityLabel && (_titleEnabled || _cycleSkipped);
            Render(renderer => renderer.SetScene(_scene));
            if (_rendererKind == OverlayRendererKind.Legacy) Invalidate();
        }

        public void SetDamageFlash(uint? titleColor, uint? tint, double intensity = 1)
        {
            intensity = double.IsFinite(intensity) ? Math.Clamp(intensity, 0, 1) : 0;
            if (_scene.TitleColor == titleColor && _scene.DamageTint == tint && _scene.DamageFlashIntensity == intensity) return;
            _scene = _scene with { TitleColor = titleColor, DamageTint = tint, DamageFlashIntensity = intensity };
            OverlayLabel.ForeColor = Color.FromArgb(unchecked((int)_scene.EffectiveTitleColor));
            Render(renderer => renderer.SetScene(_scene));
            UpdateCompatibilityTint();
            if (_rendererKind == OverlayRendererKind.Legacy && !UseCompatibilityLabel) Invalidate();
        }

        private void UpdateCompatibilityTint()
        {
            if (IsDisposed || _rendererKind != OverlayRendererKind.Legacy) return;
            if (_scene.DamageTint.HasValue && Visible) _compatibilityTint ??= new CompatibilityDamageTint(this);
            _compatibilityTint?.UpdateTint(_scene.EffectiveDamageTint, _overlayOpacity, Visible);
        }

        internal void SetAlertBounds(PreviewRect? bounds)
        {
            if (_scene.AlertBounds == bounds) return;
            _scene = _scene with { AlertBounds = bounds };
            Render(renderer => renderer.SetScene(_scene));
        }

        internal void SetActiveBorder(OverlayBorder border)
        {
            if (_scene.ActiveBorder == border && _scene.AlertBounds == border?.InnerBounds) return;
            _scene = _scene with { ActiveBorder = border, AlertBounds = border?.InnerBounds };
            Render(renderer => renderer.SetScene(_scene));
        }

        public void ShowAlert(PreviewAlert alert)
        {
            if (Visible && !IsDisposed) Render(renderer => renderer.ShowAlert(alert.Normalize()));
        }

        public void ClearAlerts() => Render(renderer => renderer.ClearAlerts());
        public void RestoreGraphicsVisibility()
        {
            Render(renderer => renderer.SetVisible(true));
            UpdateCompatibilityTint();
        }

        internal void MaintainGraphics()
        {
            // One throttled device-health query is shared by all native overlays.
            // It submits no drawing work and also recovers otherwise-idle titles.
            if (_renderer is not NativeCompositionOverlayRenderer native) return;
            try { native.CheckDeviceHealth(); }
            catch (System.Runtime.InteropServices.COMException ex) { UseCompatibilityRenderer(ex); }
        }

        private void Render(Action<IOverlayRenderer> action)
        {
            if (_renderer == null)
            {
                if (_rendererKind == OverlayRendererKind.Legacy && !UseCompatibilityLabel) Invalidate();
                return;
            }
            try { action(_renderer); }
            catch (System.Runtime.InteropServices.COMException ex) { UseCompatibilityRenderer(ex); }
        }

        private void UseCompatibilityRenderer(Exception ex)
        {
            _renderer?.Dispose();
            _renderer = null;
            _rendererKind = OverlayRendererKind.Legacy;
            Serilog.Log.Warning(ex, "Native preview overlay unavailable; using the compatibility overlay");
            UpdateStyles();
            BackColor = Color.Fuchsia;
            TransparencyKey = Color.Fuchsia;
            base.Opacity = _overlayOpacity;
            foreach (Control child in Controls) child.Visible = true;
            OverlayLabel.Visible = UseCompatibilityLabel && (_titleEnabled || _cycleSkipped);
            UpdateCompatibilityTint();
            // Redirection allocation is fixed when the native HWND is created.
            // UpdateStyles alone cannot reliably restore a GDI backing surface.
            if (IsHandleCreated)
            {
                if (_creatingHandle) _recreateForCompatibility = true;
                else RecreateHandle();
            }
        }

        protected override void CreateHandle()
        {
            _creatingHandle = true;
            try { base.CreateHandle(); }
            finally { _creatingHandle = false; }
            if (_recreateForCompatibility)
            {
                _recreateForCompatibility = false;
                RecreateHandle();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (_rendererKind != OverlayRendererKind.NativeComposition) return;
            try
            {
                _renderer = new NativeCompositionOverlayRenderer(Handle);
                _renderer.Resize(new PreviewSize(ClientSize.Width, ClientSize.Height));
                _renderer.SetScene(_scene);
                _renderer.SetOpacity(_overlayOpacity);
                _renderer.SetVisible(Visible);
            }
            catch (Exception ex)
            {
                UseCompatibilityRenderer(ex);
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _renderer?.Dispose();
            _renderer = null;
            base.OnHandleDestroyed(e);
        }

        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnClientSizeChanged(e);
            Render(renderer => renderer.Resize(new PreviewSize(ClientSize.Width, ClientSize.Height)));
            UpdateCompatibilityTint();
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            UpdateCompatibilityTint();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            Render(renderer => renderer.SetVisible(Visible));
            UpdateCompatibilityTint();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (_rendererKind == OverlayRendererKind.Legacy) base.OnPaintBackground(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Compatibility graphics used by modern themes retain the same telemetry.
            // The host supplies no telemetry when the application theme is Legacy.
            if (_rendererKind == OverlayRendererKind.Legacy && (_scene.Stats.Count > 0 || !UseCompatibilityLabel))
                OverlaySceneRasterizer.Draw(e.Graphics, _scene, new(ClientSize.Width, ClientSize.Height), drawTitle: !UseCompatibilityLabel);
        }
        private bool UseCompatibilityLabel => _scene.Subtitle.Length == 0 && _scene.TitlePosition == OverlayPosition.TopLeft
            && (_scene.Stats.Count == 0 || _scene.StatsStyle.EffectivePosition != OverlayPosition.TopLeft);

        protected override void WndProc(ref Message message)
        {
            if (_rendererKind == OverlayRendererKind.NativeComposition && message.Msg == 0x0084) // WM_NCHITTEST
            {
                // The paired preview is on this UI thread and owns every gesture.
                // Passing through the entire visual keeps hover, right-hold movement,
                // resize and menus identical on the game image and painted text.
                message.Result = new IntPtr(-1); // HTTRANSPARENT
                return;
            }
            base.WndProc(ref message);
        }

        private void OverlayArea_Click(object sender, MouseEventArgs e)
        {
            // Label events use label-local coordinates; the preview opens its menu
            // in owner-client coordinates so the first action stays under the click.
            var source = sender as Control ?? this;
            Point point = Owner.PointToClient(source.PointToScreen(e.Location));
            this._areaClickAction(this, new MouseEventArgs(e.Button, e.Clicks, point.X, point.Y, e.Delta));
        }

        public void SetOverlayLabel(string label)
        {
            if (_scene.Title == label) return;
            this.OverlayLabel.Text = label;
            _scene = _scene with { Title = label };
            Render(renderer => renderer.SetScene(_scene));
        }
        
        public void SetOverlayFont(FontSettings fontSettings)
        {
            var font = new OverlayFont(fontSettings.Name, fontSettings.Size, (OverlayFontStyle)fontSettings.Style,
                unchecked((uint)fontSettings.ForeColor.ToArgb()), unchecked((uint)fontSettings.OutlineColor.ToArgb()),
                fontSettings.OutlineWidth, fontSettings.PositionOffsetFromLeft, fontSettings.PositionOffsetFromTop);
            if (_scene.Font == font) return;
            var previous = _ownedFont;
            _ownedFont = new Font(fontSettings.Name, fontSettings.Size, fontSettings.Style);
            this.OverlayLabel.Font = _ownedFont;
            previous?.Dispose();
            _scene = _scene with { Font = font };
            this.OverlayLabel.ForeColor = Color.FromArgb(unchecked((int)_scene.EffectiveTitleColor));
            this.OverlayLabel.OutlineColor = fontSettings.OutlineColor;
            this.OverlayLabel.OutlineWidth = fontSettings.OutlineWidth;
            this.OverlayLabel.Top = fontSettings.PositionOffsetFromTop;
            this.OverlayLabel.Left = fontSettings.PositionOffsetFromLeft;
            Render(renderer => renderer.SetScene(_scene));
        }

        public void EnableOverlayLabel(bool enable)
        {
            _titleEnabled = enable;
            this.OverlayLabel.SetTitleVisible(enable);
            this.OverlayLabel.Visible = _rendererKind == OverlayRendererKind.Legacy && UseCompatibilityLabel && (enable || _cycleSkipped);
            if (_scene.ShowTitle == enable) return;
            _scene = _scene with { ShowTitle = enable };
            Render(renderer => renderer.SetScene(_scene));
        }

        public void SetCycleSkipIndicator(bool skipped, string style, Color color)
        {
            _cycleSkipped = skipped;
            OverlayLabel.SetCycleSkipIndicator(skipped, style, color);
            OverlayLabel.Visible = _rendererKind == OverlayRendererKind.Legacy && UseCompatibilityLabel && (_titleEnabled || skipped);
            var next = _scene with { CycleSkipped = skipped, MarkerStyle = style switch
                { "Pause" => CycleMarkerStyle.Pause, "Cross" => CycleMarkerStyle.Cross, _ => CycleMarkerStyle.CircleSlash },
                MarkerColor = unchecked((uint)color.ToArgb()) };
            if (_scene == next) return;
            _scene = next;
            Render(renderer => renderer.SetScene(_scene));
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var Params = base.CreateParams;
                Params.ExStyle |= (int)InteropConstants.WS_EX_TOOLWINDOW;
                Params.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                Params.ExStyle &= ~0x00200000; // A failed native target must restore GDI redirection.
                if (_rendererKind == OverlayRendererKind.NativeComposition)
                {
                    Params.ExStyle |= 0x00200000; // WS_EX_NOREDIRECTIONBITMAP: alpha comes from composition.
                    Params.ExStyle &= ~0x00080000; // WS_EX_LAYERED is the compatibility renderer only.
                }
                return Params;
            }
        }
    }
}
