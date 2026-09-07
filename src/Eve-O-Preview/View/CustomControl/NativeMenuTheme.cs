using System.ComponentModel;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EveOPreview.UI;

namespace EveOPreview.View.CustomControl;

/// <summary>
/// Applies the workspace theme to Windows tray and preview menus when they open.
/// Menus own their event handlers; no global menu collection or preview subscription is retained.
/// </summary>
public static class NativeMenuTheme
{
    public static string CurrentTheme { get; set; } = "Dark";
    public static IReadOnlyList<string> ThumbnailMenuOrder { get; set; } = ThumbnailMenuActions.DefaultOrder;
    public static string ThumbnailTheme { get; set; } = ThumbnailMenuThemes.FollowApp;

    public static void ApplyThumbnailOrder(ContextMenuStrip menu)
    {
        var order = ThumbnailMenuActions.Normalize(ThumbnailMenuOrder);
        var items = order.Select(id => ThumbnailMenuActions.IsDivider(id)
            ? menu.Items[id] ?? new ToolStripSeparator { Name = id }
            : menu.Items[id switch
        {
            "minimize" => "menuMinimize",
            "minimize-all" => "minimizeAllToolStripMenuItem",
            "skip-cycling" => "menuCycleSkip",
            "move" => "menuReposition",
            "resize" => "resizeThumbnailToolStripMenuItem",
            _ => ""
        }]).Where(item => item != null).ToList();
        if (menu.Items.Cast<ToolStripItem>().SequenceEqual(items)) return;
        var removed = menu.Items.OfType<ToolStripSeparator>().Where(item => !items.Contains(item)).ToArray();
        menu.Items.Clear();
        foreach (var separator in removed) separator.Dispose();
        menu.Items.AddRange(items.ToArray());
    }

    public static void Track(ContextMenuStrip menu, bool thumbnail = false)
    {
        menu.Opening -= MenuOpening;
        menu.Opening -= ThumbnailMenuOpening;
        if (thumbnail) menu.Opening += ThumbnailMenuOpening;
        else menu.Opening += MenuOpening;
        Apply(menu, thumbnail);
    }

    private static void MenuOpening(object sender, CancelEventArgs e)
    {
        if (sender is ContextMenuStrip menu) Apply(menu);
    }

    private static void ThumbnailMenuOpening(object sender, CancelEventArgs e)
    {
        if (sender is ContextMenuStrip menu) Apply(menu, thumbnail: true);
    }

    private static void Apply(ContextMenuStrip menu, bool thumbnail = false)
    {
        if (SystemInformation.HighContrast)
        {
            menu.BackColor = SystemColors.Menu;
            menu.ForeColor = SystemColors.MenuText;
            menu.Renderer = new ToolStripSystemRenderer();
            return;
        }

        if (CurrentTheme == "Legacy" && (!thumbnail || ThumbnailTheme == ThumbnailMenuThemes.FollowApp))
        {
            menu.BackColor = Color.FromArgb(20, 20, 22);
            menu.ForeColor = Color.FromArgb(212, 175, 55);
            menu.Renderer = new DarkGoldRenderer();
            return;
        }

        var palette = ThumbnailMenuThemes.Resolve(thumbnail ? ThumbnailTheme : ThumbnailMenuThemes.FollowApp, CurrentTheme);
        var colors = new MenuColors(ColorTranslator.FromHtml(palette.Background), ColorTranslator.FromHtml(palette.Foreground),
            ColorTranslator.FromHtml(palette.Selection), ColorTranslator.FromHtml(palette.Border), ColorTranslator.FromHtml(palette.Muted));
        menu.BackColor = colors.Background;
        menu.ForeColor = colors.Foreground;
        menu.Renderer = new MenuRenderer(colors);
    }

    private sealed record MenuColors(Color Background, Color Foreground, Color Selection, Color Border, Color Muted);

    private sealed class MenuRenderer(MenuColors colors) : ToolStripProfessionalRenderer(new MenuColorTable(colors))
    {
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? colors.Foreground : colors.Muted;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item.Enabled ? colors.Foreground : colors.Muted;
            base.OnRenderArrow(e);
        }
    }

    private sealed class MenuColorTable(MenuColors colors) : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => colors.Background;
        public override Color ImageMarginGradientBegin => colors.Background;
        public override Color ImageMarginGradientMiddle => colors.Background;
        public override Color ImageMarginGradientEnd => colors.Background;
        public override Color MenuItemSelected => colors.Selection;
        public override Color MenuItemSelectedGradientBegin => colors.Selection;
        public override Color MenuItemSelectedGradientEnd => colors.Selection;
        public override Color MenuItemPressedGradientBegin => colors.Selection;
        public override Color MenuItemPressedGradientMiddle => colors.Selection;
        public override Color MenuItemPressedGradientEnd => colors.Selection;
        public override Color MenuItemBorder => colors.Border;
        public override Color MenuBorder => colors.Border;
        public override Color SeparatorDark => colors.Border;
        public override Color SeparatorLight => colors.Border;
        public override Color CheckBackground => colors.Selection;
        public override Color CheckSelectedBackground => colors.Selection;
        public override Color CheckPressedBackground => colors.Selection;
    }
}
