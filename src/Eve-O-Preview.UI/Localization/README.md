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
language model or new runtime package is needed. The initial catalogs include
machine-translated descriptions with reviewed navigation and common controls;
native-speaker review is still needed for specialized game terminology.

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

`ApplicationPreferences.UiLanguage` stores `auto` or a supported culture code in
the global settings file. The `language` backend command never writes gameplay
profiles or sends native settings messages. Refresh preserves unapplied edits.
Arabic mirrors the workspace; input values, zoom anchor grids and native preview
geometry remain left-to-right. Parsing uses the existing invariant formats and
does not change the process or thread culture.

Platform-owned dialogs and operating-system exception details retain their system
language. Native game previews, tray menus and the frozen Legacy surface are
outside the portable workspace catalogs.
