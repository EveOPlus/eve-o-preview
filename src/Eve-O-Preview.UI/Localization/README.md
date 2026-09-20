# Workspace languages

Light and Dark offer **Appearance → Language**. The selection applies immediately
to every profile and survives restart. Automatic follows the Windows UI language;
unsupported languages fall back to English. Legacy retains its English interface
and does not expose the language selector.

Bundled languages: English, Arabic, German, Spanish, French, Hindi, Indonesian,
Italian, Japanese, Korean, Dutch, Polish, Brazilian Portuguese, Russian, Turkish,
Ukrainian, Simplified Chinese and Traditional Chinese. Regional variants use their
bundled parent language; Portuguese variants use Brazilian Portuguese, and Taiwan,
Hong Kong and Macau use Traditional Chinese.

The catalogs are embedded UTF-8 JSON and work offline. No translation service,
language model or new runtime package is needed. The catalogs have had a source-
context review across all bundled languages, including settings, status messages,
combat statistics and overlay choices. Native-speaker proofreading is still
needed for fluency and specialized game terminology; catalog and rendering checks
do not establish linguistic correctness.

Augments uses these catalogs for navigation, settings, choice display templates,
overview labels and host status messages. SDE names and raw log content retain
their source language. Its drafts survive the workspace's language rebuild.
Blink/Fade, the first-download confirmation and position/order controls are
localized as well. `AugmentLabels` reuses translated position and direction
labels without changing the persisted enum values. Platform names, fighter-primary
notes and thumbnail flash opacity controls/errors are translated in all 18
catalogs. Persisted enum names and English SDE item names remain stable; only
their display labels are localized.

Hotkey method, press/release timing, the hidden diagnostic passthrough controls,
capture status and registration errors use all 18 catalogs. Registration error
templates keep shortcut names as separate arguments; those names are never
translated. Method and timing choices belong to the selected profile, while
the display language remains global.

Augments Data setup has a separate **Log language** preference, defaulting to
per-file automatic detection. It controls message grammars independently of UI
language. Its selector, help and validation text use all 18 UI catalogs; language
choices use native names. See [log parsing](../../docs/ai/log-languages.md).

## Adding or updating translations

1. Add the exact English display text as both key and value in `en.json`. Add the
   same key to every other catalog. Use `L` for display strings and `F` for
   interpolated messages; formatted resource keys use `{0}`, `{1}`, etc.
2. Preserve every format argument, including format specifiers, in translations.
   Arguments may move to suit grammar. Do not translate profile or character
   names, shortcut bindings, font families, setting keys or persisted enum values.
   Use `RawText` for user data, and localize choice templates instead of values.
3. To add a language, add its culture code and native name to
   `WorkspaceLocalization.Languages` and create `<culture-code>.json`. The project
   embeds matching files automatically. Keep script variants explicit when needed.
4. Run the Windows tests and the [UI smoke harness](../../tests/Eve-O-Preview.UI.Smoke/README.md).
   Catalog checks verify coverage and format arguments. Inspect narrow layouts and
   representative long labels before releasing a translation.

## Translate the control's meaning

Read the call site and its surrounding controls before translating. Use
`SettingCatalog` for setting descriptions, `WorkspaceView` partials for workspace
actions, and `CombatLogView` partials for Augments. For status messages, follow the
message back to its producer in `Services/Logs`. Short English labels frequently
have several unrelated dictionary meanings:

| Text | Meaning in this UI |
| --- | --- |
| Disabled / Enabled | A feature is switched off / on. Never a person's disability. |
| Active / Running / Online | The focused client, a running process, and a connected character are distinct states. |
| Character | An EVE pilot, except in input-validation phrases such as filename characters. |
| Client | An EVE application instance. |
| Cycle group, Skip, Resume | An ordered sequence of clients to switch through; temporarily omit or restore a member. |
| Order / Record / Load | Sequence, capture a shortcut, and load saved data. Check whether the label is a noun, action or status. |
| Frame / Border / Title | Window decoration, preview outline, and character-name text. FPS uses animation frames. |
| Regular / Bold / Strikeout | Font styles: normal weight, heavy weight and a line through the text. |
| Accent / Opacity | A theme highlight colour and how opaque an element is. |
| Clear | Remove a binding or field value; in "make permissions clear", explain them understandably. |
| Key down / Key up | Pressing / releasing the shortcut's keyboard key, not arrow keys or movement directions. |
| Hotkey trigger | The moment the shortcut action runs, not a weapon trigger. |
| Global input / Windows hotkeys | A system-wide keyboard hook / shortcuts registered with Windows. Neither label recommends one method over the other. |
| Diagnostic key passthrough | Forward the original shortcut input to the active application before queuing the action. This does not confirm the application processed it. |
| Unsigned decimal IDs | Nonnegative integers written in base ten, separated by commas; neither fractions nor signed documents. |
| Muting / CPU affinity | Suppress selected sounds / assign processor cores. |
| Hits / Largest dealt | Combat impacts / the largest outgoing damage amount. |
| Incoming / Outgoing | Damage or repairs received / sent, according to the surrounding statistic or overlay row. |
| Alpha | Per-hit damage in the combat overlay, separate from DPS and opacity. Never a software release stage. |
| Shield / Armour / Hull | The ship's three defensive layers; armour is distinct from the shield. |
| Build | The static game-data version in Data setup. |
| Listener | The character name in a log header, not an audio device or audience member. |
| Local logs / System | Logs of the in-game Local chat channel / an EVE star system. A local folder instead means a folder on the computer. |

Keep related labels and full sentences consistent, including grammatical case and
plural forms. Do not substitute a word throughout a catalog without checking each
sentence: character-name text, for example, contains both a pilot and text glyphs.
In instructions that name a button or page, use its actual translated label.
Preserve format arguments, meaningful line breaks, hexadecimal examples such as
`#RRGGBB` and `#6D9FFF`, and literal identifiers such as `EVE -`.

For ship-defence terminology, the localized EVE support pages provide examples in
[French](https://support.eveonline.com/hc/fr/articles/208289385-Structures-Upwell-%C3%89tats-de-vuln%C3%A9rabilit%C3%A9),
[German](https://support.eveonline.com/hc/de/articles/209985225-Upwell-Strukturen-FAQ)
and [Japanese](https://support.eveonline.com/hc/ja/articles/11158425410716-%E3%82%B6%E3%83%AB%E3%82%B6%E3%82%AF).

## Runtime boundaries

`ApplicationPreferences.UiLanguage` stores `auto` or a supported culture code in
the global settings file. The `language` backend command never writes gameplay
profiles or sends native settings messages. Refresh preserves unapplied edits.
Arabic mirrors the workspace; input values, zoom anchor grids and native preview
geometry remain left-to-right. Parsing uses the existing invariant formats and
does not change the process or thread culture.

Platform-owned dialogs and operating-system exception details retain their system
language. Native game previews, tray menus and the frozen Legacy surface are
outside the portable workspace catalogs.
