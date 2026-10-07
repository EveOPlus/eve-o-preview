using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using EveOPreview.Configuration;
using EveOPreview.Services;
using EveOPreview.Services.Interface;
using EveOPreview.Tests.Infrastructure;
using EveOPreview.View;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed partial class ThumbnailZOrderTests
{
    private static void RunUndoScenario(string scenario, IThumbnailConfiguration config, IThumbnailManager manager,
        Preview[] views, Action<Preview> activate, Action<int, int> drag, Action finish, List<IProcessInfo> removed)
    {
        EveOPreview.View.CustomControl.NativeMenuTheme.CurrentTheme = scenario.EndsWith("Light") ? "Light" : "Dark";
        var a = views[0]; var b = views[1]; var c = views[2];
        foreach (var view in views) view.SetFrames(false);
        a.ThumbnailLocation = new Point(50, 50);
        b.ThumbnailLocation = new Point(500, 200);
        c.ThumbnailLocation = new Point(1000, 500);
        a.ThumbnailSize = new Size(320, 180);
        b.ThumbnailSize = new Size(400, 300);
        a.ThumbnailResized(a.Id); b.ThumbnailResized(b.Id);
        config.PerClientThumbnailSizes["EVE - Offline"] = new Size(500, 250);
        ContextMenu Menu(Preview view) => (ContextMenu)typeof(ThumbnailView)
            .GetField("thumbnailContextMenu", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
        MenuItem Find(IEnumerable<object> items, string name) => items.OfType<MenuItem>()
            .Select(item => item.Name == name ? item : Find(item.Items, name)).FirstOrDefault(item => item != null);
        void Click(Preview view, string name)
        {
            var menu = Menu(view);
            if (!menu.IsOpen) AvaloniaMenuTest.Open(menu, view);
            var item = Find(menu.Items, name);
            Assert.NotNull(item);
            Assert.True(item.IsEnabled);
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            menu.Close();
            TestAvalonia.Pump();
        }
        void Move(Preview view, int dx, int dy)
        {
            Click(view, view.IsSelected ? "menuMoveSelected" : "menuReposition");
            Assert.False(manager.CanUndoThumbnailEdit);
            drag(dx, dy);
            finish();
            TestAvalonia.Pump();
            Assert.False(view.IsInteracting);
        }
        void Undo(Preview view)
        {
            Assert.True(manager.CanUndoThumbnailEdit);
            Click(view, "menuUndo");
            Assert.False(manager.CanUndoThumbnailEdit);
            AvaloniaMenuTest.Open(Menu(view), view);
            Assert.Null(Find(Menu(view).Items, "menuUndo"));
            Menu(view).Close();
        }
        void SelectPair()
        {
            manager.ClearThumbnailSelection();
            manager.ToggleThumbnailSelection(a.Id);
            manager.ToggleThumbnailSelection(b.Id);
        }

        Assert.False(manager.CanUndoThumbnailEdit);
        var originalA = a.Location;
        Move(a, 40, 25);
        Assert.Equal(new Point(90, 75), a.Location);
        // No movement or a selection change must not replace the previous edit.
        Move(b, 0, 0);
        manager.ToggleThumbnailSelection(b.Id);
        manager.ClearThumbnailSelection();
        Undo(c); // The last edit is available from any thumbnail.
        Assert.Equal(originalA, a.Location);
        Assert.Equal(originalA, config.GetThumbnailLocation(a.Title, b.Title, Point.Empty));
        a.ZoomIn(ViewZoomAnchor.NW, 2); a.ZoomOut();
        Assert.Equal(originalA, a.Location);
        Assert.Equal(new Size(320, 180), a.ThumbnailSize);

        var originalB = b.Location;
        Move(a, 20, 10);
        Move(b, -20, -10);
        Undo(a);
        Assert.Equal(new Point(70, 60), a.Location);
        Assert.Equal(originalB, b.Location);
        Check(!manager.CanUndoThumbnailEdit, "Undo restores only the latest action and offers no redo/history stack");

        SelectPair();
        originalA = a.Location;
        var untouched = c.Bounds;
        Move(b, 40, 25);
        Undo(a);
        Assert.Equal(originalA, a.Location);
        Assert.Equal(originalB, b.Location);
        Assert.Equal(untouched, c.Bounds);
        Assert.True(a.IsSelected && b.IsSelected);

        Click(a, "menuResizeSelected");
        drag(160, 90);
        finish();
        Assert.Equal(new Size(480, 270), a.ThumbnailSize);
        Assert.Equal(new Size(600, 450), b.ThumbnailSize);
        AvaloniaMenuTest.Open(Menu(b), b);
        Assert.Equal(new[] { "Move", "Resize", "Reset Aspect Ratio", "Reset Size", "Skip Cycling", "Undo" },
            Menu(b).Items.OfType<MenuItem>().Select(item => item.Header));
        var captures = System.IO.Path.Combine(AppContext.BaseDirectory, "undo-menus");
        System.IO.Directory.CreateDirectory(captures);
        AvaloniaMenuTest.Capture(Menu(b), System.IO.Path.Combine(captures, scenario + ".png"));
        Undo(b);
        Assert.Equal(new Size(320, 180), a.ThumbnailSize);
        Assert.Equal(new Size(400, 300), b.ThumbnailSize);
        Assert.Equal(a.ThumbnailSize, config.PerClientThumbnailSizes[a.Title]);
        Assert.Equal(b.ThumbnailSize, config.PerClientThumbnailSizes[b.Title]);
        Assert.Equal(originalA, a.Location);
        Assert.Equal(originalB, b.Location);
        Assert.Equal(untouched, c.Bounds);

        Click(a, "menuResetSelectedAspectRatio");
        Assert.Equal(new Size(320, 213), a.ThumbnailSize);
        Assert.Equal(new Size(400, 200), b.ThumbnailSize);
        Undo(b);
        Assert.Equal(new Size(320, 180), a.ThumbnailSize);
        Assert.Equal(new Size(400, 300), b.ThumbnailSize);
        Click(b, "menuResetSelectedSize");
        Assert.False(config.PerClientThumbnailSizes.ContainsKey(a.Title));
        Assert.False(config.PerClientThumbnailSizes.ContainsKey(b.Title));
        Undo(a);
        Assert.Equal(new Size(320, 180), config.PerClientThumbnailSizes[a.Title]);
        Assert.Equal(new Size(400, 300), config.PerClientThumbnailSizes[b.Title]);
        Check(c.Bounds == untouched, "group Undo restores distinct sizes and positions without changing unselected thumbnails");

        Click(a, "menuCycleSkipSelected");
        Assert.True(config.IsClientCycleSkipped(a.Title) && config.IsClientCycleSkipped(b.Title));
        Undo(b);
        Assert.False(config.IsClientCycleSkipped(a.Title) || config.IsClientCycleSkipped(b.Title));
        config.SetClientCycleSkipped(a.Title, true);
        config.SetClientCycleSkipped(b.Title, true);
        Click(b, "menuCycleSkipSelected");
        Undo(a);
        Assert.True(config.IsClientCycleSkipped(a.Title) && config.IsClientCycleSkipped(b.Title));
        Assert.False(config.IsClientCycleSkipped(c.Title));

        manager.ClearThumbnailSelection();
        Click(c, "menuCycleSkip");
        Undo(a);
        Assert.False(config.IsClientCycleSkipped(c.Title));
        Click(c, "menuResizeIndividual");
        drag(80, 40);
        finish();
        Assert.True(config.PerClientThumbnailSizes.ContainsKey(c.Title));
        Undo(b);
        Assert.Equal(config.ThumbnailSize, c.ThumbnailSize);
        Assert.False(config.PerClientThumbnailSizes.ContainsKey(c.Title));
        Click(a, "menuResetSize");
        Assert.Equal(config.ThumbnailSize, a.ThumbnailSize);
        Undo(c);
        Assert.Equal(new Size(320, 180), a.ThumbnailSize);
        Click(a, "menuResetAspectRatio");
        Assert.Equal(new Size(320, 213), a.ThumbnailSize);
        Undo(c);
        Assert.Equal(new Size(320, 180), a.ThumbnailSize);

        var defaultSize = config.ThumbnailSize;
        Click(a, "menuResizeAll");
        drag(160, 90);
        finish();
        Assert.NotEqual(defaultSize, config.ThumbnailSize);
        Assert.Equal(new Size(750, 375), config.PerClientThumbnailSizes["EVE - Offline"]);
        Undo(c);
        Assert.Equal(defaultSize, config.ThumbnailSize);
        Assert.Equal(new Size(500, 250), config.PerClientThumbnailSizes["EVE - Offline"]);
        Assert.Equal(new Size(320, 180), a.ThumbnailSize);
        Assert.Equal(new Size(400, 300), b.ThumbnailSize);
        Assert.Equal(defaultSize, c.ThumbnailSize);
        Assert.False(config.PerClientThumbnailSizes.ContainsKey(c.Title));

        originalA = a.Location;
        AvaloniaMenuTest.SendMessage(a.Handle, 0x0231, IntPtr.Zero, IntPtr.Zero);
        a.ThumbnailLocation = new Point(originalA.X + 25, originalA.Y + 10);
        a.ThumbnailSize = new Size(360, 220);
        AvaloniaMenuTest.SendMessage(a.Handle, 0x0232, IntPtr.Zero, IntPtr.Zero);
        Undo(c);
        Assert.Equal(originalA, a.Location);
        Assert.Equal(new Size(320, 180), a.ThumbnailSize);
        Check(true, "native move/resize notifications and all menu edits share the same single-step Undo");

        // Switching active clients may load a different position layout before Undo.
        config.EnablePerClientThumbnailLayouts = true;
        config.SetThumbnailLocation(a.Title, b.Title, new Point(90, 90));
        config.SetThumbnailLocation(a.Title, c.Title, new Point(800, 80));
        manager.ApplyRuntimeSettings();
        activate(b);
        Assert.Equal(new Point(90, 90), a.Location);
        Move(a, 30, 10);
        activate(c);
        Assert.Equal(new Point(800, 80), a.Location);
        Undo(b);
        Assert.Equal(new Point(90, 90), config.GetThumbnailLocation(a.Title, b.Title, Point.Empty));
        Assert.Equal(new Point(800, 80), a.Location);
        activate(b);
        Assert.Equal(new Point(90, 90), a.Location);
        Move(a, 30, 10);
        manager.ApplyRuntimeSettings();
        Assert.False(manager.CanUndoThumbnailEdit);
        Move(a, 30, 10);
        removed.Add(new TestProcessInfo((int)a.Id, a.Title));
        Call(manager, "UpdateThumbnailsList");
        Assert.False(manager.CanUndoThumbnailEdit);
        Check(true, "Undo restores the original client layout and is invalidated on profile reload or target removal");
    }
}
