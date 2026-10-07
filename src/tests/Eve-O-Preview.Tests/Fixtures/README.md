# Combat log samples

These small fixtures preserve real combat message structure, damage/repair amounts
and relative timing. Listener and counterparty names are replaced with fictional
`Sample Observer`, `Sample Pilot` and `Sample NPC` aliases. Dates are rebased to
January 2026; filenames, corporation/alliance labels, identity links and original
source headers are removed. No chat messages or identity mapping are included.

- `autocannon.txt`: NPC/player combat with the named Scout autocannon and rockets.
- `repairs.txt`: missile combat and incoming shield repair cycles.
  Zero-HP repair messages exercise exclusion from successful repair-cycle counts.
- `artillery.txt`: sparse artillery hits separated by long gaps; this is not proof
  that those gaps equal the weapon's firing cycle.
- `log-expectations.json`: independently counted amounts, message counts and mock
  NPC aliases for each file.

`CombatLogFixtureTests` reads these through the production complete-line reader,
parser and SQLite store. Tests use a fixed clock and temporary databases, with no
local EVE installation, personal logs, network, live clients or static-data download.
They are ordinary xUnit tests discoverable by Visual Studio Test Explorer.
