using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using EveOPreview.UI;

namespace EveOPreview.View.CustomControl;

/// <summary>Applies the shared palette and saved action order to Avalonia preview menus.</summary>
public static class NativeMenuTheme
{
    public static string CurrentTheme { get; set; } = "Dark";
    public static IReadOnlyList<string> ThumbnailMenuOrder { get; set; } = ThumbnailMenuActions.DefaultOrder;
    public static string ThumbnailTheme { get; set; } = ThumbnailMenuThemes.FollowApp;

    public static void ApplyThumbnailOrder(ContextMenu menu)
    {
        var existing = menu.Items.OfType<Control>().ToDictionary(item => item.Name ?? "");
        var items = ThumbnailMenuActions.Normalize(ThumbnailMenuOrder).Select(id =>
        {
            if (ThumbnailMenuActions.IsDivider(id))
                return existing.GetValueOrDefault(id) ?? new Separator { Name = id };
            return existing.GetValueOrDefault(id switch
            {
                "minimize" => "menuMinimize",
                "minimize-all" => "minimizeAllToolStripMenuItem",
                "skip-cycling" => "menuCycleSkip",
                "move" => "menuReposition",
                "resize" => "resizeThumbnailToolStripMenuItem",
                _ => ""
            });
        }).Where(item => item != null).ToArray();
        if (menu.Items.SequenceEqual(items)) return;
        menu.Items.Clear();
        foreach (var item in items) menu.Items.Add(item);
    }

    public static void Track(ContextMenu menu, bool thumbnail = false)
    {
        menu.Opening -= MenuOpening;
        menu.Opening -= ThumbnailOpening;
        menu.Opened -= MenuOpened;
        menu.Opened -= ThumbnailOpened;
        menu.Opening += thumbnail ? ThumbnailOpening : MenuOpening;
        // Explicit ContextMenu.Open(Control) skips Opening in Avalonia 11.3.20.
        menu.Opened += thumbnail ? ThumbnailOpened : MenuOpened;
        Apply(menu, thumbnail);
    }

    private static void MenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (sender is ContextMenu menu) Apply(menu, false);
    }

    private static void ThumbnailOpening(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (sender is ContextMenu menu) Apply(menu, true);
    }

    private static void MenuOpened(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is ContextMenu menu) Apply(menu, false);
    }

    private static void ThumbnailOpened(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is ContextMenu menu) Apply(menu, true);
    }

    private static void Apply(ContextMenu menu, bool thumbnail)
    {
        var contrast = new HighContrast { Size = (uint)Marshal.SizeOf<HighContrast>() };
        bool highContrast = SystemParametersInfo(0x0042, contrast.Size, ref contrast, 0) && (contrast.Flags & 1) != 0;
        Apply(menu, thumbnail, highContrast, SystemColor);
    }

    // Keep palette selection injectable so high-contrast rendering can be checked
    // without changing the user's Windows accessibility settings.
    internal static void Apply(ContextMenu menu, bool thumbnail, bool highContrast, Func<int, Color> systemColor)
    {
        var palette = ThumbnailMenuThemes.Resolve(thumbnail ? ThumbnailTheme : ThumbnailMenuThemes.FollowApp, CurrentTheme);
        // Windows 10/11 map contrast popups to WINDOW/WINDOWTEXT; the older
        // MENU/MENUTEXT/WINDOWFRAME color indices are no longer supported.
        IBrush background = highContrast ? new SolidColorBrush(systemColor(5)) : Brush.Parse(palette.Background); // COLOR_WINDOW
        IBrush foreground = highContrast ? new SolidColorBrush(systemColor(8)) : Brush.Parse(palette.Foreground); // COLOR_WINDOWTEXT
        IBrush selection = highContrast ? new SolidColorBrush(systemColor(13)) : Brush.Parse(palette.Selection); // COLOR_HIGHLIGHT
        IBrush selectedText = highContrast ? new SolidColorBrush(systemColor(14)) : foreground; // COLOR_HIGHLIGHTTEXT
        IBrush border = highContrast ? foreground : Brush.Parse(palette.Border);
        IBrush muted = highContrast ? new SolidColorBrush(systemColor(17)) : Brush.Parse(palette.Muted); // COLOR_GRAYTEXT
        menu.SetValue(ThemeVariantScope.RequestedThemeVariantProperty, CurrentTheme == "Light" ? ThemeVariant.Light : ThemeVariant.Dark);
        menu.Background = background;
        menu.Foreground = foreground;
        menu.BorderBrush = border;
        menu.BorderThickness = new Thickness(1);
        // Resource names match Avalonia 11.3.20 MenuItem.xaml and Separator.xaml;
        // verify the actual template parts when upgrading the Fluent theme.
        menu.Resources["MenuFlyoutPresenterBackground"] = background;
        menu.Resources["MenuFlyoutPresenterBorderBrush"] = border;
        menu.Resources["MenuFlyoutItemBackground"] = Brushes.Transparent;
        menu.Resources["MenuFlyoutItemBackgroundDisabled"] = Brushes.Transparent;
        menu.Resources["MenuFlyoutItemForeground"] = foreground;
        menu.Resources["MenuFlyoutItemForegroundPointerOver"] = selectedText;
        menu.Resources["MenuFlyoutItemForegroundPressed"] = selectedText;
        menu.Resources["MenuFlyoutItemForegroundDisabled"] = muted;
        menu.Resources["MenuFlyoutItemBackgroundPointerOver"] = selection;
        menu.Resources["MenuFlyoutItemBackgroundPressed"] = selection;
        menu.Resources["SystemControlForegroundBaseMediumLowBrush"] = border;
        foreach (string prefix in new[] { "MenuFlyoutItemKeyboardAcceleratorTextForeground", "MenuFlyoutSubItemChevron" })
        {
            menu.Resources[prefix] = foreground;
            menu.Resources[prefix + "PointerOver"] = selectedText;
            menu.Resources[prefix + "Pressed"] = selectedText;
            menu.Resources[prefix + "Disabled"] = muted;
        }
        menu.Resources["MenuFlyoutSubItemChevronSubMenuOpened"] = selectedText;
    }

    private static Color SystemColor(int index)
    {
        uint value = GetSysColor(index);
        return Color.FromRgb((byte)value, (byte)(value >> 8), (byte)(value >> 16));
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct HighContrast { public uint Size, Flags; public IntPtr Scheme; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, ref HighContrast data, uint flags);
    [DllImport("user32.dll")] private static extern uint GetSysColor(int index);
}
