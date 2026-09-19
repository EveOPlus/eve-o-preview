# Game-log languages and event parsing

The shared parser supports **English, Chinese, French, German, Japanese, Korean,
Russian and Spanish**. Log language is independent of interface language.
The client message templates are authoritative for wording and argument order.
The required text and representative examples are committed to this repository;
building and testing require no installed EVE client or external input files.

## Reading and language selection

**Augments > Data setup > Log language** defaults to **Automatic (per file)**.
The reader detects listener/session headers and prioritizes that language, while
also accepting mixed-language messages. The manual selector restricts message
matching to one of the eight languages. It cannot supply a missing listener or
override conflicting character headers. Changes affect new entries; saved offsets,
history and totals are not replayed or reset.

Localized Local filenames, channel names and system senders are recognized.
Only an exact system sender in the Local channel can supply a chat location.
Player chat is not retained. Both Local messages and game travel messages require
an exact, unambiguous SDE solar-system name before updating location.

Some wording is shared across languages. For example, French and Spanish incoming
damage both use `de`. Without a matching header or manual selection, the event
can have a known direction but retain `Automatic` (unknown language). This does
not turn a guess into a per-file language hint.

The reader accepts complete timestamped game lines in any category. Unsupported
game messages remain plain-text `Unknown` events; the parser does not discard
them simply because no typed rule exists. Chat ingestion is limited to Local
system messages. Fleet-loot tabular exports and market CSV exports are separate
formats and are not inputs to this reader.

## Components

- `CompleteLogReader` handles shared, read-only access, encoding, complete lines,
  file replacement and byte offsets.
- `EveLogText` extracts visible text from EVE/HTML formatting. It handles quoted
  attributes, unbalanced tags, entities, and unclosed `localized` names. A star
  within a localized-name span ends that decoration even when sentence text
  follows it without a closing tag. Stars outside those spans remain literal.
  Hint attributes never replace visible names. Entity decoding happens once,
  after tag removal, so encoded literal markup remains text.
  Local system messages also emit an unwrapped trailing star; the parser removes
  that marker only when the remainder is an exact SDE system name.
- `EveLogLanguageCatalog` contains the eight languages' client templates, message
  IDs, header labels, Local names and hit-quality strings.
- `EveLogTemplates` turns those literals and reviewed arguments into anchored,
  bounded, linear regexes. Captures have semantic names; translated word order
  cannot swap quantities, targets or destinations. Whitespace includes nonbreaking
  spaces; ASCII and full-width colons are accepted. Russian plural alternatives
  and French gender alternatives are recognized.
  Bounty currency comes from the client's currency label (`星币` in Chinese);
  the parsed unit remains `ISK` in every language.
- `EveLogLanguages` indexes grammars by category and caches candidate ordering.
- `EveLogParser` validates numbers, attribution and SDE names, then emits
  `ParsedLogEntry`. The retained legacy service/contract names preserve consumers;
  they do not restrict the parser to combat.
- `CombatLogService.LogEvent` publishes recent recognized events on the ingestion
  worker after commit, using the existing new-byte and 30-second receipt window.
  Consumers marshal to their own thread. Combat notifications remain limited to
  entries with a damage/repair direction.

The framework does not add alert controls or a separate dashboard for every new
event kind. It makes those events available to consumers without treating them as
damage.

## Event fields

| Kind | Meaning and fields |
| --- | --- |
| Damage / Repair | `Amount`, `Direction`, repair `Effect`, counterparty, weapon and SDE damage evidence |
| CombatMiss | Missed attack; no damage amount/direction or damage flash |
| SystemChange | Confirmed Local/jump/undock observation with `SolarSystem` and `SolarSystemId` |
| Navigation | Autopilot, docking requests/results, travel restrictions and traffic-control warnings; never proof of arrival |
| Decloak | Actual loss of cloak from proximity, traps or decloak emitters |
| Cloak | Activation failures and cloak restrictions; distinct from an actual decloak |
| Mining | `Quantity` and `ItemName`, optional exact `ItemTypeId`; ordinary/critical additional yield. Combined messages also carry `ResidueQuantity` |
| MiningResidue | Lost units, separate from mined yield; no invented ore identity |
| MiningStatus | Depleted target, full hold, crystal destruction/invalid target and range failures |
| Bounty | Decimal `Quantity`, `Unit = ISK`; accrual wording is not confirmation of a wallet payment |
| Capacitor | `Quantity`, `Unit = GJ`; neutralization, drain and transfer templates; never HP repair or damage |
| WarpDisruption / ElectronicWarfare | Scramble/disruption attempts, jams and broken target locks |
| Salvage / Cargo | Salvage outcomes, cargo restrictions and space failures |
| Drone / Targeting / Module | Drone command failures, lost/out-of-range targets, module activation/reload/safety warnings |
| Fleet / SkillTraining / Connection | Fleet membership requests/results, training queue/completion and channel connection status |
| Unknown | Retained game text without invented meaning |

`MessageId` identifies the matching client template. Some IDs have identical
visible text; it is not a recovered unique ID from the wire. New fields and enum
values are additive for stored-history compatibility. Non-combat quantities
never enter `Amount`/`Direction`, damage/repair totals or damage flashes.

## Damage ambiguity and names

Japanese source and target damage fragments currently have identical wording.
The parser requires the client color on the **leading damage number** to resolve
their direction: red incoming, cyan outgoing. It stops examining colors at the
first visible character; overview colors inside a target name cannot change the
direction. When that evidence is absent or unknown, the message stays unclassified.
Already established direction survives metadata refresh of stored plain text.

Japanese repair direction appears after the weapon name and remains mandatory.
Terminal weapon suffixes in other repair templates may be absent. Native
hit-quality suffixes are recognized in all eight languages.

The installed full SDE is authoritative for NPC, weapon, ore and system names.
Type lookup uses all localized aliases. `system_names` indexes localized
`mapSolarSystems` names and upgrades older installed exports transactionally.
Conflicting aliases do not become guessed IDs. The embedded catalog is used when
no full SDE is installed; an installed alias missing from that export does not
silently fall back to an older build.

Visible text is the lookup key. No automatic translation or handwritten item-name
translation table is involved. Unknown overview names keep the existing Player
fallback policy; that policy is not an authenticated character identity.

**Important Names in English is supported for every one of the eight languages**,
in both automatic and manual modes. The language of NPC, item, ore, station and
system names is independent of the surrounding message. English visible names
use the same SDE alias lookup as localized names; they do not switch the detected
message language to English. The client may put a translated name in `hint` while
displaying English, reversing the usual wrapper. Ignore that hint for identity
and display purposes. Unknown visible names must not resolve through a known hint.

Custom overview formatting is independent of message grammar. Nested/unclosed
font, size, color, emphasis and link tags are removed; literal angle-bracket
labels, entities and symbols remain visible. A label containing ` - ` can share
the weapon delimiter: an exact SDE weapon suffix recovers that boundary without
assuming a particular pilot/ship/ticker layout. Colors inside the label cannot
change the direction supplied by the event (including Japanese's leading amount
color). Unknown labels still allow amounts, directions and event kinds to parse.
No parser can recover a hidden pilot name or unambiguously split arbitrary text
that duplicates message delimiters without additional evidence. Keep the full
plain-text event when identity or item metadata is unavailable; do not fabricate
an identity or treat an arbitrary display label as an exact SDE alias.

## Adding patterns and testing

Add a reviewed client template with its message ID, actual **log** category,
event kind and explicit combat direction/effect. A dialog's UI severity is not
necessarily its wire category: combat messages are routed to `combat`, while
jump/undock lines use `None`. Status wording must not be promoted to a confirmed
location, yield or reward. Append persisted enum values; never reorder them.

Retain complete template wording in `EveLogLanguageCatalog` and representative
rendered lines in `Fixtures/logs/localization-templates.json`. Update both when
changing a template and review unsupported argument syntax explicitly. Normal
builds and tests use only the committed C# catalog and fixtures.

The standalone regressions cover:

- [Template cases](../../tests/Eve-O-Preview.Tests/Checks/LogTemplateTests.cs):
  every bundled template in automatic/manual modes, marked-up/plain input,
  category gates, header/Local routing, language ambiguity, quantities, word order,
  and Japanese direction evidence.
- [Chinese client samples](../../tests/Eve-O-Preview.Tests/Checks/ChineseLogFixtureTests.cs):
  anonymized reader fixtures for travel, reload, targeting, damage/misses, mining,
  proximity decloak, rejected cloak activation, adjusted bounties and remote
  repairs, capacitor neutralization, scrambling, jamming, jump/autopilot and drone
  warnings with custom overview labels. UTF-16 Local includes English header keys,
  Chinese message text, a per-line BOM and an unwrapped localization marker.
  See the [fixture inventory](../../tests/Eve-O-Preview.Tests/Fixtures/logs/README.md).
- [Reader and language checks](../../tests/Eve-O-Preview.Tests/Checks/LogLanguageTests.cs):
  attribution, overrides, typed events, spoof rejection, persistence and live
  file-tail updates without replay.
- [Japanese client samples](../../tests/Eve-O-Preview.Tests/Checks/JapaneseLogFixtureTests.cs):
  sixteen anonymized recorded events through the production reader in automatic and
  manual modes; incoming/outgoing damage colors, separate damage totals, named
  missile SDE evidence, native hit qualities, misses, proximity decloak, mining
  and full-hold status. Controlled removal of the amount color
  verifies that overview colors cannot invent a direction.
- [SDE name checks](../../tests/Eve-O-Preview.Tests/Checks/StaticDataTests.LogNames.cs):
  localized aliases, ambiguity and offline index upgrades.
- [Mixed-name checks](../../tests/Eve-O-Preview.Tests/Checks/MixedNameLogFixtureTests.cs):
  recorded Russian messages with English names and Chinese Important Names in
  English output; all eight grammars with English NPC/item/ore/system names,
  automatic/manual modes and plain/closed/unclosed wrappers with misleading hints.
  Full-SDE integration also verifies English names and rejects hint-only matches.
- [UI smoke checks](../../tests/Eve-O-Preview.UI.Smoke/Program.CombatLogs.cs):
  the eight-language selector, settings commands, search and Light/Dark
  presentation. Catalog checks cover localized labels and format arguments.

Examples use synthetic identities and rebased timestamps. There are no source
paths, original filenames, identity links or user chat in the fixtures.

Numeric rendering is checked separately from translated wording. Current rules
accept dot decimals and correctly grouped comma thousands, independent of the
operating-system culture. Templates alone do not establish alternate decimal or
group separators; unsupported formats remain unclassified. Template-derived
fixtures validate grammar and semantics, not every live-client rendering setting.
Recorded English, Chinese, Japanese and Russian samples provide additional wire evidence;
other languages still need recorded-client samples for that level of verification.

The broader Windows suite has a reported, unresolved process-handle delta
assertion in the [settings resource scenario](../../tests/Eve-O-Preview.Tests/Checks/SettingsIntegrationTests.cs).
Focused parser checks do not resolve that failure or establish live-client
coverage for the remaining sample matrix. See [build and test](build-and-test.md)
for the test commands and native validation boundaries.

## Real-client sample gaps

Keep this checklist current as anonymized samples are added. A passing generated
template case is not proof that the client emits that exact wire format. Existing
recorded English fixtures cover damage and remote repairs. Chinese fixtures cover
headers, undocking, reload, warp interference, target range, zero/nonzero outgoing
damage, incoming damage, incoming/outgoing misses, ordinary mining, named proximity
decloak, rejected cloak activation while targeted, adjusted bounty accrual (including
comma-grouped amounts), incoming shield/armor/hull repairs, zero-amount outgoing
shield/armor repairs, Local/jump changes, capacitor neutralization, warp scrambling,
burst jamming, autopilot status and drone target warnings. They include custom overview formatting
and unclosed localized-name markup. These establish specific variants, not all
rendering settings or repair amounts.

The Japanese fixtures cover a native header, incoming NPC damage with four hit
qualities, an incoming miss, two named proximity decloaks, ordinary mining and
a full-hold status. It preserves original damage colors and unclosed localized
names, including mining wording after the name marker. A second excerpt verifies
incoming and outgoing hits using the same wording, distinguished by the original
amount colors, and outgoing missile metadata from the visible Japanese SDE name.
Repairs and other Japanese event variants remain unverified by recorded fixtures.

Russian fixtures cover native headers, connection, undock, incoming/outgoing
damage, named proximity decloak, ordinary mining and full-hold status, including
English important names and nonbreaking spaces. Chinese has a recorded undock
with English visible names and translated hints. This display option is tested
across all eight grammars; equivalent recorded samples for other languages and
event types remain useful to verify actual client rendering.

| Priority | Real samples needed | What they establish |
| --- | --- | --- |
| Chinese | Nonzero outgoing shield/armor repair and outgoing hull repair; additional damage/weapon/quality display settings | Remaining repair directions/amounts and peer/weapon boundaries |
| Chinese | Critical extra yield, residue, combined yield/residue; depleted target and full hold | Actual yield/residue quantities and status separation |
| Chinese | Normal bounty accrual and fractional currency values | Remaining currency rendering variants |
| Chinese | Unnamed proximity decloak, trap pulse and deployable pulse; additional cloak activation failures | Remaining decloak and cloak-status variants |
| French, German, Korean, Spanish | Headers, Local/jump/undock, damage/misses, all remote repair directions, mining/residue/status, bounties and decloak/cloak | Real word order, client composition, name decoration and encoding across all eight languages |
| Russian | Local/jump, misses, all remote repairs, critical mining/residue/depleted target, bounties, other decloak/cloak variants and localized-name display | Remaining event and name-display variants |
| Japanese | Outgoing misses, all remote repair directions and additional weapon/overview display settings | Remaining direction-bearing endings and display variants |
| Japanese | Local/jump/undock, critical mining/residue/depleted target, bounties, other decloak variants and cloak activation failures | Remaining event families and rendering variants |
| All languages | Capacitor neutralization/drain/transfer, scramble/disruption, jam and broken locks | Separate effects that must not enter damage/repair totals |
| All languages | Salvage success/failure/empty result, cargo limits, drone failures, lost/out-of-range targets, module reload/activation/safety warnings, fleet membership and skill-training events | Broader status coverage and actual log categories |

For the two broad status/effect rows, Chinese has specific examples of
neutralization, scrambling, burst jamming, drone target warnings, reload and target
range. Other variants in those families still need recorded fixtures.

For English, prioritize committed examples of the non-combat families above;
the combat/repair fixtures already provide a base. Some event categories can be
validated by aggregate replay without a committed example; retain a small fixture
before treating the corresponding regression as independently reproducible.

Collect a few representative lines per variant, not a large archive. Keep the
listener/session header (replace the identity), original category, full raw
markup, encoding/BOM and relative timing. For Local, retain only system-generated
location lines. Rebase timestamps and remove personal names, identity links,
original filenames and paths before committing.

Across those samples, include:

- Default and customized overview formatting, closed/unclosed/nested tags,
  localized-name display enabled/disabled, and ship/ticker/weapon/quality display
  options. Preserve raw tags when collecting Japanese damage.
- Fractional and large amounts, grouped digits, nonbreaking spaces, full-width
  punctuation, and untranslated English fragments inside a localized file.
- Visible localized and English SDE names, including names containing hyphens or
  parentheses. A hint translation must not replace the visible lookup name.
- A language change between sessions, fresh file headers, and UTF-8/UTF-16 Local
  files where produced by the client. Byte-split writes, rollover, duplicates and
  truncation also have controlled reader tests; real examples supplement them.

Do not infer that a missing message is emitted to the log just because a client
template exists. Retain unrecognized text, identify the real category/composition,
then add the smallest rule and fixture needed. New event types remain separate
from damage, actual arrival, successful yield or wallet-payment confirmation.

An optional aggregate-only replay is available from `src`:

```powershell
dotnet run --project tests/StaticData.Smoke -- --log-audit C:\path\to\EVE\logs C:\path\to\isolated\StaticData
```

The static-data directory is optional. Use an isolated copy for upgrade checks.
Replay never modifies source logs or production history.
It accepts standard `Gamelogs`/`Chatlogs` folders and exported `Gamelog`/`Chat-Local`
folders; only Local system messages contribute chat-derived events.
Aggregate counts describe only the selected input corpus. Unrecognized messages
remain unclassified, and oversized lines are subject to the reader's existing
limit. A replay does not establish complete event coverage or replace committed
fixtures for reproducible regressions.
