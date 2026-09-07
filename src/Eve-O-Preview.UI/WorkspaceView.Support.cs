using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace EveOPreview.UI;

public sealed partial class WorkspaceView
{
    private const string SupportCharacterName = "Aura Asuna";
    private const string SupportCharacterId = "95465272";
    private const string SupportHeading = "Enjoying EVE-O? Make its developer's day.";
    private Bitmap? _supportPortrait;
    private Task<byte[]?>? _supportPortraitRequest;
    private string SupportSurface => _theme.Name == "Dark" ? "#332B1C" : "#FFF3D6";
    private string SupportInk => _theme.Name == "Dark" ? "#F4D28B" : "#76500B";

    private Control SupportPortrait(double size)
    {
        var image = new Image { Source = _supportPortrait, Stretch = Stretch.UniformToFill };
        AutomationProperties.SetName(image, "Portrait of " + SupportCharacterName);
        var content = new Grid { Background = B(_theme.Inset) };
        var placeholder = Icon("character");
        placeholder.HorizontalAlignment = HorizontalAlignment.Center;
        content.Children.Add(placeholder); content.Children.Add(image);
        if (_supportPortrait is null) image.AttachedToVisualTree += (_, _) => { if (!_disposed) _ = LoadSupportPortrait(image); };
        return new Border { Width = size, Height = size, CornerRadius = new CornerRadius(_theme.Legacy ? 0 : 8),
            ClipToBounds = true, Child = content, VerticalAlignment = VerticalAlignment.Center };
    }

    private async Task LoadSupportPortrait(Image image)
    {
        if (_backend is not IWorkspacePortraitProvider provider) return;
        try
        {
            // Share one asynchronous request across sidebar, About and theme changes.
            var bytes = await (_supportPortraitRequest ??= provider.GetCharacterPortraitAsync(long.Parse(SupportCharacterId)));
            if (bytes is null || _disposed) return;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_disposed) return;
                using var stream = new System.IO.MemoryStream(bytes);
                _supportPortrait ??= new Bitmap(stream);
                image.Source = _supportPortrait;
            });
        }
        catch
        {
            // A delayed or unavailable portrait must not interrupt settings or donations.
        }
    }

    private Button SupportButton()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("32,*") };
        row.Children.Add(SupportPortrait(32));
        var words = new StackPanel { Margin = new Thickness(8, 0, 0, 0), Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        words.Children.Add(Text("Say thanks", 13, SupportInk, true));
        words.Children.Add(Text("Send an ISK gift", 10, SupportInk));
        Grid.SetColumn(words, 1); row.Children.Add(words);
        var button = ActionButton(row, ShowSupport, "support-open");
        button.Padding = new Thickness(8);
        button.Background = B(SupportSurface);
        button.BorderBrush = B(SupportInk);
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        AutomationProperties.SetName(button, "Donate ISK to support EVE-O Preview");
        ToolTip.SetTip(button, "Enjoying EVE-O? Send an optional ISK thank-you to Aura Asuna.");
        return button;
    }

    private Control SupportAboutCard()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("48,*") };
        row.Children.Add(SupportPortrait(48));
        var words = new StackPanel { Spacing = 7, Margin = new Thickness(12, 0, 0, 0) };
        words.Children.Add(Text(SupportHeading, 18, _theme.Text, true));
        words.Children.Add(Text("I love investing my time in EVE-O so you can enjoy yours. An ISK gift means less time grinding and more time to improve the tool, help fellow pilots, and undock for some pew pew.", 13, _theme.Muted));
        words.Children.Add(ActionButton("Learn more", ShowSupport, "support-about", true));
        Grid.SetColumn(words, 1); row.Children.Add(words);
        return Card(row);
    }

    private void ShowSupport()
    {
        DismissConfirmation();
        _confirmationPreviousFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        var body = new StackPanel { Spacing = 14, Margin = new Thickness(0, 12, 12, 12) };
        body.Children.Add(Text("Made for fellow pilots, with every feature free to use.", 13, _theme.Text, true));
        var recipient = new Grid { ColumnDefinitions = new ColumnDefinitions("64,*") };
        recipient.Children.Add(SupportPortrait(64));
        var identity = new StackPanel { Margin = new Thickness(12, 0, 0, 0), Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        identity.Children.Add(Text("SEND ISK IN GAME TO", 10, _theme.Muted, true));
        identity.Children.Add(new SelectableTextBlock { Name = "support-recipient", Text = SupportCharacterName, FontSize = 20, FontWeight = FontWeight.SemiBold, Foreground = B(_theme.Text) });
        identity.Children.Add(new SelectableTextBlock { Text = "Character ID: " + SupportCharacterId, FontSize = 11, Foreground = B(_theme.Muted) });
        Grid.SetColumn(identity, 1); recipient.Children.Add(identity);
        body.Children.Add(Card(recipient, new Thickness(12)));
        body.Children.Add(Text("In EVE, search for Aura Asuna, open the character's menu and choose Give Money. Any amount is appreciated.", 12, _theme.Text));
        body.Children.Add(Text("I build and maintain EVE-O to make life easier for fellow pilots. If it has made your time in New Eden better, an ISK gift is a lovely way to say thanks. Your generosity means less time earning ISK and more time improving the tool, helping the community, and undocking myself.", 13, _theme.Muted));
        body.Children.Add(Text("Donations are always optional. Thanks for flying with EVE-O. See you in New Eden!\nAura Asuna o7", 12, _theme.Muted));

        var feedback = Text("", 11, _theme.Positive);
        feedback.Name = "support-copy-status";
        AutomationProperties.SetLiveSetting(feedback, AutomationLiveSetting.Polite);
        var copy = ActionButton("Copy character name", async () =>
        {
            try
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard is null) throw new InvalidOperationException("Clipboard unavailable");
                await clipboard.SetTextAsync(SupportCharacterName);
                feedback.Foreground = B(_theme.Positive);
                feedback.Text = "Aura Asuna copied. Paste into EVE's character search.";
            }
            catch
            {
                feedback.Foreground = B(_theme.Danger);
                feedback.Text = "Couldn't copy. Select the character name above and copy it manually.";
            }
        }, "support-copy-name", true);
        var close = ActionButton("Close", DismissConfirmation, "support-close");
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        copy.Margin = new Thickness(0, 0, 8, 0);
        actions.Children.Add(copy); actions.Children.Add(close);
        var discord = CommandButton("Pop into Discord and say hi ↗", new("discord"), "support-discord");
        discord.FontSize = 12;
        discord.Padding = new Thickness(0, 3);
        discord.Background = Brushes.Transparent;
        discord.BorderThickness = new Thickness(0);
        discord.Foreground = B(_theme.Accent);
        discord.HorizontalAlignment = HorizontalAlignment.Left;
        ToolTip.SetTip(discord, "Join the EVE-O community on Discord. Everyone is welcome.");
        var footer = new StackPanel { Spacing = 6, Children = { actions, feedback, discord } };
        var panel = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        panel.Children.Add(Text(SupportHeading, _theme.Legacy ? 19 : 22, _theme.Text, true));
        var scroll = new ScrollViewer { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); panel.Children.Add(scroll);
        Grid.SetRow(footer, 2); panel.Children.Add(footer);
        KeyboardNavigation.SetTabNavigation(panel, KeyboardNavigationMode.Cycle);
        var dialog = new Border { Name = "support-dialog", Background = B(_theme.Surface), BorderBrush = B(_theme.Border),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(_theme.Radius), Padding = new Thickness(_theme.Legacy ? 16 : 24),
            MaxWidth = 520, MaxHeight = 560, Margin = new Thickness(16), HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, Child = panel };
        AutomationProperties.SetName(dialog, "Support EVE-O Preview with an optional ISK donation");
        _confirmation = new Border { Name = "support-backdrop", Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), Child = dialog };
        _confirmation.PointerPressed += (_, e) =>
        {
            if (ReferenceEquals(e.Source, _confirmation) && e.GetCurrentPoint(_confirmation).Properties.IsLeftButtonPressed)
            {
                DismissConfirmation();
                e.Handled = true;
            }
        };
        Grid.SetColumnSpan(_confirmation, 2); _root.Children.Add(_confirmation);
        foreach (var child in _root.Children.Where(c => c != _confirmation)) child.IsEnabled = false;
        copy.Focus();
    }
}
