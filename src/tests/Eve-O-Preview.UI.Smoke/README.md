# Portable workspace visual smoke test

This executable renders the **production Avalonia controls** with the headless
windowing platform and real Skia renderer. It uses representative in-memory
profiles, characters, cycle groups and settings. It never starts the Windows
application, installs hooks, reads credentials, or modifies real profiles.

From `src`:

```powershell
dotnet run --project .\tests\Eve-O-Preview.UI.Smoke\Eve-O-Preview.UI.Smoke.csproj -- --output .\bin\ui-review
```

Pass `--catalog <simulation-catalog.json>` after `--output <directory>` to also
render and select every modeled weapon family from an actual SDE catalog exported
by `StaticData.Smoke --simulation-catalog <static-data-directory> <output.json>`.
This remains an isolated UI backend: selected IDs are checked against that same
catalog, and the production parser round-trip belongs to StaticData.Smoke.

The command checks Light, Dark and Legacy theme selection through the real theme
buttons, renders each feature area, verifies navigation geometry and keyboard
availability, rejects invalid FPS input, retains drafts across page navigation,
exercises an FPS editor command, and searches directly for an audio setting.
It also changes a profile accent and switches profiles through production
controls, checking the visible header accent and independent global theme. The
modern profile dropdown includes Manage profiles below a divider; opening it
retains the selected profile and unapplied edits. Open dropdowns are captured in
Light and Dark for text-contrast review.
It returns a nonzero exit code on failure. PNG files go to ignored `bin` output
by default, or the explicitly supplied `--output` directory. Inspect the images
for typography, contrast, spacing and content clipping; automated geometry checks
do not establish complete accessibility or visual quality.

The runner reports its render count, including the lower portions of audio
and cycling pages. Legacy renders at its original 460 by 417 client size;
modern themes also render at 940 by 650 and the minimum 784 by 581 client area.
Each modern size preserves independent scrolling for navigation and settings.

Hotkey localization checks render the method, press/release timing and unlocked
diagnostic controls in all 18 languages, verify translated search and selected
option labels, and retain the raw method/trigger values. The separate diagnostic
gesture checks verify the hidden option's unlock and method restrictions.

Augments checks cover combined statistics above the character list, time-weighted
recorded averages, missing-sample placeholders and explicit repair HP/cycle units
in Incoming/Outgoing columns. Search phrases for averages, combined statistics and
repair cycles must open Overview. Both themes retain row focus and open details
while statistics refresh.

The donation invitation is checked in all three themes and at compact sizes:
no automatic opening, the correct recipient, clipboard text, Escape dismissal,
focus restoration, and preservation of unapplied edits. The portrait provider
uses a delayed response and a test-only image fixture, so these checks do not
depend on FC availability. The real host uses the shared disk portrait cache.

Character order checks use actual pointer input for downward/upward dragging,
hold the pointer at the list edge while pumping the dispatcher to test automatic
scrolling, and cancel with Escape without saving. They check scrollbar clearance,
one command per drop, shared Skip/Resume state, retained scroll position, and the
expanded editor across themes. Both the smoke harness and Windows host suite
verify that expansion and dismissal leave window bounds and state unchanged.

Thumbnail-menu checks drag Skip/Resume and divider rows, cancel a drag with
Escape, insert/remove dividers, revisit the editor and reset its two-divider
default. All 11 menu palettes are selected/rendered in every application theme,
including Legacy's compact Order/Theme tabs. The Windows suite separately opens
real menus and sends the second right-click at the original pointer for Minimize
and Skip/Resume, verifies explicit divider placement and native palette colors,
and exercises hover leave, refresh, zoom, opacity, focus-based hiding and title
click coordinates. Native captures are under the test output's native-menu-themes.

The Windows suite's `WorkspaceVisualReviewTests` separately captures the actual
Windows renderer inside the production host, including the title editor and
Legacy's nine original tabs. `WorkspacePreviewRenderingTests` compares title
and highlight pixels with the original native controls; the portable fixture
does not claim native font-rendering accuracy.

The project targets plain `net10.0` and does not reference the Windows app. This
is a UI portability check, not Linux support for Windows preview capture, focus,
audio interception or CPU services. The Windows test suite separately covers the
platform adapter. Real desktop keyboard/focus, screen-reader and high-DPI checks
are still necessary before release.

The rendering setup follows the [Avalonia headless platform guidance](https://v11.docs.avaloniaui.net/docs/concepts/headless/).

`portraits-*.png` uses the existing public fixture image as a controlled response
for synthetic identity records. Checks cover pending lookup, client controls,
stable portrait bounds, normal/expanded cycle order, both modern themes, compact
sizes and Arabic layout. They do not read a running client's launch token.

Language checks exercise all bundled catalogs using the production selector,
localized settings search, retained font drafts and unchanged character names.
They also check global selection across profile switches, right-to-left layout
with unchanged preview geometry, and the absence of a language selector in Legacy.
`language-*.png` includes every language's Appearance page and compact German,
Arabic, Hindi and Japanese workspace pages. Catalog and native host tests remain
separate; these renders do not establish native-speaker translation quality.

Augments checks cover Simple/Advanced themes, shared and per-client editing,
icon samples, popup RGB colours, standard font/weapon/ammunition dropdowns, all flash targets and
Blink/Fade selection, position/order controls, real-parser simulation requests,
simultaneous incoming/outgoing shield, armour and hull repairs, multi-source
compact repair rates with IN/OUT prefixes, disabled weapon-platform headings, scoped reset/cancel
popups, and compact overview/details. The optional `--catalog` export from
`StaticData.Smoke --simulation-catalog` checks the actual local T1/T2 choices. Missing-data checks verify confirmation
before downloading, cancellation without repeated prompts, disabled SDE-dependent
simulation and continued basic settings. These checks use controlled data; the
opt-in corpus and Windows renderer runners cover real logs and live thumbnails.

Augments search checks cover DPS/reps and multiword repair phrases, log/logs/logging
and data aliases, and direct reset/jump navigation. Results open the exact tab and
clear the query; section matches count in the summary. Translated repair/log terms
are checked in every catalog, including the renamed Data setup tab. Legacy never
exposes these modern routes. Full appearance samples include both damage/alpha
rows and all repair types independently of the chosen simulation event.
