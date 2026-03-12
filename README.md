
## Notice
This software is no longer licensed under the MIT License. Commits prior to b8b25d9 may still be used under MIT. All subsequent commits are provided under GPLv3.

## License
Copyright © 2026 Aura Asuna. All Rights Reserved.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program.  If not, see <https://www.gnu.org/licenses/>.

## Join us on Discord
Chat with the devs and eachother here on Eve-O Plus Discord https://discord.gg/HzQHBtTEcB

## Overview

The purpose of this application is to provide a simple way to keep an eye on several simultaneously running EVE Online clients and to easily switch between them. While running it shows a set of live thumbnails for each of the active EVE Online clients. These thumbnails allow fast switch to the corresponding EVE Online client either using mouse or configurable hotkeys.

It's essentially a task switcher, it does not relay any keyboard/mouse events and suchlike. The application works with EVE, EVE through Steam, or any combination thereof.

The program does NOT (and will NOT ever) do the following things:

* modify EVE Online interface
* display modified EVE Online interface
* broadcast any keyboard or mouse events
* anyhow interact with EVE Online in a way that changes gameplay or provides an unfair advantage.

<div style="page-break-after: always;"></div>

**Under any conditions you should NOT use EVE-O Preview for any actions that break EULA or ToS of EVE Online.**

If you have find out that some of the features or their combination of EVE-O Preview might cause actions that can be considered as breaking EULA or ToS of EVE Online you should consider them as a bug and immediately notify the Developer ( Aura Asuna ) via Discord.

<div style="page-break-after: always;"></div>

## How To Install & Use

1. Download and extract the contents of the .zip archive to a location of your choice (ie: Desktop, CCP folder, etc)
..**Note**: While we make a best effort to support installing the program into *Program Files* or *Program files (x86)* folders. These folders in general do not allow applications to write anything there while EVE-O Preview stores its logs and configuration files next to its executable, thus making it difficult to support.
2. Start up both EVE-O Preview and your EVE Clients (the order does not matter)
3. Adjust settings as you see fit. Program options are described below

Video Guides:

* [Eve online , How To : EVE-O Preview (multiboxing; legal)](https://youtu.be/2r0NMKbogXU)


## System Requirements

* Windows 7, Windows 8/8.1, Windows 10, Windows 11
* Microsoft .NET Framework 4.8+
* EVE clients Display Mode should be set to **Fixed Window** or **Window Mode**. **Fullscreen** mode is not supported.

<div style="page-break-after: always;"></div>

## EVE Online EULA/ToS

This application attempts to be legal under the EULA/ToS:

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

**Note**: CCP have adopted a stance that they "will not authorize or otherwise sanction the use of any third party software." and "Please use such third party applications or other software at your own risk."

With that said, any feature in Eve-O Preview that could be used for cheating or gaining an in game advantage, is not our intention, must be considered a bug, and reported immediately.

e.g. https://www.eveonline.com/news/view/client-modification-the-eula-and-you
>It should be clear to everybody that we have no interest in banning people who do not do anything bad in New Eden.

and

>Our stance on third-party software is that we do not endorse such software as we have no control over what it does. As such, we can’t say that multiboxing software isn’t against our EULA. But the same goes in this case, that unless we determine that people are doing things beyond “multiboxing”, we will not be taking any action. We only care about the instances where people are messing with our process for the purposes of cheating, and running multiple clients at the same time is not in violation of our EULA in and of itself unless it involves trial accounts.

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
| Title Font Section | Used to set the Font, Foreground Color, Outline Color, and Offset potion (from the top left) of the Clients Title

#### **Active Clients** Tab
| Option | Description |
| --- | --- |
| Thumbnails list | List of currently active EVE client thumbnails. Checking an element in this list will hide the corresponding thumbnail. However these checks are not persisted and on the next EVE client or EVE-O Preview run the thumbnail will be visible again |
| Hide Thumbnails | Temporarily hide all thumbnails until toggle back to visiblie again through the same feature. A hotkey for this feature may be set by double clicking in the space to the right of "Hotkey"
| Minimize | Immediately minimize all Eve Clients. A hotkey for this feature may be set by double clicking in the space to the right of "Hotkey"

#### **Cycle Groups** Tab
| Option | Description |
| --- | --- |
| Select Cycle Group Dropdown | A list of Cycle Groups that have been setup. Select the group you wish to view or edit here first |
| Select Cycle Group - | Delete the currently selected Cycle Group |
| Select Cycle Group + | Create a new Cycle Group |
| Description | A unique identifier / name for the selected Cycle Group |
| Forward Hotkey | This is the hotkey used to cycle forward in your order. If you only have a Single client in the group, then set only this value (as there is no backwards for only one). Note: Double click in either of the two spaces provided to set the Primary or Secondary hotkey |
| Backward Hotkey | This is the hotkey used to cycle backward in your order. Note: Double click in either of the two spaces provided to set the Primary or Secondary hotkey |
| Clients and Order + | Add one of the active (currently running) Eve Clients into this Cycle Group |
| Clients and Order - | Remove the selected client from the below list, so they no longer participate in this Cycle Group |
| Clients and Order Up | Move the position of the currently selected client up by one, in the order in which it cycles |

#### **FPS / Audio** Tab
| Option | Description |
| --- | --- |
| Enable DirectX FPS Limits | Fully turn on or off the FPL Limiter, None of the below FPS limits will apply while this is disabled |
| Active Client | The maximum FPS of the currently active / foreground Client |
| Inactive Client | The maximum FPS of the non-active / background Clients. Please avoid setting this below 15 FPS for the best experience, although lower may be possible it is not advised |
| Predicted Client | When using Cycle Groups, Attempt to predict the next next client upcoming in the list to increase the FPS in preparation for taking foreground next |
|  |  |
| Mute Jump Gate Tunnel | Attempt to silence the audio when jumping through a gate |
| Mute Asteroid Belt Warp In | Attempt to silence the audio (the big dong thing and machine gun ticking sound) when loading grid on each asteroid belt |

#### **Profiles** Tab
**Note**: Profiles are like a complete copy of all of your settings, hot swappable at runtime without having to close down Eve-O Preview. Each Profile can be setup completely different to one another and do not currently share any settings.

At this time, Eve-O Preview will always launch with the Default profile, and does not remember the last profile used.
| Option | Description |
| --- | --- |
| Clone Current Profile | Creates a new profile with an exact copy of your current profile |
| Delete Current Profile | Deletes the currently selected profile. Note: Default cannot be deleted |
| Current Profile | A unique name to help identify the currently selected profile. This matches the windows Folder name |

<div style="page-break-after: always;"></div>

### Mouse Gestures and Actions

Mouse gestures are applied to the thumbnail window currently being hovered over.

| Action | Gesture |
| --- | --- |
| Activate the EVE Online client and bring it to front  | Click the thumbnail |
| Minimize the EVE Online client | Hold Control key and click the thumbnail, or right click and select Minimize |
| Minimize ALL EVE Online clients | Right click any thumbnail and select Minimize All |
| Switch to the last used application that is not an EVE Online client | Hold Control + Shift keys and click any thumbnail |
| Move thumbnail to a new position | Press and hold right click for a moment, or press right click to bring up menu, select Move, and then click when done |
| Adjust thumbnail size | Press right click to bring up menu, select Resize, and then click when done |
| Adjust thumbnail size, maintaining aspect ratio | Hold Shift key while re-sizing. See steps above to reside |

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

<div style="page-break-after: always;"></div>

### Cycle Clients with Hotkeys

It is possible to set a key combinations to immediately jump to certain EVE window. This applied to either a single client or an ordered list of clients. 

There are an unlimited number of Cycle Groups that you can create which may provide useful if you want to have one HotKey to cycle through a group of DPS characters, while another HotKey cycles through support roles such as gate scouts, or a group of logi.

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
