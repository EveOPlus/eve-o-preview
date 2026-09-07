using System.Security.Cryptography;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace EveOPreview.UI.Smoke;

internal static class Program
{
    private static readonly string[] ModernPages =
        ["Overview", "Clients", "Previews", "Switching", "FpsAudio", "Profiles", "Appearance", "About"];
    private static readonly string[] LegacyPages =
        ["General", "Thumbnail", "Zoom", "Overlay", "ActiveClients", "CycleGroups", "FpsAudio", "Profiles", "Appearance", "About"];

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string output = Path.Combine(AppContext.BaseDirectory, "screenshots");
            if (args.Length != 0)
            {
                if (args.Length != 2 || args[0] != "--output")
                    throw new ArgumentException("Usage: Eve-O-Preview.UI.Smoke [--output <directory>]");
                output = Path.GetFullPath(args[1]);
            }
            Directory.CreateDirectory(output);
            AppBuilder.Configure<WorkspaceApp>()
                .WithInterFont()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .SetupWithoutStarting();

            var backend = new SmokeBackend();
            using var view = new WorkspaceView(backend);
            var window = new Window { Title = "EVE-O Preview UI smoke", Width = 1180, Height = 820, Content = view };
            window.Show();
            Flush();
            Require(FindControl<Button>(view, "support-open").IsEnabled, "A delayed portrait must not block the UI.");
            backend.CompletePortrait();
            Flush();
            var hashes = new List<string>();
            int renders = 0;
            foreach (string theme in new[] { "Dark", "Light", "Legacy" })
            {
                view.Navigate("Appearance");
                Flush();
                Click(view, "theme-" + theme);
                Require(backend.Read().Theme == theme, "Theme selection did not reach the backend: " + theme);
                window.Width = theme == "Legacy" ? 460 : 1180;
                window.Height = theme == "Legacy" ? 417 : 820;
                Flush();
                if (theme != "Legacy")
                {
                    var profilePicker = FindControl<ComboBox>(view, "profile-picker");
                    profilePicker.IsDropDownOpen = true; Flush();
                    var menuItems = profilePicker.ItemsSource!.Cast<ComboBoxItem>().ToArray();
                    Require(menuItems[^1].Name == "manage-profiles" && menuItems[^1].IsEffectivelyVisible,
                        "Manage profiles must appear at the bottom of the open dropdown.");
                    Require(!menuItems[^2].IsEnabled, "A nonselectable divider must separate management from profile choices.");
                    var menuRoot = (TopLevel)menuItems[^1].GetVisualRoot()!;
                    using (var menuImage = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)Math.Ceiling(menuRoot.Bounds.Width), (int)Math.Ceiling(menuRoot.Bounds.Height))))
                    {
                        menuImage.Render(menuRoot);
                        menuImage.Save(Path.Combine(output, theme.ToLowerInvariant() + "-profile-dropdown.png"));
                    }
                    renders++;
                    profilePicker.IsDropDownOpen = false; Flush();
                }
                foreach (string page in theme == "Legacy" ? LegacyPages : ModernPages)
                {
                    view.Navigate(page);
                    Flush();
                    Require(!view.GetVisualDescendants().Any(control => control.Name == "support-dialog"),
                        "Donation details must never open automatically.");
                    AssertNavigationVisible(window, view);
                    string path = Path.Combine(output, $"{theme.ToLowerInvariant()}-{page.ToLowerInvariant()}.png");
                    Capture(window, path);
                    renders++;
                    if (page == "About")
                    {
                        Click(view, "open-documentation");
                        Require(backend.Commands.Last().Action == "documentation", "Documentation must open the project page.");
                        Click(view, "open-discord");
                        Require(backend.Commands.Last().Action == "discord", "Discord must have its own destination.");
                        Require(view.GetVisualDescendants().OfType<Button>().Count(button => button.Name == "exit-application") == 1,
                            "Exit must appear once in the sidebar.");
                    }
                    if (page == "Appearance") hashes.Add(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
                    if (theme != "Legacy" && page is "FpsAudio" or "Switching")
                    {
                        var contentScroll = view.GetVisualDescendants().OfType<ScrollViewer>()
                            .Where(scroll => scroll.Bounds.Width > 400).OrderByDescending(scroll => scroll.Extent.Height).First();
                        contentScroll.ScrollToEnd();
                        Flush();
                        Capture(window, Path.Combine(output, $"{theme.ToLowerInvariant()}-{page.ToLowerInvariant()}-details.png"));
                        renders++;
                    }
                }
                CheckSupport(window, view, backend, Path.Combine(output, $"{theme.ToLowerInvariant()}-support.png"));
                renders++;
                CheckThumbnailMenu(window, view, backend, Path.Combine(output, $"{theme.ToLowerInvariant()}-thumbnail-menu.png"));
                renders += 1 + ThumbnailMenuThemes.All.Count;
                CheckCycleOrder(window, view, backend, output, theme.ToLowerInvariant());
                renders += theme == "Legacy" ? 2 : 3;
                CheckAdvancedSettings(window, view, backend, output, theme);
                renders += 3;
            }
            Require(hashes.Distinct().Count() == 3, "The three themes must produce different rendered appearances.");

            backend.ExecuteAsync(new WorkspaceCommand("theme", Value: "Dark")).GetAwaiter().GetResult();
            view.RefreshFromBackend();
            window.Width = 1180;
            window.Height = 820;

            // Exercise a production editor and verify its command preserves the existing setting identity.
            view.Navigate("FpsAudio");
            Flush();
            var editor = FindControl<TextBox>(view, "setting-FpsFocused");
            editor.Text = "-5";
            Flush();
            Require(!FindControl<Button>(view, "apply-FpsFocused").IsEnabled,
                "Invalid FPS must be rejected before applying.");
            editor.Text = "165";
            Flush();
            CheckSupport(window, view, backend, Path.Combine(output, "dark-support-with-draft.png"));
            Require(view.HasUnappliedEdits && editor.Text == "165", "Opening donation details must preserve editor drafts.");
            renders++;
            OpenProfileManagement(view);
            Require(view.HasUnappliedEdits, "Opening profile management must preserve unapplied edits.");
            view.Navigate("Appearance");
            view.Navigate("FpsAudio");
            Flush();
            Require(FindControl<TextBox>(view, "setting-FpsFocused").Text == "165",
                "Navigating between pages must preserve unapplied edits.");
            Click(view, "apply-FpsFocused");
            Require(backend.Commands.Any(command => command is { Action: "setting", Target: "FpsFocused", Value: "165" }),
                "FPS Apply must send its edited value to the backend.");

            var search = FindControl<TextBox>(view, "search-settings");
            Require(search.Focus(), "Settings search must accept keyboard focus.");
            search.Text = "jump gate";
            Flush();
            Require(FindControl<ToggleSwitch>(view, "setting-AudioMuteJumpGateTunnel").IsEffectivelyVisible,
                "Search must expose matching advanced settings directly.");

            OpenProfileManagement(view);
            Flush();
            Click(view, "accent-Rose");
            string savedAccent = backend.Read().Settings["ProfileAccentColor"];
            Require(savedAccent != "#6D9FFF", "Profile accent selection must reach the backend.");
            Require(((ISolidColorBrush)FindControl<Border>(view, "active-profile-card").BorderBrush!).Color == Color.Parse(savedAccent),
                "The header must display the profile's selected accent.");
            Capture(window, Path.Combine(output, "dark-profile-accent-selected.png"));
            renders++;
            var picker = FindControl<ComboBox>(view, "profile-picker");
            picker.SelectedIndex = 2;
            Flush();
            Require(backend.Read().ProfileName == "Exploration and scouting", "Profile picker must switch the backend profile.");
            Require(backend.Read().Theme == "Dark", "Profile switching must preserve the global theme.");
            Require(((ISolidColorBrush)FindControl<Border>(view, "active-profile-card").BorderBrush!).Color == Color.Parse("#F2B56E"),
                "The header must refresh the newly selected profile's accent.");
            Capture(window, Path.Combine(output, "dark-profile-accent-switched.png"));
            renders++;

            // Include the actual minimum Windows host client area, as well as a compact everyday size.
            foreach (var size in new[] { (Width: 940, Height: 650, Name: "compact"), (Width: 784, Height: 581, Name: "minimum") })
            {
                window.Width = size.Width;
                window.Height = size.Height;
                foreach (string theme in new[] { "Light", "Dark" })
                {
                    backend.ExecuteAsync(new WorkspaceCommand("theme", Value: theme)).GetAwaiter().GetResult();
                    view.RefreshFromBackend();
                    view.Navigate("FpsAudio");
                    Flush();
                    AssertNavigationVisible(window, view);
                    Require(view.GetVisualDescendants().OfType<ScrollViewer>().Any(), "Advanced settings need a scrollable viewport.");
                    Capture(window, Path.Combine(output, $"{theme.ToLowerInvariant()}-{size.Name}-fpsaudio.png"));
                    renders++;
                    if (size.Name == "minimum")
                    {
                        CheckSupport(window, view, backend, Path.Combine(output, $"{theme.ToLowerInvariant()}-minimum-support.png"));
                        renders++;
                    }
                }
            }
            window.Close();
            Require(backend.PortraitRequests.SequenceEqual(new long[] { 95465272 }), "Request Aura's portrait once across navigation and theme changes.");
            Console.WriteLine($"PASS: {renders} production UI renders; three distinct themes; keyboard-accessible navigation; input validation; draft retention; advanced setting command; settings search; compact scrollable layouts.");
            Console.WriteLine("Screenshots: " + output);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void CheckAdvancedSettings(Window window, WorkspaceView view, SmokeBackend backend, string output, string theme)
    {
        window.Width = theme == "Legacy" ? 460 : 784;
        window.Height = theme == "Legacy" ? 417 : 560;
        view.NavigatePreviewTab("Advanced"); Flush();
        Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-advanced-preview.png"));
        var minimum = FindControl<NumericUpDown>(view, "setting-ThumbnailMinimumWidth");
        minimum.Text = "961"; Flush();
        Require(!FindControl<Button>(view, "apply-preview-settings").IsEnabled, "Out-of-range preview bounds must prevent Apply.");
        minimum.Text = "500";
        FindControl<NumericUpDown>(view, "setting-ThumbnailMaximumWidth").Text = "400"; Flush();
        Require(!FindControl<Button>(view, "apply-preview-settings").IsEnabled, "Inverted preview bounds must prevent Apply.");
        minimum.Text = "192";
        FindControl<NumericUpDown>(view, "setting-ThumbnailMaximumWidth").Text = "960";
        FindControl<NumericUpDown>(view, "setting-HideDelaySeconds").Text = theme == "Light" ? "1.5" : "1.1"; Flush();
        Click(view, "apply-preview-settings");
        Require(backend.Commands.Last().Action == "setting" && backend.Commands.Last().Target == "HideDelaySeconds", "Advanced Apply must reach the configuration route.");
        backend.ExecuteAsync(new("theme", Value: theme == "Legacy" ? "Dark" : "Legacy")).GetAwaiter().GetResult();
        view.RefreshFromBackend(); Flush();
        Require(FindControl<NumericUpDown>(view, "setting-HideDelaySeconds") is not null, "Changing themes must keep the advanced editor available.");
        backend.ExecuteAsync(new("theme", Value: theme)).GetAwaiter().GetResult();
        view.RefreshFromBackend(); Flush();
        var scroll = view.GetVisualDescendants().OfType<ScrollViewer>().Where(s => s.Extent.Height > s.Viewport.Height + 10 && s.Viewport.Width > 200)
            .OrderByDescending(s => s.Extent.Height).First();
        scroll.ScrollToEnd(); Flush();
        var apply = FindControl<Button>(view, "apply-preview-settings");
        var applyPosition = apply.TranslatePoint(default, window)!.Value;
        Require(applyPosition.Y >= 0 && applyPosition.Y + apply.Bounds.Height <= window.Height, "Apply must remain visible while advanced settings scroll.");
        Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-advanced-preview-bottom.png"));
        view.Navigate(theme == "Legacy" ? "ActiveClients" : "Clients"); Flush();
        Click(view, "open-client-settings");
        FindControl<AutoCompleteBox>(view, "client-settings-character").Text = "Offline " + theme;
        Click(view, "edit-client-settings");
        FindControl<CheckBox>(view, "client-settings-priority").IsChecked = true;
        Click(view, "client-color-72C9FF");
        Click(view, "save-client-settings");
        var saved = backend.Read().ClientPreferences!.Single(entry => entry.Title == "EVE - Offline " + theme);
        Require(saved.Priority && saved.BorderColor == "#72C9FF", "Offline character controls must save the selected color and minimization exception.");
        Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-character-settings.png"));
        FindControl<CheckBox>(view, "client-settings-inherit-color").IsChecked = true;
        Click(view, "save-client-settings");
        Require(backend.Read().ClientPreferences!.Single(entry => entry.Title == saved.Title).BorderColor == "", "Inheriting must remove the character color override.");
    }

    private static T FindControl<T>(WorkspaceView view, string name) where T : Control
    {
        Visual scope = view.GetVisualDescendants().OfType<Border>().FirstOrDefault(c => c.Name == "expanded-cycle-order") ?? (Visual)view;
        return scope.GetVisualDescendants().OfType<T>().SingleOrDefault(control => control.Name == name)
            ?? throw new InvalidOperationException($"Missing {typeof(T).Name} '{name}'.");
    }

    private static void CheckCycleOrder(Window window, WorkspaceView view, SmokeBackend backend, string output, string theme)
    {
        backend.PrepareCycleOrder(); Flush();
        view.Navigate("Switching"); Flush();
        bool legacy = theme == "legacy";
        if (!legacy)
        {
            var inline = FindControl<ScrollViewer>(view, "group-members-scroll");
            Require(inline.Extent.Height > inline.Viewport.Height, "The order fixture must exercise an actual scrollbar.");
            var remove = FindControl<Button>(view, "group-remove-0");
            Require(remove.TranslatePoint(default, inline)!.Value.X + remove.Bounds.Width <= inline.Bounds.Width - 14,
                "Remove must leave clearance before the scrollbar.");
            Capture(window, Path.Combine(output, theme + "-cycle-order.png"));
            var grip = FindControl<Border>(view, "group-drag-0");
            var startDrag = grip.TranslatePoint(new Point(10, 14), window)!.Value;
            var lowerEdge = inline.TranslatePoint(new Point(10, inline.Bounds.Height - 5), window)!.Value;
            int beforeDrag = backend.Commands.Count;
            window.MouseDown(startDrag, MouseButton.Left);
            window.MouseMove(lowerEdge, RawInputModifiers.LeftMouseButton);
            using (var timerWindow = new CancellationTokenSource(TimeSpan.FromMilliseconds(350))) Dispatcher.UIThread.MainLoop(timerWindow.Token);
            Flush();
            Require(inline.Offset.Y > 40, $"Holding a drag near the lower edge must scroll the list. Offset={inline.Offset}, extent={inline.Extent}, viewport={inline.Viewport}, start={startDrag}, end={lowerEdge}, row opacity={FindControl<Grid>(view, "group-row-0").Opacity}");
            view.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape }); Flush();
            window.MouseUp(lowerEdge, MouseButton.Left); Flush();
            Require(backend.Commands.Count == beforeDrag, "Cancelling an autoscrolling drag must preserve the saved order.");
        }
        double width = window.Width, height = window.Height;
        Click(view, "expand-cycle-order");
        bool Expanded() => view.GetVisualDescendants().Any(c => c.Name == "expanded-cycle-order");
        Require(Expanded() && window.Width == width && window.Height == height, "Expand must fill the existing window without resizing it.");
        var scroll = FindControl<ScrollViewer>(view, "group-members-scroll");
        scroll.Offset = default; Flush();
        string first = backend.Read().CycleGroups[0].Clients[0];
        var handle = FindControl<Border>(view, "group-drag-0");
        var target = FindControl<Grid>(view, "group-row-2");
        var start = handle.TranslatePoint(new Point(10, 14), window)!.Value;
        var drop = target.TranslatePoint(new Point(10, target.Bounds.Height - 3), window)!.Value;
        int before = backend.Commands.Count;
        window.MouseDown(start, MouseButton.Left); Flush();
        window.MouseMove(drop, RawInputModifiers.LeftMouseButton); Flush();
        window.MouseUp(drop, MouseButton.Left); Flush();
        Require(backend.Commands.Count == before + 1 && backend.Commands.Last().Action == "group-client-move", "A drag must save exactly one reorder command.");
        Require(backend.Read().CycleGroups[0].Clients[2] == first, "Dragging down must move the selected full title to the destination.");
        handle = FindControl<Border>(view, "group-drag-2");
        target = FindControl<Grid>(view, "group-row-0");
        start = handle.TranslatePoint(new Point(10, 14), window)!.Value;
        drop = target.TranslatePoint(new Point(10, 1), window)!.Value;
        window.MouseDown(start, MouseButton.Left); window.MouseMove(drop, RawInputModifiers.LeftMouseButton); window.MouseUp(drop, MouseButton.Left); Flush();
        Require(backend.Read().CycleGroups[0].Clients[0] == first, "Dragging upward must restore the selected title.");
        before = backend.Commands.Count;
        handle = FindControl<Border>(view, "group-drag-0");
        start = handle.TranslatePoint(new Point(10, 14), window)!.Value;
        target = FindControl<Grid>(view, "group-row-2");
        drop = target.TranslatePoint(new Point(10, 1), window)!.Value;
        window.MouseDown(start, MouseButton.Left); window.MouseMove(drop, RawInputModifiers.LeftMouseButton);
        view.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape }); Flush();
        window.MouseUp(drop, MouseButton.Left); Flush();
        Require(backend.Commands.Count == before && Expanded(), "Escape cancels a drag without saving or closing the expanded editor.");
        Click(view, "group-skip-0");
        Require(backend.Read().CycleGroups.All(g => g.SkippedClients!.Contains(first)), "Skip must affect every group containing the title.");
        Require((string?)FindControl<Button>(view, "group-skip-0").Content == "Resume", "Skipped characters need an immediate resume action.");
        Capture(window, Path.Combine(output, theme + "-cycle-expanded-skipped.png"));
        Click(view, "group-skip-0");
        Require(backend.Read().CycleGroups.All(g => g.SkippedClients!.Count == 0), "Resume must clear all affected groups.");
        scroll = FindControl<ScrollViewer>(view, "group-members-scroll");
        scroll.ScrollToEnd(); Flush();
        double previousOffset = scroll.Offset.Y;
        Click(view, "group-up-14");
        Require(FindControl<ScrollViewer>(view, "group-members-scroll").Offset.Y >= previousOffset - 1, "Editing near the bottom must preserve scroll position.");
        Capture(window, Path.Combine(output, theme + "-cycle-expanded-bottom.png"));
        Click(view, "close-cycle-order");
        Require(!Expanded() && window.Width == width && window.Height == height, "Done must return to settings without changing window size.");
    }

    private static void CheckSupport(Window window, WorkspaceView view, SmokeBackend backend, string capturePath)
    {
        int commands = backend.Commands.Count;
        var opener = FindControl<Button>(view, "support-open");
        Require(opener.Focus(), "Donation invitation must be keyboard accessible.");
        Click(view, "support-open");
        var dialog = FindControl<Border>(view, "support-dialog");
        Require(FindControl<SelectableTextBlock>(view, "support-recipient").Text == "Aura Asuna", "Wrong donation recipient.");
        Require(dialog.GetVisualDescendants().OfType<Image>().Any(image => image.Source?.Size.Width == 128), "The downloaded portrait must reach the UI.");
        Click(view, "support-copy-name");
        Require(window.Clipboard!.TryGetTextAsync().GetAwaiter().GetResult() == "Aura Asuna", "Copy must contain exactly the in-game character name.");
        foreach (string name in new[] { "support-dialog", "support-copy-name", "support-close", "support-discord" })
        {
            var control = FindControl<Control>(view, name);
            var position = control.TranslatePoint(default, window)!.Value;
            Require(position.X >= 0 && position.Y >= 0 && position.X + control.Bounds.Width <= window.ClientSize.Width + 1 &&
                position.Y + control.Bounds.Height <= window.ClientSize.Height + 1, "Donation controls must fit the window: " + name);
        }
        Require(backend.Commands.Count == commands, "Opening donation details and copying the recipient must not execute settings commands.");
        Click(view, "support-discord");
        Require(backend.Commands.Count == commands + 1 && backend.Commands.Last().Action == "discord",
            "The donation invitation must use the existing Discord navigation command.");
        Require(FindControl<Border>(view, "support-dialog").IsEffectivelyVisible, "Opening Discord must leave donation details available.");
        Capture(window, capturePath);
        view.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Flush();
        Require(!view.GetVisualDescendants().Any(control => control.Name == "support-dialog"), "Escape must dismiss donation details.");
        var restoredOpener = FindControl<Button>(view, "support-open");
        Require(restoredOpener.IsEnabled && restoredOpener.IsFocused, "Closing details must restore the workspace and keyboard focus.");
        Require(backend.Commands.Skip(commands).All(command => command.Action == "discord"), "Donating must not execute a settings or profile command.");
        Click(view, "support-open");
        window.MouseDown(new Point(5, 5), MouseButton.Left);
        window.MouseUp(new Point(5, 5), MouseButton.Left);
        Flush();
        Require(!view.GetVisualDescendants().Any(control => control.Name == "support-dialog"), "Clicking outside must dismiss donation details.");
        Require(FindControl<Button>(view, "support-open").IsFocused, "Outside dismissal must restore focus.");
    }

    private static void Click(WorkspaceView view, string name)
    {
        var button = FindControl<Button>(view, name);
        Require(button.IsEffectivelyVisible && button.IsEnabled, "Button is unavailable: " + name);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Flush();
    }

    private static void AssertNavigationVisible(Window window, WorkspaceView view)
    {
        var navigation = view.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Name?.StartsWith("nav-", StringComparison.Ordinal) == true).ToArray();
        Require(navigation.Length > 5, "Expected the workspace navigation to expose its feature areas.");
        foreach (var button in navigation)
        {
            button.BringIntoView();
            Flush();
            Require(button.IsEffectivelyVisible && button.IsEnabled && button.Focusable, "Navigation is not keyboard accessible: " + button.Name);
            Point? topLeft = button.TranslatePoint(default, window);
            Require(topLeft.HasValue && button.Bounds.Width > 0 && button.Bounds.Height > 0, "Navigation was not laid out: " + button.Name);
            var position = topLeft.GetValueOrDefault();
            Require(position.X >= -1 && position.Y >= -1 &&
                    position.X + button.Bounds.Width <= window.ClientSize.Width + 1 &&
                    position.Y + button.Bounds.Height <= window.ClientSize.Height + 1,
                $"Navigation is clipped: {button.Name}, {position}, {button.Bounds.Size}, window {window.ClientSize}.");
            Require(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)) || button.Content != null,
                "Navigation needs a readable accessible name: " + button.Name);
        }
        if (view.GetVisualDescendants().OfType<ComboBox>().Any(control => control.Name == "profile-picker"))
        {
            var profile = FindControl<Border>(view, "active-profile-card");
            var picker = FindControl<ComboBox>(view, "profile-picker");
            var search = FindControl<TextBox>(view, "search-settings");
            Require(picker.GetVisualAncestors().Contains(profile), "Profile selection and accent must share the top header.");
            Require(view.GetVisualDescendants().Count(control => control.Name == "active-profile-card") == 1,
                "The shell must have only one active profile display.");
            Require(!navigation.Any(button => button.Name == "nav-Profiles"), "Profile management belongs beside the top selector.");
            var top = profile.TranslatePoint(default, window)!.Value;
            var searchLeft = search.TranslatePoint(default, window)!.Value.X;
            Require(top.Y < 80 && top.X + profile.Bounds.Width <= searchLeft,
                "The profile header must remain at the top without overlapping search at compact sizes.");
            Require(picker.Bounds.Width >= 160 && picker.Focusable, "Profile selection must remain readable and keyboard accessible at minimum window width.");
            Require(!view.GetVisualDescendants().OfType<Button>().Any(button => button.Name == "manage-profiles"),
                "Management belongs inside the dropdown, without a separate header button.");
        }
        foreach (var scroll in view.GetVisualDescendants().OfType<ScrollViewer>()) scroll.Offset = default;
        Flush();
    }

    private static void OpenProfileManagement(WorkspaceView view)
    {
        var picker = FindControl<ComboBox>(view, "profile-picker");
        var selected = ((ComboBoxItem)picker.SelectedItem!).Tag;
        picker.IsDropDownOpen = true; Flush();
        picker.SelectedItem = picker.ItemsSource!.Cast<ComboBoxItem>().Single(item => item.Name == "manage-profiles");
        Flush();
        Require(!picker.IsDropDownOpen && Equals(selected, ((ComboBoxItem)picker.SelectedItem!).Tag),
            "Manage profiles must close the dropdown and keep the active profile selected.");
        Require(FindControl<Button>(view, "clone-profile").IsEffectivelyVisible, "Management must open the Profiles page.");
    }

    private static void Capture(Window window, string path)
    {
        using var bitmap = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("The production UI did not render a frame.");
        Require(bitmap.PixelSize.Width > 0 && bitmap.PixelSize.Height > 0, "Empty screenshot.");
        bitmap.Save(path);
    }

    private static void CheckThumbnailMenu(Window window, WorkspaceView view, SmokeBackend backend, string path)
    {
        view.Navigate("Appearance"); Flush();
        Click(view, "thumbnail-menu-settings");
        Require(!FindControl<Button>(view, "menu-order-up-minimize").IsEnabled, "The default first item cannot move up.");
        void Drag(string id, string targetId, bool below = false, bool cancel = false)
        {
            var handle = FindControl<Border>(view, "menu-order-drag-" + id);
            handle.BringIntoView(); Flush();
            var target = FindControl<Grid>(view, "menu-order-row-" + targetId);
            var start = handle.TranslatePoint(new Point(8, 12), window)!.Value;
            var drop = target.TranslatePoint(new Point(8, below ? target.Bounds.Height - 2 : 1), window)!.Value;
            window.MouseDown(start, MouseButton.Left); Flush();
            window.MouseMove(drop, RawInputModifiers.LeftMouseButton); Flush();
            if (cancel) { view.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape }); Flush(); }
            window.MouseUp(drop, MouseButton.Left); Flush();
        }
        int before = backend.Commands.Count;
        Drag("skip-cycling", "minimize");
        Require(backend.Commands.Count == before + 1 && backend.Commands.Last().Action == "thumbnail-menu-move", "A menu drag must save once, on release.");
        Require(backend.Read().ThumbnailMenuOrder![0] == "skip-cycling", "Menu edits must choose the double right-click action.");
        before = backend.Commands.Count;
        Drag("minimize", "skip-cycling", cancel: true);
        Require(backend.Commands.Count == before, "Escape must cancel a menu drag without saving.");
        Drag("divider:minimize", "minimize");
        Require(backend.Read().ThumbnailMenuOrder![1] == "divider:minimize", "Divider rows must be draggable independently.");
        Click(view, "menu-divider-remove-divider:minimize");
        Click(view, "menu-divider-add-skip-cycling");
        string addedDivider = backend.Read().ThumbnailMenuOrder![1];
        Require(ThumbnailMenuActions.IsDivider(addedDivider) && addedDivider != "divider:minimize", "Insert must create an explicit divider at the chosen gap.");
        Click(view, "menu-divider-remove-" + addedDivider);
        Click(view, "menu-divider-remove-divider:skip");
        Require(!FindControl<Button>(view, "menu-order-down-resize").IsEnabled, "The last item cannot move down.");
        Require(backend.Read().ThumbnailMenuOrder!.Count == 5, "All actions must remain available.");
        var editor = FindControl<StackPanel>(view, "thumbnail-menu-editor");
        foreach (var button in editor.GetVisualDescendants().OfType<Button>())
        {
            var point = button.TranslatePoint(default, window)!.Value;
            Require(point.X >= 0 && point.X + button.Bounds.Width <= window.Bounds.Width && point.Y + button.Bounds.Height <= window.Bounds.Height,
                "Menu order controls must fit in the current window, including Legacy.");
        }
        Capture(window, path);
        Click(view, "menu-order-back");
        Click(view, "thumbnail-menu-settings");
        Require(!FindControl<Button>(view, "menu-order-up-skip-cycling").IsEnabled, "Reopening must retain the selected first action.");
        Click(view, "menu-order-reset");
        Require(backend.Read().ThumbnailMenuOrder!.SequenceEqual(ThumbnailMenuActions.DefaultOrder), "Reset must restore the familiar default order.");
        Capture(window, path);
        bool legacy = backend.Read().Theme == "Legacy";
        if (legacy) Click(view, "menu-tab-Theme");
        foreach (var palette in ThumbnailMenuThemes.All)
        {
            FindControl<ComboBox>(view, "menu-theme").SelectedIndex = ThumbnailMenuThemes.All.ToList().IndexOf(palette) + 1;
            Flush();
            Require(backend.Read().ThumbnailMenuTheme == palette.Id, "Menu palette must save independently of the app theme.");
            var preview = FindControl<Border>(view, "thumbnail-menu-preview");
            Require(((ISolidColorBrush)preview.Background!).Color == Color.Parse(palette.Background), "The preview must use the selected palette.");
            Require(preview.TranslatePoint(new Point(preview.Bounds.Width, preview.Bounds.Height), window)!.Value.Y <= window.Bounds.Height,
                "Theme preview must fit in the window, including Legacy.");
            Capture(window, Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-" + palette.Id + ".png"));
        }
        FindControl<ComboBox>(view, "menu-theme").SelectedIndex = 0; Flush();
        if (legacy) Click(view, "menu-tab-Order");
    }

    private static void Flush()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
