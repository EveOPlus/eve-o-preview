# EVE-O Preview

Keep an eye on your EVE characters and switch between them with a click or a hotkey. Arrange live previews, build character cycling groups, and tune frame rates and audio for your fleet.

**Every feature is free.** Available on Windows today.

[Download EVE-O Preview](https://github.com/EveOPlus/eve-o-preview/releases/latest) · [Join Discord](https://discord.gg/HzQHBtTEcB) · [Report a bug](https://github.com/EveOPlus/eve-o-preview/issues)

*This guide follows the current source; an older release may have the previous interface. Check your release notes when updating.*

[Quick start](#start-here) | [Find a setting](#find-what-you-need) | [Cycling](#cycle-through-your-fleet) | [Themes](#themes-and-right-click-menus) | [Backups](#profiles-backups-and-updates) | [Troubleshooting](#troubleshooting-and-help)

## Start here

1. Download the application ZIP from [Releases](https://github.com/EveOPlus/eve-o-preview/releases/latest), rather than the source-code archive. Extract the **whole archive** into a folder of your choice. Keep its companion files together.
2. Install the **.NET 10 Desktop Runtime for Windows x64** from [Microsoft](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) if it is not already installed. The current application uses .NET 10, not .NET Framework 4.8. You do not need the developer SDK to run it.
3. Set each EVE client's display mode to **Fixed Window** or **Window Mode**. Exclusive fullscreen is not supported by the previews.
4. Run `EVE-O Preview.exe` and log in your characters. Either application can start first. If the settings window does not appear, double-click EVE-O's icon in the Windows system tray, including the hidden-icons area.
5. Click a character's preview to switch to that client. Right-click a preview and choose **Move** or **Resize** to arrange it.
6. Open **Previews & layout** to adjust size, titles and highlights. Open **Switching & hotkeys** to set up a character cycle.

Use a Windows x64 system supported by .NET 10. EVE-O Preview requires Windows; Linux and macOS are not supported.

**Already using EVE-O?** Back up your [profiles and global settings](#profiles-backups-and-updates), exit the old copy completely, then extract the new release. Use **Appearance → Legacy** for the familiar compact layout.

## Find what you need

| I want to… | Go to… |
| --- | --- |
| See my fleet at a glance | **Overview** |
| Choose which characters have previews | **Clients** |
| Change preview size, opacity or hover zoom | **Previews & layout → Size & zoom** |
| Customize character names, fonts, colors and borders | **Previews & layout → Title & highlight** |
| Change visibility, window positioning or close-to-tray behavior | **Previews & layout → Window behavior** |
| Set cycling hotkeys or rearrange characters | **Switching & hotkeys** |
| Limit FPS, manage CPU affinity or mute selected sounds | **Performance & audio** |
| Clone, rename or switch configurations | Top **Profile** dropdown → **Manage profiles…** |
| Choose a theme or customize the thumbnail right-click menu | **Appearance** |
| Open documentation, Discord or project information | **Help & about** |
| Stop EVE-O completely | **Exit**, beside Help & about |

**Can't find a setting?** Press **Ctrl+K** to search in the modern workspace. Search recognizes familiar names such as “thumbnail” and “overlay” as well as the new labels. Legacy has **About → More… → Search settings…**.

## Everyday controls

These gestures apply to the preview under your mouse.

| Action | Control |
| --- | --- |
| Switch to a character | Left-click its preview |
| Minimize that character | Ctrl+click, or right-click → **Minimize** |
| Minimize every EVE client | Right-click → **Minimize all** |
| Return to the last non-EVE application | Ctrl+Shift+click a preview |
| Move a preview | Right-click → **Move**, then click to finish; holding the right mouse button also starts moving |
| Resize a preview | Right-click → **Resize**, then click to finish |
| Keep its proportions while resizing | Hold Shift during the resize |
| Temporarily skip a character when cycling | Right-click → the skip-cycling option; select it again to re-enable |

### Closing the window versus quitting

**Close to the system tray** is enabled by default for new profiles. Clicking **X** hides the workspace and keeps EVE-O running. Double-click its tray icon to reopen it. This setting also makes EVE-O start in the tray.

Use **Exit** beside **Help & about**, or the tray menu's Exit action, to stop EVE-O completely. If you prefer X to quit, turn off **Close to the system tray**. An existing profile's saved preference is retained.

### Saving changes

Most switches save immediately. For fields with **Apply**, use Apply or Enter to commit your changes; **Discard** abandons unapplied edits. The preview editor can show draft title and highlight changes before you apply them. Follow the save status shown in the workspace, especially after a validation error.

Switching profiles or quitting asks before discarding unapplied edits. Closing into the tray keeps those edits available.

## Make previews work for you

In **Previews & layout**:

- **Size & zoom:** set shared preview dimensions and opacity, enable hover zoom, and choose the point that stays fixed as a preview grows.
- **Title & highlight:** choose a font, style, text and outline colors, outline width and position. Set the active character's border independently. The compact title-and-border sample stays visible while you edit. **Actual size** and **Fit width** control its scale; scroll inside it to inspect large fonts or offsets. The active-character and skipped-marker sample controls sit together beneath it. The sample uses a logged-in character name, then a saved offline character, then "EVE - Sample Name"; you can edit the name for testing. It also tries to take a still from an open client for the background. The image stays in memory while EVE-O is open; **Refresh image** takes another still. If capture is unavailable, the title and border remain usable on a plain background.
- **Window behavior:** keep previews on top, hide the active character's preview, hide previews when you leave EVE, or remember game window positions. Separate preview layouts can give each active character its own arrangement.

The **Clients** page saves each character's preview visibility in the active profile. **Hide all previews** is a quick way to clear the screen without removing your layout.

Turning off remembered game window positions or separate preview layouts clears the corresponding saved positions. Clone your profile first if you want to experiment and return to the old arrangement.

## Cycle through your fleet

1. Open **Switching & hotkeys** and create or select a cycle group.
2. Give it a useful name, such as DPS, Logi or Scouts.
3. Add running characters to its order.
4. Drag the rows into position. The arrow buttons remain available for small adjustments. Use the expand button at the top right of the character-order section for more room inside the current EVE-O window, then return when finished.
5. Set forward and, optionally, backward hotkeys. Each direction supports an alternate binding. A group containing one character works as a direct switch to that character.

Choose shortcuts that do not conflict with your normal EVE controls. If you want a shortcut to work while holding a modifier, configure that combination too.

### Temporarily take a character out of the cycle

If a character is back in station or otherwise out of the fight, use its skip control in the order editor or right-click its preview. Skipping applies to **all cycle groups in the current profile**, without removing the character or changing its place in the order.

A red circle with a diagonal line appears beside its preview title by default. Choose another marker or color in **Title & highlight**. The marker remains visible even if character names are hidden. Re-enable the character when ready; skipping is temporary session state, tracked separately for each profile. It survives switching away and back, and resets when EVE-O restarts.

## Themes and right-click menus

Choose **Light**, **Dark** or **Legacy** in **Appearance**. The application theme applies to all profiles. Give a profile its own accent color in **Profiles** to make a change of configuration easier to spot; this is separate from character highlight colors.

Legacy recreates the old compact settings layout. It is a theme of the new UI, so some controls are updated while familiar settings remain in approximately their original positions.

### Coming from the old interface?

| Old tab | Modern location |
| --- | --- |
| General | **Previews & layout → Window behavior**; CPU affinity is under **Performance & audio** |
| Thumbnail | **Previews & layout → Size & zoom** |
| Zoom | **Previews & layout → Size & zoom** |
| Overlay | **Previews & layout → Title & highlight** |
| Active Clients | **Clients** |
| Cycle Groups | **Switching & hotkeys** |
| FPS / Audio | **Performance & audio** |
| Profiles | **Profiles** |
| About | **Help & about** |

In Legacy, the theme selector is at the bottom left. Additional settings, including menu customization, are under **About → More…**.

### Customize the thumbnail menu

Open **Appearance → Thumbnail right-click menu**. Drag actions and divider rows to reorder them, insert or remove dividers, and choose a menu theme independently of the workspace. Styles include Graphite, Midnight, OLED Black, Nebula, EVE Carbon and the four empire-inspired palettes, as well as Light and Dark.

The default order is Minimize, Minimize all, a divider, Skip cycling, another divider, Move and Resize. **Reset** restores that layout. With Minimize first, a quick double right-click can open the menu and select Minimize. Put Skip cycling first if you prefer that action under the pointer.

## Performance and audio

**Performance & audio** provides separate FPS limits for the active client, background clients and the predicted next client in a cycle. Enable **Limit client frame rates** to apply them. A limit of **0** means unlimited. Very low background limits can make switching feel less responsive, so adjust them while flying your normal number of clients.

Automatic CPU affinity assigns processor resources according to active and predicted client roles. Selective muting includes **Mute Jump Gate Tunnel** and **Mute Asteroid Belt Warp In**. Advanced users can enter a comma-separated list of custom audio event IDs.

## Profiles, backups and updates

Profiles hold gameplay settings, preview layouts, cycle groups, character visibility and a profile accent. Use the top **Profile** dropdown to switch setups. Choose **Manage profiles…** at the bottom of that dropdown to clone a working setup before experimenting, rename it or delete a profile you no longer need. Legacy keeps its **Profiles** tab. **Default** cannot be deleted. EVE-O currently starts with Default rather than remembering your last selection.

Application theme and thumbnail-menu layout/style are global, shared across profiles.

### Where settings are stored

EVE-O looks for an existing `Profiles` folder beside its executable first, then under `%LOCALAPPDATA%\Eve-O Preview`. With no existing profiles, it tries to create them beside the executable and falls back to Local AppData when that location is not writable, such as a protected Program Files installation.

| Data | Location |
| --- | --- |
| Each profile | `Profiles\<profile name>\EVE-O Preview.json` |
| Global preferences | `EVE-O Preview.settings.json`, beside the resolved Profiles folder, with a Local AppData fallback if needed |

To back up or move your setup, **Exit EVE-O**, then copy the entire `Profiles` folder and the global settings file. Keep those files when updating the application. If settings appear to be missing, check both storage locations and whether you launched a different copy of EVE-O.

### Advanced settings

All configurable options are available in EVE-O's settings window. You do not need to edit JSON files. Changes below belong to the selected profile.

In **Previews & layout → Advanced**, you can configure:

| Setting | What it does |
| --- | --- |
| Snap previews together | Align previews while arranging them |
| Delay before hiding | Wait before hiding previews outside EVE; enter seconds, rounded up to the next client check |
| Resize limits | Set minimum and maximum width and height together; the current preview size adjusts to fit |
| Client check interval | Check for clients and update preview properties every 300–1000 ms; this does **not** control game FPS or the live preview frame rate |
| Use compatibility capture | Use slower still-image previews when live previews do not work; changing this recreates preview windows |
| Login preview position | Set the position of login-screen previews, including negative coordinates for monitors above or left of the main display |

Use **Apply changes** to save. The hide delay is counted in client checks, so review its displayed duration if you change the check interval. In **Legacy**, open **Thumbnail → Advanced preview settings** (also available from General).

For individual characters, open **Clients → Character colors & minimization**. Choose a known character or type an offline character name and select **Edit**. You can:

- Give the character its own active border color using the palette, custom color picker or color code. Highlighting must be enabled in **Title & highlight**.
- Select **Use the profile's border color** to remove its color override.
- Select **Keep open when switching characters** to exempt it from automatic minimization. Manual minimize actions still work.

Select **Save character settings** to apply, or **Discard character edits** to undo unapplied changes. In Legacy, open **Active Clients → Colors / priority**. The title editor also links to these character settings, and setting search includes the old JSON property names.

## Troubleshooting and help

| Problem | Check first |
| --- | --- |
| EVE-O is running but there is no settings window | Double-click its system tray icon; check Windows' hidden-icons area |
| A character's preview is missing | Check **Clients**, **Hide all previews**, and the visibility settings in **Window behavior** |
| A preview is black or not updating | Confirm EVE is in Fixed Window or Window Mode and restore the game window; if it persists, report your display mode and whether the client was minimized |
| A character is missing from cycling | Check group membership, order and the temporary skip marker |
| A title looks different from the editor | Apply the changes, check Actual size versus Fit width, and check for a per-character active border override |
| A setting did not save | Look for Apply, validation feedback or Retry; confirm the active profile |
| The application asks for a runtime | Install the **Windows x64 .NET 10 Desktop Runtime**, not only the base .NET or ASP.NET runtime |
| Settings disappeared after moving or updating | Check both the portable and Local AppData storage locations above |

For help, [pop into Discord and say hi](https://discord.gg/HzQHBtTEcB). To [report a bug](https://github.com/EveOPlus/eve-o-preview/issues), include the version from **Help & about**, steps to reproduce, theme, number of clients and relevant display settings. The included **Launch Eve-O Preview with Verbose Logging.cmd** can collect more detail. Review logs before sharing them because they can contain character names and local paths.

ESI character login and DPS overview configuration are not available yet; their unfinished pages are hidden.

## Say thanks in New Eden

I love investing my time in EVE-O so you can enjoy yours. An ISK gift means less time grinding and more time to improve the tool, help fellow pilots, and undock for some pew pew.

If you would like to send a gift, search for **Aura Asuna** in EVE, open the character's menu and choose **Give Money**. Character ID: **95465272**. You can also open **Say thanks → Learn more** in the workspace for the donation details and a button to copy the name.

Every feature is free, and donations are always optional. Thanks for flying with EVE-O. See you in New Eden!

Aura Asuna o7

## EVE Online and third-party tools

EVE-O switches focus between clients; it does not broadcast your keyboard or mouse input to multiple clients. Use it in accordance with [EVE Online's EULA](https://www.eveonline.com/legal/eula). Historical comments about older EVE-O versions are not approval of every feature in the current application. Report any behavior that may violate the rules to the maintainer.

## Contributing, credits and license

For source navigation and development instructions, start with [src/README.md](src/README.md). The [build and test guide](src/docs/ai/build-and-test.md) covers the current .NET projects, Windows validation and release tooling.

Maintained by **Aura Asuna**. Created by **StinkRay**, with thanks to previous maintainers **Phrynohyas Tig-Rah**, **Makari Aeron** and **StinkRay**, **CCP FoxFour**, and the EVE-O contributors and community.

[Original repository](https://bitbucket.org/ulph/eve-o-preview-git) · [EVE forum thread](https://forums.eveonline.com/t/4202)

Copyright © 2026 Aura Asuna. Distributed under [GNU GPLv3](src/Eve-O-Preview/LICENSE.txt), without warranty. Commits prior to `b8b25d9` may still be used under MIT; subsequent commits are provided under GPLv3.

EVE Online and its associated names, logos, artwork and characters are the intellectual property of Fenris Creations (FC). EVE-O Preview is an independent project and is not affiliated with or endorsed by Fenris Creations (FC). All other trademarks belong to their respective owners.
