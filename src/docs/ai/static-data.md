# Portable EVE static data

`StaticDataService` owns the complete FC JSONL SDE, independently of combat
history. `StaticDataDatabase` supplies indexed item lookups and `ReadRecord(dataset,
key)` for any SDE dataset; callers own the returned `JsonDocument`. The host reuses
its existing SQLite dependency. Robin and the portable UI have no database access.

## Download and storage

The modern **Augments → Data setup → FC static data** section offers download/update,
progress, cancellation and installed build/size/counts. `WorkspaceView.OfferStaticDataDownload`
asks once per workspace session when no usable copy exists and logs are enabled or
Augments is opened. Accepting opens Data setup and starts the download; cancelling
keeps basic augments usable. `StaticDataService` construction and preference changes
never start network access. Subsequent launches with installed data work offline.
Update checks are explicit, with no recurring HTTP or directory polling.

Follow [FC's automation contract](https://developers.eveonline.com/docs/services/static-data/):
read `tranquility/latest.jsonl`, select `_key: sde`, then download the build-pinned
`eve-online-static-data-<build>-jsonl.zip`. An already-installed build needs only the
small metadata request. All 102 datasets in build 3503375 are retained, including
datasets unrelated to combat. The importer verifies JSON keys, required datasets
and the archive's own build metadata. Unsupported/malformed exports fail without
replacing usable data.

Storage is `StaticData/` beside the resolved global preferences file, following
the existing portable/installed path policy. The SDE never writes in an EVE folder.
The database format uses:

- `blocks`: approximately 64 KiB of JSONL per independently Brotli-compressed block;
  an unusually large record gets its own block.
- `records`: dataset/key → block ID and UTF-8 byte offset/length. A lookup decompresses
  only its block, rather than a dataset or the whole export.
- `names` and `combat`: indexed localized type aliases, category, weapon platform,
  damage mask and NPC gun/missile metadata.
- `manifest`: local schema version, FC build and complete dataset/record counts.
- `combat_index`: independent classification revision (currently 2). Older
  schema-2 databases without this table are revision 1.

Build 3503375 contains 676,271 records: about 552 MiB of source JSONL becomes a
151 MiB database. Runtime SQLite has a 4 MiB page cache, no memory mapping, and at
most 4,096 encountered-name results in memory. Import is a background operation;
the full raw export is never materialized as an object graph. There is no server,
installer or separate service. The ZIP is discarded after successful import.

Updates download/import under unique staging names. A lock in **our own data
directory** serializes installers; it is unrelated to EVE log files. The previous
reader remains usable throughout import. After a successful transaction, durable
file flush and validation, an atomic `current.json` replacement selects the new
generation. Swapping the reader invalidates its lookup cache. Cancel, truncated
downloads, failed imports and shutdown keep the previous generation. Abandoned
staging files are cleaned on the next update; old generated databases are removed
after successful activation. Disposal cancels work without blocking the UI thread.

`StaticDataDatabase.UpgradeCombatIndex` upgrades an older derived index locally
before opening its reader. A single SQLite write transaction rebuilds `names` and
`combat` from the installed `groups`, `types` and `typeDogma` blocks, then writes
the revision marker. Complete records and the SDE manifest are preserved. Failure
or cancellation rolls back the complete change; concurrent constructors serialize
at the transaction and recheck the marker. No download is needed. New service
instances begin with empty lookup/simulation caches; SDE replacement still clears
both caches. Log-service startup re-enriches only the displayed real window,
without changing totals/cursors, binary classification or dispatching alerts.

`WeaponPlatformClassifier` maps explicit SDE groups, with same-group
`variationParentTypeID` ancestry for named/faction turrets. It distinguishes the
missile families, pulse/beam lasers, fighters, Vorton, precursor weapons, bombs,
structure weapons and damaging superweapons. Rapid launchers share their ammo's
platform. Shared charges keep generic hybrid/projectile/laser fallbacks. Broad
Super Weapon membership needs positive damage attributes: PANIC and the
transportation oscillator remain unidentified as damaging weapons. Lances,
reapers and bosonic fields use their explicit dogma effects. Utility bombs share
the Bomb platform but are excluded from damage simulation.

Fighter category 87 uses ability masks 2227–2230, 2131–2134 and 2325–2328.
A name alone retains composition only when those damaging abilities agree.
Bomb-launch fighters remain unresolved because their charge is a separate attack;
abilities are never unioned into an invented damage mask.

## Damage evidence

The exact longest named item in a hit takes precedence. Attributes 114/118/117/116
describe EM/Thermal/Kinetic/Explosive damage; positive components form a bit mask.
This covers NPC missiles, ammunition, drones and fixed-damage smartbomb modules.
Turrets/launchers without charge information cannot establish player ammunition.

When an incoming line contains only an exact NPC name and hit quality, use that
NPC's normal attack attributes. Do not union its missiles or superweapon damage
into a gun hit. NPC missile-only attacks can follow `entityMissileTypeID` (507)
when no gun effect exists. `gfxTurretID` (245) can identify the NPC weapon platform.
Conflicting localized aliases retain only facts on which all matching types agree.
Known character names take precedence over NPC aliases.

For example, the current SDE gives Hypnosian Warden guns EM/Thermal, while its
named Praedormitan missile is Kinetic/Explosive. Vexing Phase-I Swarmer guns deal
all four. Damage-type icons describe the components of an attack, **not exact
post-resistance proportions**. The log supplies only the total applied hit amount.
Unknown ammunition/custom NPC labels stay unresolved without a placeholder icon.
An identified weapon platform remains visible independently of damage evidence.
Zero-damage attacks never display alpha or trigger a name flash.

Parsed entries retain mask, evidence, source type ID and SDE build. Snapshots
retain the union of positive hits' masks in the current window, separately for
NPC and Player damage. DPS presentation uses plain numeric values; each alpha
suffix shows its current hit's identified components and weapon platform, omitting
icons for missing facts. No damage amounts
are redistributed. Completed SDE updates refresh current real-window metadata
without replaying alerts, changing totals/cursors or reinterpreting simulated data.

The embedded fallback catalog is generated from this same production index, so
NPC and item evidence still works before download or when offline. An installed
SDE is authoritative. Regenerate using the matching original ZIP and database:

```powershell
python scripts/generate-log-catalog.py path/to/sde-jsonl.zip path/to/sde-build.sqlite
```

## Simulation catalog

`ReadSimulationCatalogAsync` lazily builds and caches a portable
`CombatSimulationCatalog` from the installed records. It uses a separate,
background read connection so catalog construction never holds the live lookup
lock. Schema-2 installs already contain all required datasets: only the derived
classification index needs a local upgrade. A successful SDE update invalidates the catalog;
active runs keep their original resolved attack profiles until they finish.

`StaticDataDatabase.Simulation` reads `types`, `groups`, `typeDogma`, `factions`
and `npcCorporations`. NPC faction IDs take priority, including corporation IDs
that refer to a faction. Older NPCs without IDs can use an unambiguous faction
stem from their SDE group name (for example Asteroid Guristas Frigate). Ship names
and racial associations are not faction evidence. Unassigned ships remain
available under All factions.

Simulation lists include published standard T1 and T2 weapons and damage ammunition.
Type meta groups 0/1/54 require dogma meta level (633) zero; groups 2/53 require
level five. Missing levels default to the corresponding standard level. This
excludes named meta modules that still belong to Tech I, and polarized modules
that belong to Tech II, as well as faction/deadspace/officer/storyline variants.
Categories 66 and 87 cover structure modules and fighters; category 23 covers
starbase batteries. Variants remain fully indexed for live logs; only simulation
choices are filtered. The weapon dropdown groups these choices under small,
disabled platform headings; each actual choice retains its SDE type identity.
Weapons and ammunition are matched using
`chargeGroup1`–`chargeGroup5` (604/605/606/609/610), `chargeSize` (128) and module
capacity. This retains size and T2 restrictions. Damage masks and baseline
amounts use 114/118/117/116, multiplied by normal gun or missile modifiers
(64/212); cadence uses speed/duration/missileLaunchDuration (51/73/506).
NPC guns and named missiles become separate attacks, with their own source type,
damage evidence and interval. Simulation does not apply fitting skills, target
resistances, range, tracking or other dynamic modifiers.
Fighter primary attack amounts use 2227–2230, multiplier 2226 and cadence 2233,
for one fighter. Secondary/cooldown abilities, support fighters and primary-less
interceptors are not offered. Defender missiles and utility bombs are excluded.
Weapons without a positive known amount/cadence are excluded instead of receiving
an invented cycle. Superweapon delays use 2262/1839, bursts 2264/2265 and cooldowns
73/669 without the former 60-second cap. Disintegrator ramp uses 2733/2734.
`CombatSimulationSequence` retains independent activation/burst clocks; the
single-hit appearance sample skips activation delay. Reloads, fighter squadrons,
flight time, area geometry and Vorton chain targets are outside the model.

The portable UI provides searchable NPC/weapon/ammo fields and a faction filter.
The service resolves IDs again against its current catalog before queueing the
run. Generated log text passes through `EveLogParser`, then takes the normal temporary-store and combat-event route,
with the selected NPC/Player classification retained throughout. Presentation
does not need a separate simulated damage renderer.

Catalog attack amounts/cadence come from selected type IDs, but visible metadata
uses the same conservative `FindItem` name lookup as live logs. Ammo records
carry optional logged platform/source IDs so ambiguous aliases and bomb/launcher
differences do not gain unsupported icons. NPC labels whose aliases do not agree
on NPC classification are excluded from NPC choices. Known NPC names containing
` - ` are matched whole before parsing weapon/quality suffixes. An NPC target's
attributes must never identify the player's outgoing damage.

`CombatSimulationCatalog.LogsAmmunition` covers missiles/rockets/torpedoes, bombs
and structure missile/guided-bomb launchers. Turrets and point defense emit the module name.
Turret ammo affects the amount without exposing its damage composition. NPC
incoming weapons and the user's selected outgoing weapon are separate streams,
each with its own cadence. Repair log text follows the observed `remote ... by/to`
format. Both damage and repair numbers are invariant-culture log text.

## Validation

`StaticDataTests` exercises complete dataset retention, named-item/NPC precedence,
unknown player ammunition, offline reopen, cache invalidation, update failure and
cancellation. The opt-in `tests/StaticData.Smoke` tool uses the production downloader
and parser. It requires explicit paths and does not modify EVE files or app history:

```powershell
dotnet run --project tests/StaticData.Smoke -- --download bin/static-data-check
dotnet run --project tests/StaticData.Smoke -- --audit bin/static-data-check path/to/Gamelogs
dotnet run --project tests/StaticData.Smoke -- --simulation-catalog bin/static-data-check
```

The September 14 year-to-date corpus check read every available file dated from
January 1 onward (2,980 files; 382,519 parsed entries). The production parser recognized 40,105 incoming damage entries
and 126,081 outgoing entries. Of 39,491 positive incoming hits, 37,995 (96.2%) had
SDE-supported composition; 33,578 incoming entries had multiple components.
The other positive hits lacked sufficient item/attack evidence. Zero-damage NPC
attack messages explain most additional entries without a damage composition.
One additional damage-like line had no listener and was excluded from character
statistics. The audit reports aggregates only; local corpora and generated databases stay
outside tracked source. These checks do not prove live client disk-flush latency.

The earlier catalog check against build 3503375 found 18 combat factions, 5,223 NPCs,
899 weapons and 823 ammunition types. It round-tripped 12,508 generated entries
through the production parser with zero direction, classification, platform,
damage, amount, effect, weapon, evidence or source-ID differences. Catalog loading
and the comparison completed in about 3.2 seconds; the smoke process retained
approximately 13.8 MB of managed memory after collection. It also checked
Guristas group-based attribution, separate Hypnosian Warden guns/missiles,
small/large charge incompatibility, blaster/railgun T2 restrictions and T1 rocket
launcher restrictions. Focused integration tests verify selected source IDs,
NPC/Player totals and indicators, invalid selection rejection and removal of
temporary stats. UI smoke checks cover search selection, dependent ammo/faction
lists and clearing an incomplete search.

The complete platform update supersedes that earlier catalog: 187 weapons in 27
modeled families and 303 standard ammunition choices, with the same 18 factions
and 5,223 NPCs. All compatible ammunition pairs in both directions plus every
NPC scenario produced 13,869 round trips with zero differences. The full raw
audit covered 52,999 types and 1,610 groups, including category 23 batteries,
category 66 structure modules and category 87 fighters. It identified generic
fallbacks and utility members separately from modeled damaging weapons.
