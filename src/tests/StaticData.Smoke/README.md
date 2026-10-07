# Static data and log replay checks

This opt-in runner uses the production static-data importer, log parser and
SQLite store. It never writes to EVE logs or the user's combat history. Run from
`src`; use an isolated build output if EVE-O is open.

```powershell
dotnet build tests/StaticData.Smoke -c Debug -p:UsedAvaloniaProducts= -p:OutDir=C:/dev/eve-o-preview/src/bin/static-data-check/
& ./bin/static-data-check/StaticData.Smoke.exe --simulation-catalog <StaticData-directory>
& ./bin/static-data-check/StaticData.Smoke.exe --audit <StaticData-directory> <EVE-logs/Gamelogs-directory>
& ./bin/static-data-check/StaticData.Smoke.exe --weapon-icons <StaticData-directory>
```

`--simulation-catalog` checks standard T1/T2 modules and ammunition using type
meta groups and dogma meta levels, compatible ammunition, fighter primary
attributes, superweapon cadence and parser round-trips for every compatible
weapon/ammo pair in both directions plus every NPC scenario. Append an output
JSON path to export the portable catalog for the optional UI smoke selections.
Existing complete databases upgrade their derived index locally without another
download. Use a copy of the installed StaticData folder when auditing the upgrade;
the runner's service constructor performs the same transaction as normal startup.

`--weapon-icons` checks every offline weapon/item alias through both the installed
and embedded lookups, the production parser, and the shared icon formatter in
both directions. It covers all 32 known platforms and includes named/meta/faction
variants outside the simulator lists. Known weapon icons never receive an automatic
unknown marker when ammunition is absent from the log.

`--audit` reads all available game logs within the preceding year, and nearby
`Chatlogs/Local_*.txt` files. Reads are shared and framed at complete entries.
An in-memory SQLite replay checks parsed totals, duplicate-delivery idempotency,
travel/activity snapshots, fixed row layouts and temporary-state isolation.
Recorded DPS sample damage must remain within each character/category/direction's
logged total; the report includes accepted sample seconds without character names.
Only aggregate results are printed; no raw entries or character names are exported.
It does not alter the production measurement period or saved totals.

`--download <directory>` explicitly downloads/imports the complete current SDE.
`--record <directory> <dataset> <key> [key...]` reads specific static records for
diagnostics and icon reference IDs. Network import is never part of ordinary tests.
