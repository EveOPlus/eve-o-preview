
## Notes from Awox

<div style="color:red; font-weight:bold">Please read this section carefully!!</div>

* It's not yet clear if the crop feature is permissable by CCP. I don't see why it wouldn't, given it is very common for people to use ISBoxer to generate multiple cropped displays (which are even clickable). 
* There are some bugs that I haven't figured out how to fix yet, namely:
  * When initially launching, thumbnail labels and stylings are not applied (this is a pre-existing bug). I just deal with it and hover over each thumbnail to get the labels and styles to apply.
  * Somewhere thumbnail sizes get messed up, I think in `ApplyRestrictions()` somewhere but I'm not really sure. For the time being I just make sure **ThumbnailSize**, **ThumbnailMinimumSize** and **ThumbnailMaximumSize** are all set to the same value.
  
<hr>

## Overview

The purpose of this application is to provide a simple way to keep an eye on several simultaneously running EVE Online clients and to easily switch between them. While running it shows a set of live thumbnails for each of the active EVE Online clients. These thumbnails allow fast switch to the corresponding EVE Online client either using mouse or configurable hotkeys.

It's essentially a task switcher, it does not relay any keyboard/mouse events and suchlike. The application works with EVE, EVE through Steam, or any combination thereof.

The program does NOT (and will NOT ever) do the following things:

* modify EVE Online interface
* <strike>display modified EVE Online interface</strike> <i>oops - Awox</i>
* broadcast any keyboard or mouse events
* anyhow interact with EVE Online except of bringing its main window to foreground or resizing/minimizing it

<div style="page-break-after: always;"></div>

**Under any conditions you should NOT use EVE-O Preview for any actions that break EULA or ToS of EVE Online.**

If you have find out that some of the features or their combination of EVE-O Preview might cause actions that can be considered as breaking EULA or ToS of EVE Online you should consider them as a bug and immediately notify the Developer ( Aura Asuna ) via in-game mail.

<div style="page-break-after: always;"></div>

## How To Install & Use

1. Download and extract the contents of the .zip archive to a location of your choice (ie: Desktop, CCP folder, etc)
..* **Note**: Please do not install the application into the *Program Files* or *Program files (x86)* folders. These folders in general do not allow applications to write anything there while EVE-O Preview now stores its configuration file next to its executable, thus requiring the write access to the folder it is installed into.
2. Start up both EVE-O Preview and your EVE Clients (the order does not matter)
3. Adjust settings as you see fit. Program options are described below

Video Guides:

* [Eve online , How To : EVE-O Preview (multiboxing; legal)](https://youtu.be/2r0NMKbogXU)


## System Requirements

* Windows 7, Windows 8/8.1, Windows 10
* Microsoft .NET Framework 4.6.2+
* EVE clients Display Mode should be set to **Fixed Window** or **Window Mode**. **Fullscreen** mode is not supported.

<div style="page-break-after: always;"></div>

## EVE Online EULA/ToS

This application is legal under the EULA/ToS:

CCP FoxFour wrote:
> Please keep the discussion on topic. The legitimacy of this software has already been discussed
> and doesn't need to be again. Assuming the functionality of the software doesn't change, it is
> allowed in its current state.

CCP Grimmi wrote:
> Overlays which contain a full, unchanged, EVE Client instance in a view only mode, no matter
> how large or small they are scaled, like it is done by EVE-O Preview as of today, are fine
> with us. These overlays do not allow any direct interaction with the EVE Client and you have
> to bring the respective EVE Client to the front/put the window focus on it, in order to
> interact with it.

<div style="page-break-after: always;"></div>

## Application Options

### Application Options Available Via GUI

#### **General** Tab
| Option | Description |
| --- | --- |
| Minimize to System Tray | Determines whether the main window form be minimized to windows tray when it is closed |
| Track client locations | Determines whether the client's window position should be restored when it is activated or started |
| Hide preview of active EVE client | Determines whether the thumbnail corresponding to the active EVE client is not displayed |
| Minimize inactive EVE clients | Allows to auto-minimize inactive EVE clients to save CPU and GPU |
| Previews always on top | Determines whether EVE client thumbnails should stay on top of all other windows |
| Hide previews when EVE client is not active | Determines whether all thumbnails should be visible only when an EVE client is active |
| Unique layout for each EVE client | Determines whether thumbnails positions are different depending on the EVE client being active |

#### **Thumbnail** Tab
| Option | Description |
| --- | --- |
| Opacity | Determines the inactive EVE thumbnails opacity (from almost invisible 20% to 100% solid) |
| Thumbnail Width | Thumbnails width. Can be set to any value from **100** to **640** points |
| Thumbnail Height | Thumbnails Height. Can be set to any value from **80** to **400** points |

#### **Zoom** Tab
| Option | Description |
| --- | --- |
| Zoom on hover | Determines whether a thumbnail should be zoomed when the mouse pointer is over it  |
| Zoom factor | Thumbnail zoom factor. Can be set to any value from **2** to **10** |
| Zoom anchor | Sets the starting point of the thumbnail zoom |

#### **Overlay** Tab
| Option | Description |
| --- | --- |
| Show overlay | Determines whether a name of the corresponding EVE client should be displayed on the thumbnail |
| Show frames | Determines whether thumbnails should be displays with window caption and borders |
| Highlight active client | Determines whether the thumbnail of the active EVE client should be highlighted with a bright border |
| Color | Color used to highlight the active client's thumbnail in case the corresponding option is set |

#### **Active Clients** Tab
| Option | Description |
| --- | --- |
| Thumbnails list | List of currently active EVE client thumbnails. Checking an element in this list will hide the corresponding thumbnail. However these checks are not persisted and on the next EVE client or EVE-O Preview run the thumbnail will be visible again |

<div style="page-break-after: always;"></div>

### Mouse Gestures and Actions

Mouse gestures are applied to the thumbnail window currently being hovered over.

| Action | Gesture |
| --- | --- |
| Activate the EVE Online client and bring it to front  | Click the thumbnail |
| Minimize the EVE Online client | Hold Control key and click the thumbnail |
| Switch to the last used application that is not an EVE Online client | Hold Control + Shift keys and click any thumbnail |
| Move thumbnail to a new position | Press right mouse button and move the mouse |
| Adjust thumbnail height | Press both left and right mouse buttons and move the mouse up or down |
| Adjust thumbnail width | Press both left and right mouse buttons and move the mouse left or right |

<div style="page-break-after: always;"></div>

### Configuration File-Only Options

Some of the application options are not exposed in the GUI. They can be adjusted directly in the configuration file.

**Note:** Do any changes to the configuration file only while the EVE-O Preview itself is closed. Otherwise the changes you made might be lost.

| Option | Description |
| --- | --- |
| **ActiveClientHighlightThickness** | <div style="font-size: small">Thickness of the border used to highlight the active client's thumbnail.<br />Allowed values are **1**...**6**.<br />The default value is **3**<br />For example: **"ActiveClientHighlightThickness": 3**</div> |
| **CompatibilityMode** | <div style="font-size: small">Enables the alternative render mode (see below)<br />The default value is **false**<br />For example: **"CompatibilityMode": true**</div> |
| **EnableThumbnailSnap** | <div style="font-size: small">Allows to disable thumbnails snap feature by setting its value to **false**<br />The default value is **true**<br />For example: **"EnableThumbnailSnap": true**</div> |
| **HideThumbnailsDelay** | <div style="font-size: small">Delay before thumbnails are hidden if the **General** -> **Hide previews when EVE client is not active** option is enabled<br />The delay is measured in thumbnail refresh periods<br />The default value is **2** (corresponds to 1 second delay)<br />For example: **"HideThumbnailsDelay": 2**</div> |
| **PriorityClients** | <div style="font-size: small">Allows to set a list of clients that are not auto-minimized on inactivity even if the **Minimize inactive EVE clients** option is enabled. Listed clients still can be minimized using Windows hotkeys or via _Ctrl+Click_ on the corresponding thumbnail<br />The default value is empty list **[]**<br />For example: **"PriorityClients": [ "EVE - Phrynohyas Tig-Rah", "EVE - Ondatra Patrouette" ]**</div> |
| **ThumbnailMinimumSize** | <div style="font-size: small">Minimum thumbnail size that can be set either via GUI or by resizing a thumbnail window. Value is written in the form "width, height"<br />The default value is **"100, 80"**.<br />For example: **"ThumbnailMinimumSize": "100, 80"**</div> |
| **ThumbnailMaximumSize** | <div style="font-size: small">Maximum thumbnail size that can be set either via GUI or by resizing a thumbnail window. Value is written in the form "width, height"<br />The default value is **"640, 400"**.<br />For example: **"ThumbnailMaximumSize": "640, 400"**</div> |
| **ThumbnailRefreshPeriod** | <div style="font-size: small">Thumbnail refresh period in milliseconds. This option accepts values between **300** and **1000** only.<br />The default value is **500** milliseconds.<br />For example: **"ThumbnailRefreshPeriod": 500**</div> |
| **ClientCrop** | <div style="font-size: small">Specify cropping options for thumbnail capture.<br />This does nothing unless explicitly configured, however you can specify a global value to apply to all clients.<br />For example: **"ClientCrop": { "EVE - Awox": "45, 43, 1600, 1360" }** for just Awox, or<br>for all clients: **"ClientCrop": { "*": "45, 43, 1600, 1360" }**</div> |
| **LabelVerticalAlign** | <div style="font-size: small">Vertical alignment of label on thumbnails<br/>The default value is <b>Top</b>. Accepted values are Top and Bottom.</div> |
| **LabelHorizontalAlign** | <div style="font-size: small">Horizontal alignment of label on thumbnails<br/>The default value is <b>Center</b>. Accepted values are Left, Right and Center.</div> |
| **LabelFontSize** | <div style="font-size: small">Font size to use for label on thumbnails<br/>The default value is <b>14</b>.</div> |
| **LabelFontName** | <div style="font-size: small">Font family to use for label on thumbnails<br/>The default value is <b>Consolas</b>.</div> |
| **LabelForeground** | <div style="font-size: small">Color of the text of the label on thumbnails<br/>The default value is <b>#000000</b>. There is also **LabelForegroundActive** which applies when the preview is highlighted. <br/>If a non hexidecimal value is supplied, a default value will be used. </div> | |
| **LabelBackground** | <div style="font-size: small">Color of the background of the label on thumbnails<br/>The default value is <b>#000000</b>. There is also **LabelBackgroundActive** which applies when the preview is highlighted.<br/>If a non hexidecimal value is supplied, a default value will be used. </div> |
| **PaddingWidth** | <div style="font-size: small">Border width to draw inside the window<br/>The default value is <b>2</b>. There is also **PaddingWidthActive** which applies when the preview is highlighted.</div> | |
| **PaddingColor** | <div style="font-size: small">Color of border drawn on top of thumbnails<br/>The default value is <b>#222222</b>. There is also **PaddingColorActive** which applies when the preview is highlighted.</div> |

The original thumbnail highlighting will not work if you have **PaddingWidth**/**PaddingWidthActive** set.

<div style="page-break-after: always;"></div>

### Hotkey Setup

It is possible to set a key combinations to immediately jump to certain EVE window. However currently EVE-O Preview doesn't provide any GUI to set the these hotkeys. It should be done via editing the configuration file directly. Don't forget to make a backup copy of the file before editing it.

**Note**: Don't forget to make a backup copy of the file before editing it.

Open the file using any text editor. find the entry **ClientHotkey**. Most probably it will look like

    "ClientHotkey": {},

This means that no hotkeys are defined. Edit it to be like

    "ClientHotkey": {
      "EVE - Phrynohyas Tig-Rah": "F1",
      "EVE - Ondatra Patrouette": "F2"
    }

This simple edit will assign **F1** as a hotkey for Phrynohyas Tig-Rah and **F2** as a hotkey for Ondatra Patrouette, so pressing F1 anywhere in Windows will immediately open EVE client for Phrynohyas Tig-Rah if he is logged on.

The following hotkey is described as `modifier+key` where `modifier` can be **Control**, **Alt**, **Shift**, or their combination. F.e. it is possible to setup the hotkey as

    "ClientHotkey": {
      "EVE - Phrynohyas Tig-Rah": "F1",
      "EVE - Ondatra Patrouette": "Control+Shift+F4"
    }

**Note:** Do not set hotkeys to use the key combinations already used by EVE. It won't work as "_I set hotkey for my DPS char to F1 and when I'll press F1 it will automatically open the DPS char's window and activate guns_". Key combination will be swallowed by EVE-O Preview and NOT retranslated to EVE window. So it will be only "_it will automatically open the DPS char's window_".

<div style="page-break-after: always;"></div>

### Cycle Clients with Hotkey Setup

In a similar pattern to the per client Hotkey Setup, It is possible to set a key combinations to cycle through select Eve Windows. EVE-O Preview doesn't provide any GUI to set the these hotkeys. It should be done via editing the configuration file directly. Don't forget to make a backup copy of the file before editing it.

If you have not run EVE-O Preview before, or since this feature was added then it is recommended to quickly open and close EVE-O Preview once to trigger the config to update with some sample values. 

**Note**: Don't forget to make a backup copy of the file before editing it.

Open the file using any text editor. find the entries **CycleGroup1ForwardHotkeys** and **CycleGroup1BackwardHotkeys**. Most probably it will look like

    "CycleGroup1ForwardHotkeys": [
      "F14",
      "Control+F14"
    ],
    "CycleGroup1BackwardHotkeys": [
      "F13",
      "Control+F13"
    ]

**Note**: It is highly recommended to leave the Hotkey values as default and bind them with a gaming device if you can support it.

Next find the entry **CycleGroup1ForwardHotkeys**. Most probably it will look like

    "CycleGroup1ClientsOrder": {
      "EVE - Example DPS Toon 1": 1,
      "EVE - Example DPS Toon 2": 2,
      "EVE - Example DPS Toon 3": 3
    }

You should modify this entry with a list of each of your clients replacing "Example DPS Toon 1", etc with the name of your character. The numbers on the right are used to force the order in which they cycle.
If a character appears in the list but is not currently logged in, then it will simply be skipped.
If a character does not appear in the list, then they will never become active when cycling clients.

By now you may have noticed that there are four groups. The above configuration can be followed for a second group by using the values **CycleGroup2ForwardHotkeys**, **CycleGroup2BackwardHotkeys**, and **CycleGroup2ForwardHotkeys** or a fourth group using **CycleGroup4ForwardHotkeys** etc.
This may provide useful if you want to have one HotKey to cycle through a group of DPS characters, while another HotKey cycles through support roles such as gate scouts, or a group of logi.

Alternatively you may not want to use any of these HotKeys. Please note that deleting the values in their entirety will simply result in them being automatically re-generated.
Should you wish to remove these HotKeys completely, Simply set the values to empty, such as the example below:

      "CycleGroup1ForwardHotkeys": [],
	  "CycleGroup1BackwardHotkeys": [],
	  "CycleGroup1ClientsOrder": {},
	  "CycleGroup2ForwardHotkeys": [],
	  "CycleGroup2BackwardHotkeys": [],
	  "CycleGroup2ClientsOrder": {}
      "CycleGroup3ForwardHotkeys": [],
	  "CycleGroup3BackwardHotkeys": [],
	  "CycleGroup3ClientsOrder": {},
	  "CycleGroup4ForwardHotkeys": [],
	  "CycleGroup4BackwardHotkeys": [],
	  "CycleGroup4ClientsOrder": {}

**Hints** 
* Minimise the use of modifiers or standard keys to minimise issues with the client playing up. In the default example unusual Function keys (e.g. F14) are used which are then bound to a game pad or gaming mouse.
* The Eve client can be somewhat less than stable, often getting confused as client focus switches. It is near certain that you will experience issues such as keys sticking or even in some cases D-Scan running each time the client swaps. So far I have found no perfect solution and opt for the most stable solution instead, of sticking to the F14+ keys.
* For the best experience try to use the Control modifier. In the default example F14 is used to cycle to the next client, but if pressed mid locking a target (Control + Clicking) then the client will not cycle. By registering Control+F4 as an additional hotkey, the client will cycle.
* For a list of supported keys, see: https://docs.microsoft.com/en-us/dotnet/api/system.windows.forms.keys

### Per Client Border Color
Have you ever wanted your main client to show up in a different color so that it more easily catches your eye? Or maybe your Logi to stand out?

EVE-O Preview doesn't provide any GUI to set the these per client overrides as yet. Though, It can be done via editing the configuration file directly. 
**Note** Don't forget to make a backup copy of the file before editing it.

Open the file using any text editor. find the entry **PerClientActiveClientHighlightColor**. Most probably it will look like

    "PerClientActiveClientHighlightColor": {
      "EVE - Example Toon 1": "Red",
      "EVE - Example Toon 2": "Green"
    }

You should modify this entry with a list of each of your clients replacing "Example Toon 1", etc with the name of your character. The names on the right represent which highligh color to use for that clients border.

If a client does not appear in this list, then it will use the global highlight color by default.

**Hint** For a list of supported colors see: https://docs.microsoft.com/en-us/dotnet/api/system.drawing.color#properties

### Compatibility Mode

This setting allows to enable an alternate thumbnail render. This render doesn't use advanced DWM API to create live previews. Instead it is a screenshot-based render with the following pros and cons:
* `+`  Should work even in remote desktop environments
* `-`  Consumes significantly more memory. In the testing environment EVE-O Preview did consume around 180 MB to manage 3 thumbnails using this render. At the same time the primary render did consume around 50 MB when run in the same environment.
* `-`  Thumbnail images are refreshed at 1 FPS rate
* `-`  Possible short mouse cursor freezes

<div style="page-break-after: always;"></div>

## Credits

### Maintained by

* Aura Asuna


### Created by

* StinkRay



### Previous maintainers

* Phrynohyas Tig-Rah
 
* Makari Aeron

* StinkRay


### With contributions from

* CCP FoxFour


### Forum thread

https://forums.eveonline.com/t/4202


### Original repository

https://bitbucket.org/ulph/eve-o-preview-git

<div style="page-break-after: always;"></div>

## CCP Copyright Notice

EVE Online, the EVE logo, EVE and all associated logos and designs are the intellectual property of CCP hf. All artwork, screenshots, characters, vehicles, storylines, world facts or other recognizable features of the intellectual property relating to these trademarks are likewise the intellectual property of CCP hf. EVE Online and the EVE logo are the registered trademarks of CCP hf. All rights are reserved worldwide. All other trademarks are the property of their respective owners. CCP hf. has granted permission to pyfa to use EVE Online and all associated logos and designs for promotional and information purposes on its website but does not endorse, and is not in any way affiliated with, pyfa. CCP is in no way responsible for the content on or functioning of this program, nor can it be liable for any damage arising from the use of this program. 

