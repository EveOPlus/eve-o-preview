using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using EveOPreview.Configuration;
using EveOPreview.Services;
using EveOPreview.Tests.Infrastructure;
using EveOPreview.UI;
using EveOPreview.View;
using EveOPreview.Preview;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed partial class ThumbnailZOrderTests
{
    private static void RunRegionScenario(IThumbnailConfiguration config, IThumbnailManager manager, Preview[] views, Action refresh)
    {
        EveOPreview.View.CustomControl.NativeMenuTheme.CurrentTheme = "Dark";
        var a = views[0]; var b = views[1]; var c = views[2];
        var handles = views.Select(view => view.Handle).ToArray();
        config.SetThumbnailLocation(a.Title, "", new Point(20, 30));
        config.ThumbnailRegions.Add(new() { Id = "left", Name = "Left", X = -400, Y = 80, Width = 400, Height = 250 });
        config.ClientRegionAssignments[a.Title] = "left";
        config.ClientRegionAssignments[b.Title] = "left";
        manager.ApplyRegionLayout(); refresh();
        foreach (var view in new[] { a, b })
        {
            Assert.Equal(new Point(-400, 80), view.Location);
            Assert.Equal(new Size(400, 250), view.ThumbnailSize);
            Assert.Equal(SystemDecorations.None, view.SystemDecorations);
            view.ZoomIn(ViewZoomAnchor.NW, 2);
            Assert.Equal(new Size(400, 250), view.ThumbnailSize);
            manager.ToggleThumbnailSelection(view.Id);
            Assert.False(view.IsSelected);
        }
        // Occupied regions must stay editable above both the thumbnail images and labels.
        var docked = new RegionItem("left", "Left", -400, 80, 400, 250);
        var editorLimits = new RegionSnapshot([docked], new System.Collections.Generic.Dictionary<string, string>(), true, "", 192, 108, 960, 540);
        using (var occupiedEditor = new ThumbnailRegionWindow(docked, editorLimits, _ => { }))
        using (var emptyEditor = new ThumbnailRegionWindow(docked with { Id = "empty", X = 50 }, editorLimits, _ => { }))
        {
            IntPtr activeBefore = Native.GetActiveWindow(), foregroundBefore = Native.GetForegroundWindow();
            Assert.NotEqual(IntPtr.Zero, activeBefore);
            occupiedEditor.ShowEditor(null); emptyEditor.ShowEditor(null); TestAvalonia.Pump();
            void AssertEditorsAbovePreviews()
            {
                foreach (var regionEditor in new[] { occupiedEditor, emptyEditor })
                {
                    IntPtr handle = regionEditor.TryGetPlatformHandle().Handle;
                    Assert.True(Native.IsWindowVisible(handle));
                    foreach (var preview in new[] { a, b })
                    {
                        Assert.True(IsAbove(handle, preview.Handle), "Region editor must stay above a docked thumbnail.");
                        Assert.True(IsAbove(handle, preview.Overlay.Handle), "Region editor must stay above a docked thumbnail's overlay.");
                    }
                }
                Assert.Equal(activeBefore, Native.GetActiveWindow());
                Assert.Equal(foregroundBefore, Native.GetForegroundWindow());
            }
            AssertEditorsAbovePreviews();
            Assert.True(a.RestoreAndBringToFront());
            AssertEditorsAbovePreviews();
            Assert.True(b.RestoreAndBringToFront());
            AssertEditorsAbovePreviews();
            manager.GetType().GetMethod("SetActive").Invoke(manager,
                [new System.Collections.Generic.KeyValuePair<IntPtr, IThumbnailView>(a.Id, a)]);
            AssertEditorsAbovePreviews();
            refresh(); AssertEditorsAbovePreviews(); AssertStack(a, b, c);
            a.Hide(); a.Show(); TestAvalonia.Pump(); AssertEditorsAbovePreviews();
            a.SetTopMost(false); a.SetTopMost(true); AssertEditorsAbovePreviews();
            manager.ApplyRegionLayout(); refresh(); AssertEditorsAbovePreviews();
        }
        config.ThumbnailRegions[0].X = -600; config.ThumbnailRegions[0].Width = 440;
        manager.ApplyRegionLayout();
        Assert.Equal(new Point(-600, 80), a.Location);
        Assert.Equal(new Size(440, 250), b.ThumbnailSize);
        config.ThumbnailSize = new Size(500, 280); manager.UpdateThumbnailsSize();
        Assert.Equal(new Size(440, 250), a.ThumbnailSize);
        var menu = (ContextMenu)typeof(ThumbnailView).GetField("thumbnailContextMenu", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(a);
        AvaloniaMenuTest.Open(menu, a);
        Assert.False(menu.Items.OfType<MenuItem>().Single(item => item.Name == "menuReposition").IsEnabled);
        Assert.False(menu.Items.OfType<MenuItem>().Single(item => item.Name == "resizeThumbnailToolStripMenuItem").IsEnabled);
        var undock = menu.Items.OfType<MenuItem>().Single(item => item.Name == "menuUndock");
        undock.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        menu.Close(); TestAvalonia.Pump(); refresh();
        Assert.Null(config.GetThumbnailRegion(a.Title));
        Assert.NotNull(config.GetThumbnailRegion(b.Title));
        Assert.Equal(new Point(-600, 80), a.Location);
        Assert.Equal(new Size(440, 250), a.ThumbnailSize);
        AvaloniaMenuTest.Open(menu, a);
        Assert.DoesNotContain(menu.Items.OfType<MenuItem>(), item => item.Name == "menuUndock");
        Assert.True(menu.Items.OfType<MenuItem>().Single(item => item.Name == "menuReposition").IsEnabled);
        menu.Close();
        a.ThumbnailLocation = new Point(100, 120); a.ThumbnailMoved(a.Id);
        a.ThumbnailSize = new Size(500, 300); a.ThumbnailResized(a.Id);
        refresh();
        Assert.Equal(new Point(100, 120), a.Location);
        Assert.Equal(new Size(500, 300), a.ThumbnailSize);
        Assert.Equal(new Point(-600, 80), b.Location);
        Assert.Equal(handles, views.Select(view => view.Handle).ToArray());
        manager.ApplyRuntimeSettings();
        Assert.Equal(new Point(-600, 80), b.Location);
        Assert.Equal(handles, views.Select(view => view.Handle).ToArray());

        RegionItem committed = null;
        var item = new RegionItem("edit", "Edit region", 50, 50, 400, 250);
        var snapshot = new RegionSnapshot([item], new System.Collections.Generic.Dictionary<string, string>(), true, "", 192, 108, 960, 540);
        using var editor = new ThumbnailRegionWindow(item, snapshot, value => committed = value);
        IntPtr active = Native.GetActiveWindow();
        editor.ShowEditor(null); TestAvalonia.Pump();
        using (var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize(400, 250)))
        {
            bitmap.Render(editor);
            bitmap.Save(System.IO.Path.Combine(AppContext.BaseDirectory, "region-editor.png"));
        }
        Assert.Equal(active, Native.GetActiveWindow());
        IntPtr hwnd = editor.TryGetPlatformHandle().Handle;
        Assert.True((Native.GetWindowLong(hwnd, -20) & 0x08000000) != 0);
        SendRegionPointer(hwnd, 0x0201, 40, 40, true);
        SendRegionPointer(hwnd, 0x0200, 70, 60, true);
        SendRegionPointer(hwnd, 0x0202, 40, 40, false);
        Assert.NotNull(committed);
        Assert.Equal(80, committed.X); Assert.Equal(70, committed.Y);
        committed = null;
        SendRegionPointer(hwnd, 0x0201, 398, 248, true);
        SendRegionPointer(hwnd, 0x0200, 438, 278, true);
        SendRegionPointer(hwnd, 0x0202, 438, 278, false);
        Assert.NotNull(committed);
        Assert.Equal(440, committed.Width); Assert.Equal(280, committed.Height);
        editor.Dispose(); Assert.False(Native.IsWindowVisible(hwnd));

        // The same target collector feeds actual thumbnail gestures and region editors.
        var targets = new System.Collections.Generic.List<ThumbnailSnapTarget>();
        DesktopSnapTargets.Collect(targets, a, config, manager, excludedRegion: "left");
        Assert.DoesNotContain(targets, target => target.Id == b.Id.ToInt64());
        Assert.Contains(targets, target => target.Id == a.Id.ToInt64());
        Assert.Equal(a.Screens.All.Count, targets.Count(target => target.FixedBounds));
        foreach (var view in views) view.Hide();
        a.Show(); TestAvalonia.Pump();
        config.EnableThumbnailSnap = true;
        var screen = a.Screens.All[0].Bounds;
        var raw = new PreviewRect(screen.X + 5, screen.Y + 5, 200, 100);
        var snap = typeof(ThumbnailView).GetMethod("Snap", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.Equal(new PreviewRect(screen.X, screen.Y, 200, 100), (PreviewRect)snap.Invoke(a, [raw, false]));
        Assert.Equal(raw, (PreviewRect)snap.Invoke(a, [raw, true]));
        typeof(ThumbnailView).GetMethod("ClearSnapGuides", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(a, null);

        // A peer region uses the normal snap lock, guide and breakaway path.
        committed = null;
        item = item with { X = 50, Y = 400 };
        bool enabled = true;
        using var snappingEditor = new ThumbnailRegionWindow(item, snapshot, value => committed = value, () => enabled,
            list => list.Add(new(-42, new(500, 400, 400, 250), true)));
        snappingEditor.ShowEditor(null); TestAvalonia.Pump();
        hwnd = snappingEditor.TryGetPlatformHandle().Handle;
        SendRegionPointer(hwnd, 0x0201, 40, 40, true);
        SendRegionPointer(hwnd, 0x0200, 84, 40, true); // raw x=94, right=494 -> 500
        Assert.Equal(100, snappingEditor.Position.X);
        Assert.NotNull(typeof(ThumbnailRegionWindow).GetField("_guides", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(snappingEditor));
        SendRegionPointer(hwnd, 0x0200, 27, 40, true); // raw x=87, retained
        Assert.Equal(100, snappingEditor.Position.X);
        SendRegionPointer(hwnd, 0x0200, 23, 40, true); // raw x=83, breakaway
        Assert.Equal(83, snappingEditor.Position.X);
        SendRegionPointer(hwnd, 0x0202, 40, 40, false);
        Assert.Equal(83, committed.X);
        Assert.Null(typeof(ThumbnailRegionWindow).GetField("_guides", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(snappingEditor));
        snappingEditor.UpdateRegion(item);
        SendRegionPointer(hwnd, 0x0201, 398, 248, true);
        SendRegionPointer(hwnd, 0x0200, 442, 248, true); // width=444 -> 450 at peer left
        SendRegionPointer(hwnd, 0x0202, 442, 248, false);
        Assert.Equal(450, committed.Width);
        snappingEditor.UpdateRegion(item);
        enabled = false;
        SendRegionPointer(hwnd, 0x0201, 40, 40, true);
        SendRegionPointer(hwnd, 0x0200, 84, 40, true);
        SendRegionPointer(hwnd, 0x0202, 40, 40, false);
        Assert.Equal(94, committed.X);
    }

    private static void SendRegionPointer(IntPtr hwnd, uint message, int x, int y, bool down)
    {
        RegionSendMessage(hwnd, message, new IntPtr(down ? 1 : 0), new IntPtr((y << 16) | (x & 0xffff)));
        TestAvalonia.Pump();
    }
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr RegionSendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
}
