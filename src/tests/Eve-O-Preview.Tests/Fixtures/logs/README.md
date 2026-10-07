# Log fixtures

`localization-templates.json` contains generated client-template examples, not
recorded sessions. The other files contain small anonymized client excerpts:

| File | Recorded behavior |
| --- | --- |
| `chinese-client.txt` | Header, undocking, reload, warp interference, target range and zero outgoing damage |
| `chinese-activities.txt` | Incoming/outgoing misses and nonzero damage, ordinary mining, named proximity decloak |
| `chinese-overview.txt` | Adjusted bounty accrual, grouped currency, rejected cloak activation, shield/armor repairs in both directions and incoming hull repair |
| `chinese-effects.txt` | Jump, warp scrambling, capacitor neutralization, autopilot status, burst jamming and drone target warning |
| `chinese-local.txt` | UTF-16 Local system message, English header keys, Chinese channel/sender, per-line BOM and unwrapped trailing localization marker |
| `japanese-client.txt` | Japanese header, incoming damage with four hit qualities, incoming miss, two named proximity decloaks, ordinary mining and full-hold status |
| `japanese-damage.txt` | Three incoming and three outgoing hits with original amount colors, missile names and relative timing |
| `russian-client.txt` | Russian header/messages with English NPC, missile and ore names; connection, undock, incoming/outgoing damage, decloak, mining with nonbreaking space and full-hold status |
| `chinese-english-names.txt` | Important Names in English: Chinese messages and hints with visible English station/system names |

Game excerpts retain UTF-8 text, raw tag nesting and missing closing tags. The
overview/effects files assemble independent examples on a synthetic timeline; activity
excerpts preserve relative timing. Dates, listeners, player names and corporation/
alliance labels are replaced. Public SDE item names remain unchanged. Source paths,
original filenames, identity links, player chat and reverse identity mappings are
not retained. Formatting variations constructed in tests are additional controlled
cases, not evidence of recorded client output.

The production reader/parser regressions are in `ChineseLogFixtureTests.cs` and
`JapaneseLogFixtureTests.cs`. The Japanese excerpts retain relative timing and
original leading damage colors; outgoing misses and repair examples are still needed.
`MixedNameLogFixtureTests.cs` covers the Russian/Chinese excerpts and controlled
English-name variations across all eight languages. Those controlled variations
include reversed hints and are not additional recorded sessions.
Remaining real-client coverage is tracked in
[the language guide](../../../../docs/ai/log-languages.md#real-client-sample-gaps).
