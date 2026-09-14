using System;
using System.Drawing;
using System.Windows.Forms;

namespace EveOPreview.View.Rendering;

/// <summary>
/// Alpha wash for colour-key compatibility graphics. One solid owned window;
/// no screenshot, frame bitmap, per-pixel upload or game-window modification.
/// Native composition uses its retained one-pixel visual instead.
/// </summary>
internal sealed class CompatibilityDamageTint : Form
{
    public CompatibilityDamageTint(Form owner)
    {
        Owner = owner;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.Red;
        Opacity = 0;
    }

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            // Tool window, no activation, and input passes to the preview underneath.
            parameters.ExStyle |= 0x00000080 | 0x08000000 | 0x00000020;
            return parameters;
        }
    }

    public void UpdateTint(uint? tint, double opacity, bool visible)
    {
        if (Owner is null || Owner.IsDisposed) return;
        Bounds = Owner.Bounds;
        if (tint is { } color) BackColor = Color.FromArgb(unchecked((int)(color | 0xFF000000u)));
        Opacity = visible && tint is { } value ? (value >> 24) / 255d * opacity : 0;
        // Show once. On/off phases only change alpha, never raise/recreate a window.
        // Owner visibility makes the retained window transparent while hidden.
        if (visible && tint.HasValue && !IsHandleCreated) Show(Owner);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x0084) { message.Result = new IntPtr(-1); return; } // HTTRANSPARENT
        base.WndProc(ref message);
    }
}
