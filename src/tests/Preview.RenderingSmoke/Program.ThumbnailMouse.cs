using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using ContextMenu = Avalonia.Controls.ContextMenu;
using MenuItem = Avalonia.Controls.MenuItem;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using EveOPreview.Configuration;
using EveOPreview.Services;
using EveOPreview.View;

namespace EveOPreview.RenderingSmoke;

internal static partial class Program
{
    private static object ValidateThumbnailMouse(IReadOnlyList<ThumbnailView> views, IThumbnailManager manager, IThumbnailConfiguration config)
    {
        Native.GetCursorPos(out var originalCursor);
        var checks = new List<string>();
        var saved = views.Select(v => (v, v.Location, v.Size)).ToArray();
        bool zoom = config.ThumbnailZoomEnabled;
        var managerType = manager.GetType();
        Action<nint> Handler(string name) => managerType.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.CreateDelegate<Action<nint>>(manager);
        foreach (var view in views)
        {
            view.ThumbnailActivated = Handler("ThumbnailActivated");
            view.ThumbnailFocused = Handler("ThumbnailViewFocused");
            view.ThumbnailLostFocus = Handler("ThumbnailViewLostFocus");
            view.ThumbnailMoved = Handler("ThumbnailViewMoved");
            view.ThumbnailResized = Handler("ThumbnailViewResized");
        }
        void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException("Thumbnail mouse check failed: " + check);
            checks.Add(check);
        }
        void Away()
        {
            var screen = Screen.PrimaryScreen!.WorkingArea;
            Native.SetCursorPos(screen.Right - 20, screen.Bottom - 20);
            Pump(TimeSpan.FromMilliseconds(100));
        }
        void Target(ThumbnailView view, bool title = false)
        {
            Away(); view.RestoreAndBringToFront();
            var point = view.PointToScreen(new(view.ClientSize.Width / 2, title ? 10 : view.ClientSize.Height * 3 / 4));
            Native.SetCursorPos(point.X, point.Y); Pump(TimeSpan.FromMilliseconds(80));
            Native.GetCursorPos(out var actualPointer);
            var root = Native.GetAncestor(Native.WindowFromPoint(actualPointer), 2);
            if (root != view.Handle && root != Overlay(view).Handle)
                throw new InvalidOperationException($"The intended thumbnail is occluded or the source constrained the cursor: requested={point}, actual={actualPointer}, root={root}; no mouse button was sent.");
        }
        void Focused(ThumbnailView view, string check)
        {
            var deadline = Stopwatch.StartNew();
            while (Native.GetForegroundWindow() != view.Id && deadline.ElapsedMilliseconds < 1000) Pump(TimeSpan.FromMilliseconds(2));
            Require(Native.GetForegroundWindow() == view.Id, check);
        }
        ContextMenu Menu(ThumbnailView view) => (ContextMenu)typeof(ThumbnailView).GetField("thumbnailContextMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
        var previousGuard = Native.MouseDownGuard;
        Native.MouseDownGuard = point =>
        {
            var root = Native.GetAncestor(Native.WindowFromPoint(point), 2);
            return views.Any(view => root == view.Handle || root == Overlay(view).Handle ||
                (Menu(view).IsOpen && root == TopLevel.GetTopLevel(Menu(view))?.TryGetPlatformHandle()?.Handle));
        };
        string MouseMode(ThumbnailView view) => typeof(ThumbnailView).GetField("_customMouseModeActive", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!.ToString()!;
        void SelectMenu(ThumbnailView view, string name)
        {
            Target(view); Native.MouseTransition(true, true); Native.MouseTransition(true, false); Pump(TimeSpan.FromMilliseconds(100));
            var menu = Menu(view);
            if (!menu.IsOpen) throw new InvalidOperationException("Thumbnail context menu did not open.");
            var item = menu.Items.OfType<MenuItem>().SingleOrDefault(item => item.Name == name) ?? throw new InvalidOperationException("Missing menu item " + name);
            var pixel = item.PointToScreen(new Avalonia.Point(item.Bounds.Width / 2, item.Bounds.Height / 2));
            var point = new System.Drawing.Point(pixel.X, pixel.Y);
            Native.SetCursorPos(point.X, point.Y); Pump(TimeSpan.FromMilliseconds(30));
            if (Native.GetAncestor(Native.WindowFromPoint(point), 2) != TopLevel.GetTopLevel(item)?.TryGetPlatformHandle()?.Handle)
                throw new InvalidOperationException("The thumbnail menu is occluded; no click was sent.");
            Native.ClickMouse(); Pump(TimeSpan.FromMilliseconds(50));
        }
        try
        {
            config.ThumbnailZoomEnabled = false;
            Target(views[1]); Native.ClickMouse(); Focused(views[1], "image click activates the source client");
            Target(views[0], title: true); Native.ClickMouse(); Focused(views[0], "title/overlay click activates the source client");
            var view = views[0];
            Away(); Size baseSize = view.Size;
            config.ThumbnailZoomEnabled = true;
            Target(view);
            Native.GetCursorPos(out var hoverPointer);
            Require(view.Size.Width > baseSize.Width, $"mouse enter applies hover zoom (before {baseSize}, after {view.Size}, pointer {hoverPointer}, host {view.Bounds}, managerHover={managerType.GetField("_isHoverEffectActive", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)}, pointerOver={view.IsPointerOver}, interacting={view.IsInteracting})");
            Away(); Require(view.Size == baseSize, "mouse leave restores hover zoom");
            config.ThumbnailZoomEnabled = false;
            Target(view); Native.MouseTransition(true, true); Native.MouseTransition(true, false); Pump(TimeSpan.FromMilliseconds(100));
            Require(Menu(view).IsOpen, "right-click opens the context menu");
            Menu(view).Close(); Pump(TimeSpan.FromMilliseconds(50));

            // Exercise the existing 350 ms hold timer and real native global pointer movement/release subscriptions.
            Target(view); Point before = view.Location;
            Native.MouseTransition(true, true); Pump(TimeSpan.FromMilliseconds(450));
            Require(MouseMode(view) == "Move", "right-button hold starts dragging");
            before = view.Location;
            Native.GetCursorPos(out var drag); Native.MoveMouseTo(drag.X + 32, drag.Y + 19); Pump(TimeSpan.FromMilliseconds(80));
            Native.GetCursorPos(out var moved);
            Native.MouseTransition(true, false); Pump(TimeSpan.FromMilliseconds(50));
            Require(view.Location != before && view.Location == new Point(before.X + moved.X - drag.X, before.Y + moved.Y - drag.Y),
                $"global mouse movement drags the thumbnail (before {before}, after {view.Location}, pointer delta {moved.X - drag.X},{moved.Y - drag.Y})");
            Require(MouseMode(view) == "Disabled", "global mouse-up ends dragging");

            SelectMenu(view, "menuReposition");
            Require(MouseMode(view) == "Move", "Move menu action starts dragging");
            Native.MouseTransition(false, true); Native.MouseTransition(false, false); Pump(TimeSpan.FromMilliseconds(50));
            Require(MouseMode(view) == "Disabled", "click ends menu-driven movement");
            SelectMenu(view, "resizeThumbnailToolStripMenuItem");
            Size beforeResize = view.Size;
            Native.GetCursorPos(out var resize); Native.MoveMouseTo(resize.X + 30, resize.Y + 15); Pump(TimeSpan.FromMilliseconds(80));
            Native.GetCursorPos(out moved);
            Require(view.Size != beforeResize && view.Size == new Size(beforeResize.Width + moved.X - resize.X, beforeResize.Height + moved.Y - resize.Y), "Resize menu action supports free resizing");
            Native.MouseTransition(false, true); Native.MouseTransition(false, false); Pump(TimeSpan.FromMilliseconds(50));
            Require(MouseMode(view) == "Disabled", "mouse-up ends resizing");

            SelectMenu(view, "resizeThumbnailToolStripMenuItem");
            beforeResize = view.Size;
            Native.KeyTransition(0xA0, true);
            Pump(TimeSpan.FromMilliseconds(40));
            var modifiers = System.Windows.Forms.Control.ModifierKeys;
            bool asyncShift = (Native.GetAsyncKeyState(0x10) & 0x8000) != 0;
            Native.GetCursorPos(out resize); Native.MoveMouseTo(resize.X + 40, resize.Y + 3); Pump(TimeSpan.FromMilliseconds(80));
            Require(Math.Abs((double)view.Size.Width / view.Size.Height - (double)beforeResize.Width / beforeResize.Height) < .02,
                $"Shift-resize preserves aspect ratio (before {beforeResize}, after {view.Size}, modifiers {modifiers}, async Shift {asyncShift})");
            Native.KeyTransition(0xA0, false); Native.MouseTransition(false, true); Native.MouseTransition(false, false); Pump(TimeSpan.FromMilliseconds(50));
            Require(MouseMode(view) == "Disabled", "shift-resize releases its global mouse subscriptions");
            return new { Passed = true, Checks = checks };
        }
        finally
        {
            Native.MouseDownGuard = previousGuard;
            Native.MouseTransition(true, false); Native.MouseTransition(false, false); Native.KeyTransition(0xA0, false);
            foreach (var view in views) Menu(view).Close();
            Away(); config.ThumbnailZoomEnabled = zoom;
            foreach (var item in saved)
            {
                item.v.ThumbnailMoved = null; item.v.ThumbnailResized = null;
                item.v.Location = item.Location; item.v.Size = item.Size;
            }
            Native.SetCursorPos(originalCursor.X, originalCursor.Y);
        }
    }
}
