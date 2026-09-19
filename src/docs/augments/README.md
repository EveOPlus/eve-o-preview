# Thumbnail augments

Augments adds damage, repairs and the current solar system to EVE-O Preview
thumbnails. It is available in the Light and Dark themes. These graphics appear
on thumbnails; they do not cover the EVE client window.

## Getting started

In EVE, enable **Log game messages to file** and Local chat logging. In EVE-O
Preview, open **Augments → Data setup** and enable **Read EVE logs**. The usual
Documents/EVE/logs folder is detected automatically. For another location, expand
**Advanced: custom log folder**, choose the parent folder containing `Gamelogs`
and `Chatlogs`, then select **Apply folder**. **Use automatic location** removes
the custom path. If reading fails, check the displayed status and use **Retry
reading logs**.

**Log language** defaults to **Automatic (per file)**, allowing clients with
different languages. You can select English, Chinese, French, German, Japanese,
Korean, Russian or Spanish manually. This is separate from the interface language
and applies to new entries; saved history is unchanged. Coverage varies by message
type. All eight languages have client-template tests; recorded English, Chinese,
Japanese and Russian fixtures cover specific client formats. See the
[coverage and remaining samples](../ai/log-languages.md#real-client-sample-gaps).
Formatting tags are removed while visible item and system names are resolved
using the installed static data.

If FC static data is missing, a popup offers to download it. Choose **Download**
to open Data setup, where progress and **Cancel download** are available. Cancel
the popup to continue without it; it is offered at most once per session.
Basic log reading, totals, DPS, repairs and incoming indicators still work.
Item-dependent simulation stays disabled until data is installed. Available
offline references still identify known damage and systems; unavailable details
are omitted rather than guessed. **Download / update** is always available in
Data setup. Downloads require internet access; installed data works offline.

The reader associates logs with their character, reads complete entries without
blocking EVE's file access, and follows newly created files. Augments settings
apply across gameplay profiles.

## Shared settings and themes

**Thumbnail augments → All thumbnails** configures existing and future clients
together. **Per-client settings** is optional: select a character to change only
that client's appearance. **Use shared settings** removes its custom settings.
Select All thumbnails or collapse Per-client settings to return to shared editing.

Simple mode provides three themes:

| Theme | Initial damage display | Appearance |
| --- | --- | --- |
| Classic | Alpha + DPS | Red incoming text, green outgoing text and damage icons |
| Damage colours | Alpha + DPS | Text follows the identified damage type, with damage icons |
| Minimal | DPS only | Compact display; damage icons appear if alpha is enabled |

The known system name appears below the character name by default. Configure it
under **Previews & layout → Title & highlight → Current solar system**. Choose
Below, Above, Left or Right relative to the title, a separate font size or **Use
title size**, and its colour. Apply changes to update all thumbnails. Combat
theme changes preserve these settings. Nothing appears on a live thumbnail until
its system is known; the settings sample uses Jita when no system is available.
Repairs have a separate visibility setting; shield repairs are blue and armour
repairs are bronze in the defaults.

Simple mode has independent **Show DPS**, **Show alpha** and **Show weapon icons**
switches. Advanced mode retains the **Damage display** choices and the weapon
icon switch. Weapon visibility is shared between modes; switching to Simple
preserves saved Advanced colours and custom symbols. Hiding alpha keeps the
weapon-icon preference for when alpha is shown again.
Alpha (α) is the damage from one logged hit, briefly shown when it arrives. DPS
is the damage averaged over the rolling window set in Data setup. Simultaneous
weapon hits are not combined into an estimated volley.

Incoming and outgoing damage have fixed rows. Each DPS value appears only while
that direction has damage in its rolling window; empty rows keep their space.
Incoming alpha sits to the right of incoming DPS, and outgoing alpha beside
outgoing DPS, each with an `α` prefix. Their latest hits expire independently.
Damage and weapon icons belong to alpha; DPS values use plain direction colours.
Each weapon platform has one fixed icon, inspired by T1 ammunition shapes. It
stays the same when ammunition changes; damage-type icons describe the hit.
The 32 platform symbols cover rockets; light, heavy, heavy-assault, cruise and XL
cruise missiles; torpedoes and XL torpedoes; blasters and railguns; pulse and beam
lasers; autocannons and artillery; drones and fighters; smartbombs, disintegrators
and Vorton projectors; bombs and guided bombs; structure missiles and point
defense; doomsdays, lances (including disruptive lances), reapers and bosonic
fields; and defender missiles. Generic missile, laser, hybrid and projectile
symbols remain available when the log cannot establish a narrower family.
Rapid launchers use their ammunition family: a light missile cannot tell the
reader whether it came from a rapid launcher. Faction, size and Tech II variants
within a platform use the same symbol. Shared turret ammunition does not identify
the gun subtype, and a turret module does not reveal its hidden ammunition.

Existing downloaded data upgrades its weapon index locally on startup, without
another download. Saved colours and custom symbols remain intact; newly split
families inherit previous custom family colours and deliberately changed icons.
Log cursors, historical totals and NPC/Player classification are preserved.
When alpha is hidden, weapon styling cannot appear. The simulator offers **Show
alpha + weapon icons** to enable both through the normal thumbnail settings,
including current per-client settings when configuring all thumbnails.
Select **Weapon platform** under **Alpha text colour** to use each platform's text
colour for alpha. With Direction selected, alpha retains the incoming/outgoing
colour. Inactive icon/text controls are disabled. Unidentified weapons retain
direction colours and have no platform icon; the simulator names the platform
it can identify.

Advanced mode adds separate incoming/outgoing DPS visibility, NPC/Player
breakdown, positioning, font size and event duration. Text can follow direction,
damage type or weapon platform for alpha. Icon and text colours are independent, including
repair colours. Each damage, weapon and repair setting shows its selected icon;
the sample follows colour edits immediately. Click a swatch to open the colour popup: a spectrum, RGB sliders
and numeric values, hex input, and a palette of text, damage and repair colours.
Select **Apply** to save. You can also enter `#RRGGBB` directly. Unapplied colour
and folder edits are retained when navigating between pages.

DPS, alpha and repair text use the title's font family and style by default.
Under **Advanced → DPS / alpha font**, choose an installed font and **Apply**,
or change **Font style**. **Use title font** clears both overrides. Size and combat
colours remain independent. These settings apply to All thumbnails or the selected
per-client configuration; unapplied font edits survive navigation.

Font families use a standard dropdown with the full installed-font list and a
visible scrollbar, just like font styles. The same picker is used for title fonts.
Title & Highlight uses the shared colour popup for text, outline, marker and border
colours; **Apply changes** saves those drafts.

## Position and order

- **Previews & layout → Title & highlight → Title position** places the character
  title and its system name at any of the nine thumbnail positions. Apply the
  change with the other title settings. The system's Below/Above/Left/Right
  setting remains relative to the title. The compact sample pans to the selected
  position while editing; scroll within it to inspect the rest of the thumbnail.
- **Thumbnail augments → Advanced → Position & order** places the DPS block at
  the same nine positions. **Row order** arranges incoming damage, outgoing
  damage and repairs. Incoming and outgoing repairs have separate fixed rows
  when enabled, so one direction cannot replace the other. Hidden DPS
  rows keep their space, so starting or stopping fire does not move other rows.
- When title and DPS use the same position, the title/system block appears first
  and DPS follows beneath it. The blocks never draw on top of each other.
  Horizontal offsets adjust each block from its selected edge or centre; vertical
  offsets adjust the block position. When stacked, the DPS vertical offset controls
  the gap beneath the title (at least four pixels).

Simple mode keeps the Classic theme, alpha and DPS, title-font inheritance and
the system below the title as the starting configuration. Switch to Advanced
for position, font and separate text/icon customisation. Changing modes preserves
custom settings for a later return to Advanced.

## Incoming damage indicator

Under **Thumbnail augments → Incoming damage indicator**, enable flashing and
choose **Title**, **Thumbnail** or **Both**. The thumbnail uses a translucent wash
in the selected flash colour (red by default). Choose the colour and duration,
and select **Player only** or **NPC + Player**.
**Flash interval (ms)** sets the time between flashes (100–2000 ms, 500 by default).
**Animation → Fade** is the default and smoothly fades in and out at the selected interval,
including a smooth finish when the indicator expires. Both targets share the
same animation phase. Title also flashes the name in Augments
Overview; Thumbnail leaves names unchanged. Each positive
incoming hit extends the duration without restarting the blink rhythm. Outgoing damage, repairs and zero-damage
attacks do not trigger it. Original colours return when the duration ends.
The section heading shows **(off)** when the indicator is disabled.
**Thumbnail flash opacity (%)** controls the strongest part of the thumbnail wash:
0% is invisible and 100% is opaque. The default is 20%; try 10% for a dull,
less distracting pulse. Title colour is independent of this opacity. Explicit
saved **Blink** selections remain available; Blink spends half the cycle in each
colour. Opacity and animation are global Augments settings and persist across profiles.

## Simulation

**Simulate** sends events through the same display and notification path as live
logs, on all visible thumbnails or the selected client. Choose **NPC** or
**Player** under Combatant:

- **NPC:** choose a faction and search for a ship. **All factions** lets you
  search every NPC. **Any ship** chooses a random ship from the selected faction
  for each thumbnail's run. NPC gun and missile attacks keep their separate
  damage types and firing intervals. Outgoing and mixed scenarios also show
  **Your weapon** and ammunition: damage to the NPC comes from that weapon.
- **Player:** choose a standard **T1 or T2 weapon**, then compatible T1 or T2
  ammunition. Named meta, polarized, faction, deadspace and officer variants are
  omitted. The ammo list respects charge groups and sizes; drones and smartbombs
  do not need ammunition. Standard structure, starbase and superweapon choices
  are included. This filtering affects simulation choices only: the live parser
  still recognises all supported item variants.

Choices use the installed **FC static data**. Existing downloads work immediately;
if none is installed, download it under Data setup. Hit amounts and firing intervals start from
the static attributes, with modest hit-size variation; **Damage scale (%)** adjusts their
size. These are unfitted examples without character skills, target resistances
or other combat modifiers.
Fighters simulate one fighter's primary repeatable attack, not an entire squadron
or its secondary abilities. Support fighters and fighters without that attack
are omitted. Defender interception and non-damaging bombs are not simulated.
Disintegrators ramp across successive cycles. Superweapons retain activation
delays, damage ticks and long cooldowns; a 20-second run may finish before a
delayed weapon fires. A single-event appearance sample shows the hit immediately.
Simulation models baseline attacks, not reloads, chain targets, area geometry,
flight time, squadron losses or target application.

Simulation uses the real log parser and the same name-based static-data lookups.
Missiles and rockets are logged by ammunition name; turret entries name the
module and generally do not reveal its loaded charge. Selected turret ammo sets
the simulated amount, but the display leaves its damage types unresolved just
as it does for a real module-only log. Uncertain static-data aliases stay uncertain;
NPC labels that cannot be classified as NPC by the log reader are not offered.

**Mixed combat** varies damage direction and sends both incoming and outgoing
shield and armour repairs on a separate cadence. Friendly repairs remain player
events even when the selected damage source is an NPC. **Selected event** reveals direction
and event controls. All damage in an NPC run remains NPC damage; all damage in a
Player run remains player damage, including for the incoming-damage indicator.
Selected repairs do not require a weapon or ammunition. They respect the same
repair visibility setting as live logs. If repairs are hidden, **Show repairs on
simulated thumbnails** enables them for the selected target, including relevant
per-client settings when simulating all thumbnails.

Enable **Incoming and outgoing repairs together** to send both directions on
each repair cycle. Use **Selected event** and choose Shield, Armour or Hull
repairs to preview that type in both directions. The single Direction control
is disabled while both are selected. This option also pairs the repairs in
Mixed combat. **Repair sources** selects 1 to 20 separate pilots (3 by default).
Choose **Mixed repairs** to distribute them across shield, armour and hull.
With more than one source, timed simulations alternate one pilot and the selected
number of pilots. Mixed combat also includes combined repair types. Samples are
spaced by the DPS window (at least 3 seconds), so the previous combined rates can
expire before a single-repair sample appears. At the default 10-second window,
a 20-second run shows single then combined repairs; a longer run repeats the cycle.
One source keeps the ordinary repair cadence. The appearance preview above the simulator always shows all enabled items
together: both damage directions and alpha values, all three repair types with
IN/OUT prefixes, and the title/system text. It respects appearance visibility
settings and is independent of the selected simulation scenario.

Repair overlays show **HP repaired per second**, using the same rolling window as
DPS (10 seconds by default). All cycles from all sources contribute to the total
for each type and direction. Each active type is a compact icon and number pair,
with both using that repair type's colour. Each row starts with **IN** or **OUT**,
including when several types share the condensed row. Rates disappear when their contributions leave the window.

**Weapon** and **Ammunition** use standard dropdowns with visible scrollbars.
Weapons are grouped under small platform headings that cannot be selected.
Changing weapons filters the ammunition list and keeps the current ammunition
when compatible; otherwise it selects the first compatible option.

Augments follows the selected workspace language. Character and solar-system
names, raw log entries, font names and the current English SDE item names remain
unchanged. Language changes preserve unapplied appearance and folder edits.

The overview includes simulated statistics during the run. They are discarded
when it ends or when **Stop simulation** is pressed. Real statistics continue to
be collected and saved. Simulation also works with log reading disabled, does
not control the game and does not write to EVE's log files. Clearing a search
field leaves it unselected; choose a result before starting.

## Damage types and FC static data

**Data setup → FC static data** downloads the complete static data for offline
item and NPC lookups. Missing data triggers a confirmation popup when log reading
is enabled or Augments is opened. Download starts only after acceptance. Later updates are manual. Progress and cancellation remain
available during the download, and the previous copy stays usable until its
replacement is ready. Data is stored beside EVE-O Preview's global settings.

Damage icons show the EM, Thermal, Kinetic and Explosive components supported by
the logged item or NPC attack. Several icons mean the attack contains several
types. Icons show only what is known: the weapon platform, damage components,
or both. Missing ammunition or platform information adds no placeholder icon.
A named turret still shows its platform even when the log omits its loaded ammo.
The displayed DPS is the measured total; it is not divided into
estimated amounts for each damage type.

Icons appear beside the alpha value (`α 1,250`). If missing in Advanced mode,
enable **Show damage / repair icon**; per-client settings may override All thumbnails.

NPC names are matched against FC static data; other combatants count as players.
Custom target labels can prevent an NPC match. An embedded catalog provides
fallback lookups before the first download. See the [static-data guide](../ai/static-data.md)
for storage, lookup and update details.

## Overview, systems and history

Overview starts with **Combined statistics** for all characters: damage, weighted
average DPS, hit counts, largest hits, repairs and total system jumps. Incoming and
outgoing remain separate. Repair cells show HP and **cycles**, for example
**8,000 HP (16 cycles)**. One counted cycle means one logged positive repair event.

Below this is one row per character: portrait, name, system, current combat/online
state, combined incoming/outgoing DPS and jumps. Click a row, or focus it and
press Enter, to open that character's details. **All characters** returns to the
list. Ordinary log updates preserve keyboard focus and an open detail view.

Details include damage totals and recorded average DPS split by NPC/Player, hits received
and dealt, largest hits, distinct targets and attackers, system jumps and unique
systems, plus incoming/outgoing shield, armour and hull repair HP and cycles.
**Log details** contains that character's source counts, observation times and
recent parsed entries. Damage between tracked characters appears from both
perspectives; adding both directions does not give unique fleet damage.

Recorded average DPS accumulates until **Damage / DPS** or **All statistics** is
reset. It uses damage divided by accepted sample time across combat periods,
excluding the first and last 10 seconds and gaps of more than 60 seconds between
positive hits. Complete intervals between hits must fit inside that middle section;
this avoids misleading partial samples for slow weapons such as artillery. Long
firing intervals up to 60 seconds stay within a combat period. Longer gaps start a
new period; logs cannot establish whether a pause is a cooldown, reload or idle time.
Very short fights may contribute no valid samples, shown as **-**.
Incoming/outgoing and NPC/Player each have independent samples. The combined
average pools accepted damage and sample seconds across characters and categories;
it is a weighted average, not simultaneous fleet DPS. Thumbnail DPS and character
row DPS remain live rolling values. Hover over a detail average for its available
measurement start; upgrades can only backfill timestamped history still retained.

**NPC types** counts distinct NPC labels in the logs, not individual ships or
kills. Repeated ships with the same label count once. Renamed targets can affect
this count and NPC classification. Player uniqueness also uses logged names.
Zero-damage events and repairs do not count as damage hits or damage targets.

System jumps count observed changes between known systems in timestamp order.
The first known system and reconnecting to the same Local channel do not count
as jumps. Multiple Local files and delayed files are reconciled by observation
time. The count does not distinguish gates, wormholes, cynos or clone travel,
and cannot recover unlogged intermediate systems. Conflicting locations in the
same log second are omitted from travel counts. The current parser requires
English Local system messages. The last known system remains in memory and
across restarts and can be stale if logging stops.

Recent parsed history is limited to the retention period and 200,000 entries.
Counters, distinct encounters and location observations survive cleanup and
restart. Existing databases backfill the new counters from retained entries;
**Activity since** gives their available start, which may be later than the older
damage totals. Simulation uses the same counters temporarily, then restores real
statistics. Chat conversations are not stored.

On **Overview**, **Reset statistics** opens a small popup with **All statistics**,
**Damage / DPS**, **Repairs**, and **Jumps**. From a character's detail page, the
selected counters reset only for that character; the popup names them. From the
full overview, they reset for all characters. Other counters remain intact.
Cancel changes nothing. Resets retain
current systems and file positions, and older files cannot refill reset counters.
For travel, the current system becomes the starting point for the next measurement.
The existing Data setup reset still clears all counters and recent history.

See the [combat-log guide](../ai/combat-logs.md) for implementation details and
validation limits.

## Finding settings

Use the workspace search: **DPS**, **reps**, **alpha**, **weapon icons** and
**thumbnail augments** open Thumbnail augments. **Log**, **logs**, **logging**,
**log folder**, **SDE** and **data setup** open Data setup. **Jumps** and
**reset statistics**, **average DPS**, **combined statistics**, **repair totals** and
**repair cycles** open Overview. Results also match localized section labels.

## Updating from an older version

Exit EVE-O Preview before replacing its application files. Keep `Profiles`,
`EVE-O Preview.settings.json`, `Logs` and `StaticData` together in the resolved
settings location. Copy them while EVE-O is closed if making a backup.
The same portable-folder or Local AppData policy continues to apply.

Older profile settings, title offsets and per-client configurations remain valid.
Missing new fields keep the original Title/Blink indicator, incoming/outgoing/repair
order, and top/bottom DPS placement. Explicit Light/Dark/Legacy selections from
versioned global settings are preserved. Older unversioned settings start in Dark;
Legacy remains available, but does not contain Augments.

Combat history uses `Logs/Combat.sqlite`, a lightweight local SQLite file with no
database server or separate installer. It is created when needed. Earlier combat
databases upgrade in place, retaining totals and read positions; new activity
counters are backfilled from retained history only. Existing complete static-data
databases are reused without another download. An unavailable or unsupported static
copy can be replaced through the confirmation/download flow; basic logs keep working.
